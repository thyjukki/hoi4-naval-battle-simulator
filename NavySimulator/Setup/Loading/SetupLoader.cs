using System.Text.Json;
using NavySimulator.Domain;
using NavySimulator.Setup.Contracts;

namespace NavySimulator.Setup;

public class SetupLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly HashSet<string> KnownShipStatsKeys = typeof(ShipStatsDto)
        .GetProperties()
        .Select(property => property.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> warnings = [];

    public IReadOnlyList<string> Warnings => warnings;

    public BattleScenario LoadScenarioFromDirectory(string dataDirectoryPath)
    {
        warnings.Clear();
        var errors = new List<string>();

        if (!Directory.Exists(dataDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Data directory not found: {dataDirectoryPath}");
        }

        ValidateModuleStatWarnings(dataDirectoryPath, warnings);

        var hullsFile = ReadJsonFile<HullsFileDto>(dataDirectoryPath, "hulls.json", errors) ?? new HullsFileDto();
        var modulesFile = ReadJsonFile<ModulesFileDto>(dataDirectoryPath, "modules.json", errors) ?? new ModulesFileDto();
        var miosFile = ReadJsonFile<MiosFileDto>(dataDirectoryPath, "mios.json", errors) ?? new MiosFileDto();
        var designsFile = ReadJsonFile<ShipDesignsFileDto>(dataDirectoryPath, "ship-designs.json", errors) ?? new ShipDesignsFileDto();
        var forceCompositionsFile = ReadJsonFile<ForceCompositionsFileDto>(dataDirectoryPath, "force-compositions.json", errors) ?? new ForceCompositionsFileDto();
        var battleScenarioFile = ReadJsonFile<BattleScenarioFileDto>(dataDirectoryPath, "battle-scenario.json", errors) ?? new BattleScenarioFileDto();

        ValidateRequiredIds(hullsFile.Hulls.Select(h => h.ID), "hulls", errors);
        ValidateRequiredValues(hullsFile.Hulls.Select(h => h.Role), "hulls.role", errors);
        ValidateRequiredIds(modulesFile.Modules.Select(m => m.ID), "modules", errors);
        ValidateRequiredIds(miosFile.Mios.Select(m => m.ID), "mios", errors);
        ValidateRequiredIds(designsFile.ShipDesigns.Select(d => d.ID), "shipDesigns", errors);
        ValidateRequiredIds(forceCompositionsFile.Fleets.Select(f => f.ID), "fleets", errors);

        ValidateDuplicateIds(hullsFile.Hulls.Select(h => h.ID), "hulls", errors);
        ValidateDuplicateIds(modulesFile.Modules.Select(m => m.ID), "modules", errors);
        ValidateDuplicateIds(miosFile.Mios.Select(m => m.ID), "mios", errors);
        ValidateDuplicateIds(designsFile.ShipDesigns.Select(d => d.ID), "shipDesigns", errors);
        ValidateDuplicateIds(forceCompositionsFile.Fleets.Select(f => f.ID), "fleets", errors);

        var hullIds = hullsFile.Hulls.Select(h => h.ID).ToHashSet();
                        foreach (var hull in hullsFile.Hulls)
                        {
                            if (!Enum.TryParse<ShipRole>(hull.Role, true, out _))
                            {
                                errors.Add($"hull '{hull.ID}' has unknown role '{hull.Role}'.");
                            }
                        }

        var moduleIds = modulesFile.Modules.Select(m => m.ID).ToHashSet();
        var mioIds = miosFile.Mios.Select(m => m.ID).ToHashSet();
        var designIds = designsFile.ShipDesigns.Select(d => d.ID).ToHashSet();
        var fleetIds = forceCompositionsFile.Fleets.Select(f => f.ID).ToHashSet();

        foreach (var design in designsFile.ShipDesigns)
        {
            if (!hullIds.Contains(design.HullID))
            {
                errors.Add($"shipDesign '{design.ID}' references unknown hullID '{design.HullID}'.");
            }

            foreach (var moduleId in design.ModuleIDs)
            {
                if (!moduleIds.Contains(moduleId))
                {
                    errors.Add($"shipDesign '{design.ID}' references unknown moduleID '{moduleId}'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(design.MioID) && !mioIds.Contains(design.MioID))
            {
                errors.Add($"shipDesign '{design.ID}' references unknown mioID '{design.MioID}'.");
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
        }

        ValidateScenario(battleScenarioFile.BattleScenario, fleetIds, errors);

        if (errors.Count > 0)
        {
            throw new SetupValidationException(errors);
        }

        var hullById = hullsFile.Hulls.ToDictionary(
            h => h.ID,
            h => new Hull(h.ID, Enum.Parse<ShipRole>(h.Role, true), h.BaseStats.ToDomain()));
        var moduleById = modulesFile.Modules.ToDictionary(
            m => m.ID,
            m => new StatModule(
                m.ID,
                m.StatModifiers.ToDomain(),
                m.StatMultipliers.ToDomain(),
                m.StatAverages.ToDomain()));
        var mioById = miosFile.Mios.ToDictionary(m => m.ID, m => new MioBonus(m.ID, m.PercentBonus.ToDomain()));

        var designById = new Dictionary<string, ShipDesign>();

        foreach (var design in designsFile.ShipDesigns)
        {
            var hull = hullById[design.HullID];
            var modules = design.ModuleIDs.Select(moduleId => moduleById[moduleId]).ToList();
            var mio = string.IsNullOrWhiteSpace(design.MioID) ? null : mioById[design.MioID];
            designById[design.ID] = new ShipDesign(hull, modules, mio);
        }

        var fleetById = new Dictionary<string, Fleet>();

        foreach (var fleet in forceCompositionsFile.Fleets)
        {
            var ships = new List<Ship>();

            foreach (var shipDesign in fleet.ShipDesigns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var design = designById[shipDesign.Key];

                for (var i = 1; i <= shipDesign.Value; i++)
                {
                    var shipId = $"{fleet.ID}_{shipDesign.Key}_{i:D3}";
                    ships.Add(new Ship(shipId, design));
                }
            }

            fleetById[fleet.ID] = new Fleet(fleet.ID, ships);
        }

        var attacker = BuildParticipant(battleScenarioFile.BattleScenario.Attacker, fleetById);
        var defender = BuildParticipant(battleScenarioFile.BattleScenario.Defender, fleetById);

        return new BattleScenario(
            battleScenarioFile.BattleScenario.ID,
            battleScenarioFile.BattleScenario.Terrain,
            battleScenarioFile.BattleScenario.Weather,
            battleScenarioFile.BattleScenario.MaxHours,
            attacker,
            defender);
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

    private static void ValidateModuleStatWarnings(string dataDirectoryPath, List<string> warnings)
    {
        var modulesPath = Path.Combine(dataDirectoryPath, "modules.json");

        if (!File.Exists(modulesPath))
        {
            return;
        }

        try
        {
            ValidateDuplicateModuleStatKeysWithReader(modulesPath, warnings);
            using var document = JsonDocument.Parse(File.ReadAllText(modulesPath));

            if (!document.RootElement.TryGetProperty("modules", out var modulesElement) || modulesElement.ValueKind != JsonValueKind.Array)
            {
                return;
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
        catch (JsonException)
        {
            // Invalid JSON is already captured as a setup error by ReadJsonFile.
        }
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

    private static void ValidateRequiredValues(IEnumerable<string> values, string sectionName, List<string> errors)
    {
        var index = 0;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{sectionName}[{index}] must not be empty.");
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

    private static void ValidateScenario(BattleScenarioDto scenario, HashSet<string> fleetIds, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(scenario.ID))
        {
            errors.Add("battleScenario.id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(scenario.Terrain))
        {
            errors.Add("battleScenario.terrain must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(scenario.Weather))
        {
            errors.Add("battleScenario.weather must not be empty.");
        }

        if (scenario.MaxHours <= 0)
        {
            errors.Add("battleScenario.maxHours must be greater than 0.");
        }

        ValidateParticipant("attacker", scenario.Attacker, fleetIds, errors);
        ValidateParticipant("defender", scenario.Defender, fleetIds, errors);
    }

    private static void ValidateParticipant(
        string role,
        BattleParticipantDto participant,
        HashSet<string> fleetIds,
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
    }

    private static BattleParticipant BuildParticipant(
        BattleParticipantDto participant,
        IReadOnlyDictionary<string, Fleet> fleetById)
    {
        var fleet = fleetById[participant.FleetID];

        return new BattleParticipant(
            fleet,
            participant.Commander,
            participant.Doctrine,
            participant.TechnologyLevel,
            participant.NationModifier);
    }
}


