using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;

namespace NavySimulator.Web.Services;

/// <summary>
/// An immutable copy of a <see cref="BattleSession"/> for rendering, as it was at the end of <see cref="Hour"/>
/// (or before the first hour when <see cref="Hour"/> is -1). The record lists hold everything up to the session's
/// current hour; views filter them by <see cref="Hour"/>.
/// </summary>
public sealed class BattleView
{
    public required string ScenarioId { get; init; }
    public required int Seed { get; init; }
    public required int HoursFought { get; init; }
    public required bool IsOver { get; init; }
    public required BattleOutcome Outcome { get; init; }
    public required int StartHourOfDay { get; init; }

    /// <summary>The battle hour shown: the last fought hour when live, an earlier one when rewound, -1 before the start.</summary>
    public required int Hour { get; init; }

    public bool IsLive => Hour == HoursFought - 1;

    public required SideView Attacker { get; init; }
    public required SideView Defender { get; init; }
    public required IReadOnlyList<ShotRecord> Shots { get; init; }
    public required IReadOnlyList<AirStrikeRecord> AirStrikes { get; init; }
    public required IReadOnlyList<BattleEvent> Events { get; init; }
    public required IReadOnlyList<HourSummary> Timeline { get; init; }
    public required IReadOnlyList<string> HourlyLog { get; init; }
    public required IReadOnlyList<string> Notes { get; init; }
    public required IReadOnlyDictionary<string, int> SkipCounts { get; init; }

    public IEnumerable<SideView> Sides => [Attacker, Defender];

    public SideView Side(string label)
    {
        return label == Attacker.Label ? Attacker : Defender;
    }

    public required IReadOnlyDictionary<string, ShipRow> ShipsById { get; init; }

    public ShipRow? FindShip(string id)
    {
        return ShipsById.GetValueOrDefault(id);
    }

    /// <summary>"design #n" for ships of this battle, the raw ID otherwise (e.g. "land-based" planes).</summary>
    public string NameOf(string id)
    {
        return ShipsById.TryGetValue(id, out var ship) ? $"{ship.DesignId} {ship.Number}" : id;
    }

    /// <summary>"attacker" or "defender" for ships of this battle; the CSS class that colors their names.</summary>
    public string? SideOf(string id)
    {
        return ShipsById.TryGetValue(id, out var ship) ? ship.Side : null;
    }

    public int HourOfDay(int hour)
    {
        return (StartHourOfDay + hour) % 24;
    }
}
