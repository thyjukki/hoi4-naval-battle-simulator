using System.Diagnostics;
using System.Globalization;
using System.Text;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;
using NavySimulator.GameData;

namespace NavySimulator.Web.Services;

/// <summary>
/// Runs the edited scenario many times on all cores, like the CLI's <c>--iterations</c>: run <c>i</c> (0-based) uses
/// seed <c>baseSeed + i</c>, so any run can be replayed on the Battle page.
/// </summary>
public sealed class BatchRunner(Workspace workspace)
{
    private CancellationTokenSource? cancellation;
    private int completed;

    public event Action? Changed;

    public bool IsRunning { get; private set; }
    public int Completed => completed;
    public int Total { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = [];
    public BatchResult? Last { get; private set; }

    public async Task RunAsync(int iterations, int baseSeed)
    {
        if (IsRunning || iterations <= 0)
        {
            return;
        }

        IsRunning = true;
        Errors = [];
        Total = iterations;
        completed = 0;
        Notify();

        // Ships carry battle state, so every worker needs its own scenario. Build them before the clock starts.
        var workers = Math.Clamp(Environment.ProcessorCount - 1, 1, iterations);
        var scenarioUsed = Workspace.Clone(workspace.Scenario);
        var scenarioFile = workspace.ScenarioFile;
        var builds = await Task.Run(() => Enumerable.Range(0, workers).Select(_ => workspace.BuildScenario(scenarioUsed)).ToList());

        if (builds.Any(build => build.Scenario is null))
        {
            Errors = builds.First(build => build.Scenario is null).Errors;
            IsRunning = false;
            Notify();
            return;
        }

        var scenarios = builds.Select(build => build.Scenario!).ToList();
        var results = new BattleResult?[iterations];
        using var stop = new CancellationTokenSource();
        cancellation = stop;
        var clock = Stopwatch.StartNew();

        try
        {
            var work = Task.Run(() => Parallel.For(0, workers, new ParallelOptions { MaxDegreeOfParallelism = workers }, worker =>
            {
                var engine = new BattleEngine();

                for (var run = worker; run < iterations && !stop.IsCancellationRequested; run += workers)
                {
                    results[run] = Strip(engine.Simulate(scenarios[worker], unchecked(baseSeed + run)));
                    Interlocked.Increment(ref completed);
                }
            }), CancellationToken.None);

            while (!work.IsCompleted)
            {
                await Task.WhenAny(work, Task.Delay(300));
                Notify();
            }

            await work;
        }
        catch (Exception exception)
        {
            Errors = [exception.Message];
        }
        finally
        {
            Last = new BatchResult
            {
                Scenario = scenarios[0],
                ScenarioUsed = scenarioUsed,
                ScenarioFile = scenarioFile,
                BaseSeed = baseSeed,
                Requested = iterations,
                Runs = results.OfType<BattleResult>().OrderBy(result => result.Seed).ToList(),
                Cancelled = stop.IsCancellationRequested,
                Elapsed = clock.Elapsed
            };
            cancellation = null;
            IsRunning = false;
            Notify();
        }
    }

    public void Stop()
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The batch just finished.
        }
    }

    /// <summary>Writes the CLI's aggregate summary and a runs.csv for the last batch. Returns the folder.</summary>
    public string Export(string outputDirectory)
    {
        var batch = Last ?? throw new InvalidOperationException("No batch to export.");
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(batch.Scenario.ID.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        var directory = Path.Combine(outputDirectory, $"{name}_batch_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "summary.txt"), ReportWriter.BuildAggregateSummary(batch.Scenario, batch.Runs));
        File.WriteAllText(Path.Combine(directory, "runs.csv"), RunsCsv(batch.Runs));
        return Path.GetFullPath(directory);
    }

    public static string RunsCsv(IEnumerable<BattleResult> runs)
    {
        var invariant = CultureInfo.InvariantCulture;
        var builder = new StringBuilder();
        builder.AppendLine("seed,outcome,hours,attackerSunk,attackerRetreated,attackerHpLost,attackerHpDealt,defenderSunk,defenderRetreated,defenderHpLost,defenderHpDealt,attackerIcLost,defenderIcLost");

        foreach (var run in runs)
        {
            builder.AppendLine(string.Join(',',
                run.Seed.ToString(invariant),
                run.Outcome,
                run.HoursFought.ToString(invariant),
                run.Attacker.ShipsSunk.ToString(invariant),
                run.Attacker.ShipsRetreated.ToString(invariant),
                run.Attacker.HpLost.ToString("F2", invariant),
                run.Attacker.HpDamageDealt.ToString("F2", invariant),
                run.Defender.ShipsSunk.ToString(invariant),
                run.Defender.ShipsRetreated.ToString(invariant),
                run.Defender.HpLost.ToString("F2", invariant),
                run.Defender.HpDamageDealt.ToString("F2", invariant),
                FleetCost.Of(run, run.Attacker.Label).Sunk.ToString("F2", invariant),
                FleetCost.Of(run, run.Defender.Label).Sunk.ToString("F2", invariant)));
        }

        return builder.ToString();
    }

    private static BattleResult Strip(BattleResult result)
    {
        return new BattleResult
        {
            ScenarioId = result.ScenarioId,
            Seed = result.Seed,
            Outcome = result.Outcome,
            HoursFought = result.HoursFought,
            Reengagements = result.Reengagements,
            Attacker = result.Attacker,
            Defender = result.Defender,
            Ships = result.Ships,
            Shots = [],
            AirStrikes = [],
            SkipCounts = result.SkipCounts,
            HourlyLog = [],
            Notes = result.Notes,
            Events = [],
            Timeline = result.Timeline
        };
    }

    private void Notify()
    {
        Changed?.Invoke();
    }
}
