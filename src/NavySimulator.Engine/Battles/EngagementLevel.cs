namespace NavySimulator.Engine.Battles;

/// <summary>
/// Task force engagement level ("aggressiveness"; game field taskforce+0x394, values 0..4). It selects the in-combat
/// retreat threshold (S-RETREAT) and which lines submarines may attack (S-TARGET-SUB).
/// </summary>
public enum EngagementLevel
{
    DoNotEngage = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    DeathIncarnate = 4
}
