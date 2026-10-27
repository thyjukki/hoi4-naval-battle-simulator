using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>
/// Runs of one batch, in seed order. Each result keeps its ships, timeline and notes; per-shot records are dropped
/// to save memory (re-run a seed on the Battle page to see them).
/// </summary>
public sealed class BatchResult
{
    public required BattleScenario Scenario { get; init; }
    public required BattleScenarioDto ScenarioUsed { get; init; }
    public required string? ScenarioFile { get; init; }
    public required int BaseSeed { get; init; }
    public required int Requested { get; init; }
    public required IReadOnlyList<BattleResult> Runs { get; init; }
    public required bool Cancelled { get; init; }
    public required TimeSpan Elapsed { get; init; }

    /// <summary>The override set the runs used, or null.</summary>
    public OverrideSetDto? Overrides { get; init; }

    /// <summary>The same seeds without the override set, when the batch compared them; null otherwise.</summary>
    public BatchResult? Baseline { get; init; }
}
