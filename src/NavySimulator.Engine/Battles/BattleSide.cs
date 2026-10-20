namespace NavySimulator.Engine.Battles;

/// <summary>
/// Per-side battle state, including the values the game computes at the start of each hour (positioning, screening)
/// and at the start of the side's own turn (enemy line counts, submarine detection).
/// </summary>
public sealed class BattleSide
{
    public BattleSide(string label, BattleParticipant participant)
    {
        Label = label;
        Participant = participant;
        Lines = new BattleLines(participant.Fleet.Ships);
    }

    public string Label { get; }
    public BattleParticipant Participant { get; }
    public List<Ship> Ships => Participant.Fleet.Ships;

    public BattleLines Lines { get; set; }
    public double Positioning { get; set; } = 1;
    public ScreeningSummary Screening { get; set; } = new(1, 1);

    /// <summary>A-AA-REDUCE: the side's fleet AA term, set with positioning at the start of each hour (game side+0x170).</summary>
    public double FleetAntiAir { get; set; }

    /// <summary>S-TARGET: visible enemy ships per line, taken at the start of this side's turn (game group+0xa0).</summary>
    public EnemyLineCounts EnemyCounts { get; set; }

    /// <summary>S-SUB: this side's submarine detection value (mean sub detection of ships that have any).</summary>
    public double SubmarineDetection { get; set; }

    /// <summary>S-DOMINANCE: this side's naval dominance at the start of the battle (0 with the rule off).</summary>
    public double NavalDominance { get; set; }

    /// <summary>S-DOMINANCE: whether this side has naval dominance in the battle's region (S-ORG-REGAIN bonus).</summary>
    public bool HasNavalDominance { get; set; }

    public bool HasShipsInBattle => Ships.Any(ship => ship.IsInBattle);
}
