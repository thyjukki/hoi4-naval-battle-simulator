using System.Text.RegularExpressions;
using NavySimulator.Engine;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Reads ship hulls (<c>common/units/equipment/ship_hull_*.txt</c>), ship modules
/// (<c>common/units/equipment/modules/</c>), naval sub-units (<c>common/units/*.txt</c>) and their localised names from
/// the game and mods, following the script rules in the game's <c>common/units/equipment/_documentation.md</c> [PDX].
/// Only Man the Guns hulls are read: the <c>ship_hull_*</c> equipment of a ship archetype. The other equipment of
/// those archetypes (<c>light_cruiser_1</c>, <c>battleship_1</c>, ...) is the non-DLC tech tree's (<c>naval.txt</c>)
/// and is skipped; nothing in the equipment itself marks it, the naming convention holds in vanilla and TFB.
/// </summary>
public static class ShipDataImporter
{
    public const string HullPrefix = "ship_hull_";

    private static readonly HashSet<string> NavalUnitTypes = new(["capital_ship", "screen_ship", "carrier", "submarine"], StringComparer.Ordinal);

    // Numeric keys of an equipment entry that are not stats.
    private static readonly HashSet<string> EquipmentNonStatKeys = new(["year", "priority", "visual_level", "manpower", "visual_tech_level_addition", "naval_dominance_factor"], StringComparer.Ordinal);

    private static readonly string[] ModuleStatBlocks = ["add_stats", "multiply_stats", "add_average_stats"];

    public static ShipDataImport Read(GameFileSystem files, string language = "english")
    {
        var import = new ShipDataImport();
        var localisation = LocalisationTable.Load(files, language);
        var equipment = ReadEntries(files, "common/units/equipment", "equipments", import);
        var archetypes = equipment.Values
            .Where(item => item.Block.GetBool("is_archetype") == true &&
                           NavalUnitTypes.Contains(item.Block.GetString("type") ?? string.Empty) &&
                           item.Block.Get("module_slots") is not null)
            .ToDictionary(item => item.ID, StringComparer.Ordinal);
        var resolver = new EquipmentResolver(equipment, import);

        foreach (var item in equipment.Values.Where(item => item.Block.GetBool("is_archetype") != true &&
                                                            archetypes.ContainsKey(item.Block.GetString("archetype") ?? string.Empty)))
        {
            var slots = item.ID.StartsWith(HullPrefix, StringComparison.Ordinal) ? resolver.Slots(item.ID) : null;

            if (slots is null)
            {
                import.SkippedEquipment.Add(item.ID);
                continue;
            }

            var archetype = archetypes[item.Block.GetString("archetype")!];
            import.Hulls.Add(ReadHull(item, archetype, slots, resolver, localisation, import));
        }

        var gameOrder = SlotNameOrder(files, import.Hulls.SelectMany(hull => hull.Slots).Select(slot => slot.ID).ToHashSet(StringComparer.Ordinal));

        foreach (var slot in import.Hulls.SelectMany(hull => hull.Slots))
        {
            slot.GameOrder = gameOrder.TryGetValue(slot.ID, out var order) ? order : null;
        }

        var categories = import.Hulls.SelectMany(hull => hull.Slots).SelectMany(slot => slot.AllowedCategories).ToHashSet(StringComparer.Ordinal);

        foreach (var item in ReadEntries(files, "common/units/equipment/modules", "equipment_modules", import).Values)
        {
            var category = item.Block.GetString("category");

            if (category is not null && categories.Contains(category))
            {
                import.Modules.Add(ReadModule(item, category, localisation, import));
            }
        }

        var hullIds = archetypes.Keys.Concat(import.Hulls.Select(hull => hull.ID)).ToHashSet(StringComparer.Ordinal);

        foreach (var item in ReadEntries(files, "common/units", "sub_units", import).Values)
        {
            var shipType = ReadShipType(item, hullIds, localisation, import);

            if (shipType is not null)
            {
                import.ShipTypes.Add(shipType);
            }
        }

        var usedParts = import.Modules.SelectMany(module => module.CriticalParts)
            .Concat(import.ShipTypes.SelectMany(shipType => shipType.CriticalParts))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in ReadEntries(files, "common/units/critical_parts", "critical_parts", import).Values.Where(item => usedParts.Contains(item.ID)))
        {
            import.CriticalParts.Add(ReadCriticalPart(item, localisation, import));
        }

        foreach (var missing in usedParts.Where(id => import.CriticalParts.All(part => part.ID != id)).Order(StringComparer.Ordinal))
        {
            import.Warnings.Add($"critical part '{missing}' is used by a module or sub-unit but not defined.");
        }

        return import;
    }

    private static HullDto ReadHull(
        Entry item,
        Entry archetype,
        List<ModuleSlotDto> slots,
        EquipmentResolver resolver,
        LocalisationTable localisation,
        ShipDataImport import)
    {
        // S-DESIGN-HULL [ASSUMED]: the archetype's stats, overridden by the hull's own.
        var stats = new ShipStatsDto();

        foreach (var source in new[] { archetype, item })
        {
            foreach (var entry in source.Block.KeyedEntries.Where(entry => entry.Value.TryGetDouble(out _) && !EquipmentNonStatKeys.Contains(entry.Key!)))
            {
                entry.Value.TryGetDouble(out var value);

                if (!ShipStatKeys.TrySet(stats, entry.Key!, value))
                {
                    import.Ignore(entry.Key!, source.ID);
                }
            }
        }

        foreach (var slot in slots)
        {
            slot.Name = localisation.Get($"EQ_MOD_SLOT_{slot.ID}_TITLE");
        }

        return new HullDto
        {
            ID = item.ID,
            Name = localisation.Get(item.ID),
            Archetype = archetype.ID,
            ArchetypeName = localisation.Get(archetype.ID),
            EquipmentType = archetype.Block.GetString("type") ?? string.Empty,
            Year = (int?)(item.Block.GetDouble("year") ?? archetype.Block.GetDouble("year")),
            Manpower = (int)(item.Block.GetDouble("manpower") ?? archetype.Block.GetDouble("manpower") ?? 0),
            NavalDominanceFactor = item.Block.GetDouble("naval_dominance_factor") ?? archetype.Block.GetDouble("naval_dominance_factor") ?? 0,
            BaseStats = stats,
            Slots = slots,
            ModuleCountLimits = resolver.CountLimits(item.ID),
            DefaultModules = resolver.DefaultModules(item.ID, slots)
        };
    }

    private static ModuleDto ReadModule(Entry item, string category, LocalisationTable localisation, ShipDataImport import)
    {
        var module = new ModuleDto
        {
            ID = item.ID,
            Name = localisation.Get(item.ID),
            ShortName = localisation.Get($"{item.ID}_short"),
            Category = category,
            CategoryName = localisation.Get($"EQ_MOD_CAT_{category}_TITLE"),
            AddEquipmentTypes = Words(item.Block.GetAll("add_equipment_type")),
            ForbiddenCategories = Words(item.Block.GetAll("forbid_module_categories")),
            CriticalParts = Words(item.Block.GetAll("critical_parts"))
        };

        foreach (var blockName in ModuleStatBlocks)
        {
            var stats = blockName switch
            {
                "add_stats" => module.StatModifiers,
                "multiply_stats" => module.StatMultipliers,
                _ => module.StatAverages
            };
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Repeated blocks and keys are summed, as the stat aggregator counts every entry.
            foreach (var entry in item.Block.GetAll(blockName).Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries))
            {
                if (!entry.Value.TryGetDouble(out var value))
                {
                    continue;
                }

                if (!seen.Add(entry.Key!))
                {
                    import.Warnings.Add($"module '{item.ID}' sets '{entry.Key}' more than once in {blockName}; the values are summed.");
                }

                if (!ShipStatKeys.TryAdd(stats, entry.Key!, value))
                {
                    import.Ignore(entry.Key!, item.ID);
                }
            }
        }

        return module;
    }

    private static ShipTypeDto? ReadShipType(Entry item, HashSet<string> hullIds, LocalisationTable localisation, ShipDataImport import)
    {
        var unitTypes = Words(item.Block.GetAll("type"));

        if (!unitTypes.Any(NavalUnitTypes.Contains))
        {
            return null;
        }

        var hulls = new[] { "need", "need_equipment" }
            .SelectMany(key => item.Block.GetAll(key))
            .Where(value => value.Block is not null)
            .SelectMany(value => value.Block!.KeyedEntries.Select(entry => entry.Key!))
            .Where(hullIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (hulls.Count == 0)
        {
            return null;
        }

        var requiredModules = new List<string>();

        foreach (var entry in item.Block.GetAll("need_equipment_modules").Where(value => value.Block is not null).SelectMany(value => value.Block!.Entries))
        {
            if (entry.Key == "any" && entry.Value.Block is not null)
            {
                requiredModules.AddRange(entry.Value.Block.KeyedEntries.Select(module => module.Key!));
            }
            else
            {
                import.Warnings.Add($"sub-unit '{item.ID}': need_equipment_modules entry '{entry.Key}' is not an 'any' block and is ignored.");
            }
        }

        return new ShipTypeDto
        {
            ID = item.ID,
            Name = localisation.Get(item.ID),
            Role = RoleOf(unitTypes).ToString(),
            UnitTypes = unitTypes,
            Priority = (int)(item.Block.GetDouble("priority") ?? 0),
            Hulls = hulls,
            RequiredModules = requiredModules,
            Organization = item.Block.GetDouble("max_organisation") ?? 0,
            CriticalParts = Words(item.Block.GetAll("critical_parts")),
            CriticalPartDamageChanceMult = item.Block.GetDouble("critical_part_damage_chance_mult") ?? 1
        };
    }

    // S-CRIT-PART [CAL]: the game walks a design's slots in the order their names first appear in the equipment files,
    // files in load order (interned names); in 18 logged TFB battles every part pick fits it, the hulls' own order not.
    private static Dictionary<string, int> SlotNameOrder(GameFileSystem files, HashSet<string> slotIds)
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles("common/units/equipment", "*.txt"))
        {
            var text = Regex.Replace(File.ReadAllText(path), @"#[^\n]*", string.Empty);

            foreach (Match token in Regex.Matches(text, "[A-Za-z0-9_]+"))
            {
                if (slotIds.Contains(token.Value))
                {
                    order.TryAdd(token.Value, order.Count);
                }
            }
        }

        return order;
    }

    // S-CRIT-PART [PDX + RE]: keys a part leaves out take the game's defaults (constructor 0x1019290): multipliers and
    // chance 1, extra damage 0, base_damage_instance 1, damage_instance_per_added_module 0, max_damage_instance -1.
    private static CriticalPartDto ReadCriticalPart(Entry item, LocalisationTable localisation, ShipDataImport import)
    {
        var block = item.Block;
        var part = new CriticalPartDto
        {
            ID = item.ID,
            Name = localisation.Get(item.ID),
            Chance = block.GetDouble("chance") ?? 1,
            StrDamageMultiplier = block.GetDouble("str_damage_multiplier") ?? 1,
            OrgDamageMultiplier = block.GetDouble("org_damage_multiplier") ?? 1,
            StrDamage = block.GetDouble("str_damage") ?? 0,
            OrgDamage = block.GetDouble("org_damage") ?? 0,
            BaseDamageInstance = (int)(block.GetDouble("base_damage_instance") ?? 1),
            DamageInstancePerAddedModule = (int)(block.GetDouble("damage_instance_per_added_module") ?? 0),
            MaxDamageInstance = (int)(block.GetDouble("max_damage_instance") ?? -1)
        };

        foreach (var entry in block.GetAll("stat_penalties").Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries))
        {
            if (entry.Value.TryGetDouble(out var value) && !ShipStatKeys.TryAdd(part.StatPenalties, entry.Key!, value))
            {
                import.Ignore(entry.Key!, item.ID);
            }
        }

        foreach (var entry in block.GetAll("modifier").Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries))
        {
            if (entry.Value.TryGetDouble(out var value))
            {
                part.Modifiers[entry.Key!] = part.Modifiers.GetValueOrDefault(entry.Key!) + value;
            }
        }

        return part;
    }

    private static ShipRole RoleOf(List<string> unitTypes)
    {
        return unitTypes.Contains("carrier") ? ShipRole.Carrier
            : unitTypes.Contains("capital_ship") ? ShipRole.Capital
            : unitTypes.Contains("submarine") ? ShipRole.Submarine
            : ShipRole.Screen;
    }

    /// <summary>The words of <c>key = word</c> and <c>key = { word word }</c> entries.</summary>
    private static List<string> Words(IEnumerable<PdxValue> values)
    {
        return values
            .SelectMany(value => value.Block is not null ? value.Block.BareValues.Select(bare => bare.Scalar) : [value.Scalar])
            .Where(word => !string.IsNullOrEmpty(word))
            .Select(word => word!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Every <c>root = { id = { ... } }</c> entry of a folder's effective files, by ID; a later definition wins.</summary>
    private static Dictionary<string, Entry> ReadEntries(GameFileSystem files, string folder, string rootKey, ShipDataImport import)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

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
                // "limit" is the DLC trigger of equipment_modules, not an entry.
                if (entry.Value.Block is null || entry.Key == "limit")
                {
                    continue;
                }

                if (entries.TryGetValue(entry.Key!, out var previous))
                {
                    import.Warnings.Add($"'{entry.Key}' is defined in {previous.File} and {files.RelativePath(path)}; the later one is used.");
                }

                entries[entry.Key!] = new Entry(entry.Key!, entry.Value.Block, files.RelativePath(path));
            }
        }

        return entries;
    }

    private sealed record Entry(string ID, PdxBlock Block, string File);

    /// <summary>The inherited parts of an equipment entry: module slots, module count limits and default modules.</summary>
    private sealed class EquipmentResolver(Dictionary<string, Entry> equipment, ShipDataImport import)
    {
        private readonly Dictionary<string, List<ModuleSlotDto>?> slotCache = new(StringComparer.Ordinal);

        /// <summary>
        /// The resolved <c>module_slots</c>, or null when the entry has none. <c>module_slots = inherit</c> and
        /// <c>slot = inherit</c> take the parent equipment's slots (the archetype's when there is no parent);
        /// <c>slot = other_slot</c> copies a slot defined before it.
        /// </summary>
        public List<ModuleSlotDto>? Slots(string id)
        {
            if (slotCache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            slotCache[id] = null;
            var result = ResolveSlots(equipment[id]);
            slotCache[id] = result;
            return result;
        }

        public List<ModuleCountLimitDto> CountLimits(string id)
        {
            // Inherited by children; a child's limit with the same match conditions replaces the inherited one.
            var limits = new List<ModuleCountLimitDto>();

            foreach (var item in Chain(id))
            {
                foreach (var block in item.Block.GetAll("module_count_limit").Where(value => value.Block is not null).Select(value => value.Block!))
                {
                    var limit = ReadCountLimit(block, item.ID);

                    if (limit is null)
                    {
                        continue;
                    }

                    var index = limits.FindIndex(existing => Conditions(existing) == Conditions(limit));

                    if (index >= 0)
                    {
                        limits[index] = limit;
                    }
                    else
                    {
                        limits.Add(limit);
                    }
                }
            }

            return limits.Where(limit => limit.Operator != ModuleCountLimit.AnyKeyword).ToList();
        }

        public Dictionary<string, string> DefaultModules(string id, List<ModuleSlotDto> slots)
        {
            var modules = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var block in Chain(id).Select(item => item.Block.GetBlock("default_modules")).OfType<PdxBlock>())
            {
                foreach (var entry in block.KeyedEntries.Where(entry => entry.Value.Scalar is not null))
                {
                    modules[entry.Key!] = entry.Value.Scalar!;
                }
            }

            return slots.Where(slot => modules.TryGetValue(slot.ID, out var module) && module != ModuleCountLimit.EmptyKeyword)
                .ToDictionary(slot => slot.ID, slot => modules[slot.ID], StringComparer.Ordinal);
        }

        private List<ModuleSlotDto>? ResolveSlots(Entry item)
        {
            var value = item.Block.Get("module_slots");

            if (value is null)
            {
                return item.Block.GetBool("is_archetype") == true ? [] : null;
            }

            if (value.Scalar == "inherit")
            {
                return Copy(Inherited(item));
            }

            if (value.Block is null)
            {
                import.Warnings.Add($"'{item.ID}' has an unreadable module_slots value '{value.Scalar}'.");
                return null;
            }

            var slots = new List<ModuleSlotDto>();

            foreach (var entry in value.Block.KeyedEntries)
            {
                ModuleSlotDto? slot;

                if (entry.Value.Block is { } slotBlock)
                {
                    slot = new ModuleSlotDto
                    {
                        ID = entry.Key!,
                        Required = slotBlock.GetBool("required") == true,
                        AllowedCategories = Words(slotBlock.GetAll("allowed_module_categories"))
                    };
                }
                else
                {
                    var source = entry.Value.Scalar == "inherit"
                        ? Inherited(item).FirstOrDefault(candidate => candidate.ID == entry.Key)
                        : slots.FirstOrDefault(candidate => candidate.ID == entry.Value.Scalar);

                    if (source is null)
                    {
                        import.Warnings.Add($"'{item.ID}' slot '{entry.Key} = {entry.Value.Scalar}' has nothing to inherit and is skipped.");
                        continue;
                    }

                    slot = new ModuleSlotDto { ID = entry.Key!, Required = source.Required, AllowedCategories = [.. source.AllowedCategories] };
                }

                slots.RemoveAll(existing => existing.ID == slot.ID);
                slots.Add(slot);
            }

            return slots;
        }

        private List<ModuleSlotDto> Inherited(Entry item)
        {
            var baseId = BaseOf(item);
            return baseId is null ? [] : Slots(baseId) ?? [];
        }

        private string? BaseOf(Entry item)
        {
            var parent = item.Block.GetString("parent");

            if (parent is not null && equipment.ContainsKey(parent))
            {
                return parent;
            }

            var archetype = item.Block.GetString("archetype");
            return archetype is not null && archetype != item.ID && equipment.ContainsKey(archetype) ? archetype : null;
        }

        /// <summary>The archetype, then the parents from the oldest, then the item.</summary>
        private List<Entry> Chain(string id)
        {
            var chain = new List<Entry>();

            for (var current = equipment.GetValueOrDefault(id); current is not null && chain.All(item => item.ID != current.ID);)
            {
                chain.Insert(0, current);
                var parent = current.Block.GetString("parent");
                current = parent is null ? null : equipment.GetValueOrDefault(parent);
            }

            var archetype = equipment.GetValueOrDefault(equipment[id].Block.GetString("archetype") ?? string.Empty);

            if (archetype is not null && chain.All(item => item.ID != archetype.ID))
            {
                chain.Insert(0, archetype);
            }

            return chain;
        }

        private ModuleCountLimitDto? ReadCountLimit(PdxBlock block, string itemId)
        {
            var count = block.Entries.LastOrDefault(entry => entry.Key == "count");

            if (count is null)
            {
                import.Warnings.Add($"'{itemId}' has a module_count_limit without count; it is ignored.");
                return null;
            }

            var limit = new ModuleCountLimitDto
            {
                Modules = Words(block.GetAll("module")),
                Categories = Words(block.GetAll("category"))
            };

            if (count.Value.Scalar == ModuleCountLimit.AnyKeyword)
            {
                limit.Operator = ModuleCountLimit.AnyKeyword;
            }
            else if (count.Value.TryGetDouble(out var number) && count.Operator is "<" or ">" or "=")
            {
                limit.Operator = count.Operator;
                limit.Count = (int)number;
            }
            else
            {
                import.Warnings.Add($"'{itemId}' has a module_count_limit with count {count.Operator} {count.Value}; it is ignored.");
                return null;
            }

            return limit;
        }

        private static string Conditions(ModuleCountLimitDto limit)
        {
            return string.Join(",", limit.Modules.Order(StringComparer.Ordinal)) + "|" + string.Join(",", limit.Categories.Order(StringComparer.Ordinal));
        }

        private static List<ModuleSlotDto> Copy(List<ModuleSlotDto> slots)
        {
            return slots.Select(slot => new ModuleSlotDto { ID = slot.ID, Required = slot.Required, AllowedCategories = [.. slot.AllowedCategories] }).ToList();
        }
    }
}
