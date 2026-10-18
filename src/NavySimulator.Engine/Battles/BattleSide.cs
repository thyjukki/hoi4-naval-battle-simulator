namespace NavySimulator.Engine.Battles;

/// <summary>
/// Per-side battle state, including the values computed at the start of each hour.
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
    public ScreeningSummary Screening { get; set; }

    public bool HasShipsInBattle => Ships.Any(ship => ship.IsInBattle);
}
