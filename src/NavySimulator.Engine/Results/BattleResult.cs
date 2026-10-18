namespace NavySimulator.Engine.Results;

public sealed class BattleResult
{
    public required string ScenarioId { get; init; }
    public required int Seed { get; init; }
    public required BattleOutcome Outcome { get; init; }

    /// <summary>Number of hours simulated (battle hours 0..HoursFought-1).</summary>
    public required int HoursFought { get; init; }

    public required int Reengagements { get; init; }
    public required SideResult Attacker { get; init; }
    public required SideResult Defender { get; init; }
    public required IReadOnlyList<ShipResult> Ships { get; init; }
    public required IReadOnlyList<ShotRecord> Shots { get; init; }
    public required IReadOnlyList<AirStrikeRecord> AirStrikes { get; init; }
    public required IReadOnlyDictionary<string, int> SkipCounts { get; init; }
    public required IReadOnlyList<string> HourlyLog { get; init; }

    /// <summary>Warnings about inputs the engine ignored, e.g. planes while air combat is not implemented.</summary>
    public required IReadOnlyList<string> Notes { get; init; }

    /// <summary>Ship state changes (activation, retreat, escape, sinking) in the order they happened.</summary>
    public IReadOnlyList<BattleEvent> Events { get; init; } = [];

    /// <summary>One entry per battle hour with both sides' lines, positioning, losses and fire totals.</summary>
    public IReadOnlyList<HourSummary> Timeline { get; init; } = [];
}
