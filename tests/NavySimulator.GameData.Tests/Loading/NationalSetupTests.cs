using NavySimulator.Engine.Battles;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Tests.Loading;

/// <summary>S-NATIONAL in the scenario setup, on the imported TFB nations: the defender plays JAP, the attacker USA.</summary>
public sealed class NationalSetupTests : IDisposable
{
    private const string Scenario = "jukki_usa_vs_bc_japan.json";
    private const string TorpedoHitChance = "naval_torpedo_hit_chance_factor";

    private readonly string data = Path.Combine(Path.GetTempPath(), "navysim-national-setup-" + Guid.NewGuid().ToString("N"));

    public NationalSetupTests()
    {
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, data);
    }

    public void Dispose()
    {
        Directory.Delete(data, true);
    }

    [Fact]
    public void WithoutANationalSetup_TheSideHasNoNationalEffects()
    {
        var (scenario, _) = Build(_ => { });

        Assert.Null(scenario.Defender.National);
        Assert.Equal(0, scenario.Defender.CountryModifiers.Get(TorpedoHitChance) - Baseline().Defender.CountryModifiers.Get(TorpedoHitChance));
    }

    [Fact]
    public void JapansHistory_GivesTheImperialNavyModifier_AndFocusesRaiseItsVariables()
    {
        var baseline = Baseline().Defender.CountryModifiers.Get(TorpedoHitChance);
        var (history, _) = Build(side => side.National = new NationalSetupDto());
        var (focused, _) = Build(side => side.National = new NationalSetupDto { Focuses = ["JAP_establish_the_mine_warfare_force"] });
        var (edited, _) = Build(side => side.National = new NationalSetupDto
        {
            Focuses = ["JAP_establish_the_mine_warfare_force"],
            Variables = new Dictionary<string, double> { ["JAP_navy_naval_torpedo_hit_chance_factor"] = 0.3 }
        });

        Assert.Contains(history.Defender.ActiveEffects, effect => effect.Source == "JAP_imperial_navy_dm");
        Assert.Equal(0.15, history.Defender.CountryModifiers.Get(TorpedoHitChance) - baseline, 10);
        Assert.Equal(0.2, focused.Defender.CountryModifiers.Get(TorpedoHitChance) - baseline, 10);
        Assert.Equal(0.3, edited.Defender.CountryModifiers.Get(TorpedoHitChance) - baseline, 10);
        Assert.All(history.Defender.Fleet.Ships, ship => Assert.Equal(history.Defender.CountryModifiers, ship.CountryModifiers));
    }

    [Fact]
    public void ASpiritAddedByHand_ListsItsTargetedModifiersAsNotSimulated()
    {
        var (scenario, _) = Build(side => side.National = new NationalSetupDto { FromHistory = false }, attacker => attacker.National = new NationalSetupDto
        {
            AddedSpirits = ["JAP_day_of_infamy_2"]
        });

        Assert.Equal(-0.2, scenario.Attacker.CountryModifiers.Get("navy_anti_air_attack_factor") - Baseline().Attacker.CountryModifiers.Get("navy_anti_air_attack_factor"), 10);
        Assert.Contains(scenario.Attacker.IgnoredEffects(), entry => entry.StartsWith("navy_capital_ship_attack_factor_against -0.25 against JAP", StringComparison.Ordinal));
        Assert.DoesNotContain(scenario.Defender.ActiveEffects, effect => effect.Source.StartsWith("JAP_", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownFocusesSpiritsAndCountries_AreErrors()
    {
        var exception = Assert.Throws<SetupValidationException>(() => Build(side => side.National = new NationalSetupDto
        {
            Focuses = ["JAP_no_such_focus"],
            RemovedSpirits = ["JAP_no_such_spirit"]
        }));
        Assert.Contains("battleScenario.defender.national.focuses: JAP has no focus 'JAP_no_such_focus' with naval effects.", exception.Errors);
        Assert.Contains("battleScenario.defender.national: JAP has no national spirit 'JAP_no_such_spirit' with naval effects.", exception.Errors);

        exception = Assert.Throws<SetupValidationException>(() => Build(side =>
        {
            side.Country = "GER";
            side.National = new NationalSetupDto();
        }));
        Assert.Contains(exception.Errors, error => error.StartsWith("battleScenario.defender.national: no national data for GER", StringComparison.Ordinal));
    }

    [Fact]
    public void MutuallyExclusiveFocuses_AreSimulatedWithAWarning()
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(data);
        var japan = catalog.Nations.Single(nation => nation.ID == "JAP");
        var first = japan.Focuses[0];
        var second = japan.Focuses[1];
        first.MutuallyExclusive = [second.ID];
        var scenario = SetupLoader.ReadScenario(data, Scenario);
        scenario.Defender.National = new NationalSetupDto { Focuses = [second.ID, first.ID] };

        loader.BuildScenario(catalog, scenario);

        var (a, b) = string.CompareOrdinal(first.ID, second.ID) < 0 ? (first.ID, second.ID) : (second.ID, first.ID);
        Assert.Contains($"defender: focuses '{a}' and '{b}' are mutually exclusive in the game; both are simulated.", loader.Warnings);
    }

    private BattleScenario Baseline()
    {
        return Build(_ => { }).Scenario;
    }

    private (BattleScenario Scenario, SetupLoader Loader) Build(Action<BattleParticipantDto> editDefender, Action<BattleParticipantDto>? editAttacker = null)
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(data);
        var scenario = SetupLoader.ReadScenario(data, Scenario);
        editDefender(scenario.Defender);
        editAttacker?.Invoke(scenario.Attacker);
        return (loader.BuildScenario(catalog, scenario), loader);
    }
}
