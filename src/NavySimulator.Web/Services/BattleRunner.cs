using System.Diagnostics;
using NavySimulator.Engine.Battles;
using NavySimulator.GameData;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>
/// The battle shown on the Battle page: one <see cref="BattleSession"/> stepped on a background thread. Every read of
/// the session goes through <see cref="View"/>, which copies it under the lock the stepping thread holds per hour.
/// </summary>
public sealed class BattleRunner(Workspace workspace)
{
    private readonly object sync = new();
    private CancellationTokenSource? cancellation;
    private BattleView? cachedView;
    private (int Version, int? Hour) cachedKey = (-1, null);

    /// <summary>Raised when the battle advanced, started or stopped. Handlers may run on any thread.</summary>
    public event Action? Changed;

    public bool HasBattle { get; private set; }
    public bool IsRunning { get; private set; }

    /// <summary>Increases with every simulated hour and every new battle.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<string> StartErrors { get; private set; } = [];
    public IReadOnlyList<string> StartWarnings { get; private set; } = [];
    public string? RunError { get; private set; }

    /// <summary>The scenario as it was when the battle started, and the catalog version it was built from.</summary>
    public BattleScenarioDto? ScenarioUsed { get; private set; }

    public string? ScenarioFileUsed { get; private set; }
    public int CatalogVersionUsed { get; private set; }
    public IReadOnlyList<string> DefineSourcesUsed { get; private set; } = [];

    /// <summary>The override set the battle was built with (as JSON), or null.</summary>
    public string? OverridesUsed { get; private set; }

    private BattleSession? Session { get; set; }

    /// <summary>True when the scenario or the content was edited after the battle started.</summary>
    public bool IsOutdated => HasBattle &&
                              (CatalogVersionUsed != workspace.CatalogVersion ||
                               OverridesUsed != (workspace.ActiveOverrides is { } active ? Workspace.Json(active.Set) : null) ||
                               Workspace.Json(ScenarioUsed) != Workspace.Json(workspace.Scenario));

    /// <summary>
    /// Builds the edited scenario (or <paramref name="scenario"/>, e.g. a batch's) and starts a new battle with
    /// <paramref name="seed"/>. Returns false on setup errors.
    /// </summary>
    public bool Start(int seed, BattleScenarioDto? scenario = null)
    {
        if (IsRunning)
        {
            return false;
        }

        var overrides = workspace.OverridesSnapshot();
        var build = workspace.BuildScenario(scenario, overrides: overrides);
        StartErrors = build.Errors;
        StartWarnings = build.Warnings;
        RunError = null;

        if (build.Scenario is null)
        {
            Notify();
            return false;
        }

        lock (sync)
        {
            Session = new BattleSession(build.Scenario, seed, recordShipHistory: true);
            HasBattle = true;
            ScenarioUsed = Workspace.Clone(scenario ?? workspace.Scenario);
            ScenarioFileUsed = workspace.ScenarioFile;
            CatalogVersionUsed = workspace.CatalogVersion;
            DefineSourcesUsed = build.Scenario.Defines.Sources.ToList();
            OverridesUsed = overrides is null ? null : Workspace.Json(overrides);
            Version++;
        }

        Notify();
        return true;
    }

    public Task StepAsync(int hours)
    {
        return RunAsync(Math.Max(1, hours));
    }

    public Task RunToEndAsync()
    {
        return RunAsync(int.MaxValue);
    }

    public void Stop()
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run just finished.
        }
    }

    /// <summary>A copy of the battle at <paramref name="atHour"/> (null: the latest hour), or null before the first battle.</summary>
    public BattleView? View(int? atHour = null)
    {
        lock (sync)
        {
            if (Session is null)
            {
                return null;
            }

            if (cachedView is null || cachedKey != (Version, atHour))
            {
                cachedView = BattleViewBuilder.Build(Session, atHour);
                cachedKey = (Version, atHour);
            }

            return cachedView;
        }
    }

    /// <summary>The ship's state at the end of every hour fought so far (index into the session's ship list).</summary>
    public IReadOnlyList<Engine.Results.ShipHourState> ShipHistory(int shipIndex)
    {
        lock (sync)
        {
            return Session is null
                ? []
                : Enumerable.Range(0, Session.HoursFought).Select(hour => Session.ShipStateAt(hour, shipIndex)).ToList();
        }
    }

    /// <summary>Writes the CLI's report files for the battle so far into <paramref name="directory"/>.</summary>
    public string ExportReport(string directory)
    {
        lock (sync)
        {
            if (Session is null)
            {
                throw new InvalidOperationException("No battle to export.");
            }

            var result = Session.BuildResult();
            var path = Path.Combine(directory, $"{Sanitize(result.ScenarioId)}_seed{result.Seed}_{DateTime.Now:yyyyMMdd_HHmmss}");
            ReportWriter.WriteRun(path, result);
            return Path.GetFullPath(path);
        }
    }

    public Engine.Results.BattleResult? Result()
    {
        lock (sync)
        {
            return Session?.BuildResult();
        }
    }

    private async Task RunAsync(int hours)
    {
        var session = Session;

        if (IsRunning || session is null || session.IsOver)
        {
            return;
        }

        IsRunning = true;
        using var stop = new CancellationTokenSource();
        cancellation = stop;
        Notify();

        try
        {
            await Task.Run(() =>
            {
                var sinceNotify = Stopwatch.StartNew();

                for (var i = 0; i < hours && !stop.IsCancellationRequested; i++)
                {
                    lock (sync)
                    {
                        if (!session.StepHour())
                        {
                            break;
                        }

                        Version++;
                    }

                    if (sinceNotify.ElapsedMilliseconds > 400)
                    {
                        Notify();
                        sinceNotify.Restart();
                    }
                }
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            RunError = exception.Message;
        }
        finally
        {
            cancellation = null;
            IsRunning = false;
            Notify();
        }
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

    private void Notify()
    {
        Changed?.Invoke();
    }
}
