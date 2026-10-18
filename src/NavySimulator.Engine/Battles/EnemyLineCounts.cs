namespace NavySimulator.Engine.Battles;

/// <summary>Visible enemy ships in battle per line. Convoys sit in the carrier line.</summary>
public readonly record struct EnemyLineCounts(int Screens, int Capitals, int Carriers, int Convoys, int VisibleSubmarines)
{
    public int CarrierLine => Carriers + Convoys;

    /// <summary>[RE] Counts ships that are in the battle and visible (hidden submarines are not counted).</summary>
    public static EnemyLineCounts Of(IEnumerable<Ship> ships)
    {
        var visible = ships.Where(ship => ship.IsInBattle && ship.IsVisible).ToList();
        return new EnemyLineCounts(
            visible.Count(ship => ship.Role == ShipRole.Screen),
            visible.Count(ship => ship.Role == ShipRole.Capital),
            visible.Count(ship => ship.Role == ShipRole.Carrier),
            visible.Count(ship => ship.Role == ShipRole.Convoy),
            visible.Count(ship => ship.Role == ShipRole.Submarine));
    }
}
