using NavySimulator.Engine.Stats;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.GameData.Tests.Loading;

/// <summary>S-MIO in the scenario setup, on the imported MIOs (TFB): validation, warnings, and the bonus on the built ships.</summary>
public sealed class MioSetupTests : IDisposable
{
    private const string Brooklyn = "USA_brooklyn_naval_yard_organization";
    private const string ElectricBoat = "USA_electric_boat_company_organization";
    private const string Kure = "JAP_kure_naval_arsenal_organization";
    private const string Design = "cl3_armor4_full_la";

    private readonly string data = Path.Combine(Path.GetTempPath(), "navysim-mio-setup-" + Guid.NewGuid().ToString("N"));

    public MioSetupTests()
    {
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, data);
    }

    public void Dispose()
    {
        Directory.Delete(data, true);
    }

    [Fact]
    public void UnlockedTraits_ReachTheSidesShips_AndTheOtherSideKeepsTheInitialTrait()
    {
        var (scenario, loader) = Build(Brooklyn, side =>
        {
            side.ResearchIDs.Clear();
            side.Mios[Brooklyn] = new MioSetupDto { Traits = ["generic_mio_trait_anti_air_layout", "generic_mio_trait_secondary_battery_layout"] };
        });
        var ship = scenario.Attacker.Fleet.Ships[0];

        // Brooklyn's initial trait has no equipment bonus; its armor belts are limited to battleships.
        Assert.Equal(ship.Design.ComputeStats([], new ShipStats(AntiAir: 0.05, LightAttack: 0.05)), ship.VariantStats);
        Assert.Equal(ship.Design.ComputeStats([]), ship.Design.GetFinalStats());
        Assert.True(ship.VariantStats.LightAttack > ship.Design.GetFinalStats().LightAttack);
        Assert.DoesNotContain(loader.Warnings, warning => warning.Contains("MIO", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSideCanAssignAnMio_ToADesignWithout_AndTakeTheDesignersAway()
    {
        var (assigned, loader) = Build(null, side =>
        {
            side.ResearchIDs.Clear();
            side.Country = "USA";
            side.DesignMios[Design] = Brooklyn;
            side.Mios[Brooklyn] = new MioSetupDto { Traits = ["generic_mio_trait_anti_air_layout"] };
        });
        var ship = assigned.Attacker.Fleet.Ships[0];

        Assert.Null(ship.Design.Mio);
        Assert.Equal(Brooklyn, assigned.Attacker.MioOf(ship.Design)?.ID);
        Assert.Equal(ship.Design.ComputeStats([], new ShipStats(AntiAir: 0.05)), ship.VariantStats);
        Assert.Null(assigned.Defender.MioOf(assigned.Defender.Fleet.Ships[0].Design));
        Assert.DoesNotContain(loader.Warnings, warning => warning.Contains("MIO", StringComparison.Ordinal));

        var (removed, _) = Build(Brooklyn, side =>
        {
            side.ResearchIDs.Clear();
            side.DesignMios[Design] = string.Empty;
            side.Mios[Brooklyn] = new MioSetupDto { Traits = ["generic_mio_trait_anti_air_layout"] };
        });
        var bare = removed.Attacker.Fleet.Ships[0];

        Assert.Equal(Brooklyn, bare.Design.Mio?.ID);
        Assert.Null(removed.Attacker.MioOf(bare.Design));
        Assert.Equal(bare.Design.ComputeStats([], new ShipStats()), bare.VariantStats);
    }

    [Fact]
    public void CountriesAndDesignAssignments_TheGameWouldNotAllow_AreWarnings()
    {
        var (_, loader) = Build(null, side =>
        {
            side.Country = "USA";
            side.DesignMios[Design] = Kure;
            side.DesignMios["no_such_design"] = Brooklyn;
            side.Mios[ElectricBoat] = new MioSetupDto();
        });

        Assert.Contains($"attacker: USA cannot have MIO '{Kure}' (JAP); it is simulated anyway.", loader.Warnings);
        Assert.Contains("attacker: designMios names design 'no_such_design', which is not in the side's fleet; it is ignored.", loader.Warnings);
        Assert.DoesNotContain(loader.Warnings, warning => warning.Contains($"MIO '{ElectricBoat}'", StringComparison.Ordinal));

        var (_, covering) = Build(null, side => side.DesignMios[Design] = ElectricBoat);
        Assert.Contains($"attacker: MIO '{ElectricBoat}' does not cover design '{Design}' (ship_hull_submarine); the game would not allow it.", covering.Warnings);
    }

    [Fact]
    public void UnknownCountriesAndAssignedMios_AreErrors()
    {
        var exception = Assert.Throws<SetupValidationException>(() => Build(null, side =>
        {
            side.Country = "XXX";
            side.DesignMios[Design] = "no_such_mio";
        }));

        Assert.Contains("battleScenario.attacker.country references unknown country tag 'XXX'.", exception.Errors);
        Assert.Contains(exception.Errors, error => error.StartsWith($"battleScenario.attacker.designMios.{Design} references unknown MIO 'no_such_mio'", StringComparison.Ordinal));
    }

    [Fact]
    public void Availability_FollowsTheAllowedTags()
    {
        var catalog = new SetupLoader().ReadCatalog(data);
        var escort = catalog.Mios.Single(mio => mio.ID == "generic_escort_ship_organization");

        Assert.True(MioCountries.IsAvailable(catalog.Mios.Single(mio => mio.ID == Brooklyn), "USA"));
        Assert.False(MioCountries.IsAvailable(catalog.Mios.Single(mio => mio.ID == Kure), "USA"));
        Assert.True(MioCountries.IsAvailable(catalog.Mios.Single(mio => mio.ID == Kure), null));
        Assert.False(MioCountries.IsAvailable(catalog.Mios.Single(mio => mio.ID == "generic_battle_line_ship_organization"), "USA"));
        Assert.False(MioCountries.IsAvailable(escort, "USA"));
        Assert.True(MioCountries.IsAvailable(escort, "LUX"));
    }

    [Fact]
    public void UnknownMiosTraitsAndPolicies_AreErrors()
    {
        var exception = Assert.Throws<SetupValidationException>(() => Build(Brooklyn, side =>
        {
            side.Mios["no_such_mio"] = new MioSetupDto();
            side.Mios[Brooklyn] = new MioSetupDto { Traits = ["no_such_trait"], Policy = "no_such_policy" };
        }));

        Assert.Contains(exception.Errors, error => error.StartsWith("battleScenario.attacker.mios.no_such_mio references unknown MIO 'no_such_mio'", StringComparison.Ordinal));
        Assert.Contains($"battleScenario.attacker.mios.{Brooklyn}.traits: MIO '{Brooklyn}' has no trait 'no_such_trait'.", exception.Errors);
        Assert.Contains($"battleScenario.attacker.mios.{Brooklyn}.policy references unknown MIO policy 'no_such_policy'.", exception.Errors);
    }

    [Fact]
    public void SetupsTheGameForbids_AreWarnings()
    {
        var (_, loader) = Build(ElectricBoat, side =>
        {
            side.Mios[Brooklyn] = new MioSetupDto
            {
                Traits = ["generic_mio_trait_external_armor_belt", "generic_mio_trait_internal_armor_belt", "generic_mio_trait_gyro_stabilized_mounts"]
            };
            side.Mios[ElectricBoat] = new MioSetupDto { Policy = "mio_policy_navy_stable_platforms" };
        });

        Assert.Contains($"attacker: MIO '{Brooklyn}': traits 'generic_mio_trait_external_armor_belt' and 'generic_mio_trait_internal_armor_belt' are mutually exclusive; it is simulated anyway.", loader.Warnings);
        Assert.Contains($"attacker: MIO '{Brooklyn}': trait 'generic_mio_trait_gyro_stabilized_mounts' needs all of generic_mio_trait_anti_air_layout, generic_mio_trait_secondary_battery_layout unlocked first; it is simulated anyway.", loader.Warnings);
        Assert.Contains(loader.Warnings, warning => warning.StartsWith($"attacker: MIO '{ElectricBoat}' cannot take policy 'mio_policy_navy_stable_platforms'", StringComparison.Ordinal));
        Assert.Contains(loader.Warnings, warning => warning.EndsWith($"'{Design}': MIO '{ElectricBoat}' does not cover this design (ship_hull_submarine); the game would not allow it.", StringComparison.Ordinal));
    }

    // The attacker's light cruisers (cl3_armor4_full_la) are assigned to the MIO in the designer (none when null).
    private (NavySimulator.Engine.Battles.BattleScenario Scenario, SetupLoader Loader) Build(string? mio, Action<BattleParticipantDto> edit)
    {
        if (mio is not null)
        {
            var path = Path.Combine(data, "ship-designs", "light-cruisers.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace($"\"id\": \"{Design}\",", $"\"id\": \"{Design}\", \"mioID\": \"{mio}\","));
        }

        var loader = new SetupLoader();
        var catalog = loader.ReadCatalog(data);
        Assert.Equal(mio, catalog.ShipDesigns.Single(design => design.ID == Design).MioID);
        var scenario = SetupLoader.ReadScenario(data, "battle-scenario.json");
        edit(scenario.Attacker);
        return (loader.BuildScenario(catalog, scenario), loader);
    }
}
