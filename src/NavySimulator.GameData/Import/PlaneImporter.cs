using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Reads the game's fixed plane equipment (A-PLANES) [PDX]: every variant of <c>common/units/equipment</c> whose archetype
/// is an air archetype (<c>interface_category = interface_category_air</c>) without <c>module_slots</c>, so the plane
/// designer's airframes and the missiles are left out. Keys a variant leaves out are its archetype's. Each plane also
/// gets the air sub-unit (<c>common/units/*.txt</c> <c>sub_units</c>) whose <c>need</c> names its archetype: its
/// <c>type</c> and wing sizes (A-WINGS).
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
            .Where(entry => entry.Value.GetBool("is_archetype") == true &&
                            entry.Value.GetString("interface_category") == AirCategory &&
                            entry.Value.Get("module_slots") is null &&
                            !Words(entry.Value.GetAll("type")).Any(MissileTypes.Contains))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        foreach (var (id, block) in equipment)
        {
            if (block.GetBool("is_archetype") == true || block.GetString("archetype") is not { } archetypeId ||
                !archetypes.TryGetValue(archetypeId, out var archetype))
            {
                continue;
            }

            var plane = ReadPlane(id, block, archetypeId, archetype, localisation);
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

    private static PlaneDto ReadPlane(string id, PdxBlock block, string archetypeId, PdxBlock archetype, LocalisationTable localisation)
    {
        double Stat(string key) => block.GetDouble(key) ?? archetype.GetDouble(key) ?? 0;
        var types = Words(block.GetAll("type"));
        var missions = Words(block.GetAll("allow_mission_type"));

        return new PlaneDto
        {
            ID = id,
            Name = localisation.Get(id) ?? localisation.Get($"{id}_short"),
            Archetype = archetypeId,
            ArchetypeName = localisation.Get(archetypeId),
            Year = (int?)(block.GetDouble("year") ?? archetype.GetDouble("year")),
            Types = types.Count > 0 ? types : Words(archetype.GetAll("type")),
            Missions = missions.Count > 0 ? missions : Words(archetype.GetAll("allow_mission_type")),
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
