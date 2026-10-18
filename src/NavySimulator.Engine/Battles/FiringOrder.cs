namespace NavySimulator.Engine.Battles;

/// <summary>
/// How shots from both sides are sequenced within one hour. The game's order is not known yet (docs/mechanics/surface.md, S-ORDER).
/// </summary>
public enum FiringOrder
{
    /// <summary>All ships of both sides fire in a random order each hour; damage applies immediately.</summary>
    Interleaved,

    /// <summary>All attacker ships fire, then all defender ships; damage applies immediately. Behavior of the previous simulator.</summary>
    AttackerFirst,

    /// <summary>All shots are aimed at the start-of-hour state and damage is applied at the end of the hour.</summary>
    Simultaneous
}
