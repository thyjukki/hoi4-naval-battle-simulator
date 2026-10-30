using NavySimulator.GameData.Contracts;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

/// <summary>S-NATIONAL in the GUI: summaries, hand edits of national spirits, and the variables of Japan's imported dynamic modifiers (TFB data).</summary>
public sealed class NationalInfoTests : IDisposable
{
    private const string Torpedo = "JAP_navy_naval_torpedo_hit_chance_factor";

    private readonly TestWorkspace test = new();

    private Workspace Workspace => test.Workspace;

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void Describe_NamesSpiritsAndTheModifiersAVariableFeeds()
    {
        var japan = Workspace.FindNation("JAP")!;
        var focus = japan.Focuses.Single(item => item.ID == "JAP_establish_the_mine_warfare_force");

        // The history already adds the Imperial Navy modifier, so the summary leaves it out.
        Assert.Equal($"Torpedo hit chance ({Torpedo}) +0.05", NationalInfo.Summarize(focus, japan));
        Assert.Equal("adds Supremacy of the Battlefleet for 30 days (conditional)",
            NationalInfo.Describe(new NationalEffectDto { Effect = "add_ideas", Target = "JAP_supremacy_of_the_battlefleet", Days = 30, Condition = "x" }, japan));
        Assert.Equal("someone_else × 2", NationalInfo.Describe(new NationalEffectDto { Effect = "multiply_variable", Target = "someone_else", Value = 2 }, japan));
    }

    [Fact]
    public void Spirits_TellTheirSourcesAndTargetedModifiers()
    {
        var usa = Workspace.FindNation("USA")!;
        var infamy = usa.Spirits.Single(spirit => spirit.ID == "JAP_day_of_infamy_1");

        Assert.Equal("targets JAP", NationalInfo.Sources(infamy, usa));
        Assert.EndsWith("navy_capital_ship_attack_factor_against -10% against JAP (no effect at sea)", NationalInfo.Summarize(infamy));
    }

    [Fact]
    public void SetSpirit_RecordsOnlyHandEditsThatChangeSomething()
    {
        var national = new NationalSetupDto();

        NationalInfo.SetSpirit(national, "a", active: true, activeWithoutHandEdits: false);
        NationalInfo.SetSpirit(national, "b", active: false, activeWithoutHandEdits: true);
        Assert.Equal(["a"], national.AddedSpirits);
        Assert.Equal(["b"], national.RemovedSpirits);

        NationalInfo.SetSpirit(national, "a", active: false, activeWithoutHandEdits: false);
        NationalInfo.SetSpirit(national, "b", active: true, activeWithoutHandEdits: true);
        Assert.Empty(national.AddedSpirits);
        Assert.Empty(national.RemovedSpirits);
    }

    [Fact]
    public void Variables_FollowHistoryFocusesAndHandValues()
    {
        var side = new BattleParticipantDto { Country = "JAP", National = new NationalSetupDto { Focuses = ["JAP_establish_the_mine_warfare_force"] } };
        var japan = Workspace.FindNation("JAP")!;
        var state = NationalInfo.State(Workspace.NationalSetup(side));
        var torpedo = NationalInfo.Variables(japan, side.National, state).Single(variable => variable.Name == Torpedo);

        Assert.Equal(0.2, torpedo.Value, 10);
        Assert.False(torpedo.SetByHand);
        Assert.Equal(["naval_torpedo_hit_chance_factor"], torpedo.Modifiers);
        Assert.StartsWith("JAP: 1936 history, 1 focus, ", NationalInfo.Summary(side, state));

        side.National.Variables[Torpedo] = 0.5;
        state = NationalInfo.State(Workspace.NationalSetup(side));
        Assert.Equal(0.5, NationalInfo.Variables(japan, side.National, state).Single(variable => variable.Name == Torpedo).Value);
        Assert.Equal(0.5, Workspace.SideEffects(side).CountryModifiers.Get("naval_torpedo_hit_chance_factor"), 10);
        Assert.Equal("none", NationalInfo.Summary(new BattleParticipantDto(), state));
    }
}
