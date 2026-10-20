using NavySimulator.GameData.Contracts;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

/// <summary>S-ADMIRAL in the GUI: trait summaries, the country filter and the side preview (imported TFB data).</summary>
public sealed class AdmiralInfoTests : IDisposable
{
    private readonly TestWorkspace test = new();

    private Workspace Workspace => test.Workspace;

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void Summarize_MarksWhatCombatReadsFromAnAdmiral()
    {
        var gentlemanly = Workspace.Catalog.LeaderTraits.Single(trait => trait.ID == "gentlemanly");
        var blueWater = Workspace.Catalog.LeaderTraits.Single(trait => trait.ID == "blue_water_expert");
        var tactician = Workspace.Catalog.LeaderTraits.Single(trait => trait.ID == "superior_tactician");

        Assert.Equal("naval_enemy_retreat_chance +20%, navy_org +5", AdmiralInfo.Summarize(gentlemanly));
        Assert.Equal("water_deep_ocean: attack +10%, defence +10%, movement +10% (naval terrain not simulated)", AdmiralInfo.Summarize(blueWater));
        Assert.Contains("naval_coordination +1.5% (no effect in battle)", AdmiralInfo.Summarize(tactician), StringComparison.Ordinal);
        Assert.Contains("ships_at_battle_start +5% (not simulated)", AdmiralInfo.Summarize(tactician), StringComparison.Ordinal);
        Assert.Equal("navy_carrier_air_targetting_factor +20% (no effect in battle), sortie_efficiency +10%",
            AdmiralInfo.Summarize(Workspace.Catalog.LeaderTraits.Single(trait => trait.ID == "air_controller")));
        Assert.Equal("Basic", AdmiralInfo.Group(tactician));
        Assert.Equal("Terrain", AdmiralInfo.Group(blueWater));
    }

    [Fact]
    public void Admirals_AreFilteredByTheSidesCountry()
    {
        var american = Workspace.Admirals("USA");

        Assert.NotEmpty(american);
        Assert.All(american, admiral => Assert.Equal("USA", admiral.Country));
        Assert.Contains(american, admiral => admiral.ID == "USA_chester_w_nimitz");
        Assert.True(Workspace.Admirals(null).Count > american.Count);
    }

    [Fact]
    public void PrideOfTheFleetCandidates_AreTheFleetsCapitalShipsAndCarriers()
    {
        var side = new BattleParticipantDto { FleetID = "Jukki_bb_fleet_usa" };

        Assert.Equal(["39_battleship_pure_la", "39_carrier", "shbb_4battery_2aa"], Workspace.PrideOfTheFleetCandidates(side).Select(design => design.ID).Order(StringComparer.Ordinal));
        Assert.StartsWith("The ship takes half the gun and torpedo crits", AdmiralInfo.PrideOfTheFleetEffects(null), StringComparison.Ordinal);
    }

    [Fact]
    public void SideEffects_BuildTheSidesAdmiral_AndListWhatIsNotSimulated()
    {
        var nimitz = Workspace.Clone(Workspace.FindAdmiral("USA_chester_w_nimitz")!);
        var side = new BattleParticipantDto { Admiral = nimitz };

        var effects = Workspace.SideEffects(side);

        Assert.True(AdmiralInfo.SameAs(nimitz, Workspace.FindAdmiral("USA_chester_w_nimitz")!));
        Assert.Equal("Chester W. Nimitz", effects.Admiral!.Name);
        Assert.Contains("spotting_chance (admiral)", effects.IgnoredAdmiralEffects());
        Assert.Contains("water_deep_ocean adjuster (admiral trait blue_water_expert)", effects.IgnoredAdmiralEffects());

        nimitz.AttackSkill = 6;
        Assert.False(AdmiralInfo.SameAs(nimitz, Workspace.FindAdmiral("USA_chester_w_nimitz")!));
        Assert.Equal("Chester W. Nimitz (4 · 6/2/4/5)", AdmiralInfo.Label(nimitz));
    }
}
