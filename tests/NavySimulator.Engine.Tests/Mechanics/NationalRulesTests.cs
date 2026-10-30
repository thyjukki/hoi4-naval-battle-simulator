using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Doctrines;
using NavySimulator.Engine.Nations;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-NATIONAL, S-NATIONAL-DM, S-NATIONAL-FOCUS and S-NATIONAL-HISTORY.</summary>
public class NationalRulesTests
{
    private const string Torpedo = "JAP_navy_naval_torpedo_hit_chance_factor";

    private static readonly NationalFocus MineWarfare = new("mine_warfare",
        [Effect(NationalEffectKind.AddDynamicModifier, "imperial_navy_dm"), Effect(NationalEffectKind.AddToVariable, Torpedo, 0.05)], []);

    private static readonly NationalFocus Battleships = new("battleships",
        [Effect(NationalEffectKind.AddIdea, "battlefleet")], ["carriers"]);

    private static readonly NationalFocus Carriers = new("carriers",
        [Effect(NationalEffectKind.RemoveIdea, "militarism"), Effect(NationalEffectKind.MultiplyVariable, Torpedo, 2)], ["battleships"]);

    private static readonly Nation Japan = new(
        "JAP",
        new Dictionary<string, NationalSpirit>
        {
            ["militarism"] = Spirit("militarism", StaticModifiers.NavyOrgFactor, 0.05),
            ["battlefleet"] = Spirit("battlefleet", StaticModifiers.NavyCapitalShipDefenceFactor, 0.1)
        },
        new Dictionary<string, DynamicModifier>
        {
            ["imperial_navy_dm"] = new("imperial_navy_dm", new ModifierSet([new(StaticModifiers.NavalDetection, -0.1)]),
                new Dictionary<string, string> { [StaticModifiers.NavalTorpedoHitChanceFactor] = Torpedo, [StaticModifiers.NavalHeavyGunHitChanceFactor] = "unset" })
        },
        [Effect(NationalEffectKind.AddIdea, "militarism"), Effect(NationalEffectKind.AddDynamicModifier, "imperial_navy_dm"), Effect(NationalEffectKind.SetVariable, Torpedo, 0.15)],
        [MineWarfare, Battleships, Carriers]);

    [Fact]
    public void History_AddsItsIdeasModifiersAndVariables()
    {
        var state = NationalRules.Resolve(Setup());

        Assert.Equal(["militarism"], state.Spirits);
        Assert.Equal(["imperial_navy_dm"], state.DynamicModifiers);
        Assert.Equal(0.15, state.Variables[Torpedo]);
    }

    [Fact]
    public void Focuses_ApplyInTreeOrder_AfterTheHistory()
    {
        // Listed carriers first: the tree order still adds 0.05 before doubling.
        var state = NationalRules.Resolve(Setup(focuses: [Carriers, MineWarfare]));

        Assert.Equal(0.4, state.Variables[Torpedo], 10);
        Assert.Empty(state.Spirits);
        Assert.Equal(["imperial_navy_dm"], state.DynamicModifiers);
    }

    [Fact]
    public void WithoutHistory_OnlyTheFocusesCount_AndUnsetVariablesAreZero()
    {
        var state = NationalRules.Resolve(Setup(fromHistory: false, focuses: [MineWarfare]));

        Assert.Empty(state.Spirits);
        Assert.Equal(0.05, state.Variables[Torpedo], 10);

        var effects = NationalRules.ActiveEffects(Setup(fromHistory: false, focuses: [MineWarfare]));
        var (source, dm) = Assert.Single(effects);
        Assert.Equal("imperial_navy_dm", source);
        Assert.Equal(0.05, dm.CountryModifiers.Get(StaticModifiers.NavalTorpedoHitChanceFactor), 10);
        Assert.Equal(0, dm.CountryModifiers.Get(StaticModifiers.NavalHeavyGunHitChanceFactor));
        Assert.Equal(-0.1, dm.CountryModifiers.Get(StaticModifiers.NavalDetection));
    }

    [Fact]
    public void HandEdits_AddAndRemoveSpirits_AndSetVariablesLast()
    {
        var state = NationalRules.Resolve(Setup(focuses: [MineWarfare], added: ["battlefleet", "unknown"], removed: ["militarism"],
            variables: new Dictionary<string, double> { [Torpedo] = 0.3 }));

        Assert.Equal(["battlefleet", "unknown"], state.Spirits);
        Assert.Equal(0.3, state.Variables[Torpedo]);

        // An ID the nation does not have gives nothing.
        Assert.Equal(["battlefleet", "imperial_navy_dm"], NationalRules.ActiveEffects(Setup(added: ["battlefleet", "unknown"], removed: ["militarism"]))
            .Select(effect => effect.Source));
    }

    [Fact]
    public void MutuallyExclusiveFocuses_AreReportedOnce()
    {
        Assert.Equal([("battleships", "carriers")], NationalRules.ExclusiveFocusPairs([Carriers, Battleships, MineWarfare]));
        Assert.Empty(NationalRules.ExclusiveFocusPairs([Carriers, MineWarfare]));
    }

    [Fact]
    public void Participant_SumsTheNationalEffectsIntoItsCountryModifiers()
    {
        var setup = Setup(focuses: [MineWarfare, Battleships]);
        var participant = new BattleParticipant(new Fleet("jap", [TestEntityFactory.Capital("bb")]), null, [], [], national: setup,
            defines: GameDefines.Vanilla());

        Assert.Same(setup, participant.National);
        Assert.Equal(["militarism", "battlefleet", "imperial_navy_dm"], participant.ActiveEffects.Select(effect => effect.Source));
        Assert.Equal(0.2, participant.CountryModifiers.Get(StaticModifiers.NavalTorpedoHitChanceFactor), 10);
        Assert.Equal(0.1, participant.CountryModifiers.Get(StaticModifiers.NavyCapitalShipDefenceFactor));
        Assert.Equal(0.05, participant.CountryModifiers.Get(StaticModifiers.NavyOrgFactor));
    }

    private static NationalSetup Setup(
        bool fromHistory = true,
        IReadOnlyList<NationalFocus>? focuses = null,
        IReadOnlyList<string>? added = null,
        IReadOnlyList<string>? removed = null,
        IReadOnlyDictionary<string, double>? variables = null)
    {
        return new NationalSetup(Japan, fromHistory, focuses ?? [], added ?? [], removed ?? [], variables ?? new Dictionary<string, double>());
    }

    private static NationalEffect Effect(NationalEffectKind kind, string target, double value = 0)
    {
        return new NationalEffect(kind, target, value);
    }

    private static NationalSpirit Spirit(string id, string key, double value)
    {
        return new NationalSpirit(id, "country", new CountryEffects(new ModifierSet([new(key, value)]), [], []));
    }
}
