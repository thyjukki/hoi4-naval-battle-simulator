using System.Text.Json;
using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Mios;
using NavySimulator.Engine.Stats;
using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Import;

namespace NavySimulator.Web.Services;

/// <summary>
/// The GUI's shared state: the data directory's content, the scenario being edited, and the operations that change
/// them on disk. One instance serves every browser tab, so state survives navigation and page reloads.
/// Editors work on clones (<see cref="Clone{T}"/>) and hand them back to the Save methods.
/// </summary>
public sealed class Workspace
{
    public const string DefaultScenarioFile = "battle-scenario.json";

    private static readonly JsonSerializerOptions CloneOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly object sync = new();
    private readonly Dictionary<string, string> sourceNames = new(StringComparer.Ordinal);
    private readonly GuiSettingsStore settingsStore;
    private IReadOnlyList<DefinesCandidate>? modCandidates;
    private readonly GuiSettings settings;
    private string savedScenarioJson = string.Empty;

    // The catalog files' names, sizes and write times when the catalog last matched them (see CatalogStamp).
    private string catalogStamp = string.Empty;

    public Workspace(WorkspaceOptions options, GuiSettingsStore settingsStore)
    {
        DataDirectory = options.DataDirectory;
        CommandLineDefineSources = options.CommandLineDefineSources;
        this.settingsStore = settingsStore;
        settings = settingsStore.Load();
        LoadCatalog();

        var initial = new[] { settings.LastScenarioFile, DefaultScenarioFile }
            .FirstOrDefault(file => file is not null && File.Exists(Path.Combine(DataDirectory, file)));

        if (initial is null)
        {
            NewScenario();
            return;
        }

        try
        {
            OpenScenario(initial);
        }
        catch (SetupValidationException exception)
        {
            NewScenario();
            ScenarioLoadError = exception.Message;
        }
    }

    /// <summary>Raised after the catalog, the scenario or the settings changed. Handlers may run on any thread.</summary>
    public event Action? Changed;

    public string DataDirectory { get; }

    /// <summary>Where reports are written: the <c>output</c> folder next to the data directory, as the CLI uses from the repository root.</summary>
    public string OutputDirectory => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(DataDirectory).TrimEnd(Path.DirectorySeparatorChar)) ?? DataDirectory, "output");
    public ContentCatalog Catalog { get; private set; } = null!;
    public ContentLibrary Library { get; private set; } = null!;

    /// <summary>Increases whenever the catalog changes, so views can tell their battle is out of date.</summary>
    public int CatalogVersion { get; private set; }

    public IReadOnlyList<string> CommandLineDefineSources { get; }
    public IReadOnlyList<string> SettingsDefineSources => settings.ExtraDefineSources;
    public string SettingsPath => settingsStore.Path;

    /// <summary>The last error writing the settings file, if any.</summary>
    public string? SettingsError { get; private set; }

    /// <summary>Define sources applied after the scenario's: GUI settings first, then the command line.</summary>
    public IReadOnlyList<string> ExtraDefineSources =>
        settings.ExtraDefineSources.Concat(CommandLineDefineSources).Select(ResolveDefinesSource).ToList();

    public string? ScenarioFile { get; private set; }
    public BattleScenarioDto Scenario { get; private set; } = new();
    public string? ScenarioLoadError { get; private set; }
    public bool ScenarioDirty => Json(Scenario) != savedScenarioJson;

    public static T Clone<T>(T value)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value), CloneOptions)!;
    }

    public static string Json<T>(T value)
    {
        return CatalogWriter.Serialize(value).ToJsonString();
    }

    public void ReloadCatalog()
    {
        lock (sync)
        {
            LoadCatalog();
        }

        Notify();
    }

    /// <summary>The HOI4 install and the mods found on this computer (searched once; see <see cref="RescanMods"/>).</summary>
    public IReadOnlyList<DefinesCandidate> ModCandidates => modCandidates ??= Hoi4Locator.Find();

    public void RescanMods()
    {
        modCandidates = Hoi4Locator.Find();
        sourceNames.Clear();
        Notify();
    }

    /// <summary>
    /// What an import of hulls, modules and ship types reads: the HOI4 install found on this computer, then the define
    /// sources (Simulate as) that are mod folders, in order.
    /// </summary>
    public (string? Game, IReadOnlyList<string> Mods) ShipDataSources()
    {
        var game = ModCandidates.FirstOrDefault(candidate => candidate.Kind == DefinesCandidateKind.Game)?.Path;
        var mods = ExtraDefineSources.Select(ResolveDefinesSource)
            .Where(Directory.Exists)
            .Where(path => game is null || Hoi4Locator.Normalize(path) != Hoi4Locator.Normalize(game))
            .ToList();
        return (game, mods);
    }

    /// <summary>
    /// Reads hulls, modules and ship types from the game and the Simulate-as mods (<see cref="ShipDataSources"/>), writes
    /// them over <c>hulls/</c>, <c>modules/</c> and <c>ship-types/</c> and reloads the catalog. Designs are not touched.
    /// </summary>
    public ShipDataImport ImportShipData()
    {
        var (game, mods) = ShipDataSources();

        if (game is null)
        {
            throw new InvalidOperationException("No Hearts of Iron IV install was found. Use 'NavySimulator import-ship-data --game <dir>' instead.");
        }

        ShipDataImport import;

        lock (sync)
        {
            import = ShipDataImporter.Read(new GameFileSystem(game, mods));
            ShipDataWriter.Write(DataDirectory, import);
            LoadCatalog();
        }

        Notify();
        return import;
    }

    /// <summary>
    /// Reads the naval and air doctrines and the navy and air spirits from the game and the Simulate-as mods (the same
    /// sources as <see cref="ShipDataSources"/>), writes them over <c>doctrines/</c> and <c>spirits/</c> and reloads the
    /// catalog. Scenarios keep their doctrine and spirit IDs.
    /// </summary>
    public DoctrineImport ImportDoctrines()
    {
        var (game, mods) = ShipDataSources();

        if (game is null)
        {
            throw new InvalidOperationException("No Hearts of Iron IV install was found. Use 'NavySimulator import-doctrines --game <dir>' instead.");
        }

        DoctrineImport import;

        lock (sync)
        {
            import = DoctrineImporter.Read(new GameFileSystem(game, mods));
            DoctrineWriter.Write(DataDirectory, import);
            LoadCatalog();
        }

        Notify();
        return import;
    }

    /// <summary>
    /// Reads the naval MIOs, their policies and the equipment groups from the game and the Simulate-as mods (the same
    /// sources as <see cref="ShipDataSources"/>), writes them over <c>mios/</c>, <c>mio-policies/</c> and
    /// <c>equipment-groups/</c> and reloads the catalog. Designs and scenarios keep their MIO IDs.
    /// </summary>
    public MioImport ImportMios()
    {
        var (game, mods) = ShipDataSources();

        if (game is null)
        {
            throw new InvalidOperationException("No Hearts of Iron IV install was found. Use 'NavySimulator import-mios --game <dir>' instead.");
        }

        MioImport import;

        lock (sync)
        {
            import = MioImporter.Read(new GameFileSystem(game, mods));
            MioWriter.Write(DataDirectory, import);
            LoadCatalog();
        }

        Notify();
        return import;
    }

    /// <summary>
    /// Reads the game's fixed plane equipment (A-PLANES) from the game and the Simulate-as mods (the same sources as
    /// <see cref="ShipDataSources"/>), writes it over <c>planes/</c> and reloads the catalog. Fleets and scenarios keep
    /// their plane IDs; the scenario check reports IDs that no longer exist.
    /// </summary>
    public PlaneImport ImportPlanes()
    {
        var (game, mods) = ShipDataSources();

        if (game is null)
        {
            throw new InvalidOperationException("No Hearts of Iron IV install was found. Use 'NavySimulator import-planes --game <dir>' instead.");
        }

        PlaneImport import;

        lock (sync)
        {
            import = PlaneImporter.Read(new GameFileSystem(game, mods));
            PlaneWriter.Write(DataDirectory, import);
            LoadCatalog();
        }

        Notify();
        return import;
    }

    /// <summary>
    /// Reads the navy skill levels, the admiral traits and the admirals from the game and the Simulate-as mods (the same
    /// sources as <see cref="ShipDataSources"/>), writes them over <c>leader-skills/</c>, <c>leader-traits/</c> and
    /// <c>admirals/</c> and reloads the catalog. Scenarios keep their admirals (each side holds a copy).
    /// </summary>
    public LeaderImport ImportLeaders()
    {
        var (game, mods) = ShipDataSources();

        if (game is null)
        {
            throw new InvalidOperationException("No Hearts of Iron IV install was found. Use 'NavySimulator import-leaders --game <dir>' instead.");
        }

        LeaderImport import;

        lock (sync)
        {
            import = LeaderImporter.Read(new GameFileSystem(game, mods));
            LeaderWriter.Write(DataDirectory, import);
            LoadCatalog();
        }

        Notify();
        return import;
    }

    /// <summary>
    /// Reads the sea regions (naval terrain, weather periods, province positions) and the naval terrains from the game and
    /// the Simulate-as mods (the same sources as <see cref="ShipDataSources"/>), writes them over <c>sea-regions/</c> and
    /// <c>naval-terrains/</c> and reloads the catalog. Scenarios keep their region and province IDs.
    /// </summary>
    public RegionImport ImportRegions()
    {
        var (game, mods) = ShipDataSources();

        if (game is null)
        {
            throw new InvalidOperationException("No Hearts of Iron IV install was found. Use 'NavySimulator import-regions --game <dir>' instead.");
        }

        RegionImport import;

        lock (sync)
        {
            import = RegionImporter.Read(new GameFileSystem(game, mods));
            RegionWriter.Write(DataDirectory, import);
            LoadCatalog();
        }

        Notify();
        return import;
    }

    /// <summary>What every battle simulates: "Vanilla 1.19.3", or the names of the GUI's define sources (e.g. the mod).</summary>
    public string RulesetName => ExtraDefineSources.Count == 0
        ? "Vanilla 1.19.3"
        : string.Join(" + ", ExtraDefineSources.Select(SourceName));

    /// <summary>A readable name for a define source: the mod's name, or the file or folder name.</summary>
    public string SourceName(string source)
    {
        lock (sourceNames)
        {
            if (!sourceNames.TryGetValue(source, out var name))
            {
                name = Hoi4Locator.DisplayName(source, ModCandidates);
                sourceNames[source] = name;
            }

            return name;
        }
    }

    /// <summary>Simulates with one mod's (or the game's) defines and static modifiers, or with vanilla when null.</summary>
    public void UseRuleset(string? source)
    {
        SetSettingsDefineSources(source is null ? [] : [source]);
    }

    public void SetSettingsDefineSources(IEnumerable<string> sources)
    {
        settings.ExtraDefineSources = sources.Where(source => !string.IsNullOrWhiteSpace(source)).Select(source => source.Trim()).Distinct().ToList();
        SaveSettings();
        Notify();
    }

    /// <summary>Call after editing <see cref="Scenario"/> in place so other views refresh.</summary>
    public void ScenarioEdited()
    {
        Notify();
    }

    /// <summary>Scenario files in the data directory root (JSON files with a <c>battleScenario</c> block).</summary>
    public IReadOnlyList<string> ListScenarioFiles()
    {
        var files = new List<string>();

        foreach (var path in Directory.GetFiles(DataDirectory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));

                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("battleScenario", out _))
                {
                    files.Add(Path.GetFileName(path));
                }
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
                // Not a scenario.
            }
        }

        return files;
    }

    /// <summary>
    /// Opens a scenario file of the data directory. Content files changed on disk since they were read are reloaded
    /// first, so a scenario written outside the GUI finds the designs and fleets written with it.
    /// </summary>
    public void OpenScenario(string file)
    {
        var scenario = SetupLoader.ReadScenario(DataDirectory, file);
        Normalize(scenario);

        lock (sync)
        {
            if (CatalogStamp() != catalogStamp)
            {
                LoadCatalog();
            }

            Scenario = scenario;
            ScenarioFile = file;
            ScenarioLoadError = null;
            savedScenarioJson = Json(scenario);
            settings.LastScenarioFile = file;
        }

        SaveSettings();
        Notify();
    }

    public void NewScenario()
    {
        var fleets = Catalog.Fleets.Select(fleet => fleet.ID).ToList();

        lock (sync)
        {
            Scenario = new BattleScenarioDto
            {
                ID = "new_scenario",
                Terrain = "ocean",
                Weather = "clear",
                MaxHours = new BattleRules().MaxHours,
                Iterations = 10,
                Attacker = new BattleParticipantDto { FleetID = fleets.FirstOrDefault() ?? string.Empty },
                Defender = new BattleParticipantDto { FleetID = fleets.Skip(1).FirstOrDefault() ?? fleets.FirstOrDefault() ?? string.Empty }
            };
            ScenarioFile = null;
            savedScenarioJson = string.Empty;
        }

        Notify();
    }

    /// <summary>Saves the scenario to <paramref name="fileName"/> (a file name in the data directory root), or to its current file.</summary>
    public void SaveScenario(string? fileName = null)
    {
        var file = fileName?.Trim() ?? ScenarioFile ?? throw new InvalidOperationException("Choose a file name for the new scenario.");

        if (file.IndexOfAny(['/', '\\']) >= 0 || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException($"'{file}' is not a plain file name.");
        }

        if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            file += ".json";
        }

        lock (sync)
        {
            CatalogWriter.SaveScenario(DataDirectory, file, Scenario);
            ScenarioFile = file;
            savedScenarioJson = Json(Scenario);
            settings.LastScenarioFile = file;
        }

        SaveSettings();
        Notify();
    }

    public void RevertScenario()
    {
        if (ScenarioFile is null)
        {
            NewScenario();
        }
        else
        {
            OpenScenario(ScenarioFile);
        }
    }

    /// <summary>Validates and builds the edited scenario (or <paramref name="scenario"/>) with fresh ships.</summary>
    public ScenarioBuild BuildScenario(BattleScenarioDto? scenario = null)
    {
        lock (sync)
        {
            var loader = new SetupLoader();

            try
            {
                var built = loader.BuildScenario(Catalog, scenario ?? Scenario, ExtraDefineSources);
                return new ScenarioBuild(built, [], loader.Warnings.ToList(), loader.DefinesReport);
            }
            catch (SetupValidationException exception)
            {
                return new ScenarioBuild(null, exception.Errors, loader.Warnings.ToList(), loader.DefinesReport);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException)
            {
                return new ScenarioBuild(null, [exception.Message], loader.Warnings.ToList(), loader.DefinesReport);
            }
        }
    }

    public ShipDesignDto? FindDesign(string id)
    {
        return Catalog.ShipDesigns.FirstOrDefault(design => design.ID == id);
    }

    public FleetDto? FindFleet(string id)
    {
        return Catalog.Fleets.FirstOrDefault(fleet => fleet.ID == id);
    }

    public MioDto? FindMio(string? id)
    {
        return id is null ? null : Catalog.Mios.FirstOrDefault(mio => mio.ID == id);
    }

    /// <summary>
    /// S-MIO-MATCH, S-MIO-COUNTRY: the MIOs whose equipment types cover the design, so the game lets it be assigned to
    /// them, and that <paramref name="country"/> can have (every country when null).
    /// </summary>
    public IReadOnlyList<MioDto> AssignableMios(ShipDesignDto design, string? country = null)
    {
        var shipDesign = Library.TryCreateDesign(design);
        return shipDesign is null
            ? []
            : Catalog.Mios.Where(mio => MioCountries.IsAvailable(mio, country) &&
                                        Library.Mios.TryGetValue(mio.ID, out var engine) && MioRules.CanAssign(engine, shipDesign))
                .ToList();
    }

    /// <summary>The designs of the side's fleet, in the fleet's order. Unknown IDs are left out.</summary>
    public IReadOnlyList<ShipDesignDto> SideDesigns(BattleParticipantDto side)
    {
        return (FindFleet(side.FleetID)?.ShipDesigns.Keys.ToList() ?? [])
            .Select(id => Catalog.ShipDesigns.FirstOrDefault(design => design.ID == id))
            .OfType<ShipDesignDto>()
            .ToList();
    }

    /// <summary>S-MIO: the MIO the side assigns a design to: its own assignment (empty: none), else the design's.</summary>
    public static string? EffectiveMioId(BattleParticipantDto side, ShipDesignDto design)
    {
        return side.DesignMios.TryGetValue(design.ID, out var assigned) ? (string.IsNullOrEmpty(assigned) ? null : assigned) : design.MioID;
    }

    /// <summary>
    /// The MIOs a side deals with: those its fleet's designs are assigned to (with the designs), then any other MIO it has
    /// a setup for. Unknown IDs are left out.
    /// </summary>
    public IReadOnlyList<(MioDto Mio, IReadOnlyList<string> Designs)> SideMios(BattleParticipantDto side)
    {
        var assigned = SideDesigns(side)
            .Select(design => (Design: design.ID, Mio: EffectiveMioId(side, design)))
            .Where(entry => entry.Mio is not null)
            .ToList();
        var ids = assigned.Select(entry => entry.Mio!).Concat(side.Mios.Keys).Distinct(StringComparer.Ordinal);
        return ids.Select(FindMio)
            .OfType<MioDto>()
            .Select(mio => (mio, (IReadOnlyList<string>)assigned.Where(entry => entry.Mio == mio.ID).Select(entry => entry.Design).ToList()))
            .ToList();
    }

    /// <summary>
    /// S-MIO-COUNTRY: the imported country tags, those with MIOs of their own first (alphabetical by name within each
    /// group), with whether they have any.
    /// </summary>
    public IReadOnlyList<(CountryDto Country, bool HasOwnMios)> Countries()
    {
        var withMios = Catalog.Mios.SelectMany(mio => mio.Countries ?? []).ToHashSet(StringComparer.Ordinal);
        return Catalog.Countries
            .Select(country => (Country: country, HasOwnMios: withMios.Contains(country.ID)))
            .OrderBy(entry => entry.HasOwnMios ? 0 : 1)
            .ThenBy(entry => entry.Country.Name ?? entry.Country.ID, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>S-POTF [RE]: the side's fleet's designs the game lets a country make its pride of the fleet: capital ships and carriers.</summary>
    public IReadOnlyList<ShipDesignDto> PrideOfTheFleetCandidates(BattleParticipantDto side)
    {
        return SideDesigns(side).Where(design => Library.TryCreateDesign(design) is { } shipDesign && PrideOfTheFleetRules.CanBePrideOfTheFleet(shipDesign)).ToList();
    }

    /// <summary>S-ADMIRAL: the imported admirals of a country (every admiral when null), by country then name.</summary>
    public IReadOnlyList<AdmiralDto> Admirals(string? country)
    {
        return Catalog.Admirals
            .Where(admiral => string.IsNullOrEmpty(country) || admiral.Country == country)
            .OrderBy(admiral => admiral.Country ?? "~", StringComparer.Ordinal)
            .ThenBy(AdmiralInfo.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public AdmiralDto? FindAdmiral(string? id)
    {
        return string.IsNullOrEmpty(id) ? null : Catalog.Admirals.FirstOrDefault(admiral => admiral.ID == id);
    }

    /// <summary>S-ADMIRAL-TRAIT: the imported admiral traits, grouped as the editor lists them (basic first), then by name.</summary>
    public IReadOnlyList<LeaderTraitDto> LeaderTraits =>
        Catalog.LeaderTraits.OrderBy(AdmiralInfo.GroupIndex).ThenBy(trait => trait.Name ?? trait.ID, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>S-MIO-POLICY: the policies the MIO can take.</summary>
    public IReadOnlyList<MioPolicyDto> PoliciesFor(MioDto mio)
    {
        return Library.Mios.TryGetValue(mio.ID, out var engine)
            ? Catalog.MioPolicies.Where(policy => Library.MioPolicies.TryGetValue(policy.ID, out var rule) && MioRules.PolicyAllowed(engine, rule)).ToList()
            : [];
    }

    public HullDto? FindHull(string id)
    {
        return Catalog.Hulls.FirstOrDefault(hull => hull.ID == id);
    }

    /// <summary>A module's category as the game's designer titles it, e.g. "Rapid Fire Guns".</summary>
    public string ModuleCategory(ModuleDto module)
    {
        return module.CategoryName ?? module.Category;
    }

    /// <summary>The design's role, from its ship type (S-DESIGN-TYPE); null when the design cannot be built.</summary>
    public ShipRole? RoleOf(ShipDesignDto design)
    {
        return Library.TryCreateDesign(design)?.Role;
    }

    /// <summary>The design's ship type and whether it accepts the design (false: a fallback, S-DESIGN-TYPE).</summary>
    public (ShipTypeDto? Type, bool Matches) ShipTypeOf(ShipDesignDto design)
    {
        var check = Library.CheckDesign(design);
        var type = check?.ShipType is { } shipType ? Catalog.ShipTypes.FirstOrDefault(candidate => candidate.ID == shipType.ID) : null;
        return (type, check?.ShipTypeMatches ?? false);
    }

    /// <summary>The design's stats (S-STATS-DESIGN), or null when its hull is unknown.</summary>
    public ShipStats? DesignStats(ShipDesignDto design, List<string>? problems = null)
    {
        return Library.TryCreateDesign(design, problems)?.GetFinalStats();
    }

    /// <summary>
    /// The stats of one ship of the design on a side with the side's researches, doctrines and spirits (S-STATS-SHIP,
    /// S-DOCTRINE-EFFECT).
    /// </summary>
    public ShipStats? ShipStats(ShipDesignDto design, BattleParticipantDto side, List<string>? problems = null)
    {
        var shipDesign = Library.TryCreateDesign(design, problems);

        if (shipDesign is null)
        {
            return null;
        }

        var ship = new Ship(design.ID, shipDesign);
        _ = PreviewParticipant(side, [ship]);
        return ship.GetFinalStats();
    }

    /// <summary>
    /// S-DOCTRINE-EFFECT: what the side's doctrines and spirits give, each effect with its source, and the parts combat
    /// ignores. Unknown IDs are left out (the scenario check reports them).
    /// </summary>
    public BattleParticipant SideEffects(BattleParticipantDto side)
    {
        return PreviewParticipant(side, []);
    }

    /// <summary>The imported doctrine folders, the naval one (the one combat simulates) first.</summary>
    public IReadOnlyList<DoctrineFolderDto> DoctrineFolders =>
        Catalog.DoctrineFolders.OrderBy(folder => folder.ID == BattleParticipant.NavalFolder ? 0 : 1).ToList();

    /// <summary>The imported spirits, navy spirits before air spirits, each in the game's slot order.</summary>
    public IReadOnlyList<SpiritDto> Spirits =>
        Catalog.Spirits.OrderBy(spirit => spirit.Category.StartsWith("air", StringComparison.Ordinal) ? 1 : 0).ToList();

    /// <summary>The doctrine folder with this ID (<c>naval</c>, <c>air</c>), or null when it is not imported.</summary>
    public DoctrineFolderDto? DoctrineFolder(string id)
    {
        return Catalog.DoctrineFolders.FirstOrDefault(folder => folder.ID == id);
    }

    // Previews use the vanilla defines (NDoctrines.DEFAULT_REWARD_MASTERY); battles use the scenario's.
    private BattleParticipant PreviewParticipant(BattleParticipantDto side, List<Ship> ships)
    {
        var researches = side.ResearchIDs.Where(Library.Researches.ContainsKey).Select(id => Library.Researches[id]).ToList();
        var spirits = side.SpiritIDs.Where(Library.Spirits.ContainsKey).Select(id => Library.Spirits[id]).ToList();
        var admiral = side.Admiral is { } dto ? Library.CreateAdmiral(dto) : null;
        return new BattleParticipant(new Fleet("preview", ships), admiral, researches, spirits,
            doctrines: SetupLoader.BuildDoctrines(side, Library), mios: SetupLoader.BuildMios(side, Library),
            designMios: SetupLoader.BuildDesignMios(side, Library));
    }

    public IReadOnlyList<string> FleetsUsingDesign(string designId)
    {
        return Catalog.Fleets.Where(fleet => fleet.ShipDesigns.ContainsKey(designId)).Select(fleet => fleet.ID).ToList();
    }

    /// <summary>
    /// Writes the design to <paramref name="file"/> (data-directory relative) and updates the catalog. When the ID
    /// changed, fleets that used <paramref name="previousId"/> are updated and saved too.
    /// </summary>
    public void SaveDesign(ShipDesignDto design, string file, string? previousId)
    {
        lock (sync)
        {
            var filesWereCurrent = CatalogStamp() == catalogStamp;
            var existing = Catalog.ShipDesigns.FirstOrDefault(candidate => candidate.ID == (previousId ?? design.ID));
            ValidateId(design.ID, Catalog.ShipDesigns.Where(candidate => !ReferenceEquals(candidate, existing)).Select(candidate => candidate.ID), "design");

            if (!Library.Hulls.ContainsKey(design.HullID))
            {
                throw new InvalidOperationException($"Unknown hull '{design.HullID}'.");
            }

            var saved = Clone(design);
            DesignSlots.Normalize(Catalog, saved);
            var oldFile = existing is null ? null : Catalog.SourceFileOf(existing);

            if (existing is not null && oldFile is not null && !SameFile(oldFile, file))
            {
                CatalogWriter.DeleteShipDesign(DataDirectory, oldFile, existing.ID);
            }

            CatalogWriter.SaveShipDesign(DataDirectory, file, saved, existing?.ID);
            Replace(Catalog.ShipDesigns, existing, saved, file);

            if (existing is not null && existing.ID != saved.ID)
            {
                RenameDesignInFleets(existing.ID, saved.ID);
            }

            RebuildAfterWrite(filesWereCurrent);
        }

        Notify();
    }

    /// <summary>Deletes a design that no fleet uses. Returns the fleets that block the deletion.</summary>
    public IReadOnlyList<string> DeleteDesign(string id)
    {
        lock (sync)
        {
            var filesWereCurrent = CatalogStamp() == catalogStamp;
            var users = FleetsUsingDesign(id);
            var design = FindDesign(id);

            if (users.Count > 0 || design is null)
            {
                return users;
            }

            if (Catalog.SourceFileOf(design) is { } file)
            {
                CatalogWriter.DeleteShipDesign(DataDirectory, file, id);
            }

            Catalog.ShipDesigns.Remove(design);
            RebuildAfterWrite(filesWereCurrent);
        }

        Notify();
        return [];
    }

    /// <summary>Writes the fleet and updates the catalog. A renamed fleet is also renamed in the edited scenario.</summary>
    public void SaveFleet(FleetDto fleet, string file, string? previousId)
    {
        lock (sync)
        {
            var filesWereCurrent = CatalogStamp() == catalogStamp;
            var existing = Catalog.Fleets.FirstOrDefault(candidate => candidate.ID == (previousId ?? fleet.ID));
            ValidateId(fleet.ID, Catalog.Fleets.Where(candidate => !ReferenceEquals(candidate, existing)).Select(candidate => candidate.ID), "fleet");

            if (fleet.ShipDesigns.Count == 0)
            {
                throw new InvalidOperationException("A fleet needs at least one ship design.");
            }

            var saved = Clone(fleet);
            var oldFile = existing is null ? null : Catalog.SourceFileOf(existing);

            if (existing is not null && oldFile is not null && !SameFile(oldFile, file))
            {
                CatalogWriter.DeleteFleet(DataDirectory, oldFile, existing.ID);
            }

            CatalogWriter.SaveFleet(DataDirectory, file, saved, existing?.ID);
            Replace(Catalog.Fleets, existing, saved, file);

            if (existing is not null && existing.ID != saved.ID)
            {
                foreach (var side in new[] { Scenario.Attacker, Scenario.Defender })
                {
                    if (side.FleetID == existing.ID)
                    {
                        side.FleetID = saved.ID;
                    }
                }
            }

            RebuildAfterWrite(filesWereCurrent);
        }

        Notify();
    }

    public void DeleteFleet(string id)
    {
        lock (sync)
        {
            var filesWereCurrent = CatalogStamp() == catalogStamp;
            var fleet = FindFleet(id);

            if (fleet is null)
            {
                return;
            }

            if (Catalog.SourceFileOf(fleet) is { } file)
            {
                CatalogWriter.DeleteFleet(DataDirectory, file, id);
            }

            Catalog.Fleets.Remove(fleet);
            RebuildAfterWrite(filesWereCurrent);
        }

        Notify();
    }

    // Relative --defines paths resolve against the working directory first, then the data directory (as in the CLI).
    private string ResolveDefinesSource(string source)
    {
        if (Path.IsPathRooted(source) || File.Exists(source) || Directory.Exists(source))
        {
            return source;
        }

        var inData = Path.Combine(DataDirectory, source);
        return File.Exists(inData) || Directory.Exists(inData) ? inData : source;
    }

    private void LoadCatalog()
    {
        // Stamped before reading, so a file changed during the read is reloaded next time.
        catalogStamp = CatalogStamp();
        Catalog = new SetupLoader().ReadCatalog(DataDirectory);
        Rebuild();
    }

    private void Rebuild()
    {
        Library = ContentLibrary.From(Catalog);
        CatalogVersion++;
    }

    /// <summary>
    /// Rebuild after the GUI wrote catalog files. The catalog matches the files again unless they had already changed
    /// on disk before the write (<paramref name="filesWereCurrent"/> false); then the next open still reloads them.
    /// </summary>
    private void RebuildAfterWrite(bool filesWereCurrent)
    {
        Rebuild();

        if (filesWereCurrent)
        {
            catalogStamp = CatalogStamp();
        }
    }

    private string CatalogStamp()
    {
        return string.Join('\n', SetupLoader.CatalogFiles(DataDirectory).Select(file =>
        {
            var info = new FileInfo(Path.Combine(DataDirectory, file));
            return info.Exists ? $"{file}|{info.Length}|{info.LastWriteTimeUtc.Ticks}" : $"{file}|missing";
        }));
    }

    private void Replace<T>(List<T> items, T? existing, T replacement, string file)
        where T : class
    {
        var index = existing is null ? -1 : items.IndexOf(existing);

        if (index >= 0)
        {
            items[index] = replacement;
        }
        else
        {
            items.Add(replacement);
        }

        Catalog.SetSourceFile(replacement, file);
    }

    private void RenameDesignInFleets(string oldId, string newId)
    {
        foreach (var fleet in Catalog.Fleets.Where(fleet => fleet.ShipDesigns.ContainsKey(oldId)).ToList())
        {
            fleet.ShipDesigns = RenameKey(fleet.ShipDesigns, oldId, newId);
            fleet.ShipExperienceLevels = RenameKey(fleet.ShipExperienceLevels, oldId, newId);
            fleet.CarrierAirwings = RenameKey(fleet.CarrierAirwings, oldId, newId);

            if (Catalog.SourceFileOf(fleet) is { } file)
            {
                CatalogWriter.SaveFleet(DataDirectory, file, fleet);
            }
        }
    }

    private static Dictionary<string, TValue> RenameKey<TValue>(Dictionary<string, TValue> source, string oldKey, string newKey)
    {
        return source.ToDictionary(entry => entry.Key == oldKey ? newKey : entry.Key, entry => entry.Value);
    }

    private static void ValidateId(string id, IEnumerable<string> otherIds, string kind)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException($"The {kind} needs an ID.");
        }

        if (otherIds.Contains(id, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Another {kind} already has the ID '{id}'.");
        }
    }

    private static bool SameFile(string first, string second)
    {
        return string.Equals(first.Replace('\\', '/'), second.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    private static void Normalize(BattleScenarioDto scenario)
    {
        scenario.Defines ??= [];
        scenario.Attacker ??= new BattleParticipantDto();
        scenario.Defender ??= new BattleParticipantDto();

        foreach (var side in new[] { scenario.Attacker, scenario.Defender })
        {
            side.ResearchIDs ??= [];
            side.SpiritIDs ??= [];
            side.Doctrines ??= [];
            side.Mios ??= [];
            side.DesignMios ??= [];
            side.LandBasedAirwings ??= [];
            side.PlaneStockpile ??= [];
        }
    }

    // Settings are a convenience: a read-only settings folder must not break editing.
    private void SaveSettings()
    {
        try
        {
            settingsStore.Save(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SettingsError = exception.Message;
        }
    }

    private void Notify()
    {
        Changed?.Invoke();
    }
}
