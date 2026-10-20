using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Air;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Doctrines;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-DOCTRINE, S-DOCTRINE-MASTERY, S-DOCTRINE-EFFECT and S-SPIRIT.</summary>
public class DoctrineRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    [Fact]
    public void Rewards_UnlockInOrder_EachAfterItsOwnMastery()
    {
        // Default reward mastery 100; the second reward overrides it with 150.
        var subdoctrine = Subdoctrine("line_of_battle", [null, 150, null]);

        Assert.Equal(100, DoctrineRules.Threshold(subdoctrine, 0, defines));
        Assert.Equal(250, DoctrineRules.Threshold(subdoctrine, 1, defines));
        Assert.Equal(350, DoctrineRules.MaxMastery(subdoctrine, defines));
        Assert.Equal(0, DoctrineRules.UnlockedRewards(subdoctrine, 99.9, defines));
        Assert.Equal(1, DoctrineRules.UnlockedRewards(subdoctrine, 100, defines));
        Assert.Equal(1, DoctrineRules.UnlockedRewards(subdoctrine, 249, defines));
        Assert.Equal(3, DoctrineRules.UnlockedRewards(subdoctrine, 1000, defines));
    }

    [Fact]
    public void Rewards_UseTheDefinesDefaultMastery()
    {
        var modded = GameDefines.Vanilla();
        modded.NDoctrines.DEFAULT_REWARD_MASTERY = 50;

        Assert.Equal(2, DoctrineRules.UnlockedRewards(Subdoctrine("s", [null, null, null]), 100, modded));

        // [RE] 0x9e4da0: mastery = 0 takes the default too.
        Assert.Equal(100, DoctrineRules.Threshold(Subdoctrine("s", [0, null]), 0, defines));
    }

    [Fact]
    public void ActiveEffects_AreTheGrandDoctrine_TheSubdoctrines_TheirUnlockedRewards_AndMilestonesOfCompleteTracks()
    {
        var capitals = Subdoctrine("line_of_battle", [null, null]);
        var screens = Subdoctrine("torpedo_primacy", [null, null], track: "screens");
        var grand = Grand("new_fleet_in_being", ["screens", "capital_ships"]);
        var setup = new DoctrineSetup(grand, [new TrackSetup("capital_ships", capitals, 150), new TrackSetup("screens", screens, 200)]);

        var sources = DoctrineRules.ActiveEffects(setup, defines).Select(effect => effect.Source).ToList();

        // Tracks in the grand doctrine's order; only the complete screens track gives its milestone.
        Assert.Equal(
        [
            "new_fleet_in_being",
            "torpedo_primacy", "torpedo_primacy/reward1", "torpedo_primacy/reward2", "new_fleet_in_being/screens milestone",
            "line_of_battle", "line_of_battle/reward1"
        ], sources);
    }

    [Fact]
    public void HasDoctrine_IsTrueForTheGrandDoctrineAndSlottedSubdoctrines()
    {
        var setup = new DoctrineSetup(Grand("new_base_strike", ["screens"]), [new TrackSetup("screens", Subdoctrine("torpedo_primacy", [], "screens"), 0)]);
        var nightFighting = new Spirit("night_fighting_spirit", "naval_command_spirit", [["torpedo_primacy", "new_base_strike"]], CountryEffects.Empty);
        var decisiveBattle = new Spirit("decisive_battle_spirit", "naval_command_spirit", [["new_fleet_in_being"]], CountryEffects.Empty);

        Assert.True(DoctrineRules.HasDoctrine([setup], "new_base_strike"));
        Assert.True(DoctrineRules.HasDoctrine([setup], "torpedo_primacy"));
        Assert.False(DoctrineRules.HasDoctrine([setup], "line_of_battle"));
        Assert.True(nightFighting.IsAvailable([setup]));
        Assert.False(decisiveBattle.IsAvailable([setup]));
    }

    [Fact]
    public void Participant_AppliesSubUnitBonusesLikeTechnologies_ToTheirShipTypeOnly()
    {
        // battleship = { max_organisation = 5 hg_attack = 0.1 }: organization flat, attack x 1.1 (S-STATS-SHIP).
        var effects = Effects(bonuses: [Bonus("battleship", new ShipStats(Organization: 5, HeavyAttack: 0.1), equipment: false)]);
        var battleship = Ship("bb", "battleship", "capital_ship");
        var cruiser = Ship("ca", "heavy_cruiser", "capital_ship");

        _ = Participant([battleship, cruiser], Setup(Grand("new_fleet_in_being", [], effects)));

        Assert.Equal(40 + 5, battleship.GetFinalStats().Organization, 9);
        Assert.Equal(40 * 1.1, battleship.GetFinalStats().HeavyAttack, 9);
        Assert.Equal(40, cruiser.GetFinalStats().Organization, 9);
        Assert.Equal(40, cruiser.GetFinalStats().HeavyAttack, 9);
    }

    [Fact]
    public void Participant_AppliesEquipmentBonusesToTheDesign_MatchingItsEquipmentTypes()
    {
        // equipment_bonus = { capital_ship = { armor_value = 0.1 } } reaches a design whose hull type is capital_ship.
        var effects = Effects(bonuses: [Bonus("capital_ship", new ShipStats(Armor: 0.1), equipment: true)]);
        var battleship = Ship("bb", "battleship", "capital_ship");
        var destroyer = Ship("dd", "destroyer", "screen_ship");

        _ = Participant([battleship, destroyer], Setup(Grand("new_fleet_in_being", [], effects)));

        Assert.Equal(50 * 1.1, battleship.GetFinalStats().Armor, 9);
        Assert.Equal(50, destroyer.GetFinalStats().Armor, 9);
    }

    [Fact]
    public void Participant_SumsCountryModifiersOfResearchesDoctrinesAndSpirits()
    {
        var research = new Research("smoke_generator", new ShipStats(), new ShipStats(), new ShipStats(), [], [], false,
            new ModifierSet([new("naval_retreat_speed", 0.2)]));
        var doctrine = Effects(new Dictionary<string, double> { ["naval_retreat_speed"] = 0.1, ["carrier_night_traffic"] = 0.15 });
        var spirit = new Spirit("surface_raiders_spirit", "naval_command_spirit", [],
            Effects(new Dictionary<string, double> { ["naval_retreat_speed"] = 0.25 }));
        var ship = Ship("dd", "destroyer", "screen_ship");

        var participant = new BattleParticipant(new Fleet("f", [ship]), null, [research], [spirit], carrierNightTraffic: 0.1,
            doctrines: [Setup(Grand("new_convoy_raiding", [], doctrine))]);

        Assert.Equal(0.55, participant.CountryModifiers.Get("naval_retreat_speed"), 9);
        Assert.Equal(0.55, ship.CountryModifiers.Get("naval_retreat_speed"), 9);
        Assert.Equal(0.25, participant.CarrierNightTraffic, 9);
    }

    [Fact]
    public void Participant_SimulatesAirDoctrines_AndListsWhatItIgnores()
    {
        var naval = Effects(new Dictionary<string, double> { ["naval_detection"] = 0.05, ["naval_retreat_speed"] = 0.1 }, notSimulated: ["effect"]);
        var bonus = new PlaneBonus("new_battlefield_support (category_carrier_nav_bomber)", "category_carrier_nav_bomber",
            new ModifierSet([new("naval_strike_attack", 0.1)]));
        var air = new GrandDoctrine("new_battlefield_support", "air", [],
            new CountryEffects(new ModifierSet([new("naval_retreat_speed", 1.0)]), [], [], [bonus]), new Dictionary<string, CountryEffects>());

        var participant = Participant([Ship("dd", "destroyer", "screen_ship")], Setup(Grand("new_fleet_in_being", [], naval)), Setup(air));
        var carrierBomber = new PlaneEquipment("cv_nav_bomber_equipment_1", new PlaneStats(NavalAttack: 20), subUnit: "cv_nav_bomber",
            subUnitCategories: ["category_nav_bomber", "category_carrier_nav_bomber"]);
        var landBomber = new PlaneEquipment("nav_bomber_equipment_1", new PlaneStats(NavalAttack: 20), subUnit: "nav_bomber",
            subUnitCategories: ["category_nav_bomber"]);

        Assert.Equal(1.1, participant.CountryModifiers.Get("naval_retreat_speed"), 9);
        Assert.Equal(22, participant.PlaneStatsOf(carrierBomber).NavalAttack, 9);
        Assert.Equal(20, participant.PlaneStatsOf(landBomber).NavalAttack, 9);
        Assert.Equal(["effect", "naval_detection"], participant.IgnoredEffects());
    }

    [Fact]
    public void StrikeAverages_TakeTheCountrysNavalStrikeFactors_AndCarrierFactorsForCarrierPlanes()
    {
        var plane = new PlaneEquipment("bomber", new PlaneStats(NavalAttack: 20, NavalTargeting: 10, Agility: 50));
        var wings = new List<AirWing> { new(plane, 10, defines.Hardcoded) };
        var country = new ModifierSet([
            new("naval_strike_attack_factor", 0.1), new("navy_carrier_air_attack_factor", 0.2),
            new("naval_strike_targetting_factor", 0.05), new("navy_carrier_air_targetting_factor", 0.1),
            new("navy_carrier_air_agility_factor", 0.5)
        ]);

        var carrier = AirRules.StrikeAverages(wings, defines, country, fromCarrier: true);
        var landBased = AirRules.StrikeAverages(wings, defines, country, fromCarrier: false);

        Assert.Equal(2 * 1.1 * 1.2, carrier.Attack, 9);
        Assert.Equal(1 * 1.05 * 1.1, carrier.Targeting, 9);
        Assert.Equal(defines.NAir.AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY * 50 * 1.5, carrier.Agility, 9);
        Assert.Equal(2 * 1.1, landBased.Attack, 9);
        Assert.Equal(1 * 1.05, landBased.Targeting, 9);
    }

    [Fact]
    public void PositioningModifier_JoinsTheSumBeforeTheClamp()
    {
        var own = new BattleLines([TestEntityFactory.Screen("a")]);
        var enemy = new BattleLines([TestEntityFactory.Screen("b")]);

        Assert.Equal(1, PositioningRules.Calculate(own, enemy, defines, positioningModifier: 0.1), 9);
        Assert.Equal(0.9, PositioningRules.Calculate(own, enemy, defines, positioningModifier: -0.1), 9);
    }

    [Fact]
    public void ScreeningEfficiency_RaisesTheFactor_AndScreeningWithoutScreensStandsInForMissingScreens()
    {
        // One capital needs 3 screens; 2 screens at positioning 1 give 2/3, with screening_efficiency 0.1 x 1.1.
        var screened = new BattleLines([TestEntityFactory.Capital("bb"), TestEntityFactory.Screen("dd1"), TestEntityFactory.Screen("dd2")]);
        var unscreened = new BattleLines([TestEntityFactory.Capital("bb")]);

        Assert.Equal(2.0 / 3 * 1.1, ScreeningRules.Calculate(screened, 1, defines, screeningEfficiency: 0.1).ScreeningEfficiency, 9);
        Assert.Equal(0.05, ScreeningRules.Calculate(unscreened, 1, defines, screeningWithoutScreens: 0.05).ScreeningEfficiency, 9);
        Assert.Equal(2.0 / 3, ScreeningRules.Calculate(screened, 1, defines, screeningWithoutScreens: 0.05).ScreeningEfficiency, 9);
    }

    [Fact]
    public void TorpedoScreenPenetration_ScalesTheUnscreenedShare()
    {
        // Screening 0.6 with penetration 0.5: 1 - 0.4 x 1.5 = 0.4, so a roll of 0.5 passes the screens.
        var enemy = new EnemyLineCounts(1, 1, 0, 0, 0);
        var screening = new ScreeningSummary(0.6, 1);

        Assert.Equal(1, TargetingRules.MaxLine(WeaponSlot.Torpedo, enemy, screening, new ScriptedRandomSource(0.5)));
        Assert.Equal(2, TargetingRules.MaxLine(WeaponSlot.Torpedo, enemy, screening, new ScriptedRandomSource(0.5), screenPenetration: 0.5));
    }

    [Fact]
    public void TorpedoRevealChanceFactor_ScalesVisibilityAfterFiring()
    {
        var submarine = TestEntityFactory.Submarine("ss");
        submarine.CountryModifiers = new ModifierSet([new("naval_torpedo_reveal_chance_factor", -0.1)]);

        // Visibility 10 x 0.9 after firing; the hourly roll does not take it.
        Assert.Equal(20 * 0.065 * 1.1 / (100 - 9), SubmarineRules.RevealChance(submarine, 20, 1.0, true, defines), 9);
        Assert.Equal(20 * 0.065 / (100 - 10), SubmarineRules.RevealChance(submarine, 20, 1.0, false, defines), 9);
    }

    private static BattleParticipant Participant(List<Ship> ships, params DoctrineSetup[] doctrines)
    {
        return new BattleParticipant(new Fleet("f", ships), null, [], [], doctrines: doctrines);
    }

    private static DoctrineSetup Setup(GrandDoctrine grand)
    {
        return new DoctrineSetup(grand, []);
    }

    private static GrandDoctrine Grand(string id, List<string> tracks, CountryEffects? effects = null)
    {
        return new GrandDoctrine(id, BattleParticipant.NavalFolder, tracks, effects ?? CountryEffects.Empty,
            tracks.ToDictionary(track => track, _ => CountryEffects.Empty));
    }

    private static Subdoctrine Subdoctrine(string id, List<double?> rewardMastery, string track = "capital_ships")
    {
        var rewards = rewardMastery.Select((mastery, index) => new DoctrineReward($"reward{index + 1}", mastery, CountryEffects.Empty)).ToList();
        return new Subdoctrine(id, BattleParticipant.NavalFolder, [track], false, [], CountryEffects.Empty, rewards);
    }

    private static CountryEffects Effects(Dictionary<string, double>? modifiers = null, List<Research>? bonuses = null, List<string>? notSimulated = null)
    {
        return new CountryEffects(new ModifierSet(modifiers ?? []), bonuses ?? [], notSimulated ?? []);
    }

    private static Research Bonus(string target, ShipStats values, bool equipment)
    {
        return new Research($"bonus ({target})", values, new ShipStats(), new ShipStats(), [], [target], equipment);
    }

    private static Ship Ship(string id, string shipTypeId, string equipmentType)
    {
        var role = equipmentType == "capital_ship" ? ShipRole.Capital : ShipRole.Screen;
        var hull = new Hull($"{id}_hull", $"{id}_archetype", equipmentType, 400, TestEntityFactory.CapitalStats() with { Organization = 0 });
        var shipType = new ShipType(shipTypeId, role, [equipmentType], 0, [hull.Archetype], [], 40);
        return new Ship(id, new ShipDesign($"{id}_design", hull, shipType, []));
    }
}
