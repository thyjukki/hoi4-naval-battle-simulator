namespace NavySimulator.Engine.Battles;

/// <summary>
/// Task force repair priority. Selects which <c>REPAIR_AND_RETURN_PRIO_*_COMBAT</c> HP ratio triggers in-battle retreat.
/// </summary>
public enum RetreatPriority
{
    Low,
    Medium,
    High
}
