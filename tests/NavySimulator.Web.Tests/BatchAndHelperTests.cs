using NavySimulator.Engine.Battles;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public sealed class BatchAndHelperTests : IDisposable
{
    private readonly TestWorkspace test = new();

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public async Task Batch_RunsEverySeedLikeTheEngine()
    {
        var batch = new BatchRunner(test.Workspace);

        await batch.RunAsync(4, 100);

        var last = batch.Last!;
        Assert.Empty(batch.Errors);
        Assert.False(last.Cancelled);
        Assert.Equal([100, 101, 102, 103], last.Runs.Select(run => run.Seed));

        foreach (var run in last.Runs)
        {
            var expected = new BattleEngine().Simulate(test.Workspace.BuildScenario().Scenario!, run.Seed);
            Assert.Equal(expected.Outcome, run.Outcome);
            Assert.Equal(expected.HoursFought, run.HoursFought);
            Assert.Equal(expected.Ships, run.Ships);
            Assert.Empty(run.Shots);
            Assert.Equal(run.HoursFought, run.Timeline.Count);
        }

        Assert.StartsWith("seed,outcome,hours", BatchRunner.RunsCsv(last.Runs));
    }

    [Fact]
    public async Task Batch_WithAnInvalidScenario_ReportsErrors()
    {
        test.Workspace.Scenario.Attacker.FleetID = "nope";
        var batch = new BatchRunner(test.Workspace);

        await batch.RunAsync(2, 1);

        Assert.NotEmpty(batch.Errors);
        Assert.Null(batch.Last);
        Assert.False(batch.IsRunning);
    }

    [Fact]
    public void Distribution_InterpolatesPercentiles()
    {
        var distribution = Distribution.Of([4, 1, 3, 2, 5]);

        Assert.Equal(3, distribution.Mean);
        Assert.Equal(3, distribution.Median);
        Assert.Equal(1, distribution.Min);
        Assert.Equal(5, distribution.Max);
        Assert.Equal(1.4, distribution.P10, 9);
        Assert.Equal(4.6, distribution.P90, 9);
        Assert.Equal(0, Distribution.Of([]).Count);
    }

    [Fact]
    public void WebOptions_ParseTheCommandLine()
    {
        var options = WebOptions.Parse(["--data", "somewhere", "--defines", "a.lua", "--defines", "mod", "--port", "6000", "--no-browser"]);

        Assert.Equal(Path.GetFullPath("somewhere"), options.DataDirectory);
        Assert.Equal(["a.lua", "mod"], options.DefineSources);
        Assert.Equal(6000, options.Port);
        Assert.False(options.OpenBrowser);
        Assert.Throws<ArgumentException>(() => WebOptions.Parse(["--bogus"]));
        Assert.Throws<ArgumentException>(() => WebOptions.Parse(["--port", "0"]));
    }

    [Fact]
    public void DataDirectoryLocator_RecognisesADataDirectory()
    {
        Assert.True(DataDirectoryLocator.IsDataDirectory(test.DataDirectory));
        Assert.False(DataDirectoryLocator.IsDataDirectory(test.Root));
    }

    [Fact]
    public void StatSummaries_ShowPercentStatsAsPercentages()
    {
        var module = test.Workspace.Catalog.Modules.First(candidate => candidate.StatModifiers.LightHitChanceFactor != 0);

        var summary = StatInfo.Summarize(module.StatModifiers, module.StatAverages, module.StatMultipliers);

        Assert.Contains($"Light hit chance +{module.StatModifiers.LightHitChanceFactor * 100:0.##}%", summary);
    }

    [Fact]
    public void ResearchSummaries_ShowCountryModifiersWithTheGamesNames()
    {
        var smokeGenerator = test.Workspace.Catalog.Researches.First(research => research.ID == "smoke_generator");

        Assert.Equal("Fleet speed while retreating +20%", ModifierInfo.SummarizeResearch(smokeGenerator));
        Assert.Equal("Naval Hit Chance +5%", ModifierInfo.Summarize(new Dictionary<string, double> { ["naval_hit_chance"] = 0.05 }));
        Assert.Equal("naval_mines_damage_factor +5% (not simulated)", ModifierInfo.Summarize(new Dictionary<string, double> { ["naval_mines_damage_factor"] = 0.05 }));
    }

    [Fact]
    public void GameLabels_UseTheGamesNames()
    {
        var aggressiveness = Enum.GetValues<Aggressiveness>().Select(value => GameLabels.Of(value)).ToList();
        var repairPriority = Enum.GetValues<RepairPriority>().Select(value => GameLabels.Of(value)).ToList();

        Assert.Equal(["Do Not Engage", "Engage At Low Risk", "Engage At Medium Risk", "Engage At High Risk", "Always Engage"], aggressiveness);
        Assert.Equal(["Never repair", "Repair priority - Low", "Repair priority - Medium", "Repair priority - High"], repairPriority);
    }
}
