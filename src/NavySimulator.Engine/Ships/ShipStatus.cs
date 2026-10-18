namespace NavySimulator.Engine;

/// <summary>
/// Fire-exchange member state, as in the game (naval_fire_exchange_member.cpp, member+0xe0).
/// </summary>
public enum ShipStatus
{
    /// <summary>In the battle, line not activated yet (game state 1). Does not fire.</summary>
    Waiting = 0,

    /// <summary>Fighting (game state 2). Fires; may decide to retreat.</summary>
    Active = 1,

    /// <summary>Retreating (game state 8). Still fires and can be targeted; escapes when progress reaches 1.</summary>
    Retreating = 2,

    /// <summary>Escaped (game state 0x10). Out of the battle.</summary>
    Retreated = 3
}
