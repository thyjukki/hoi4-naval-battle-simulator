using System.Text.Json;
using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Doctrines;
using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Mios;
using NavySimulator.GameData.Defines;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData;

public class SetupLoader
{
    // A-WINGS: planes per carrier wing when a wing gives none.
    private const int DefaultWingPlanes = 10;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly HashSet<string> KnownShipStatsKeys = typeof(ShipStatsDto)
        .GetProperties()
        .Select(property => property.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // The collections ReadCatalog reads: folder, and the root file read instead when the folder is missing.
    private static readonly (string Folder, string LegacyFile)[] CatalogSources =
    [
        ("hulls", "hulls.json"),
        ("modules", "modules.json"),
        ("ship-types", "ship-types.json"),
        ("critical-parts", "critical-parts.json"),
        ("mios", "mios.json"),
        ("ship-designs", "ship-designs.json"),
        ("researches", "researches.json"),
        ("spirits", "spirits.json"),
        ("planes", "planes.json"),
        ("force-compositions", "force-compositions.json")
    ];

    private readonly List<string> warnings = [];

    public IReadOnlyList<string> Warnings => warnings;

    public DefinesBindingReport? DefinesReport { get; private set; }

    /// <summary>Reads the data directory's content and the scenario file, then builds the battle (as the CLI does).</summary>
    public BattleScenario LoadScenarioFromDirectory(
        string dataDirectoryPath,
        string scenarioFileName = "battle-scenario.json",
        IReadOnlyList<string>? extraDefineSources = null)
    {
        var catalog = ReadCatalog(dataDirectoryPath);
        var scenarioErrors = new List<string>();
        var battleScenarioFile = ReadJsonFile<BattleScenarioFileDto>(dataDirectoryPath, scenarioFileName, scenarioErrors) ?? new BattleScenarioFileDto();
        return BuildScenario(catalog, battleScenarioFile.BattleScenario, extraDefineSources, scenarioErrors);
    }

    /// <summary>Reads every content collection of a data directory, keeping read errors and warnings in the catalog.</summary>
    public ContentCatalog ReadCatalog(string dataDirectoryPath)
    {
        if (!Directory.Exists(dataDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Data directory not found: {dataDirectoryPath}");
        }

        var catalog = new ContentCatalog(dataDirectoryPath);
        ValidateModuleStatWarnings(dataDirectoryPath, catalog.Warnings);

        foreach (var folder in new[] { "hulls", "researches" })
        {
            ValidateStatBlockWarnings(dataDirectoryPath, folder, catalog.Warnings);
        }

        catalog.Hulls.AddRange(ReadCollectionFromFolderOrFile<HullDto>(catalog, "hulls", "hulls.json", "hulls"));
        catalog.Modules.AddRange(ReadCollectionFromFolderOrFile<ModuleDto>(catalog, "modules", "modules.json", "modules"));
        catalog.ShipTypes.AddRange(ReadCollectionFromFolderOrFile<ShipTypeDto>(catalog, "ship-types", "ship-types.json", "shipTypes"));

        // Optional: data imported before critical parts existed has none (crits then use their own multiplier, S-CRIT-PART).
        if (Directory.Exists(Path.Combine(dataDirectoryPath, "critical-parts")) || File.Exists(Path.Combine(dataDirectoryPath, "critical-parts.json")))
        {
            catalog.CriticalParts.AddRange(ReadCollectionFromFolderOrFile<CriticalPartDto>(catalog, "critical-parts", "critical-parts.json", "criticalParts"));
        }

        catalog.Mios.AddRange(ReadCollectionFromFolderOrFile<MioDto>(catalog, "mios", "mios.json", "mios"));

        // S-MIO-POLICY, S-MIO-MATCH: optional; data imported before MIOs existed has none.
        if (Directory.Exists(Path.Combine(dataDirectoryPath, MioPoliciesFolder)))
        {
            catalog.MioPolicies.AddRange(ReadCollectionFromFolderOrFile<MioPolicyDto>(catalog, MioPoliciesFolder, $"{MioPoliciesFolder}.json", "policies"));
        }

        if (Directory.Exists(Path.Combine(dataDirectoryPath, EquipmentGroupsFolder)))
        {
            catalog.EquipmentGroups.AddRange(ReadCollectionFromFolderOrFile<EquipmentGroupDto>(catalog, EquipmentGroupsFolder, $"{EquipmentGroupsFolder}.json", "equipmentGroups"));
        }

        if (Directory.Exists(Path.Combine(dataDirectoryPath, CountriesFolder)))
        {
            catalog.Countries.AddRange(ReadCollectionFromFolderOrFile<CountryDto>(catalog, CountriesFolder, $"{CountriesFolder}.json", "countries"));
        }

        // S-ADMIRAL: optional; data imported before admirals existed has none.
        if (Directory.Exists(Path.Combine(dataDirectoryPath, AdmiralsFolder)))
        {
            catalog.Admirals.AddRange(ReadCollectionFromFolderOrFile<AdmiralDto>(catalog, AdmiralsFolder, $"{AdmiralsFolder}.json", "admirals"));
        }

        if (Directory.Exists(Path.Combine(dataDirectoryPath, LeaderTraitsFolder)))
        {
            catalog.LeaderTraits.AddRange(ReadCollectionFromFolderOrFile<LeaderTraitDto>(catalog, LeaderTraitsFolder, $"{LeaderTraitsFolder}.json", "traits"));
        }

        if (Directory.Exists(Path.Combine(dataDirectoryPath, LeaderSkillsFolder)))
        {
            catalog.LeaderSkills.AddRange(ReadCollectionFromFolderOrFile<LeaderSkillDto>(catalog, LeaderSkillsFolder, $"{LeaderSkillsFolder}.json", "skills"));
        }

        // S-REGION, S-TERRAIN: optional; data imported before regions existed has none.
        if (Directory.Exists(Path.Combine(dataDirectoryPath, SeaRegionsFolder)))
        {
            catalog.SeaRegions.AddRange(ReadCollectionFromFolderOrFile<SeaRegionDto>(catalog, SeaRegionsFolder, $"{SeaRegionsFolder}.json", "regions"));
        }

        if (Directory.Exists(Path.Combine(dataDirectoryPath, AirBasesFolder)))
        {
            catalog.AirBases.AddRange(ReadCollectionFromFolderOrFile<AirBaseDto>(catalog, AirBasesFolder, $"{AirBasesFolder}.json", "airBases"));
        }

        if (Directory.Exists(Path.Combine(dataDirectoryPath, NavalTerrainsFolder)))
        {
            catalog.NavalTerrains.AddRange(ReadCollectionFromFolderOrFile<NavalTerrainDto>(catalog, NavalTerrainsFolder, $"{NavalTerrainsFolder}.json", "terrains"));
        }

        catalog.ShipDesigns.AddRange(ReadCollectionFromFolderOrFile<ShipDesignDto>(catalog, "ship-designs", "ship-designs.json", "shipDesigns"));
        catalog.Researches.AddRange(ReadCollectionFromFolderOrFile<ResearchDto>(catalog, "researches", "researches.json", "researches"));
        catalog.Spirits.AddRange(ReadCollectionFromFolderOrFile<SpiritDto>(catalog, "spirits", "spirits.json", "spirits"));
        catalog.Planes.AddRange(ReadCollectionFromFolderOrFile<PlaneDto>(catalog, "planes", "planes.json", "planes"));

        // A-ACE: optional; data imported before aces existed has none (no aces are earned).
        if (Directory.Exists(Path.Combine(dataDirectoryPath, "aces")))
        {
            catalog.Aces.AddRange(ReadCollectionFromFolderOrFile<AceTemplateDto>(catalog, "aces", "aces.json", "aces"));
        }
        catalog.Fleets.AddRange(ReadCollectionFromFolderOrFile<FleetDto>(catalog, "force-compositions", "force-compositions.json", "fleets"));
        ReadDoctrineFolders(catalog);
        ValidateCountryModifierWarnings(catalog);
        ValidateDesignRuleWarnings(catalog);
        ValidateCriticalPartWarnings(catalog);
        ValidateMioWarnings(catalog);
        return catalog;
    }

    private const string MioPoliciesFolder = "mio-policies";
    private const string EquipmentGroupsFolder = "equipment-groups";
    private const string CountriesFolder = "countries";
    private const string AdmiralsFolder = "admirals";
    private const string LeaderTraitsFolder = "leader-traits";
    private const string LeaderSkillsFolder = "leader-skills";
    private const string SeaRegionsFolder = "sea-regions";
    private const string AirBasesFolder = "air-bases";
    private const string NavalTerrainsFolder = "naval-terrains";

    /// <summary>
    /// The files <see cref="ReadCatalog"/> reads, data-directory relative with forward slashes: each collection
    /// folder's JSON files, or its legacy root file (listed even when missing) when the folder does not exist.
    /// </summary>
    public static IReadOnlyList<string> CatalogFiles(string dataDirectoryPath)
    {
        var files = new List<string>();

        foreach (var (folder, legacyFile) in CatalogSources)
        {
            var folderPath = Path.Combine(dataDirectoryPath, folder);

            if (Directory.Exists(folderPath))
            {
                files.AddRange(Directory
                    .GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => $"{folder}/{Path.GetFileName(path)}"));
            }
            else
            {
                files.Add(legacyFile);
            }
        }

        // S-DOCTRINE, S-MIO, S-ADMIRAL, S-REGION: optional, no legacy file.
        foreach (var folder in new[] { "doctrines", MioPoliciesFolder, EquipmentGroupsFolder, CountriesFolder, AdmiralsFolder, LeaderTraitsFolder, LeaderSkillsFolder, SeaRegionsFolder, NavalTerrainsFolder })
        {
            var directory = Path.Combine(dataDirectoryPath, folder);

            if (Directory.Exists(directory))
            {
                files.AddRange(Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => $"{folder}/{Path.GetFileName(path)}"));
            }
        }

        return files;
    }

    /// <summary>Reads a scenario file of the data directory. Throws <see cref="SetupValidationException"/> when it is missing or invalid.</summary>
    public static BattleScenarioDto ReadScenario(string dataDirectoryPath, string scenarioFileName)
    {
        var errors = new List<string>();
        var file = ReadJsonFile<BattleScenarioFileDto>(dataDirectoryPath, scenarioFileName, errors);

        if (file is null || errors.Count > 0)
        {
            throw new SetupValidationException(errors.Count > 0 ? errors : [$"File '{scenarioFileName}' could not be read."]);
        }

        return file.BattleScenario;
    }

    /// <summary>
    /// Validates the catalog and the scenario together and builds the battle. Every call builds new fleets and ships, so
    /// two scenarios built from the same catalog can run at the same time. Throws <see cref="SetupValidationException"/>.
    /// </summary>
    public BattleScenario BuildScenario(ContentCatalog catalog, BattleScenarioDto scenarioDto, IReadOnlyList<string>? extraDefineSources = null)
    {
        return BuildScenario(catalog, scenarioDto, extraDefineSources, []);
    }

    private BattleScenario BuildScenario(
        ContentCatalog catalog,
        BattleScenarioDto scenarioDto,
        IReadOnlyList<string>? extraDefineSources,
        List<string> scenarioErrors)
    {
        warnings.Clear();
        warnings.AddRange(catalog.Warnings);
        var errors = new List<string>(catalog.ReadErrors);
        errors.AddRange(scenarioErrors);

        var dataDirectoryPath = catalog.DataDirectory;
        var hulls = catalog.Hulls;
        var modules = catalog.Modules;
        var shipTypes = catalog.ShipTypes;
        var mios = catalog.Mios;
        var designs = catalog.ShipDesigns;
        var researches = catalog.Researches;
        var spirits = catalog.Spirits;
        var planes = catalog.Planes;
        var forceCompositionsFile = new ForceCompositionsFileDto { Fleets = catalog.Fleets };
        var defines = LoadDefines(dataDirectoryPath, scenarioDto.Defines, extraDefineSources ?? [], errors);
        var rules = BuildRules(scenarioDto, errors);

        ValidateRequiredIds(hulls.Select(h => h.ID), "hulls", errors);
        ValidateRequiredIds(modules.Select(m => m.ID), "modules", errors);
        ValidateRequiredIds(shipTypes.Select(t => t.ID), "shipTypes", errors);
        ValidateRequiredIds(mios.Select(m => m.ID), "mios", errors);
        ValidateRequiredIds(designs.Select(d => d.ID), "shipDesigns", errors);
        ValidateRequiredIds(researches.Select(r => r.ID), "researches", errors);
        ValidateRequiredIds(spirits.Select(s => s.ID), "spirits", errors);
        ValidateRequiredIds(planes.Select(p => p.ID), "planes", errors);
        ValidateRequiredIds(forceCompositionsFile.Fleets.Select(f => f.ID), "fleets", errors);

        ValidateDuplicateIds(hulls.Select(h => h.ID), "hulls", errors);
        ValidateDuplicateIds(modules.Select(m => m.ID), "modules", errors);
        ValidateDuplicateIds(shipTypes.Select(t => t.ID), "shipTypes", errors);
        ValidateDuplicateIds(mios.Select(m => m.ID), "mios", errors);
        ValidateDuplicateIds(catalog.MioPolicies.Select(p => p.ID), "mio policies", errors);
        ValidateDuplicateIds(designs.Select(d => d.ID), "shipDesigns", errors);
        ValidateDuplicateIds(researches.Select(r => r.ID), "researches", errors);
        ValidateDuplicateIds(spirits.Select(s => s.ID), "spirits", errors);
        ValidateDuplicateIds(planes.Select(p => p.ID), "planes", errors);
        ValidateDuplicateIds(forceCompositionsFile.Fleets.Select(f => f.ID), "fleets", errors);

        foreach (var plane in planes)
        {
            if (plane.Stats.ProductionCost < 0)
            {
                errors.Add($"plane '{plane.ID}' has negative productionCost {plane.Stats.ProductionCost}.");
            }

            if (plane.Stats.Reliability < 0)
            {
                errors.Add($"plane '{plane.ID}' has negative reliability {plane.Stats.Reliability}.");
            }
        }

        foreach (var hull in hulls)
        {
            if (hull.Manpower <= 0)
            {
                errors.Add($"hull '{hull.ID}' must define manpower greater than 0.");
            }

            foreach (var duplicate in (hull.Slots ?? []).GroupBy(slot => slot.ID).Where(group => group.Count() > 1))
            {
                errors.Add($"hull '{hull.ID}' has slot '{duplicate.Key}' more than once.");
            }
        }

        foreach (var shipType in shipTypes)
        {
            if (!Enum.TryParse<ShipRole>(shipType.Role, true, out _))
            {
                errors.Add($"shipType '{shipType.ID}' has unknown role '{shipType.Role}'.");
            }
        }

        var library = ContentLibrary.From(catalog);
        var designIds = designs.Select(d => d.ID).ToHashSet();
        var researchIds = researches.Select(r => r.ID).ToHashSet();
        var spiritIds = spirits.Select(s => s.ID).ToHashSet();
        var planeIds = planes.Select(p => p.ID).ToHashSet();
        var fleetIds = forceCompositionsFile.Fleets.Select(f => f.ID).ToHashSet();
        var knownTypes = KnownTypeFilters(catalog);

        ValidateScopedRoleFilters(researches.Select(r => (r.ID, r.AppliesToRoles)), "research", errors);
        ValidateScopedTypeFilters(researches.Select(r => (r.ID, r.AppliesToTypes ?? [])), "research", knownTypes, errors);

        var designById = new Dictionary<string, ShipDesign>();

        foreach (var design in designs)
        {
            var problems = new List<string>();
            var shipDesign = library.TryCreateDesign(design, problems);
            errors.AddRange(problems.Select(problem => $"shipDesign '{design.ID}': {problem}."));

            if (shipDesign is not null)
            {
                designById.TryAdd(design.ID, shipDesign);
            }
        }

        foreach (var fleet in forceCompositionsFile.Fleets)
        {
            if (fleet.ShipDesigns.Count == 0)
            {
                errors.Add($"fleet '{fleet.ID}' must include at least one ship design.");
            }

            foreach (var shipDesign in fleet.ShipDesigns)
            {
                if (string.IsNullOrWhiteSpace(shipDesign.Key))
                {
                    errors.Add($"fleet '{fleet.ID}' has an empty shipDesignID key.");
                    continue;
                }

                if (!designIds.Contains(shipDesign.Key))
                {
                    errors.Add($"fleet '{fleet.ID}' references unknown shipDesignID '{shipDesign.Key}'.");
                }

                if (shipDesign.Value <= 0)
                {
                    errors.Add($"fleet '{fleet.ID}' has non-positive ship count {shipDesign.Value} for shipDesignID '{shipDesign.Key}'.");
                }
            }

            foreach (var shipExperienceByDesign in fleet.ShipExperienceLevels)
            {
                if (!fleet.ShipDesigns.ContainsKey(shipExperienceByDesign.Key))
                {
                    errors.Add($"fleet '{fleet.ID}' has shipExperienceLevels for shipDesignID '{shipExperienceByDesign.Key}' that is not present in shipDesigns.");
                }

                if (!IsValidShipExperienceLevel(shipExperienceByDesign.Value, defines))
                {
                    errors.Add($"fleet '{fleet.ID}' has unsupported ship experience level {shipExperienceByDesign.Value} for shipDesignID '{shipExperienceByDesign.Key}'. Supported levels are 0 to {ExperienceRules.MaxLevel(defines)}.");
                }
            }

            foreach (var carrierAirwing in fleet.CarrierAirwings ?? [])
            {
                var shipDesignId = carrierAirwing.Key;

                if (!designIds.Contains(shipDesignId))
                {
                    errors.Add($"fleet '{fleet.ID}' has carrierAirwings for unknown shipDesignID '{shipDesignId}'.");
                    continue;
                }

                if (!fleet.ShipDesigns.TryGetValue(shipDesignId, out var carrierCount))
                {
                    errors.Add($"fleet '{fleet.ID}' has carrierAirwings for shipDesignID '{shipDesignId}' that is not present in shipDesigns.");
                }

                if (designById.TryGetValue(shipDesignId, out var carrierDesign) && carrierDesign.Role != ShipRole.Carrier)
                {
                    errors.Add($"fleet '{fleet.ID}' has carrierAirwings for non-carrier shipDesignID '{shipDesignId}'.");
                }

                var loadouts = carrierAirwing.Value ?? [];

                if (loadouts.Count > carrierCount)
                {
                    warnings.Add($"fleet '{fleet.ID}' has {loadouts.Count} carrierAirwings loadouts for {carrierCount} ships of '{shipDesignId}'; the extra ones are ignored.");
                }

                foreach (var (loadout, index) in loadouts.Select((loadout, index) => (loadout, index)))
                {
                    var where = $"carrier {index + 1} of '{shipDesignId}'";

                    foreach (var assignment in loadout?.Wings ?? [])
                    {
                        ValidateCarrierWing(fleet.ID, where, assignment, planes, errors);
                    }
                }
            }
        }

        ValidateScenario(scenarioDto, fleetIds, researchIds, spiritIds, planeIds, defines, errors);
        ValidateDoctrines("attacker", scenarioDto.Attacker, library, errors);
        ValidateDoctrines("defender", scenarioDto.Defender, library, errors);
        ValidateMios("attacker", scenarioDto.Attacker, library, errors);
        ValidateMios("defender", scenarioDto.Defender, library, errors);
        ValidateCountry("attacker", scenarioDto.Attacker, catalog, errors);
        ValidateCountry("defender", scenarioDto.Defender, catalog, errors);
        ValidateAirBases("attacker", scenarioDto.Attacker, catalog, errors);
        ValidateAirBases("defender", scenarioDto.Defender, catalog, errors);
        ValidateAdmiral("attacker", scenarioDto.Attacker, library, errors);
        ValidateAdmiral("defender", scenarioDto.Defender, library, errors);
        var fleetsById = forceCompositionsFile.Fleets.GroupBy(fleet => fleet.ID).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        ValidatePrideOfTheFleet("attacker", scenarioDto.Attacker, fleetsById, errors);
        ValidatePrideOfTheFleet("defender", scenarioDto.Defender, fleetsById, errors);
        var (location, weatherSetup) = LocationLoader.Build(scenarioDto, catalog, errors, warnings);

        if (errors.Count > 0)
        {
            throw new SetupValidationException(errors);
        }

        var researchById = library.Researches;
        var spiritById = library.Spirits;

        var fleetDtoById = forceCompositionsFile.Fleets.ToDictionary(fleet => fleet.ID, StringComparer.Ordinal);
        var attacker = BuildParticipant(scenarioDto.Attacker, fleetDtoById, designById, researchById, spiritById, library, defines, catalog);
        var defender = BuildParticipant(scenarioDto.Defender, fleetDtoById, designById, researchById, spiritById, library, defines, catalog);
        WarnAboutDoctrineRules("attacker", attacker);
        WarnAboutDoctrineRules("defender", defender);
        WarnAboutMioRules("attacker", scenarioDto.Attacker, attacker, catalog);
        WarnAboutMioRules("defender", scenarioDto.Defender, defender, catalog);
        WarnAboutAdmiral("attacker", attacker, library);
        WarnAboutAdmiral("defender", defender, library);
        WarnAboutPrideOfTheFleet("attacker", attacker);
        WarnAboutPrideOfTheFleet("defender", defender);

        return new BattleScenario(
            scenarioDto.ID,
            scenarioDto.Terrain,
            scenarioDto.Weather,
            scenarioDto.Iterations ?? 1,
            scenarioDto.Seed,
            attacker,
            defender,
            library.Planes,
            defines,
            rules,
            catalog.Aces.Where(ace => !string.IsNullOrWhiteSpace(ace.ID))
                .Select(ace => new AceTemplate(ace.ID, new HashSet<string>(ace.Types ?? [], StringComparer.Ordinal), ace.Chance,
                    new Dictionary<string, double>(ace.Effects ?? [], StringComparer.Ordinal)))
                .ToList(),
            location,
            weatherSetup);
    }

    private GameDefines LoadDefines(
        string dataDirectoryPath,
        IEnumerable<string> scenarioSources,
        IEnumerable<string> extraSources,
        List<string> errors)
    {
        var sources = (scenarioSources ?? [])
            .Select(source => Path.IsPathRooted(source) ? source : Path.Combine(dataDirectoryPath, source))
            .Concat(extraSources)
            .ToList();

        try
        {
            var (defines, report) = DefinesLoader.Load(sources);
            DefinesReport = report;
            warnings.AddRange(report.TypeMismatches.Select(mismatch => $"defines: {mismatch}"));
            warnings.AddRange(report.ParseWarnings.Select(warning => $"defines: {warning}"));
            errors.AddRange(defines.Validate().Select(problem => $"defines: {problem}"));
            return defines;
        }
        catch (Exception exception) when (exception is IOException or FormatException or UnauthorizedAccessException or ArgumentException)
        {
            errors.Add($"defines: {exception.Message}");
            return GameDefines.Vanilla();
        }
    }

    private static BattleRules BuildRules(BattleScenarioDto scenario, List<string> errors)
    {
        var rules = new BattleRules
        {
            MaxHours = scenario.MaxHours > 0 ? scenario.MaxHours : new BattleRules().MaxHours,
            ContinueAfterRetreat = scenario.ContinueAfterRetreat ?? false,
            RetreatEnabled = !(scenario.DontRetreat ?? false)
        };

        var dto = scenario.Rules;

        if (dto is null)
        {
            return rules;
        }

        return rules with
        {
            DamageRandomness = ParseEnum(dto.DamageRandomness, rules.DamageRandomness, "battleScenario.rules.damageRandomness", errors),
            NightEnabled = dto.NightEnabled ?? rules.NightEnabled,
            WeatherEnabled = dto.WeatherEnabled ?? rules.WeatherEnabled,
            NavalDominanceEnabled = dto.NavalDominanceEnabled ?? rules.NavalDominanceEnabled,
            CarrierWingsExecuteNavalStrike = dto.CarrierWingsExecuteNavalStrike ?? rules.CarrierWingsExecuteNavalStrike,
            StartHourOfDay = dto.StartHourOfDay ?? rules.StartHourOfDay,
            AirRegionPhase = dto.AirRegionPhase ?? rules.AirRegionPhase,
            NightStartHour = dto.NightStartHour ?? rules.NightStartHour,
            NightEndHour = dto.NightEndHour ?? rules.NightEndHour
        };
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback, string fieldName, List<string> errors)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Enum.TryParse<TEnum>(value, true, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{fieldName} has unknown value '{value}'. Expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        return fallback;
    }

    private static Fleet BuildFleet(
        FleetDto fleet,
        IReadOnlyDictionary<string, ShipDesign> designById,
        int? experienceLevelOverride,
        GameDefines defines)
    {
        var ships = new List<Ship>();

        foreach (var shipDesign in fleet.ShipDesigns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var design = designById[shipDesign.Key];
            var experienceLevel = experienceLevelOverride ??
                                  (fleet.ShipExperienceLevels.TryGetValue(shipDesign.Key, out var configuredLevel) ? configuredLevel : 2);
            var experience = ExperienceRules.ExperienceForLevel(experienceLevel, defines);

            for (var i = 1; i <= shipDesign.Value; i++)
            {
                ships.Add(new Ship($"{fleet.ID}_{shipDesign.Key}_{i:D3}", design, experience));
            }
        }

        return new Fleet(fleet.ID, ships, ParseCarrierAirwings(fleet, ships));
    }

    // S-DOCTRINE: optional; data imported before doctrines existed has none.
    private static void ReadDoctrineFolders(ContentCatalog catalog)
    {
        var directory = Path.Combine(catalog.DataDirectory, "doctrines");

        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = $"doctrines/{Path.GetFileName(path)}";

            try
            {
                var folder = JsonSerializer.Deserialize<DoctrineFolderDto>(File.ReadAllText(path), JsonOptions);

                if (folder is null || string.IsNullOrWhiteSpace(folder.ID))
                {
                    catalog.ReadErrors.Add($"File '{relativePath}' must contain a doctrine folder with an 'id'.");
                    continue;
                }

                catalog.SetSourceFile(folder, relativePath);
                catalog.DoctrineFolders.Add(folder);
            }
            catch (JsonException exception)
            {
                catalog.ReadErrors.Add($"File '{relativePath}' has invalid JSON: {exception.Message}");
            }
        }
    }

    private static List<TItem> ReadCollectionFromFolderOrFile<TItem>(
        ContentCatalog catalog,
        string folderName,
        string legacyFileName,
        string rootPropertyName)
        where TItem : class
    {
        var folderPath = Path.Combine(catalog.DataDirectory, folderName);

        if (Directory.Exists(folderPath))
        {
            var jsonFiles = Directory
                .GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (jsonFiles.Count == 0)
            {
                catalog.ReadErrors.Add($"Directory '{folderName}' does not contain any JSON files.");
                return [];
            }

            var items = new List<TItem>();

            foreach (var filePath in jsonFiles)
            {
                items.AddRange(ReadCollectionFile<TItem>(catalog, filePath, $"{folderName}/{Path.GetFileName(filePath)}", rootPropertyName));
            }

            return items;
        }

        var legacyPath = Path.Combine(catalog.DataDirectory, legacyFileName);
        return ReadCollectionFile<TItem>(catalog, legacyPath, legacyFileName, rootPropertyName);
    }

    private static List<TItem> ReadCollectionFile<TItem>(ContentCatalog catalog, string filePath, string relativePath, string rootPropertyName)
        where TItem : class
    {
        var errors = catalog.ReadErrors;

        if (!File.Exists(filePath))
        {
            errors.Add($"Missing required setup source: {Path.GetFileName(filePath)}");
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            JsonElement arrayElement;

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                arrayElement = document.RootElement;
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Object &&
                     document.RootElement.TryGetProperty(rootPropertyName, out var wrappedArray) &&
                     wrappedArray.ValueKind == JsonValueKind.Array)
            {
                arrayElement = wrappedArray;
            }
            else
            {
                errors.Add($"File '{Path.GetFileName(filePath)}' must contain '{rootPropertyName}' array or be a JSON array.");
                return [];
            }

            var items = new List<TItem>();

            foreach (var itemElement in arrayElement.EnumerateArray())
            {
                var item = JsonSerializer.Deserialize<TItem>(itemElement.GetRawText(), JsonOptions);

                if (item is null)
                {
                    errors.Add($"File '{Path.GetFileName(filePath)}' contains an invalid '{rootPropertyName}' entry.");
                    continue;
                }

                catalog.SetSourceFile(item, relativePath);
                items.Add(item);
            }

            return items;
        }
        catch (JsonException ex)
        {
            errors.Add($"File '{Path.GetFileName(filePath)}' has invalid JSON: {ex.Message}");
            return [];
        }
    }

    private static T? ReadJsonFile<T>(string dataDirectoryPath, string fileName, List<string> errors)
    {
        var path = Path.Combine(dataDirectoryPath, fileName);

        if (!File.Exists(path))
        {
            errors.Add($"Missing required setup file: {fileName}");
            return default;
        }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (data is null)
            {
                errors.Add($"File '{fileName}' is empty or could not be parsed.");
            }

            return data;
        }
        catch (JsonException ex)
        {
            errors.Add($"File '{fileName}' has invalid JSON: {ex.Message}");
            return default;
        }
    }

    // S-STATS-COUNTRY: country modifiers the combat rules do not read yet would otherwise be dropped without notice.
    private static void ValidateCountryModifierWarnings(ContentCatalog catalog)
    {
        foreach (var research in catalog.Researches)
        {
            foreach (var key in (research.CountryModifiers ?? []).Keys.Where(key => !BattleParticipant.IsModelledCountryModifier(key)))
            {
                catalog.Warnings.Add(BattleParticipant.CountryModifiersWithoutBattleEffect.Contains(key)
                    ? $"{catalog.SourceFileOf(research)} '{research.ID}': country modifier '{key}' has no effect in battle (the game reads it from admirals only or outside combat)."
                    : $"{catalog.SourceFileOf(research)} '{research.ID}': country modifier '{key}' is not simulated yet and is ignored.");
            }
        }
    }

    // S-DESIGN: designs that break the designer's module restrictions are still simulated, unless they override them.
    // S-CRIT-PART: parts named by modules and ship types must be defined, or crits on those ships cannot damage them.
    private static void ValidateCriticalPartWarnings(ContentCatalog catalog)
    {
        if (catalog.CriticalParts.Count == 0)
        {
            return;
        }

        var known = catalog.CriticalParts.Select(part => part.ID).ToHashSet(StringComparer.Ordinal);
        var users = catalog.Modules.SelectMany(module => (module.CriticalParts ?? []).Select(part => (Item: $"module '{module.ID}'", Part: part)))
            .Concat(catalog.ShipTypes.SelectMany(type => (type.CriticalParts ?? []).Select(part => (Item: $"ship type '{type.ID}'", Part: part))));

        foreach (var (item, part) in users.Where(user => !known.Contains(user.Part)))
        {
            catalog.Warnings.Add($"{item}: critical part '{part}' is not defined in critical-parts/ and is ignored.");
        }
    }

    // S-MIO-MATCH: a design assigned to an MIO whose equipment types do not cover it gets the bonuses anyway.
    private static void ValidateMioWarnings(ContentCatalog catalog)
    {
        var library = ContentLibrary.From(catalog);

        foreach (var design in catalog.ShipDesigns.Where(design => !string.IsNullOrWhiteSpace(design.MioID)))
        {
            if (library.TryCreateDesign(design) is { Mio: { } mio } shipDesign && !MioRules.CanAssign(mio, shipDesign))
            {
                catalog.Warnings.Add($"{catalog.SourceFileOf(design)} '{design.ID}': MIO '{mio.ID}' does not cover this design ({string.Join(", ", mio.EquipmentTypes)}); the game would not allow it.");
            }
        }
    }

    private static void ValidateDesignRuleWarnings(ContentCatalog catalog)
    {
        var library = ContentLibrary.From(catalog);

        foreach (var design in catalog.ShipDesigns.Where(design => !design.AllowAnyModule))
        {
            foreach (var issue in library.CheckDesign(design)?.Issues.Where(issue => issue.IsRestriction) ?? [])
            {
                catalog.Warnings.Add($"{catalog.SourceFileOf(design)} '{design.ID}': {issue.Message}.");
            }
        }
    }

    private static void ValidateModuleStatWarnings(string dataDirectoryPath, List<string> warnings)
    {
        var moduleFiles = GetModuleSourceFiles(dataDirectoryPath);

        if (moduleFiles.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var modulesPath in moduleFiles)
            {
                ValidateDuplicateModuleStatKeysWithReader(modulesPath, warnings);
                using var document = JsonDocument.Parse(File.ReadAllText(modulesPath));

                if (!TryGetCollectionArray(document.RootElement, "modules", out var modulesElement))
                {
                    continue;
                }

                for (var index = 0; index < modulesElement.GetArrayLength(); index++)
                {
                    var moduleElement = modulesElement[index];
                    var moduleId = TryGetModuleId(moduleElement, index);

                    ValidateModuleStatBlock(moduleElement, moduleId, "statModifiers", warnings);
                    ValidateModuleStatBlock(moduleElement, moduleId, "statAverages", warnings);
                    ValidateModuleStatBlock(moduleElement, moduleId, "statMultipliers", warnings);
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON is already captured as a setup error by ReadJsonFile.
        }
    }

    private static readonly HashSet<string> StatBlockNames = new(["baseStats", "statModifiers", "statAverages", "statMultipliers"], StringComparer.Ordinal);

    // Unknown keys in a stat block are dropped by the JSON reader without notice, so report them for every collection.
    private static void ValidateStatBlockWarnings(string dataDirectoryPath, string folder, List<string> warnings)
    {
        var directory = Path.Combine(dataDirectoryPath, folder);

        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                ValidateStatBlocksIn(document.RootElement, $"{folder}/{Path.GetFileName(path)}", warnings);
            }
            catch (JsonException)
            {
                // Invalid JSON is already captured as a setup error by the collection reader.
            }
        }
    }

    private static void ValidateStatBlocksIn(JsonElement element, string context, List<string> warnings)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidateStatBlocksIn(item, context, warnings);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var id = element.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? $"{context} '{idElement.GetString()}'"
            : context;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == "statMultiplier")
            {
                warnings.Add($"{id}: block 'statMultiplier' is ignored (did you mean 'statMultipliers'?).");
            }
            else if (StatBlockNames.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var stat in property.Value.EnumerateObject().Where(stat => !KnownShipStatsKeys.Contains(stat.Name)))
                {
                    warnings.Add($"{id}: unknown stat '{stat.Name}' in '{property.Name}' is ignored.");
                }
            }
            else
            {
                ValidateStatBlocksIn(property.Value, context, warnings);
            }
        }
    }

    private static List<string> GetModuleSourceFiles(string dataDirectoryPath)
    {
        var modulesFolder = Path.Combine(dataDirectoryPath, "modules");

        if (Directory.Exists(modulesFolder))
        {
            return Directory
                .GetFiles(modulesFolder, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var legacyModulesPath = Path.Combine(dataDirectoryPath, "modules.json");
        return File.Exists(legacyModulesPath) ? [legacyModulesPath] : [];
    }

    private static bool TryGetCollectionArray(JsonElement rootElement, string rootPropertyName, out JsonElement arrayElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            arrayElement = rootElement;
            return true;
        }

        if (rootElement.ValueKind == JsonValueKind.Object &&
            rootElement.TryGetProperty(rootPropertyName, out var wrappedArray) &&
            wrappedArray.ValueKind == JsonValueKind.Array)
        {
            arrayElement = wrappedArray;
            return true;
        }

        arrayElement = default;
        return false;
    }

    private static string TryGetModuleId(JsonElement moduleElement, int index)
    {
        if (moduleElement.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
        {
            var id = idElement.GetString();

            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return $"modules[{index}]";
    }

    private static void ValidateModuleStatBlock(
        JsonElement moduleElement,
        string moduleId,
        string blockName,
        List<string> warnings)
    {
        if (!moduleElement.TryGetProperty(blockName, out var statBlock) || statBlock.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in statBlock.EnumerateObject())
        {
            if (!seenKeys.Add(property.Name))
            {
                warnings.Add($"module '{moduleId}' has duplicate modifier '{property.Name}' in '{blockName}'.");
            }

            if (!KnownShipStatsKeys.Contains(property.Name))
            {
                warnings.Add($"module '{moduleId}' has unknown modifier '{property.Name}' in '{blockName}'.");
            }
        }
    }

    private static void ValidateDuplicateModuleStatKeysWithReader(string modulesPath, List<string> warnings)
    {
        var bytes = File.ReadAllBytes(modulesPath);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { AllowTrailingCommas = true });

        var moduleIndex = -1;
        var inModulesArray = false;

        while (reader.Read())
        {
            if (!inModulesArray)
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.GetString() == "modules")
                {
                    reader.Read();

                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        inModulesArray = true;
                    }
                }

                continue;
            }

            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                continue;
            }

            moduleIndex++;
            ValidateModuleWithReader(ref reader, moduleIndex, warnings);
        }
    }

    private static void ValidateModuleWithReader(ref Utf8JsonReader reader, int moduleIndex, List<string> warnings)
    {
        var moduleId = $"modules[{moduleIndex}]";
        var blockNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString() ?? string.Empty;
            reader.Read();

            if (propertyName.Equals("id", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
            {
                var idValue = reader.GetString();

                if (!string.IsNullOrWhiteSpace(idValue))
                {
                    moduleId = idValue;
                }

                continue;
            }

            if (propertyName is "statModifiers" or "statAverages" or "statMultipliers" && reader.TokenType == JsonTokenType.StartObject)
            {
                if (!blockNames.Add(propertyName))
                {
                    warnings.Add($"module '{moduleId}' has duplicate '{propertyName}' blocks.");
                }

                ValidateStatBlockWithReader(ref reader, moduleId, propertyName, warnings);
                continue;
            }

            SkipValue(ref reader);
        }
    }

    private static void ValidateStatBlockWithReader(
        ref Utf8JsonReader reader,
        string moduleId,
        string blockName,
        List<string> warnings)
    {
        var statKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var statKey = reader.GetString() ?? string.Empty;

            if (!statKeys.Add(statKey))
            {
                warnings.Add($"module '{moduleId}' has duplicate modifier '{statKey}' in '{blockName}'.");
            }

            reader.Read();
            SkipValue(ref reader);
        }
    }

    private static void SkipValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
        {
            return;
        }

        var depth = 0;

        do
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                depth++;
            }
            else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
            }
        } while (depth > 0 && reader.Read());
    }

    private static void ValidateRequiredIds(IEnumerable<string> ids, string sectionName, List<string> errors)
    {
        var index = 0;

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{sectionName}[{index}] has empty ID.");
            }

            index++;
        }
    }

    private static void ValidateDuplicateIds(IEnumerable<string> ids, string sectionName, List<string> errors)
    {
        var duplicates = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicate in duplicates)
        {
            errors.Add($"Duplicate ID '{duplicate}' found in section '{sectionName}'.");
        }
    }

    private static void ValidateScenario(
        BattleScenarioDto scenario,
        HashSet<string> fleetIds,
        HashSet<string> researchIds,
        HashSet<string> spiritIds,
        HashSet<string> planeIds,
        GameDefines defines,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(scenario.ID))
        {
            errors.Add("battleScenario.id must not be empty.");
        }


        if (scenario.Iterations.HasValue && scenario.Iterations.Value <= 0)
        {
            errors.Add("battleScenario.iterations must be greater than 0 when provided.");
        }

        ValidateParticipant("attacker", scenario.Attacker, fleetIds, researchIds, spiritIds, planeIds, defines, errors);
        ValidateParticipant("defender", scenario.Defender, fleetIds, researchIds, spiritIds, planeIds, defines, errors);
    }

    private static void ValidateParticipant(
        string role,
        BattleParticipantDto participant,
        HashSet<string> fleetIds,
        HashSet<string> researchIds,
        HashSet<string> spiritIds,
        HashSet<string> planeIds,
        GameDefines defines,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(participant.FleetID))
        {
            errors.Add($"battleScenario.{role}.fleetID must not be empty.");
        }
        else if (!fleetIds.Contains(participant.FleetID))
        {
            errors.Add($"battleScenario.{role}.fleetID references unknown fleet '{participant.FleetID}'.");
        }

        foreach (var researchId in participant.ResearchIDs)
        {
            if (!researchIds.Contains(researchId))
            {
                errors.Add($"battleScenario.{role}.researchIDs references unknown research '{researchId}'.");
            }
        }

        foreach (var spiritId in participant.SpiritIDs)
        {
            if (!spiritIds.Contains(spiritId))
            {
                errors.Add($"battleScenario.{role}.spiritIDs references unknown spirit '{spiritId}'.");
            }
        }

        foreach (var landBased in participant.LandBasedAirwings)
        {
            if (!planeIds.Contains(landBased.PlaneID))
            {
                errors.Add($"battleScenario.{role}.landBasedAirwings references unknown plane '{landBased.PlaneID}'.");
            }

            if (landBased.Planes < 0 || landBased.MissionEfficiency < 0 || landBased.DisruptionFactor < 0)
            {
                errors.Add($"battleScenario.{role}.landBasedAirwings '{landBased.PlaneID}' needs planes, missionEfficiency and disruptionFactor >= 0.");
            }

            if (landBased.Mission is { Length: > 0 } mission && mission != LandBasedAirwing.NavalStrike && mission != LandBasedAirwing.AirSuperiority)
            {
                errors.Add($"battleScenario.{role}.landBasedAirwings '{landBased.PlaneID}' has mission '{mission}'; use " +
                           $"'{LandBasedAirwing.NavalStrike}' or '{LandBasedAirwing.AirSuperiority}'.");
            }
        }

        if (participant.RadarCoverage is < 0 or > 1)
        {
            errors.Add($"battleScenario.{role}.radarCoverage must be between 0 and 1.");
        }

        if (participant.NavalDominanceStart is < 0)
        {
            errors.Add($"battleScenario.{role}.navalDominanceStart must not be negative.");
        }

        foreach (var (planeId, count) in participant.PlaneStockpile)
        {
            if (!planeIds.Contains(planeId))
            {
                errors.Add($"battleScenario.{role}.planeStockpile references unknown plane '{planeId}'.");
            }

            if (count < 0)
            {
                errors.Add($"battleScenario.{role}.planeStockpile '{planeId}' must be >= 0.");
            }
        }

        if (participant.ShipExperienceLevel.HasValue &&
            !IsValidShipExperienceLevel(participant.ShipExperienceLevel.Value, defines))
        {
            errors.Add($"battleScenario.{role}.shipExperienceLevel has unsupported level {participant.ShipExperienceLevel.Value}. Supported levels are 0 to {ExperienceRules.MaxLevel(defines)}.");
        }

        ParseEnum(participant.Aggressiveness, Aggressiveness.EngageAtMediumRisk, $"battleScenario.{role}.aggressiveness", errors);
        ParseEnum(participant.RepairPriority, RepairPriority.High, $"battleScenario.{role}.repairPriority", errors);
    }

    private static BattleParticipant BuildParticipant(
        BattleParticipantDto participant,
        IReadOnlyDictionary<string, FleetDto> fleetDtoById,
        IReadOnlyDictionary<string, ShipDesign> designById,
        IReadOnlyDictionary<string, Research> researchById,
        IReadOnlyDictionary<string, Spirit> spiritById,
        ContentLibrary library,
        GameDefines defines,
        ContentCatalog catalog)
    {
        // Each side gets its own Fleet and Ship instances, even when both sides use the same fleet definition.
        var fleet = BuildFleet(fleetDtoById[participant.FleetID], designById, participant.ShipExperienceLevel, defines);
        var pride = PrideOfTheFleetShip(fleet, participant.PrideOfTheFleet);
        var participantResearches = participant.ResearchIDs.Select(researchId => researchById[researchId]).ToList();
        var participantSpirits = participant.SpiritIDs.Select(spiritId => spiritById[spiritId]).ToList();

        return new BattleParticipant(
            fleet,
            participant.Admiral is { } admiral ? library.CreateAdmiral(admiral, defines) : null,
            participantResearches,
            participantSpirits,
            ParseEnum(participant.Aggressiveness, Aggressiveness.EngageAtMediumRisk, "aggressiveness", []),
            ParseEnum(participant.RepairPriority, RepairPriority.High, "repairPriority", []),
            participant.CarrierStanceIndex,
            participant.CarrierNightTraffic ?? 0,
            BuildDoctrines(participant, library),
            defines,
            BuildMios(participant, library),
            BuildDesignMios(participant, library),
            pride,
            participant.LandBasedAirwings.Select(entry => new LandBasedAirwing(entry.PlaneID, entry.Planes, entry.MissionEfficiency ?? 1.0, entry.DisruptionFactor ?? 1.0,
                string.IsNullOrEmpty(entry.Mission) ? LandBasedAirwing.NavalStrike : entry.Mission, entry.AirBase)).ToList(),
            new Dictionary<string, int>(participant.PlaneStockpile, StringComparer.Ordinal),
            participant.CarrierWingsOnRegionMission,
            participant.CarrierMissionEfficiency ?? 1.0,
            participant.RadarCoverage ?? 0,
            BuildAirBases(participant, catalog),
            participant.NavalDominanceStart);
    }

    /// <summary>
    /// A-COVERAGE: the side's air bases: each listed one with its settings, and each other state a land wing flies from with
    /// none (no crowding, supply 1).
    /// </summary>
    private static List<AirBaseSetup> BuildAirBases(BattleParticipantDto participant, ContentCatalog catalog)
    {
        var states = participant.AirBases.Select(entry => entry.State)
            .Concat(participant.LandBasedAirwings.Where(entry => entry.AirBase is not null).Select(entry => entry.AirBase!.Value))
            .Distinct()
            .ToList();
        var result = new List<AirBaseSetup>();

        foreach (var state in states)
        {
            if (catalog.AirBases.LastOrDefault(item => item.State == state) is not { } dto)
            {
                continue;
            }

            var setup = participant.AirBases.FirstOrDefault(entry => entry.State == state);
            var airBase = new AirBase(dto.State, dto.Name ?? $"State {dto.State}", (int)dto.X, (int)dto.Y);
            result.Add(new AirBaseSetup(airBase, setup?.Level, setup?.OtherPlanes ?? 0, setup?.SupplyRatio ?? 1.0));
        }

        return result;
    }

    /// <summary>A-COVERAGE: air bases must be imported states; levels, other planes and supply must not be negative.</summary>
    private static void ValidateAirBases(string role, BattleParticipantDto participant, ContentCatalog catalog, List<string> errors)
    {
        var states = participant.AirBases.Select(entry => entry.State)
            .Concat(participant.LandBasedAirwings.Where(entry => entry.AirBase is not null).Select(entry => entry.AirBase!.Value))
            .Distinct();

        foreach (var state in states.Where(state => catalog.AirBases.All(item => item.State != state)))
        {
            errors.Add(catalog.AirBases.Count == 0
                ? $"battleScenario.{role}: air base of state {state} needs the air bases (run 'NavySimulator import-regions')."
                : $"battleScenario.{role}: no air base in state {state}.");
        }

        foreach (var setup in participant.AirBases.Where(entry => entry.Level < 0 || entry.OtherPlanes < 0 || entry.SupplyRatio < 0))
        {
            errors.Add($"battleScenario.{role}.airBases: state {setup.State} needs level, otherPlanes and supplyRatio >= 0.");
        }
    }

    /// <summary>S-POTF: the first ship of the named design in the fleet (ships of one design are alike); null when none.</summary>
    public static Ship? PrideOfTheFleetShip(Fleet fleet, string? designId)
    {
        return string.IsNullOrEmpty(designId) ? null : fleet.Ships.FirstOrDefault(ship => ship.Design.ID == designId);
    }

    /// <summary>S-POTF: the pride of the fleet must name a design of the side's fleet.</summary>
    private static void ValidatePrideOfTheFleet(string role, BattleParticipantDto participant, IReadOnlyDictionary<string, FleetDto> fleets, List<string> errors)
    {
        if (string.IsNullOrEmpty(participant.PrideOfTheFleet) || !fleets.TryGetValue(participant.FleetID, out var fleet))
        {
            return;
        }

        if (!fleet.ShipDesigns.ContainsKey(participant.PrideOfTheFleet))
        {
            errors.Add($"battleScenario.{role}.prideOfTheFleet names design '{participant.PrideOfTheFleet}', which is not in fleet '{fleet.ID}'.");
        }
    }

    /// <summary>
    /// S-POTF [RE] 0xc33a50, 0xc34280: a pride of the fleet the game would not let a player assign (not a capital ship or
    /// carrier) is simulated, with a warning, as the game does for one set by a history file.
    /// </summary>
    private void WarnAboutPrideOfTheFleet(string role, BattleParticipant participant)
    {
        if (participant.PrideOfTheFleet is { } pride && !PrideOfTheFleetRules.CanBePrideOfTheFleet(pride.Design))
        {
            warnings.Add($"{role}: the pride of the fleet ({pride.Design.ID}) is a {pride.Design.ShipType.ID}, not a capital ship or carrier; it is simulated anyway.");
        }
    }

    /// <summary>
    /// S-MIO: the side's assignments of MIOs to designs (an empty ID: none). Lenient, for previews: unknown MIOs are left
    /// out (<see cref="ValidateMios"/> reports them as errors).
    /// </summary>
    public static IReadOnlyDictionary<string, Engine.Mios.Mio?> BuildDesignMios(BattleParticipantDto participant, ContentLibrary library)
    {
        var assignments = new Dictionary<string, Engine.Mios.Mio?>(StringComparer.Ordinal);

        foreach (var (designId, mioId) in participant.DesignMios ?? [])
        {
            if (string.IsNullOrEmpty(mioId))
            {
                assignments[designId] = null;
            }
            else if (library.Mios.TryGetValue(mioId, out var mio))
            {
                assignments[designId] = mio;
            }
        }

        return assignments;
    }

    /// <summary>S-ADMIRAL: errors for an admiral the simulator cannot build: unknown traits, negative skill levels.</summary>
    private static void ValidateAdmiral(string role, BattleParticipantDto participant, ContentLibrary library, List<string> errors)
    {
        if (participant.Admiral is not { } admiral)
        {
            return;
        }

        foreach (var trait in (admiral.Traits ?? []).Where(trait => !library.LeaderTraits.ContainsKey(trait)))
        {
            errors.Add($"battleScenario.{role}.admiral.traits references unknown leader trait '{trait}' (import leaders with 'NavySimulator import-leaders').");
        }

        foreach (var (key, level) in AdmiralSkills(admiral).Where(skill => skill.Level < 0))
        {
            errors.Add($"battleScenario.{role}.admiral has {key} {level}; skill levels are 0 or more.");
        }
    }

    private static IEnumerable<(string Key, int Level)> AdmiralSkills(AdmiralDto admiral)
    {
        yield return (LeaderSkills.Skill, admiral.Skill);
        yield return (LeaderSkills.Attack, admiral.AttackSkill);
        yield return (LeaderSkills.Defense, admiral.DefenseSkill);
        yield return (LeaderSkills.Maneuvering, admiral.ManeuveringSkill);
        yield return (LeaderSkills.Coordination, admiral.CoordinationSkill);
    }

    /// <summary>
    /// S-ADMIRAL-SKILL, S-ADMIRAL-TRAIT: an admiral the game would not have is still simulated, with a warning: a skill above
    /// the highest level the skill files define (read at that level), traits whose parents the admiral lacks, mutually
    /// exclusive traits. Without imported skill levels the skills do nothing, which is warned about too.
    /// </summary>
    private void WarnAboutAdmiral(string role, BattleParticipant participant, ContentLibrary library)
    {
        if (participant.Admiral is not { } admiral)
        {
            return;
        }

        if (library.LeaderSkills.Levels.Count == 0)
        {
            warnings.Add($"{role}: no leader skill levels in the data directory (import them with 'NavySimulator import-leaders'); the admiral's skills have no effect.");
        }

        // The overall skill is read without a clamp (its navy levels are empty in vanilla and TFB).
        foreach (var (key, level) in admiral.SkillLevels.Where(skill => skill.Key != LeaderSkills.Skill))
        {
            var max = library.LeaderSkills.MaxLevel(key);

            if (max > 0 && level > max)
            {
                warnings.Add($"{role}: the admiral's {key} {level} is above the highest level the game defines ({max}); level {max} is used.");
            }
        }

        warnings.AddRange(AdmiralRules.TraitProblems(admiral).Select(problem => $"{role}: admiral {admiral.Name}: {problem}; it is simulated anyway."));
    }

    /// <summary>S-MIO-COUNTRY: a side's country must be a known tag, when tags are imported.</summary>
    private static void ValidateCountry(string role, BattleParticipantDto participant, ContentCatalog catalog, List<string> errors)
    {
        if (!string.IsNullOrEmpty(participant.Country) && catalog.Countries.Count > 0 && catalog.Countries.All(country => country.ID != participant.Country))
        {
            errors.Add($"battleScenario.{role}.country references unknown country tag '{participant.Country}'.");
        }
    }

    /// <summary>
    /// S-MIO: the side's MIO setups. Lenient, for previews: unknown MIOs, traits and policies are left out
    /// (<see cref="ValidateMios"/> reports them as errors).
    /// </summary>
    public static IReadOnlyList<MioSetup> BuildMios(BattleParticipantDto participant, ContentLibrary library)
    {
        var setups = new List<MioSetup>();

        foreach (var (id, setup) in participant.Mios ?? [])
        {
            if (setup is null || !library.Mios.TryGetValue(id, out var mio))
            {
                continue;
            }

            var traits = (setup.Traits ?? []).Distinct(StringComparer.Ordinal).Select(mio.Trait).OfType<MioTrait>().ToList();
            var policy = setup.Policy is { } policyId ? library.MioPolicies.GetValueOrDefault(policyId) : null;
            setups.Add(new MioSetup(mio, traits, policy));
        }

        return setups;
    }

    /// <summary>S-MIO: errors for MIO setups the simulator cannot build: unknown MIOs, traits, policies and assigned designs.</summary>
    private static void ValidateMios(string role, BattleParticipantDto participant, ContentLibrary library, List<string> errors)
    {
        foreach (var (designId, mioId) in participant.DesignMios ?? [])
        {
            if (!string.IsNullOrEmpty(mioId) && !library.Mios.ContainsKey(mioId))
            {
                errors.Add($"battleScenario.{role}.designMios.{designId} references unknown MIO '{mioId}' (import MIOs with 'NavySimulator import-mios').");
            }
        }

        foreach (var (id, setup) in participant.Mios ?? [])
        {
            var path = $"battleScenario.{role}.mios.{id}";

            if (!library.Mios.TryGetValue(id, out var mio))
            {
                errors.Add($"{path} references unknown MIO '{id}' (import MIOs with 'NavySimulator import-mios').");
                continue;
            }

            foreach (var trait in (setup?.Traits ?? []).Where(trait => mio.Trait(trait) is null))
            {
                errors.Add($"{path}.traits: MIO '{id}' has no trait '{trait}'.");
            }

            if (setup?.Policy is { } policy && !library.MioPolicies.ContainsKey(policy))
            {
                errors.Add($"{path}.policy references unknown MIO policy '{policy}'.");
            }
        }
    }

    /// <summary>
    /// S-MIO-TREE, S-MIO-POLICY, S-MIO-MATCH, S-MIO-COUNTRY: MIO setups the game would not allow are still simulated, with
    /// a warning: a trait whose parents are not unlocked, two mutually exclusive traits, a policy the MIO cannot take, a
    /// design the side assigns to an MIO that does not cover it, an assignment for a design not in the side's fleet, and
    /// an MIO the side's country cannot have.
    /// </summary>
    private void WarnAboutMioRules(string role, BattleParticipantDto dto, BattleParticipant participant, ContentCatalog catalog)
    {
        var fleetDesigns = participant.Fleet.Ships.Select(ship => ship.Design).DistinctBy(design => design.ID).ToList();

        foreach (var designId in (dto.DesignMios ?? []).Keys.Where(id => fleetDesigns.All(design => design.ID != id)))
        {
            warnings.Add($"{role}: designMios names design '{designId}', which is not in the side's fleet; it is ignored.");
        }

        foreach (var design in fleetDesigns.Where(design => dto.DesignMios?.ContainsKey(design.ID) == true))
        {
            if (participant.MioOf(design) is { } assigned && !MioRules.CanAssign(assigned, design))
            {
                warnings.Add($"{role}: MIO '{assigned.ID}' does not cover design '{design.ID}' ({string.Join(", ", assigned.EquipmentTypes)}); the game would not allow it.");
            }
        }

        var used = fleetDesigns.Select(participant.MioOf).OfType<Engine.Mios.Mio>().Select(mio => mio.ID).Concat(participant.Mios.Select(setup => setup.Mio.ID));

        foreach (var mio in used.Distinct(StringComparer.Ordinal).Select(id => catalog.Mios.FirstOrDefault(item => item.ID == id)).OfType<MioDto>()
                     .Where(mio => !MioCountries.IsAvailable(mio, dto.Country)))
        {
            warnings.Add($"{role}: {dto.Country} cannot have MIO '{mio.ID}' ({(mio.Countries.Count > 0 ? string.Join(", ", mio.Countries) : "archetype or excluded")}); it is simulated anyway.");
        }

        foreach (var setup in participant.Mios)
        {
            warnings.AddRange(MioRules.TreeProblems(setup).Select(problem => $"{role}: MIO '{setup.Mio.ID}': {problem}; it is simulated anyway."));

            if (setup.Policy is { } policy && !MioRules.PolicyAllowed(setup.Mio, policy))
            {
                warnings.Add($"{role}: MIO '{setup.Mio.ID}' cannot take policy '{policy.ID}' (it needs {string.Join(" or ", policy.AllowedEquipmentTypes)}); it is simulated anyway.");
            }
        }
    }

    /// <summary>
    /// S-DOCTRINE: the side's doctrine setups, one per folder with a grand doctrine. Lenient, for previews: unknown grand
    /// doctrines and subdoctrines are left out (<see cref="ValidateDoctrines"/> reports them as errors).
    /// </summary>
    public static IReadOnlyList<DoctrineSetup> BuildDoctrines(BattleParticipantDto participant, ContentLibrary library)
    {
        var setups = new List<DoctrineSetup>();

        foreach (var (_, setup) in participant.Doctrines ?? [])
        {
            if (setup is null || !library.GrandDoctrines.TryGetValue(setup.GrandDoctrine ?? string.Empty, out var grand))
            {
                continue;
            }

            var tracks = (setup.Tracks ?? [])
                .Where(track => track.Value is not null && library.Subdoctrines.ContainsKey(track.Value.Subdoctrine ?? string.Empty))
                .Select(track => new TrackSetup(track.Key, library.Subdoctrines[track.Value.Subdoctrine], track.Value.Mastery))
                .ToList();
            setups.Add(new DoctrineSetup(grand, tracks));
        }

        return setups;
    }

    /// <summary>
    /// S-DOCTRINE: errors for doctrine setups the simulator cannot build: unknown folders, grand doctrines and
    /// subdoctrines, a grand doctrine of another folder, tracks the grand doctrine does not open, a subdoctrine in a track
    /// it does not belong to, and negative mastery.
    /// </summary>
    private static void ValidateDoctrines(string role, BattleParticipantDto participant, ContentLibrary library, List<string> errors)
    {
        foreach (var (folder, setup) in participant.Doctrines ?? [])
        {
            var path = $"battleScenario.{role}.doctrines.{folder}";

            if (setup is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(setup.GrandDoctrine))
            {
                if ((setup.Tracks ?? []).Count > 0)
                {
                    errors.Add($"{path} has tracks but no grandDoctrine.");
                }

                continue;
            }

            if (!library.GrandDoctrines.TryGetValue(setup.GrandDoctrine, out var grand))
            {
                errors.Add($"{path}.grandDoctrine references unknown grand doctrine '{setup.GrandDoctrine}' (import doctrines with 'NavySimulator import-doctrines').");
                continue;
            }

            if (grand.Folder != folder)
            {
                errors.Add($"{path}.grandDoctrine '{grand.ID}' belongs to the {grand.Folder} folder.");
            }

            foreach (var (track, slot) in setup.Tracks ?? [])
            {
                if (!grand.Tracks.Contains(track))
                {
                    errors.Add($"{path}.tracks: '{grand.ID}' has no track '{track}' (tracks: {string.Join(", ", grand.Tracks)}).");
                }

                if (slot is null || string.IsNullOrWhiteSpace(slot.Subdoctrine))
                {
                    errors.Add($"{path}.tracks.{track}.subdoctrine must not be empty.");
                    continue;
                }

                if (!library.Subdoctrines.TryGetValue(slot.Subdoctrine, out var subdoctrine))
                {
                    errors.Add($"{path}.tracks.{track}.subdoctrine references unknown subdoctrine '{slot.Subdoctrine}'.");
                }
                else if (!subdoctrine.Tracks.Contains(track))
                {
                    errors.Add($"{path}.tracks.{track}: subdoctrine '{subdoctrine.ID}' belongs to track {string.Join(" or ", subdoctrine.Tracks)}.");
                }

                if (slot.Mastery < 0)
                {
                    errors.Add($"{path}.tracks.{track}.mastery must be >= 0.");
                }
            }
        }
    }

    /// <summary>
    /// S-DOCTRINE, S-SPIRIT: setups the game would not allow are still simulated, with a warning: a subdoctrine in two
    /// tracks without allow_in_multiple_tracks, xor'd subdoctrines in one folder, a spirit whose has_doctrine conditions
    /// the side's doctrines do not meet, and two spirits in one spirit slot.
    /// </summary>
    private void WarnAboutDoctrineRules(string role, BattleParticipant participant)
    {
        foreach (var setup in participant.Doctrines)
        {
            var slotted = setup.Tracks.Select(track => track.Subdoctrine).ToList();

            foreach (var repeated in slotted.GroupBy(subdoctrine => subdoctrine.ID).Where(group => group.Count() > 1 && !group.First().AllowInMultipleTracks))
            {
                warnings.Add($"{role}: subdoctrine '{repeated.Key}' is slotted in {repeated.Count()} tracks; the game allows one.");
            }

            // Either side of a pair may list the other; each pair is reported once.
            var pairs = slotted.SelectMany(subdoctrine => subdoctrine.Xor
                    .Where(id => id != subdoctrine.ID && slotted.Any(candidate => candidate.ID == id))
                    .Select(id => string.CompareOrdinal(subdoctrine.ID, id) < 0 ? (First: subdoctrine.ID, Second: id) : (First: id, Second: subdoctrine.ID)))
                .Distinct();

            foreach (var (first, second) in pairs)
            {
                warnings.Add($"{role}: subdoctrines '{first}' and '{second}' exclude each other (xor) in the game.");
            }
        }

        foreach (var spirit in participant.Spirits.Where(spirit => !spirit.IsAvailable(participant.Doctrines)))
        {
            var needs = spirit.RequiredDoctrines.Select(group => string.Join(" or ", group));
            warnings.Add($"{role}: spirit '{spirit.ID}' needs doctrine {string.Join(" and ", needs)}; it is simulated anyway.");
        }

        foreach (var slot in participant.Spirits.GroupBy(spirit => spirit.Category).Where(group => group.Count() > 1))
        {
            warnings.Add($"{role}: {slot.Count()} spirits in the '{slot.Key}' slot ({string.Join(", ", slot.Select(spirit => spirit.ID))}); the game allows one.");
        }
    }

    private static bool IsValidShipExperienceLevel(int level, GameDefines defines)
    {
        return level >= 0 && level <= ExperienceRules.MaxLevel(defines);
    }

    private static void ValidateScopedRoleFilters(
        IEnumerable<(string ID, List<string> AppliesToRoles)> entries,
        string entryType,
        List<string> errors)
    {
        foreach (var entry in entries)
        {
            foreach (var role in entry.AppliesToRoles)
            {
                if (!Enum.TryParse<ShipRole>(role, true, out _))
                {
                    errors.Add($"{entryType} '{entry.ID}' has unknown appliesToRoles value '{role}'.");
                }
            }
        }
    }

    // Type filters match a design's ship type, hull or hull archetype (ShipDesign.Types).
    private static HashSet<string> KnownTypeFilters(ContentCatalog catalog)
    {
        return catalog.ShipTypes.Select(type => type.ID)
            .Concat(catalog.Hulls.Select(hull => hull.ID))
            .Concat(catalog.Hulls.Select(hull => hull.Archetype))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateScopedTypeFilters(
        IEnumerable<(string ID, List<string> AppliesToTypes)> entries,
        string entryType,
        HashSet<string> knownTypes,
        List<string> errors)
    {
        foreach (var entry in entries)
        {
            foreach (var hullType in entry.AppliesToTypes)
            {
                if (!knownTypes.Contains(hullType))
                {
                    errors.Add($"{entryType} '{entry.ID}' has unknown appliesToTypes value '{hullType}'.");
                }
            }
        }
    }

    private void ValidateCarrierWing(string fleetId, string where, CarrierAirwingAssignmentDto assignment, List<PlaneDto> planes, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(assignment.PlaneID))
        {
            errors.Add($"fleet '{fleetId}' has a carrier wing with empty planeID on {where}.");
            return;
        }

        if (planes.FirstOrDefault(plane => plane.ID == assignment.PlaneID) is not { } plane)
        {
            errors.Add($"fleet '{fleetId}' has a carrier wing of unknown planeID '{assignment.PlaneID}' on {where}.");
        }
        else if (!plane.CarrierCapable)
        {
            warnings.Add($"fleet '{fleetId}' has '{assignment.PlaneID}' on {where}, a plane that is not carrier capable.");
        }

        if (assignment.Airwings <= 0)
        {
            errors.Add($"fleet '{fleetId}' has a non-positive airwings value {assignment.Airwings} on {where}.");
        }

        if (assignment.PlaneCount < 0)
        {
            errors.Add($"fleet '{fleetId}' has a negative planeCount {assignment.PlaneCount} on {where}.");
        }
    }

    /// <summary>A-WINGS: each carrier's wings by ship ID; the n-th loadout of a design goes to its n-th ship.</summary>
    private static Dictionary<string, List<CarrierAirwingAssignment>> ParseCarrierAirwings(FleetDto fleet, List<Ship> ships)
    {
        var parsed = new Dictionary<string, List<CarrierAirwingAssignment>>(StringComparer.Ordinal);

        foreach (var (designId, loadouts) in fleet.CarrierAirwings ?? [])
        {
            var designShips = ships.Where(ship => ship.Design.ID == designId).ToList();

            foreach (var (loadout, ship) in (loadouts ?? []).Zip(designShips))
            {
                var assignments = (loadout?.Wings ?? [])
                    .Where(wing => !string.IsNullOrWhiteSpace(wing.PlaneID) && wing.Airwings > 0)
                    .Select(wing => new CarrierAirwingAssignment(wing.PlaneID, wing.Airwings, wing.PlaneCount == 0 ? DefaultWingPlanes : wing.PlaneCount))
                    .ToList();

                if (assignments.Count > 0)
                {
                    parsed[ship.ID] = assignments;
                }
            }
        }

        return parsed;
    }
}


