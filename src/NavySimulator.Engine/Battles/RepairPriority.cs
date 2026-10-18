namespace NavySimulator.Engine.Battles;

/// <summary>
/// Task force repair priority, named as in the game's "Select repair priority" button (<c>NAVY_REPAIR_POLICY_0..3</c>;
/// save key <c>repair_mode</c>, presumably the same 0..3 order). It decides when the task force goes home for repairs
/// and is not read in naval combat [RE]; the in-combat retreat threshold comes from <see cref="Aggressiveness"/>.
/// The engine keeps it with the side so a scenario records the full task force setup (S-RETREAT).
/// </summary>
public enum RepairPriority
{
    NeverRepair = 0,
    Low = 1,
    Medium = 2,
    High = 3
}
