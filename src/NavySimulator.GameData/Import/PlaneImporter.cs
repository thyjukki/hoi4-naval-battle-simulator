using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Reads the game's fixed plane equipment (A-PLANES) [PDX]: every variant of <c>common/units/equipment</c> whose archetype
/// is an air archetype (<c>interface_category = interface_category_air</c>) and that has no <c>module_slots</c> of its
/// own, so the plane designer's airframes and the missiles are left out. An archetype is an <c>is_archetype</c> entry or
/// a <c>duplicate_archetypes</c> entry (BBA's role archetypes such as <c>small_plane_cas_airframe</c>: its base
/// archetype with the keys it sets replaced); fixed planes can have either, also an airframe with module slots (TFB's
/// planes since its air rewrite: <c>fighter_equipment_1</c> on <c>small_plane_airframe</c>). Keys a variant leaves out are
/// its archetype's. Each plane also gets the air sub-unit (<c>common/units/*.txt</c> <c>sub_units</c>) whose
/// <c>need</c> names its archetype: its <c>type</c> and wing sizes (A-WINGS).
/// </summary>
public static class PlaneImporter
{
    private const string AirCategory = "interface_category_air";

    // Air archetypes that are not planes.
    private static readonly HashSet<string> MissileTypes = new(["missile", "ballistic_missile", "nuclear_missile", "sam_missile", "emplacement_gun_ammo"], StringComparer.Ordinal);

    public static PlaneImport Read(GameFileSystem files, string language = "english")
    {
        var import = new PlaneImport();
        var localisation = LocalisationTable.Load(files, language);
        var equipment = ReadEntries(files, "common/units/equipment", "equipments", import);
        var subUnits = ReadEntries(files, "common/units", "sub_units", import);
        var archetypes = equipment
            .Where(entry => entry.Value.GetBool("is_archetype") == true)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        foreach (var (id, duplicate) in ReadEntries(files, "common/units/equipment", "duplicate_archetypes", import))
        {
            if (duplicate.GetString("archetype") is { } baseId && archetypes.TryGetValue(baseId, out var baseArchetype))
            {
                archetypes.TryAdd(id, Override(baseArchetype, duplicate));
            }
            else
            {
                import.Warnings.Add($"duplicate archetype '{id}': its archetype '{duplicate.GetString("archetype")}' is not defined.");
            }
        }

        var airArchetypes = archetypes
            .Where(entry => entry.Value.GetString("interface_category") == AirCategory &&
                            !Words(entry.Value.GetAll("type")).Any(MissileTypes.Contains))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        foreach (var (id, block) in equipment)
        {
            // A variant with its own module slots is a plane designer airframe, built from modules.
            if (block.GetBool("is_archetype") == true || block.Get("module_slots") is not null ||
                block.GetString("archetype") is not { } archetypeId || !airArchetypes.TryGetValue(archetypeId, out var archetype))
            {
                continue;
            }

            var plane = ReadPlane(id, block, archetypeId, archetype, OwnMissions(id, equipment), localisation);
            AddSubUnit(plane, subUnits, import);
            import.Planes.Add(plane);
        }

        import.Planes.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));

        // A-ACE: common/aces modifiers = { id = { type = { ... } chance = x effect = { modifier = value } } }.
        foreach (var (id, block) in ReadEntries(files, "common/aces", "modifiers", import))
        {
            import.Aces.Add(new AceTemplateDto
            {
                ID = id,
                Types = Words(block.GetAll("type")),
                Chance = block.GetDouble("chance") ?? 0,
                Effects = (block.GetBlock("effect")?.KeyedEntries ?? [])
                    .Where(entry => entry.Value.TryGetDouble(out _))
                    .GroupBy(entry => entry.Key!, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Value.TryGetDouble(out var value) ? value : 0), StringComparer.Ordinal)
            });
        }

        import.Aces.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));
        return import;
    }

    private static PlaneDto ReadPlane(string id, PdxBlock block, string archetypeId, PdxBlock archetype, List<string> ownMissions, LocalisationTable localisation)
    {
        double Stat(string key) => block.GetDouble(key) ?? archetype.GetDouble(key) ?? 0;
        var types = Words(block.GetAll("type"));
        var missions = Words(archetype.GetAll("allow_mission_type")).Union(ownMissions, StringComparer.Ordinal).ToList();

        return new PlaneDto
        {
            ID = id,
            Name = localisation.Get(id) ?? localisation.Get($"{id}_short"),
            Archetype = archetypeId,
            ArchetypeName = localisation.Get(archetypeId),
            Year = (int?)(block.GetDouble("year") ?? archetype.GetDouble("year")),
            Types = types.Count > 0 ? types : Words(archetype.GetAll("type")),
            Missions = missions,
            CarrierCapable = (block.GetBool("carrier_capable") ?? archetype.GetBool("carrier_capable")) == true,
            Stats = new PlaneStatsDto
            {
                AirDefense = Stat("air_defence"),
                AirAttack = Stat("air_attack"),
                NavalAttack = Stat("naval_strike_attack"),
                Speed = Stat("maximum_speed"),
                Agility = Stat("air_agility"),
                AirSuperiority = Stat("air_superiority"),
                NavalTargeting = Stat("naval_strike_targetting"),
                Reliability = Stat("reliability"),
                ProductionCost = Stat("build_cost_ic"),
                Range = Stat("air_range")
            }
        };
    }

    // A-WINGS [PDX + RE]: the sub-unit that needs the archetype; the game's air wing pools are by sub-unit.
    private static void AddSubUnit(PlaneDto plane, Dictionary<string, PdxBlock> subUnits, PlaneImport import)
    {
        var matches = subUnits.Where(entry => entry.Value.GetBlock("need")?.KeyedEntries.Any(need => need.Key == plane.Archetype) == true).ToList();

        if (matches.Count == 0)
        {
            import.Warnings.Add($"plane '{plane.ID}': no sub-unit needs '{plane.Archetype}'.");
            return;
        }

        var (id, block) = matches[0];
        plane.SubUnit = id;
        plane.SubUnitTypes = Words(block.GetAll("type"));
        plane.SubUnitCategories = Words(block.GetAll("categories"));
        plane.CarrierWingSize = (int?)block.GetDouble("carrier_air_wing_size");
        plane.LandWingSize = (int?)block.GetDouble("land_air_wing_size");
    }

    // A-PLANES [ASSUMED]: a variant's own allow_mission_type, else that of the nearest variant up its parent chain that
    // has one. TFB lists the missions only on each line's first variant (CAS_equipment_1; CAS_equipment_2..4 name it as
    // parent, their archetypes allow only training); read from the archetype alone, its later bombers could not strike.
    // How hoi4.exe fills a type's own missions is not decoded (the parser copies stats from the archetype only, 0xc99410).
    private static List<string> OwnMissions(string id, IReadOnlyDictionary<string, PdxBlock> equipment)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = id; equipment.TryGetValue(current, out var block) && seen.Add(current);)
        {
            var missions = Words(block.GetAll("allow_mission_type"));

            if (missions.Count > 0 || block.GetString("parent") is not { } parent)
            {
                return missions;
            }

            current = parent;
        }

        return [];
    }

    // A duplicate archetype: its base archetype's entries, each key the duplicate sets replaced by the duplicate's.
    private static PdxBlock Override(PdxBlock baseArchetype, PdxBlock duplicate)
    {
        var keys = duplicate.KeyedEntries.Select(entry => entry.Key!).ToHashSet(StringComparer.Ordinal);
        return new PdxBlock([.. baseArchetype.Entries.Where(entry => entry.Key is null || !keys.Contains(entry.Key)), .. duplicate.Entries]);
    }

    private static List<string> Words(IEnumerable<PdxValue> values)
    {
        return values
            .SelectMany(value => value.Block is not null ? value.Block.BareValues.Select(bare => bare.Scalar) : [value.Scalar])
            .Where(word => !string.IsNullOrEmpty(word))
            .Select(word => word!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Every <c>root = { id = { ... } }</c> entry of a folder's effective files; a later definition wins.</summary>
    private static Dictionary<string, PdxBlock> ReadEntries(GameFileSystem files, string folder, string rootKey, PlaneImport import)
    {
        var entries = new Dictionary<string, PdxBlock>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles(folder, "*.txt"))
        {
            PdxBlock root;

            try
            {
                root = PdxParser.ParseFile(path);
            }
            catch (PdxParseException exception)
            {
                import.Warnings.Add($"{files.RelativePath(path)} could not be read: {exception.Message}");
                continue;
            }

            foreach (var entry in root.GetAll(rootKey).Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries))
            {
                if (entry.Value.Block is not null)
                {
                    entries[entry.Key!] = entry.Value.Block;
                }
            }
        }

        return entries;
    }
}
