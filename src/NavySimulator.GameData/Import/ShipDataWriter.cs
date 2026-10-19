using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Writes a <see cref="ShipDataImport"/> to a data directory: <c>hulls/</c> (one file per archetype), <c>modules/</c>
/// (one file per kind of module), <c>ship-types/ship-types.json</c> and <c>critical-parts/critical-parts.json</c>. These
/// folders belong to the import: their old JSON files are deleted first. Zero stats and empty lists are left out.
/// </summary>
public static class ShipDataWriter
{
    public const string HullsFolder = "hulls";
    public const string ModulesFolder = "modules";
    public const string ShipTypesFolder = "ship-types";
    public const string CriticalPartsFolder = "critical-parts";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes the files and returns them, data-directory relative.</summary>
    public static IReadOnlyList<string> Write(string dataDirectory, ShipDataImport import)
    {
        var files = new Dictionary<string, (string Schema, string Root, JsonArray Items)>(StringComparer.Ordinal);

        void Add(string file, string schema, string root, JsonObject item)
        {
            if (!files.TryGetValue(file, out var entry))
            {
                entry = (schema, root, []);
                files[file] = entry;
            }

            entry.Items.Add(item);
        }

        foreach (var hull in import.Hulls)
        {
            Add($"{HullsFolder}/{FileName(hull.Archetype.StartsWith("ship_hull_", StringComparison.Ordinal) ? hull.Archetype["ship_hull_".Length..] : hull.Archetype)}.json",
                "../schemas/hulls.schema.json", "hulls", Hull(hull));
        }

        foreach (var module in import.Modules)
        {
            Add($"{ModulesFolder}/{ModuleGroup(module.Category)}.json", "../schemas/modules.schema.json", "modules", Module(module));
        }

        foreach (var shipType in import.ShipTypes)
        {
            Add($"{ShipTypesFolder}/ship-types.json", "../schemas/ship-types.schema.json", "shipTypes", ShipType(shipType));
        }

        foreach (var part in import.CriticalParts)
        {
            Add($"{CriticalPartsFolder}/critical-parts.json", "../schemas/critical-parts.schema.json", "criticalParts", CriticalPart(part));
        }

        foreach (var folder in new[] { HullsFolder, ModulesFolder, ShipTypesFolder, CriticalPartsFolder })
        {
            var directory = Path.Combine(dataDirectory, folder);
            Directory.CreateDirectory(directory);

            foreach (var old in Directory.GetFiles(directory, "*.json"))
            {
                File.Delete(old);
            }
        }

        foreach (var (file, (schema, root, items)) in files)
        {
            var document = new JsonObject { ["$schema"] = schema, [root] = items };
            File.WriteAllText(Path.Combine(dataDirectory, file), document.ToJsonString(Options) + "\n");
        }

        return files.Keys.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>The <c>modules/</c> file a category goes to, e.g. <c>light-guns</c>.</summary>
    public static string ModuleGroup(string category)
    {
        return category switch
        {
            _ when category.Contains("engine") => "engines",
            _ when category.Contains("armor") => "armor",
            _ when category.Contains("heavy_battery") => "heavy-guns",
            "ship_medium_battery" or "ship_light_medium_battery" => "medium-guns",
            _ when category.Contains("light_battery") => "light-guns",
            _ when category.Contains("secondaries") => "secondaries",
            _ when category.Contains("anti_air") => "anti-air",
            _ when category.Contains("torpedo") => "torpedoes",
            _ when category.Contains("depth_charge") || category.Contains("sonar") => "anti-sub",
            _ when category.Contains("fire_control") || category.Contains("radar") => "fire-control-and-sensors",
            _ when category.Contains("deck_space") || category.Contains("airplane") || category.Contains("seaplane") => "aircraft",
            _ when category.Contains("mine") => "mines",
            _ => "other"
        };
    }

    private static JsonObject Hull(HullDto hull)
    {
        var node = new JsonObject { ["id"] = hull.ID };
        AddText(node, "name", hull.Name);
        node["archetype"] = hull.Archetype;
        AddText(node, "archetypeName", hull.ArchetypeName);
        node["equipmentType"] = hull.EquipmentType;

        if (hull.Year is { } year)
        {
            node["year"] = year;
        }

        node["manpower"] = hull.Manpower;
        node["baseStats"] = Stats(hull.BaseStats);
        node["slots"] = new JsonArray(hull.Slots.Select(slot =>
        {
            var slotNode = new JsonObject { ["id"] = slot.ID };
            AddText(slotNode, "name", slot.Name);

            if (slot.Required)
            {
                slotNode["required"] = true;
            }

            slotNode["allowedCategories"] = Words(slot.AllowedCategories);

            if (slot.GameOrder is { } order)
            {
                slotNode["gameOrder"] = order;
            }

            return (JsonNode)slotNode;
        }).ToArray());

        if (hull.ModuleCountLimits.Count > 0)
        {
            node["moduleCountLimits"] = new JsonArray(hull.ModuleCountLimits.Select(limit =>
            {
                var limitNode = new JsonObject();
                AddWords(limitNode, "modules", limit.Modules);
                AddWords(limitNode, "categories", limit.Categories);
                limitNode["operator"] = limit.Operator;
                limitNode["count"] = limit.Count;
                return (JsonNode)limitNode;
            }).ToArray());
        }

        if (hull.DefaultModules.Count > 0)
        {
            var defaults = new JsonObject();

            foreach (var (slot, module) in hull.DefaultModules)
            {
                defaults[slot] = module;
            }

            node["defaultModules"] = defaults;
        }

        return node;
    }

    private static JsonObject Module(ModuleDto module)
    {
        var node = new JsonObject { ["id"] = module.ID };
        AddText(node, "name", module.Name);
        AddText(node, "shortName", module.ShortName);
        node["category"] = module.Category;
        AddText(node, "categoryName", module.CategoryName);
        AddWords(node, "addEquipmentTypes", module.AddEquipmentTypes);
        AddWords(node, "forbiddenCategories", module.ForbiddenCategories);
        AddWords(node, "criticalParts", module.CriticalParts);
        AddStats(node, "statModifiers", module.StatModifiers);
        AddStats(node, "statAverages", module.StatAverages);
        AddStats(node, "statMultipliers", module.StatMultipliers);
        return node;
    }

    private static JsonObject ShipType(ShipTypeDto shipType)
    {
        var node = new JsonObject { ["id"] = shipType.ID };
        AddText(node, "name", shipType.Name);
        node["role"] = shipType.Role;
        node["unitTypes"] = Words(shipType.UnitTypes);
        node["priority"] = shipType.Priority;
        node["hulls"] = Words(shipType.Hulls);
        AddWords(node, "requiredModules", shipType.RequiredModules);
        node["organization"] = shipType.Organization;
        AddWords(node, "criticalParts", shipType.CriticalParts);
        AddNumber(node, "criticalPartDamageChanceMult", shipType.CriticalPartDamageChanceMult, 1);
        return node;
    }

    private static JsonObject CriticalPart(CriticalPartDto part)
    {
        var node = new JsonObject { ["id"] = part.ID };
        AddText(node, "name", part.Name);
        AddNumber(node, "chance", part.Chance, 1);
        AddNumber(node, "strDamageMultiplier", part.StrDamageMultiplier, 1);
        AddNumber(node, "orgDamageMultiplier", part.OrgDamageMultiplier, 1);
        AddNumber(node, "strDamage", part.StrDamage, 0);
        AddNumber(node, "orgDamage", part.OrgDamage, 0);
        AddNumber(node, "baseDamageInstance", part.BaseDamageInstance, 1);
        AddNumber(node, "damageInstancePerAddedModule", part.DamageInstancePerAddedModule, 0);
        AddNumber(node, "maxDamageInstance", part.MaxDamageInstance, -1);
        AddStats(node, "statPenalties", part.StatPenalties);

        if (part.Modifiers.Count > 0)
        {
            var modifiers = new JsonObject();

            foreach (var (key, value) in part.Modifiers)
            {
                modifiers[key] = Math.Round(value, 10);
            }

            node["modifiers"] = modifiers;
        }

        return node;
    }

    private static void AddNumber(JsonObject node, string name, double value, double defaultValue)
    {
        if (value != defaultValue)
        {
            node[name] = Math.Round(value, 10);
        }
    }

    private static JsonObject Stats(ShipStatsDto stats)
    {
        var node = new JsonObject();

        foreach (var property in typeof(ShipStatsDto).GetProperties().Where(property => property.PropertyType == typeof(double)))
        {
            var value = (double)property.GetValue(stats)!;

            if (value != 0)
            {
                node[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = Math.Round(value, 10);
            }
        }

        return node;
    }

    private static void AddStats(JsonObject node, string name, ShipStatsDto stats)
    {
        var statsNode = Stats(stats);

        if (statsNode.Count > 0)
        {
            node[name] = statsNode;
        }
    }

    private static void AddText(JsonObject node, string name, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            node[name] = text;
        }
    }

    private static void AddWords(JsonObject node, string name, List<string> words)
    {
        if (words.Count > 0)
        {
            node[name] = Words(words);
        }
    }

    private static JsonArray Words(List<string> words)
    {
        return new JsonArray(words.Select(word => (JsonNode)word).ToArray());
    }

    private static string FileName(string id)
    {
        return id.Replace('_', '-');
    }
}
