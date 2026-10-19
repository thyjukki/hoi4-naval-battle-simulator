using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Tests.Loading;

/// <summary>S-ADMIRAL in the scenario setup, on the imported admirals (TFB): the built admiral, validation and warnings.</summary>
public sealed class AdmiralSetupTests : IDisposable
{
    private const string Nimitz = "USA_chester_w_nimitz";

    private readonly string data = Path.Combine(Path.GetTempPath(), "navysim-admiral-setup-" + Guid.NewGuid().ToString("N"));

    public AdmiralSetupTests()
    {
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, data);
    }

    public void Dispose()
    {
        Directory.Delete(data, true);
    }

    [Fact]
    public void APickedAdmiral_ReachesEveryShipOfTheSide()
    {
        var (scenario, loader) = Build(side => side.Admiral = Copy(Nimitz));
        var admiral = scenario.Attacker.Admiral!;
        var ship = scenario.Attacker.Fleet.Ships[0];

        // TFB: attack 2 is naval_damage_factor 0.05, maneuvering 4 positioning 0.1; Superior Tactician adds 0.025 damage.
        Assert.Equal("Chester W. Nimitz", admiral.Name);
        Assert.Equal(0.05 + 0.025, admiral.CombatModifiers.Get("naval_damage_factor"), 9);
        Assert.Equal(0.1, admiral.CombatModifiers.Get("positioning"), 9);
        Assert.Equal(0.2, admiral.TraitModifiers.Get("naval_enemy_retreat_chance"), 9);
        Assert.Same(admiral, ship.Admiral);
        Assert.Equal(admiral.CombatModifiers.Get("naval_hit_chance"), ship.AdmiralModifiers.Get("naval_hit_chance"));
        Assert.Null(scenario.Defender.Admiral);
        Assert.Equal(0, scenario.Defender.Fleet.Ships[0].AdmiralModifiers.Get("naval_hit_chance"));
        Assert.DoesNotContain(loader.Warnings, warning => warning.Contains("admiral", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnknownTraitsAndNegativeSkills_AreErrors()
    {
        var exception = Assert.Throws<SetupValidationException>(() => Build(side => side.Admiral = new AdmiralDto
        {
            Name = "Made up",
            AttackSkill = -1,
            Traits = ["no_such_trait"]
        }));

        Assert.Contains(exception.Errors, error => error.StartsWith("battleScenario.attacker.admiral.traits references unknown leader trait 'no_such_trait'", StringComparison.Ordinal));
        Assert.Contains("battleScenario.attacker.admiral has attack_skill -1; skill levels are 0 or more.", exception.Errors);
    }

    [Fact]
    public void SkillsAboveTheGamesLevelsAndTraitsTheGameWouldNotAllow_AreWarnings()
    {
        var (_, loader) = Build(side => side.Admiral = new AdmiralDto { Name = "Made up", AttackSkill = 12, Traits = ["lancer", "silent_hunter"] });

        Assert.Contains("attacker: the admiral's attack_skill 12 is above the highest level the game defines (10); level 10 is used.", loader.Warnings);
        Assert.Contains(loader.Warnings, warning => warning.StartsWith("attacker: admiral Made up: trait 'lancer' needs 1 of seawolf, spotter, fleet_protector", StringComparison.Ordinal));
        Assert.Contains("attacker: admiral Made up: traits 'lancer' and 'silent_hunter' are mutually exclusive; it is simulated anyway.", loader.Warnings);
    }

    private AdmiralDto Copy(string id)
    {
        var admiral = new SetupLoader().ReadCatalog(data).Admirals.Single(item => item.ID == id);
        return new AdmiralDto
        {
            ID = admiral.ID,
            Name = admiral.Name,
            Country = admiral.Country,
            Skill = admiral.Skill,
            AttackSkill = admiral.AttackSkill,
            DefenseSkill = admiral.DefenseSkill,
            ManeuveringSkill = admiral.ManeuveringSkill,
            CoordinationSkill = admiral.CoordinationSkill,
            Traits = [.. admiral.Traits]
        };
    }

    private (NavySimulator.Engine.Battles.BattleScenario Scenario, SetupLoader Loader) Build(Action<BattleParticipantDto> edit)
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(data);
        var scenario = SetupLoader.ReadScenario(data, "battle-scenario.json");
        edit(scenario.Attacker);
        return (loader.BuildScenario(catalog, scenario), loader);
    }
}
