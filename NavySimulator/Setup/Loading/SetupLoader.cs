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

    public BattleScenario LoadScenarioFromDirectory(string dataDirectoryPath)
    {
        var errors = new List<string>();

        if (!Directory.Exists(dataDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Data directory not found: {dataDirectoryPath}");
        }

        var hullsFile = ReadJsonFile<HullsFileDto>(dataDirectoryPath, "hulls.json", errors) ?? new HullsFileDto();
        var modulesFile = ReadJsonFile<ModulesFileDto>(dataDirectoryPath, "modules.json", errors) ?? new ModulesFileDto();
        var miosFile = ReadJsonFile<MiosFileDto>(dataDirectoryPath, "mios.json", errors) ?? new MiosFileDto();
        var designsFile = ReadJsonFile<ShipDesignsFileDto>(dataDirectoryPath, "ship-designs.json", errors) ?? new ShipDesignsFileDto();
        var forceCompositionsFile = ReadJsonFile<ForceCompositionsFileDto>(dataDirectoryPath, "force-compositions.json", errors) ?? new ForceCompositionsFileDto();
        var battleScenarioFile = ReadJsonFile<BattleScenarioFileDto>(dataDirectoryPath, "battle-scenario.json", errors) ?? new BattleScenarioFileDto();

        ValidateRequiredIds(hullsFile.Hulls.Select(h => h.ID), "hulls", errors);
        ValidateRequiredIds(modulesFile.Modules.Select(m => m.ID), "modules", errors);
        ValidateRequiredIds(miosFile.Mios.Select(m => m.ID), "mios", errors);
        ValidateRequiredIds(designsFile.ShipDesigns.Select(d => d.ID), "shipDesigns", errors);
        ValidateRequiredIds(forceCompositionsFile.Ships.Select(s => s.ID), "ships", errors);
        ValidateRequiredIds(forceCompositionsFile.Fleets.Select(f => f.ID), "fleets", errors);

        ValidateDuplicateIds(hullsFile.Hulls.Select(h => h.ID), "hulls", errors);
        ValidateDuplicateIds(modulesFile.Modules.Select(m => m.ID), "modules", errors);
        ValidateDuplicateIds(miosFile.Mios.Select(m => m.ID), "mios", errors);
        ValidateDuplicateIds(designsFile.ShipDesigns.Select(d => d.ID), "shipDesigns", errors);
        ValidateDuplicateIds(forceCompositionsFile.Ships.Select(s => s.ID), "ships", errors);
        ValidateDuplicateIds(forceCompositionsFile.Fleets.Select(f => f.ID), "fleets", errors);

        var hullIds = hullsFile.Hulls.Select(h => h.ID).ToHashSet();
        var moduleIds = modulesFile.Modules.Select(m => m.ID).ToHashSet();
        var mioIds = miosFile.Mios.Select(m => m.ID).ToHashSet();
        var designIds = designsFile.ShipDesigns.Select(d => d.ID).ToHashSet();
        var shipIds = forceCompositionsFile.Ships.Select(s => s.ID).ToHashSet();
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

        foreach (var ship in forceCompositionsFile.Ships)
        {
            if (!designIds.Contains(ship.ShipDesignID))
            {
                errors.Add($"ship '{ship.ID}' references unknown shipDesignID '{ship.ShipDesignID}'.");
            }
        }

        foreach (var fleet in forceCompositionsFile.Fleets)
        {
            if (fleet.ShipIDs.Count == 0)
            {
                errors.Add($"fleet '{fleet.ID}' must include at least one shipID.");
            }

            foreach (var shipId in fleet.ShipIDs)
            {
                if (!shipIds.Contains(shipId))
                {
                    errors.Add($"fleet '{fleet.ID}' references unknown shipID '{shipId}'.");
                }
            }
        }

        ValidateScenario(battleScenarioFile.BattleScenario, fleetIds, errors);

        if (errors.Count > 0)
        {
            throw new SetupValidationException(errors);
        }

        var hullById = hullsFile.Hulls.ToDictionary(h => h.ID, h => new Hull(h.ID, h.BaseStats.ToDomain()));
        var moduleById = modulesFile.Modules.ToDictionary(m => m.ID, m => (IModule)new StatModule(m.ID, m.StatModifiers.ToDomain()));
        var mioById = miosFile.Mios.ToDictionary(m => m.ID, m => new MioBonus(m.ID, m.PercentBonus.ToDomain()));

        var designById = new Dictionary<string, ShipDesign>();

        foreach (var design in designsFile.ShipDesigns)
        {
            var hull = hullById[design.HullID];
            var modules = design.ModuleIDs.Select(moduleId => moduleById[moduleId]).ToList();
            var mio = string.IsNullOrWhiteSpace(design.MioID) ? null : mioById[design.MioID];
            designById[design.ID] = new ShipDesign(hull, modules, mio);
        }

        var shipById = new Dictionary<string, Ship>();

        foreach (var ship in forceCompositionsFile.Ships)
        {
            shipById[ship.ID] = new Ship(ship.ID, designById[ship.ShipDesignID]);
        }

        var fleetById = new Dictionary<string, Fleet>();

        foreach (var fleet in forceCompositionsFile.Fleets)
        {
            var ships = fleet.ShipIDs.Select(shipId => shipById[shipId]).ToList();
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


