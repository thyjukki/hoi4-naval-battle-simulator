using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Tests.Loading;

/// <summary>S-POTF in the scenario setup: the named design's first ship becomes the side's pride of the fleet.</summary>
public sealed class PrideOfTheFleetSetupTests : IDisposable
{
    private const string Scenario = "jukki_usa_vs_bc_japan_no_doctrines.json";

    private readonly string data = Path.Combine(Path.GetTempPath(), "navysim-potf-setup-" + Guid.NewGuid().ToString("N"));

    public PrideOfTheFleetSetupTests()
    {
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, data);
    }

    public void Dispose()
    {
        Directory.Delete(data, true);
    }

    [Fact]
    public void TheFirstShipOfTheDesign_IsThePride()
    {
        var (scenario, loader) = Build(side => side.PrideOfTheFleet = "39_battleship_pure_la");
        var pride = scenario.Defender.PrideOfTheFleet!;

        Assert.Equal("Jukki_bb_fleet_usa_20261019_39_battleship_pure_la_001", pride.ID);
        Assert.True(pride.IsPrideOfTheFleet);
        Assert.All(scenario.Defender.Fleet.Ships, ship => Assert.Same(pride, ship.PrideOfTheFleet));
        Assert.Null(scenario.Attacker.PrideOfTheFleet);
        Assert.DoesNotContain(loader.Warnings, warning => warning.Contains("pride", StringComparison.Ordinal));
    }

    [Fact]
    public void ADesignOutsideTheFleet_IsAnError_AndAScreenAWarning()
    {
        var exception = Assert.Throws<SetupValidationException>(() => Build(side => side.PrideOfTheFleet = "dd_3_la"));
        Assert.Contains("battleScenario.defender.prideOfTheFleet names design 'dd_3_la', which is not in fleet 'Jukki_bb_fleet_usa_20261019'.", exception.Errors);

        var (_, loader) = Build(side => side.PrideOfTheFleet = "39lc_la_aa3");
        Assert.Contains(loader.Warnings, warning => warning.StartsWith("defender: the pride of the fleet (39lc_la_aa3) is a ", StringComparison.Ordinal) &&
                                                    warning.EndsWith(", not a capital ship or carrier; it is simulated anyway.", StringComparison.Ordinal));
    }

    private (NavySimulator.Engine.Battles.BattleScenario Scenario, SetupLoader Loader) Build(Action<BattleParticipantDto> edit)
    {
        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(data);
        var scenario = SetupLoader.ReadScenario(data, Scenario);
        edit(scenario.Defender);
        return (loader.BuildScenario(catalog, scenario), loader);
    }
}
