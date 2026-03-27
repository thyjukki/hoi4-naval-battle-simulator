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

        var hulls = ReadCollectionFromFolderOrFile<HullDto>(dataDirectoryPath, "hulls", "hulls.json", "hulls", errors);
        var modules = ReadCollectionFromFolderOrFile<ModuleDto>(dataDirectoryPath, "modules", "modules.json", "modules", errors);
        var mios = ReadCollectionFromFolderOrFile<MioBonusDto>(dataDirectoryPath, "mios", "mios.json", "mios", errors);
        var designs = ReadCollectionFromFolderOrFile<ShipDesignDto>(dataDirectoryPath, "ship-designs", "ship-designs.json", "shipDesigns", errors);
        var researches = ReadCollectionFromFolderOrFile<ResearchDto>(dataDirectoryPath, "researches", "researches.json", "researches", errors);
        var spirits = ReadCollectionFromFolderOrFile<SpiritDto>(dataDirectoryPath, "spirits", "spirits.json", "spirits", errors);
        var planes = ReadCollectionFromFolderOrFile<PlaneDto>(dataDirectoryPath, "planes", "planes.json", "planes", errors);
        var fleets = ReadCollectionFromFolderOrFile<FleetDto>(dataDirectoryPath, "force-compositions", "force-compositions.json", "fleets", errors);
        var forceCompositionsFile = new ForceCompositionsFileDto { Fleets = fleets };
        var battleScenarioFile = ReadJsonFile<BattleScenarioFileDto>(dataDirectoryPath, "battle-scenario.json", errors) ?? new BattleScenarioFileDto();

        ValidateRequiredIds(hulls.Select(h => h.ID), "hulls", errors);
        ValidateRequiredValues(hulls.Select(h => h.Role), "hulls.role", errors);
        ValidateRequiredIds(modules.Select(m => m.ID), "modules", errors);
        ValidateRequiredIds(mios.Select(m => m.ID), "mios", errors);
        ValidateRequiredIds(designs.Select(d => d.ID), "shipDesigns", errors);
        ValidateRequiredIds(researches.Select(r => r.ID), "researches", errors);
        ValidateRequiredIds(spirits.Select(s => s.ID), "spirits", errors);
        ValidateRequiredIds(planes.Select(p => p.ID), "planes", errors);
        ValidateRequiredIds(forceCompositionsFile.Fleets.Select(f => f.ID), "fleets", errors);

        ValidateDuplicateIds(hulls.Select(h => h.ID), "hulls", errors);
        ValidateDuplicateIds(modules.Select(m => m.ID), "modules", errors);
        ValidateDuplicateIds(mios.Select(m => m.ID), "mios", errors);
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

        var planeById = planes.ToDictionary(
            plane => plane.ID,
            plane => new PlaneEquipment(plane.ID, plane.Stats.ToDomain()));

        var hullIds = hulls.Select(h => h.ID).ToHashSet();
        foreach (var hull in hulls)
        {
            var hullTypes = hull.Types ?? [];

            if (!Enum.TryParse<ShipRole>(hull.Role, true, out _))
            {
                errors.Add($"hull '{hull.ID}' has unknown role '{hull.Role}'.");
            }

            if (hullTypes.Count == 0)
            {
                errors.Add($"hull '{hull.ID}' must define at least one type in 'type'.");
            }

            foreach (var hullType in hullTypes)
            {
                if (!ShipHullTypes.IsKnown(hullType))
                {
                    errors.Add($"hull '{hull.ID}' has unknown type '{hullType}'.");
                }
            }

            if (hull.Manpower <= 0)
            {
                errors.Add($"hull '{hull.ID}' must define manpower greater than 0.");
            }
        }

        var moduleIds = modules.Select(m => m.ID).ToHashSet();
        var mioIds = mios.Select(m => m.ID).ToHashSet();
        var designIds = designs.Select(d => d.ID).ToHashSet();
        var researchIds = researches.Select(r => r.ID).ToHashSet();
        var spiritIds = spirits.Select(s => s.ID).ToHashSet();
        var planeIds = planeById.Keys.ToHashSet();
        var fleetIds = forceCompositionsFile.Fleets.Select(f => f.ID).ToHashSet();
        var hullRoleByHullId = hulls.ToDictionary(h => h.ID, h => h.Role, StringComparer.OrdinalIgnoreCase);
        var designHullByDesignId = designs.ToDictionary(d => d.ID, d => d.HullID, StringComparer.OrdinalIgnoreCase);

        ValidateScopedRoleFilters(
            mios.SelectMany(mio => mio.Modifiers.Select(modifier => ($"{mio.ID}", modifier.AppliesToRoles))),
            "mio modifier",
            errors);
        ValidateScopedRoleFilters(researches.Select(r => (r.ID, r.AppliesToRoles)), "research", errors);
        ValidateScopedRoleFilters(spirits.Select(s => (s.ID, s.AppliesToRoles)), "spirit", errors);
        ValidateScopedTypeFilters(
            mios.SelectMany(mio => mio.Modifiers.Select(modifier => ($"{mio.ID}", modifier.AppliesToTypes ?? []))),
            "mio modifier",
            errors);
        ValidateScopedTypeFilters(researches.Select(r => (r.ID, r.AppliesToTypes ?? [])), "research", errors);
        ValidateScopedTypeFilters(spirits.Select(s => (s.ID, s.AppliesToTypes ?? [])), "spirit", errors);

        foreach (var design in designs)
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

            foreach (var shipExperienceByDesign in fleet.ShipExperienceLevels)
            {
                if (!fleet.ShipDesigns.ContainsKey(shipExperienceByDesign.Key))
                {
                    errors.Add($"fleet '{fleet.ID}' has shipExperienceLevels for shipDesignID '{shipExperienceByDesign.Key}' that is not present in shipDesigns.");
                }

                if (!IsValidShipExperienceLevel(shipExperienceByDesign.Value))
                {
                    errors.Add($"fleet '{fleet.ID}' has unsupported ship experience level {shipExperienceByDesign.Value} for shipDesignID '{shipExperienceByDesign.Key}'. Supported levels are 0 (untrained), 1 (regular), and 2 (trained).");
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

                if (!fleet.ShipDesigns.ContainsKey(shipDesignId))
                {
                    errors.Add($"fleet '{fleet.ID}' has carrierAirwings for shipDesignID '{shipDesignId}' that is not present in shipDesigns.");
                }

                if (!designHullByDesignId.TryGetValue(shipDesignId, out var hullId) ||
                    !hullRoleByHullId.TryGetValue(hullId, out var hullRole) ||
                    !hullRole.Equals(nameof(ShipRole.Carrier), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"fleet '{fleet.ID}' has carrierAirwings for non-carrier shipDesignID '{shipDesignId}'.");
                }

                foreach (var assignment in carrierAirwing.Value ?? [])
                {
                    if (string.IsNullOrWhiteSpace(assignment.PlaneID))
                    {
                        errors.Add($"fleet '{fleet.ID}' has carrierAirwings entry with empty planeID for shipDesignID '{shipDesignId}'.");
                        continue;
                    }

                    if (!planeIds.Contains(assignment.PlaneID))
                    {
                        errors.Add($"fleet '{fleet.ID}' has carrierAirwings entry referencing unknown planeID '{assignment.PlaneID}' for shipDesignID '{shipDesignId}'.");
                    }

                    if (!TryParseAirwingType(assignment.Type, out _))
                    {
                        errors.Add($"fleet '{fleet.ID}' has carrierAirwings entry with unknown type '{assignment.Type}' for shipDesignID '{shipDesignId}'.");
                    }

                    if (assignment.Airwings <= 0)
                    {
                        errors.Add($"fleet '{fleet.ID}' has non-positive airwings value {assignment.Airwings} for shipDesignID '{shipDesignId}'.");
                    }

                    if (assignment.PlaneCount < 0)
                    {
                        errors.Add(
                            $"fleet '{fleet.ID}' has carrierAirwings entry with non-positive total plane count {assignment.PlaneCount} for shipDesignID '{shipDesignId}'.");
                    }
                }
            }
        }

        ValidateScenario(battleScenarioFile.BattleScenario, fleetIds, researchIds, spiritIds, errors);

        if (errors.Count > 0)
        {
            throw new SetupValidationException(errors);
        }

        var hullById = hulls.ToDictionary(
            h => h.ID,
            h => new Hull(h.ID, Enum.Parse<ShipRole>(h.Role, true), ParseTypes(h.Types ?? []), h.Manpower, h.BaseStats.ToDomain()));
        var moduleById = modules.ToDictionary(
            m => m.ID,
            m => new StatModule(
                m.ID,
                m.StatModifiers.ToDomain(),
                m.StatMultipliers.ToDomain(),
                m.StatAverages.ToDomain()));
        var mioById = mios.ToDictionary(
            mio => mio.ID,
            mio => new MioBonus(mio.ID, mio.Modifiers
                .Select(modifier => new MioModifier(
                    modifier.StatModifiers.ToDomain(),
                    modifier.StatAverages.ToDomain(),
                    modifier.StatMultipliers.ToDomain(),
                    ParseRoles(modifier.AppliesToRoles),
                    ParseTypes(modifier.AppliesToTypes ?? [])))
                .ToList()));
        var researchById = researches.ToDictionary(
            research => research.ID,
            research => new Research(
                research.ID,
                research.StatModifiers.ToDomain(),
                research.StatAverages.ToDomain(),
                research.StatMultipliers.ToDomain(),
                ParseRoles(research.AppliesToRoles),
                ParseTypes(research.AppliesToTypes ?? [])));
        var spiritById = spirits.ToDictionary(
            spirit => spirit.ID,
            spirit => new Spirit(
                spirit.ID,
                spirit.StatModifiers.ToDomain(),
                spirit.StatAverages.ToDomain(),
                spirit.StatMultipliers.ToDomain(),
                spirit.SortieEffiency,
                ParseRoles(spirit.AppliesToRoles),
                ParseTypes(spirit.AppliesToTypes ?? [])));

        var designById = new Dictionary<string, ShipDesign>();

        foreach (var design in designs)
        {
            var hull = hullById[design.HullID];
            var designModules = design.ModuleIDs.Select(moduleId => moduleById[moduleId]).ToList();
            var mio = string.IsNullOrWhiteSpace(design.MioID) ? null : mioById[design.MioID];
            designById[design.ID] = new ShipDesign(design.ID, hull, designModules, mio);
        }

        var fleetById = new Dictionary<string, Fleet>();

        foreach (var fleet in forceCompositionsFile.Fleets)
        {
            var ships = new List<Ship>();
            var carrierAirwingsByDesign = ParseCarrierAirwings(fleet.CarrierAirwings ?? []);

            foreach (var shipDesign in fleet.ShipDesigns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var design = designById[shipDesign.Key];
                var shipExperienceLevel = fleet.ShipExperienceLevels.TryGetValue(shipDesign.Key, out var configuredExperienceLevel)
                    ? configuredExperienceLevel
                    : Hoi4Defines.SHIP_EXPERIENCE_LEVEL_REGULAR;

                for (var i = 1; i <= shipDesign.Value; i++)
                {
                    var shipId = $"{fleet.ID}_{shipDesign.Key}_{i:D3}";
                    ships.Add(new Ship(shipId, design, shipExperienceLevel));
                }
            }

            fleetById[fleet.ID] = new Fleet(fleet.ID, ships, carrierAirwingsByDesign);
        }

        var attacker = BuildParticipant(battleScenarioFile.BattleScenario.Attacker, fleetById, researchById, spiritById);
        var defender = BuildParticipant(battleScenarioFile.BattleScenario.Defender, fleetById, researchById, spiritById);

        return new BattleScenario(
            battleScenarioFile.BattleScenario.ID,
            battleScenarioFile.BattleScenario.Terrain,
            battleScenarioFile.BattleScenario.Weather,
            battleScenarioFile.BattleScenario.MaxHours,
            battleScenarioFile.BattleScenario.Iterations ?? 1,
            attacker,
            defender,
            planeById);
    }

    private static List<TItem> ReadCollectionFromFolderOrFile<TItem>(
        string dataDirectoryPath,
        string folderName,
        string legacyFileName,
        string rootPropertyName,
        List<string> errors)
    {
        var folderPath = Path.Combine(dataDirectoryPath, folderName);

        if (Directory.Exists(folderPath))
        {
            var jsonFiles = Directory
                .GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (jsonFiles.Count == 0)
            {
                errors.Add($"Directory '{folderName}' does not contain any JSON files.");
                return [];
            }

            var items = new List<TItem>();

            foreach (var filePath in jsonFiles)
            {
                items.AddRange(ReadCollectionFile<TItem>(filePath, rootPropertyName, errors));
            }

            return items;
        }

        var legacyPath = Path.Combine(dataDirectoryPath, legacyFileName);
        return ReadCollectionFile<TItem>(legacyPath, rootPropertyName, errors);
    }

    private static List<TItem> ReadCollectionFile<TItem>(string filePath, string rootPropertyName, List<string> errors)
    {
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

    private static void ValidateScenario(
        BattleScenarioDto scenario,
        HashSet<string> fleetIds,
        HashSet<string> researchIds,
        HashSet<string> spiritIds,
        List<string> errors)
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

        if (scenario.Iterations.HasValue && scenario.Iterations.Value <= 0)
        {
            errors.Add("battleScenario.iterations must be greater than 0 when provided.");
        }

        ValidateParticipant("attacker", scenario.Attacker, fleetIds, researchIds, spiritIds, errors);
        ValidateParticipant("defender", scenario.Defender, fleetIds, researchIds, spiritIds, errors);
    }

    private static void ValidateParticipant(
        string role,
        BattleParticipantDto participant,
        HashSet<string> fleetIds,
        HashSet<string> researchIds,
        HashSet<string> spiritIds,
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

        if (participant.ExternalNavalStrikePlanes < 0)
        {
            errors.Add($"battleScenario.{role}.externalNavalStrikePlanes must be >= 0.");
        }

        if (participant.ShipExperienceLevel.HasValue &&
            !IsValidShipExperienceLevel(participant.ShipExperienceLevel.Value))
        {
            errors.Add($"battleScenario.{role}.shipExperienceLevel has unsupported level {participant.ShipExperienceLevel.Value}. Supported levels are 0 (untrained), 1 (regular), and 2 (trained).");
        }
    }

    private static BattleParticipant BuildParticipant(
        BattleParticipantDto participant,
        IReadOnlyDictionary<string, Fleet> fleetById,
        IReadOnlyDictionary<string, Research> researchById,
        IReadOnlyDictionary<string, Spirit> spiritById)
    {
        var fleet = fleetById[participant.FleetID];
        var participantResearches = participant.ResearchIDs.Select(researchId => researchById[researchId]).ToList();
        var participantSpirits = participant.SpiritIDs.Select(spiritId => spiritById[spiritId]).ToList();

        return new BattleParticipant(
            fleet,
            participant.Commander,
            participant.Doctrine,
            participant.ShipExperienceLevel,
            participant.ExternalNavalStrikePlanes,
            participantResearches,
            participantSpirits);
    }

    private static bool IsValidShipExperienceLevel(int level)
    {
        return level is Hoi4Defines.SHIP_EXPERIENCE_LEVEL_UNTRAINED or
            Hoi4Defines.SHIP_EXPERIENCE_LEVEL_REGULAR or
            Hoi4Defines.SHIP_EXPERIENCE_LEVEL_TRAINED;
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

    private static void ValidateScopedTypeFilters(
        IEnumerable<(string ID, List<string> AppliesToTypes)> entries,
        string entryType,
        List<string> errors)
    {
        foreach (var entry in entries)
        {
            foreach (var hullType in entry.AppliesToTypes)
            {
                if (!ShipHullTypes.IsKnown(hullType))
                {
                    errors.Add($"{entryType} '{entry.ID}' has unknown appliesToTypes value '{hullType}'.");
                }
            }
        }
    }

    private static List<ShipRole> ParseRoles(List<string> roleNames)
    {
        var roles = new List<ShipRole>();

        foreach (var roleName in roleNames)
        {
            if (Enum.TryParse<ShipRole>(roleName, true, out var role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    private static List<string> ParseTypes(List<string> typeNames)
    {
        return typeNames
            .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, List<CarrierAirwingAssignment>> ParseCarrierAirwings(
        Dictionary<string, List<CarrierAirwingAssignmentDto>> carrierAirwingsByDesign)
    {
        var parsed = new Dictionary<string, List<CarrierAirwingAssignment>>(StringComparer.Ordinal);

        foreach (var designEntry in carrierAirwingsByDesign)
        {
            var assignments = new List<CarrierAirwingAssignment>();

            foreach (var assignment in designEntry.Value ?? [])
            {
                if (!TryParseAirwingType(assignment.Type, out var type))
                {
                    continue;
                }
                
                
                assignments.Add(new CarrierAirwingAssignment(assignment.PlaneID, type, assignment.Airwings, assignment.PlaneCount == 0 ? 10 : assignment.PlaneCount));
            }

            if (assignments.Count > 0)
            {
                parsed[designEntry.Key] = assignments;
            }
        }

        return parsed;
    }

    private static bool TryParseAirwingType(string typeName, out AirwingType type)
    {
        if (typeName.Equals("figheter", StringComparison.OrdinalIgnoreCase))
        {
            type = AirwingType.Fighter;
            return true;
        }

        return Enum.TryParse(typeName, true, out type);
    }
}


