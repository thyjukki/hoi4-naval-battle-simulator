using NavySimulator.GameData.Contracts;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

/// <summary>S-MIO in the GUI: summaries, trait states, editing a side's setup, and the workspace's MIO lookups (imported TFB data).</summary>
public sealed class MioInfoTests : IDisposable
{
    private const string Brooklyn = "USA_brooklyn_naval_yard_organization";

    private readonly TestWorkspace test = new();

    private Workspace Workspace => test.Workspace;

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void Summarize_AddsHitChanceFactors_ScalesTheRest_AndMarksStatsTheSimulatorLacks()
    {
        var bonus = new Dictionary<string, double>
        {
            ["armor_value"] = 0.15,
            ["naval_light_gun_hit_chance_factor"] = 0.05,
            ["carrier_size"] = 1,
            ["fuel_consumption"] = -0.05
        };

        Assert.Equal("Armor +15%, Light hit chance +0.05, Carrier size (decks) +1, fuel_consumption -5% (not simulated)", MioInfo.Summarize(bonus));
        Assert.Equal("production capacity factor +5% (no effect in battle)",
            MioInfo.Summarize(new MioTraitDto { ProductionBonus = { ["production_capacity_factor"] = 0.05 } }));
    }

    [Fact]
    public void Traits_TellWhichTheSideCanUnlock()
    {
        var mio = Workspace.FindMio(Brooklyn)!;
        var side = new BattleParticipantDto();
        MioInfo.SetTrait(side, Brooklyn, "generic_mio_trait_external_armor_belt", true);
        MioInfo.SetTrait(side, Brooklyn, "generic_mio_trait_gyro_stabilized_mounts", true);

        var states = MioInfo.Traits(mio, side.Mios[Brooklyn]).ToDictionary(state => state.Trait.ID);

        Assert.True(states["generic_mio_trait_external_armor_belt"].Unlocked);
        Assert.Equal(["generic_mio_trait_external_armor_belt"], states["generic_mio_trait_internal_armor_belt"].ExcludedBy);
        Assert.False(states["generic_mio_trait_internal_armor_belt"].Available);
        Assert.True(states["generic_mio_trait_hardened_critical_components"].Available);
        Assert.True(states["generic_mio_trait_gyro_stabilized_mounts"].Broken);
        Assert.False(states["generic_mio_trait_super_heavy_shells"].ParentsMet);
    }

    [Fact]
    public void SetTraitAndPolicy_EditTheSidesSetup_AndDropItWhenEmpty()
    {
        var side = new BattleParticipantDto();

        MioInfo.SetTrait(side, Brooklyn, "generic_mio_trait_anti_air_layout", true);
        MioInfo.SetPolicy(side, Brooklyn, "mio_policy_navy_stable_platforms");
        Assert.Equal(["generic_mio_trait_anti_air_layout"], side.Mios[Brooklyn].Traits);
        Assert.Equal("mio_policy_navy_stable_platforms", side.Mios[Brooklyn].Policy);

        MioInfo.SetTrait(side, Brooklyn, "generic_mio_trait_anti_air_layout", false);
        MioInfo.SetPolicy(side, Brooklyn, null);
        Assert.Empty(side.Mios);
    }

    [Fact]
    public void Workspace_ListsAssignableMios_TheSidesMios_AndAllowedPolicies()
    {
        var design = Workspace.Catalog.ShipDesigns.Single(item => item.ID == "cl3_armor4_full_la");
        var assignable = Workspace.AssignableMios(design).Select(mio => mio.ID).ToList();

        Assert.Contains(Brooklyn, assignable);
        Assert.DoesNotContain("USA_electric_boat_company_organization", assignable);

        design.MioID = Brooklyn;
        var side = new BattleParticipantDto { FleetID = "75_cl3_armor4_full_la" };
        side.Mios["USA_electric_boat_company_organization"] = new MioSetupDto { Traits = ["x"] };

        var mios = Workspace.SideMios(side);
        Assert.Equal([Brooklyn, "USA_electric_boat_company_organization"], mios.Select(entry => entry.Mio.ID));
        Assert.Equal(["cl3_armor4_full_la"], mios[0].Designs);
        Assert.Empty(mios[1].Designs);

        var policies = Workspace.PoliciesFor(Workspace.FindMio("USA_electric_boat_company_organization")!).Select(policy => policy.ID).ToList();
        Assert.Contains("mio_policy_navy_welding_specialists", policies);
        Assert.DoesNotContain("mio_policy_navy_stable_platforms", policies);
    }

    [Fact]
    public void SideAssignments_ReplaceTheDesignersMio_AndTheCountryFiltersTheChoices()
    {
        var design = Workspace.Catalog.ShipDesigns.Single(item => item.ID == "cl3_armor4_full_la");
        var side = new BattleParticipantDto { FleetID = "75_cl3_armor4_full_la", Country = "JAP" };

        Assert.Empty(Workspace.SideMios(side));
        side.DesignMios[design.ID] = "JAP_kure_naval_arsenal_organization";
        Assert.Equal("JAP_kure_naval_arsenal_organization", Workspace.EffectiveMioId(side, design));
        Assert.Equal(["cl3_armor4_full_la"], Workspace.SideMios(side).Single().Designs);

        design.MioID = Brooklyn;
        side.DesignMios[design.ID] = string.Empty;
        Assert.Null(Workspace.EffectiveMioId(side, design));
        side.DesignMios.Clear();
        Assert.Equal(Brooklyn, Workspace.EffectiveMioId(side, design));

        var japanese = Workspace.AssignableMios(design, "JAP").Select(mio => mio.ID).ToList();
        Assert.Contains("JAP_kure_naval_arsenal_organization", japanese);
        Assert.DoesNotContain(Brooklyn, japanese);
        Assert.DoesNotContain("generic_battle_line_ship_organization", japanese);
        Assert.Contains(Brooklyn, Workspace.AssignableMios(design).Select(mio => mio.ID));
    }

    [Fact]
    public void Countries_ListThoseWithTheirOwnMiosFirst()
    {
        var countries = Workspace.Countries();

        Assert.True(countries.First().HasOwnMios);
        Assert.Contains(countries, entry => entry.Country.ID == "USA" && entry.HasOwnMios);
        Assert.Equal(countries.Count, countries.TakeWhile(entry => entry.HasOwnMios).Count() + countries.SkipWhile(entry => entry.HasOwnMios).Count(entry => !entry.HasOwnMios));
    }

    [Fact]
    public void ShipStats_OfASide_HaveItsUnlockedTraits()
    {
        var design = Workspace.Catalog.ShipDesigns.Single(item => item.ID == "cl3_armor4_full_la");
        design.MioID = Brooklyn;
        var side = new BattleParticipantDto { FleetID = "75_cl3_armor4_full_la" };
        var without = Workspace.ShipStats(design, side)!;

        MioInfo.SetTrait(side, Brooklyn, "generic_mio_trait_anti_air_layout", true);
        var with = Workspace.ShipStats(design, side)!;

        Assert.True(with.AntiAir > without.AntiAir);
        Assert.Equal(without.LightAttack, with.LightAttack, 9);
    }
}
