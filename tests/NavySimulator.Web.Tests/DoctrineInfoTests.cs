using System.Globalization;
using NavySimulator.Engine.Doctrines;
using NavySimulator.GameData.Contracts;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public sealed class DoctrineInfoTests : IDisposable
{
    private readonly TestWorkspace test = new();

    private Workspace Workspace => test.Workspace;

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void Summarize_ListsCountryModifiersUnitAndEquipmentBonusesAndScriptedParts()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var effects = new CountryEffectsDto
        {
            CountryModifiers = { ["naval_hit_chance"] = 0.1, ["naval_detection"] = 0.05 },
            UnitBonuses = { ["battleship"] = new() { ["max_organisation"] = 7.5, ["hg_attack"] = 0.05, ["supply_consumption"] = -0.1 } },
            EquipmentBonuses = { ["ship_hull_heavy"] = new() { ["armor_value"] = 0.075 } },
            Scripted = ["effect"]
        };

        Assert.Equal(
            "Naval Hit Chance +10%, naval_detection +5% (not simulated), battleship: Organization +7.5, Heavy attack +5%, " +
            "supply_consumption -10% (not simulated), ship_hull_heavy equipment: Armor +7.5%, effect (not simulated)",
            DoctrineInfo.Summarize(effects));
    }

    [Fact]
    public void Rewards_HaveCumulativeThresholds()
    {
        var subdoctrine = new SubdoctrineDto { Rewards = [new() { ID = "a" }, new() { ID = "b", Mastery = 150 }, new() { ID = "c" }] };

        var rewards = DoctrineInfo.Rewards(subdoctrine, 250);

        Assert.Equal([100.0, 250.0, 350.0], rewards.Select(reward => reward.Threshold));
        Assert.Equal([true, true, false], rewards.Select(reward => reward.Unlocked));
    }

    [Fact]
    public void MissingDoctrines_NamesTheGroupsTheSideDoesNotMeet()
    {
        var spirit = new SpiritDto { RequiredDoctrines = [["torpedo_primacy", "new_base_strike"], ["new_fleet_in_being"]] };
        var library = Workspace.Library;
        var setup = new DoctrineSetup(library.GrandDoctrines["new_base_strike"], []);

        Assert.Equal("needs new_fleet_in_being", DoctrineInfo.MissingDoctrines(spirit, [setup], id => id));
        Assert.Null(DoctrineInfo.MissingDoctrines(new SpiritDto(), [], id => id));
    }

    [Fact]
    public void ShipStats_ApplyTheSidesUnlockedDoctrineRewards()
    {
        // TFB's Integrated Operations: reward 2 gives light cruisers organization +5 and light gun hit chance +5%.
        var design = Workspace.FindDesign("cl3_armor4_full_la")!;
        var locked = Side(mastery: 100);
        var unlocked = Side(mastery: 200);

        var before = Workspace.ShipStats(design, locked)!;
        var after = Workspace.ShipStats(design, unlocked)!;

        Assert.Equal(before.Organization + 5, after.Organization, 9);
        Assert.True(after.LightHitChanceFactor > before.LightHitChanceFactor);
        Assert.Contains(Workspace.SideEffects(unlocked).ActiveEffects, effect => effect.Source == "support_integrated_operations/first_line_of_attack");
    }

    private static BattleParticipantDto Side(double mastery)
    {
        return new BattleParticipantDto
        {
            Doctrines =
            {
                ["naval"] = new DoctrineSetupDto
                {
                    GrandDoctrine = "new_fleet_in_being",
                    Tracks = { ["screens"] = new TrackSetupDto { Subdoctrine = "support_integrated_operations", Mastery = mastery } }
                }
            }
        };
    }
}
