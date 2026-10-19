using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Mios;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-MIO, S-MIO-MATCH, S-MIO-TREE, S-MIO-POLICY and S-STATS-MIO.</summary>
public class MioRulesTests
{
    private static readonly Dictionary<string, IReadOnlySet<string>> Groups = new()
    {
        ["mio_cat_eq_all_battleship"] = new HashSet<string>(["ship_hull_heavy", "ship_hull_super_heavy"]),
        ["mio_cat_eq_all_cruiser"] = new HashSet<string>(["ship_hull_cruiser"])
    };

    private static readonly MioTrait Belt = Trait("belt", limit: ["mio_cat_eq_all_battleship"], bonus: new ShipStats(Armor: 0.15));
    private static readonly MioTrait Primary = Trait("primary", limit: ["capital_ship"], bonus: new ShipStats(HeavyAttack: 0.05));
    private static readonly MioTrait Secondary = Trait("secondary", bonus: new ShipStats(LightAttack: 0.05, LightHitChanceFactor: 0.05));

    private static readonly Mio BattleLine = new(
        "battle_line",
        ["mio_cat_eq_all_battleship", "mio_cat_eq_all_cruiser"],
        Trait(string.Empty, bonus: new ShipStats(Hp: 0.05)),
        [Belt, Primary, Secondary]);

    [Fact]
    public void Designs_MatchTheirHull_ItsDefaultModulesTypes_AndGroups_NotTheirInstalledModulesOrShipType()
    {
        var battleship = Design("ship_hull_heavy", "capital_ship");
        var heavyCruiser = Design("ship_hull_cruiser", "screen_ship", moduleType: "capital_ship");
        var torpedoCruiser = Design("ship_hull_cruiser", "screen_ship", defaultModuleType: "capital_ship");

        Assert.Contains("mio_cat_eq_all_battleship", battleship.EquipmentBonusTypes);
        Assert.DoesNotContain("mio_cat_eq_all_cruiser", battleship.EquipmentBonusTypes);
        Assert.True(MioRules.CanAssign(BattleLine, battleship));
        Assert.False(MioRules.CanAssign(BattleLine, Design("ship_hull_light", "screen_ship")));

        // [RE] An installed medium battery does not make a cruiser hull a capital ship; the sub-unit name matches nothing.
        Assert.Equal(["ship_hull_cruiser_1", "ship_hull_cruiser", "screen_ship", "mio_cat_eq_all_cruiser"], heavyCruiser.EquipmentBonusTypes);
        Assert.False(MioRules.Matches(["ship_hull_cruiser_type"], heavyCruiser.EquipmentBonusTypes));

        // A capital_ship default module makes the hull a capital ship, and capital_ship drops screen_ship.
        Assert.Equal(["ship_hull_cruiser_1", "ship_hull_cruiser", "capital_ship", "mio_cat_eq_all_cruiser"], torpedoCruiser.EquipmentBonusTypes);
        Assert.False(MioRules.Matches(["Capital_Ship"], torpedoCruiser.EquipmentBonusTypes));
    }

    [Fact]
    public void EquipmentBonus_SumsTheInitialTraitAndTheUnlockedTraitsThatReachTheDesign()
    {
        var setup = new MioSetup(BattleLine, [Belt, Primary, Secondary], null);
        var battleship = Design("ship_hull_heavy", "capital_ship", BattleLine);
        var lightCruiser = Design("ship_hull_cruiser", "screen_ship", BattleLine);

        Assert.Equal(new ShipStats(Hp: 0.05, Armor: 0.15, HeavyAttack: 0.05, LightAttack: 0.05, LightHitChanceFactor: 0.05),
            MioRules.EquipmentBonus(battleship, BattleLine, setup));

        // The belt is limited to battleships and the primary battery layout to capital ships.
        Assert.Equal(new ShipStats(Hp: 0.05, LightAttack: 0.05, LightHitChanceFactor: 0.05), MioRules.EquipmentBonus(lightCruiser, BattleLine, setup));
    }

    [Fact]
    public void EquipmentBonus_WithoutASetup_IsTheInitialTrait_AndAnotherMiosSetupGivesNothing()
    {
        var battleship = Design("ship_hull_heavy", "capital_ship", BattleLine);
        var other = new MioSetup(new Mio("other", ["capital_ship"], null, [Belt]), [Belt], null);

        Assert.Equal(new ShipStats(Hp: 0.05), MioRules.EquipmentBonus(battleship, BattleLine, null));
        Assert.Equal(new ShipStats(), MioRules.EquipmentBonus(battleship, BattleLine, other));
        Assert.Equal(new ShipStats(), MioRules.EquipmentBonus(Design("ship_hull_heavy", "capital_ship"), null, null));
    }

    [Fact]
    public void Policy_SameAsMioReachesEveryDesign_AndTypedBlocksOnlyTheirEquipment()
    {
        var policy = new MioPolicy("space_efficient", ["capital_ship"],
            [(MioPolicy.SameAsMio, new ShipStats(NavalRange: 0.25)), ("mio_cat_eq_all_cruiser", new ShipStats(CarrierSize: 1))], []);
        var setup = new MioSetup(BattleLine, [], policy);

        Assert.Equal(new ShipStats(Hp: 0.05, NavalRange: 0.25), MioRules.EquipmentBonus(Design("ship_hull_heavy", "capital_ship"), BattleLine, setup));
        Assert.Equal(new ShipStats(Hp: 0.05, NavalRange: 0.25, CarrierSize: 1),
            MioRules.EquipmentBonus(Design("ship_hull_cruiser", "screen_ship"), BattleLine, setup));
    }

    [Fact]
    public void Participant_GivesEachShipItsSidesMioBonus_HitChanceFactorsAddedAndTheRestMultiplied()
    {
        // S-STATS-MIO [RE]: (base + hit chance factors) × (1 + Σ other stats).
        var setup = new MioSetup(BattleLine, [Belt, Secondary], null);
        var withMio = new Ship("bb", Design("ship_hull_heavy", "capital_ship", BattleLine));
        var without = new Ship("bb2", Design("ship_hull_heavy", "capital_ship", BattleLine));

        _ = new BattleParticipant(new Fleet("a", [withMio]), null, [], [], mios: [setup]);
        _ = new BattleParticipant(new Fleet("b", [without]), null, [], []);

        var stats = TestEntityFactory.CapitalStats();
        Assert.Equal(stats.Armor * 1.15, withMio.GetFinalStats().Armor, 9);
        Assert.Equal(stats.LightAttack * 1.05, withMio.GetFinalStats().LightAttack, 9);
        Assert.Equal(0.05, withMio.GetFinalStats().LightHitChanceFactor, 9);
        Assert.Equal(stats.Hp * 1.05, withMio.GetFinalStats().Hp, 9);
        Assert.Equal(stats.Armor, without.GetFinalStats().Armor, 9);
        Assert.Equal(stats.Hp * 1.05, without.GetFinalStats().Hp, 9);
    }

    [Fact]
    public void Participant_AssignsMiosByDesign_InPlaceOfTheDesignsOwn()
    {
        // S-MIO: a side can give a design without an MIO one, and take the designer's MIO away from another.
        var bare = new Ship("bb", Design("ship_hull_heavy", "capital_ship"));
        var assigned = new Ship("bb2", Design("ship_hull_heavy", "capital_ship", BattleLine));
        var participant = new BattleParticipant(new Fleet("a", [bare, assigned]), null, [], [],
            mios: [new MioSetup(BattleLine, [Belt], null)],
            designMios: new Dictionary<string, Mio?> { [bare.Design.ID] = BattleLine, ["other"] = null });

        Assert.Same(BattleLine, participant.MioOf(bare.Design));
        Assert.Equal(TestEntityFactory.CapitalStats().Armor * 1.15, bare.GetFinalStats().Armor, 9);

        var removed = new Ship("bb3", Design("ship_hull_heavy", "capital_ship", BattleLine));
        _ = new BattleParticipant(new Fleet("b", [removed]), null, [], [], designMios: new Dictionary<string, Mio?> { [removed.Design.ID] = null });
        Assert.Equal(TestEntityFactory.CapitalStats().Hp, removed.GetFinalStats().Hp, 9);
    }

    [Fact]
    public void TreeProblems_AreMissingParentsAndMutuallyExclusivePairs()
    {
        var external = Trait("external", exclusive: ["internal"]);
        var internalBelt = Trait("internal", exclusive: ["external"]);
        var hardened = Trait("hardened", parents: [new MioTraitParent(["external", "internal"], 1)]);
        var center = Trait("center", parents: [new MioTraitParent(["hardened", "secondary"], 2)]);
        var mio = new Mio("m", ["capital_ship"], null, [external, internalBelt, hardened, Secondary, center]);

        Assert.Empty(MioRules.TreeProblems(new MioSetup(mio, [internalBelt, hardened], null)));
        Assert.Equal(["trait 'hardened' needs one of external, internal unlocked first"], MioRules.TreeProblems(new MioSetup(mio, [hardened], null)));
        Assert.Equal(
        [
            "trait 'center' needs all of hardened, secondary unlocked first",
            "traits 'external' and 'internal' are mutually exclusive"
        ], MioRules.TreeProblems(new MioSetup(mio, [external, internalBelt, center], null)));
    }

    [Fact]
    public void TreeProblems_AllowOneSidedExclusions_AndFlagGroupsThatCanNeverHold()
    {
        // [RE] 0x13af360 checks only the candidate's own list: unlock "lists" first, then "listed".
        var lists = Trait("lists", exclusive: ["listed"]);
        var listed = Trait("listed");
        var orphan = Trait("orphan", parents: [new MioTraitParent([], 1)]);
        var mio = new Mio("m", ["capital_ship"], null, [lists, listed, orphan]);

        Assert.Empty(MioRules.TreeProblems(new MioSetup(mio, [lists, listed], null)));
        Assert.Equal(["trait 'orphan' can never be unlocked (a parent group needs 1 of 0 traits)"], MioRules.TreeProblems(new MioSetup(mio, [orphan], null)));
    }

    [Fact]
    public void Policies_NeedOneOfTheirEquipmentTypesInTheMio()
    {
        Assert.True(MioRules.PolicyAllowed(BattleLine, new MioPolicy("p", ["mio_cat_eq_all_cruiser", "carrier"], [], [])));
        Assert.False(MioRules.PolicyAllowed(BattleLine, new MioPolicy("p", ["ship_hull_submarine"], [], [])));
        Assert.True(MioRules.PolicyAllowed(BattleLine, new MioPolicy("p", [], [], [])));
    }

    private static MioTrait Trait(
        string id,
        IReadOnlyList<string>? limit = null,
        ShipStats? bonus = null,
        IReadOnlyList<MioTraitParent>? parents = null,
        IReadOnlyList<string>? exclusive = null)
    {
        return new MioTrait(id, parents ?? [], exclusive ?? [], limit ?? [], bonus ?? new ShipStats(), []);
    }

    private static ShipDesign Design(string archetype, string equipmentType, Mio? mio = null, string? moduleType = null, string? defaultModuleType = null)
    {
        var role = equipmentType == "capital_ship" ? ShipRole.Capital : ShipRole.Screen;
        var hull = new Hull($"{archetype}_1", archetype, equipmentType, 400, TestEntityFactory.CapitalStats() with { Organization = 0 },
            defaultModuleEquipmentTypes: defaultModuleType is null ? [] : [defaultModuleType]);
        var shipType = new ShipType($"{archetype}_type", role, [equipmentType], 0, [archetype], [], 40);
        var modules = moduleType is null ? new List<StatModule>() : [new StatModule("battery", new ShipStats(), new ShipStats(), addedEquipmentTypes: [moduleType])];
        return new ShipDesign($"{archetype}_design", hull, shipType, modules, mio, Groups);
    }
}
