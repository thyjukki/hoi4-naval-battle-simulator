using NavySimulator.Engine.Results;

namespace NavySimulator.Web.Services;

/// <summary>
/// One line of a ship's combat log: a shot it fired or took, an air strike it flew or took, or a state change.
/// <see cref="OtherId"/> is the target (outgoing), the shooter (incoming) or the ship that caused an event.
/// </summary>
public sealed record ShipLogEntry(
    int Hour,
    ShipLogKind Kind,
    string Weapon,
    string? OtherId,
    double? HitChance,
    bool Hit,
    double Damage,
    double HpDamage,
    double OrganizationDamage,
    bool Critical,
    int? PiercingTier,
    bool KillingBlow,
    string Text,
    BattleEventKind? EventKind = null)
{
    public bool Outgoing => Kind is ShipLogKind.Fired or ShipLogKind.AirStrike;
    public bool Incoming => Kind is ShipLogKind.Received or ShipLogKind.AirAttacked;
}
