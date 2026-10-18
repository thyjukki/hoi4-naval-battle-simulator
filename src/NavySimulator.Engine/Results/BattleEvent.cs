namespace NavySimulator.Engine.Results;

/// <summary>
/// A state change during the battle. <see cref="ShipId"/> is null for side-level events such as re-engagement;
/// <see cref="OtherShipId"/> names the ship that caused the event, e.g. the shooter of a killing blow.
/// </summary>
public sealed record BattleEvent(
    int Hour,
    string Side,
    string? ShipId,
    BattleEventKind Kind,
    string Message,
    string? OtherShipId = null);
