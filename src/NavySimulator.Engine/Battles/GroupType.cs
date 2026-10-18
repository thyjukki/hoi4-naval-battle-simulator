namespace NavySimulator.Engine.Battles;

/// <summary>
/// Battle-line groups, ordered from closest to farthest from the enemy. Convoys sit in the carrier group.
/// </summary>
public enum GroupType
{
    Screen,
    Capital,
    Carrier,
    Submarine
}
