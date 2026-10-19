using NavySimulator.Engine;
using NavySimulator.Engine.Results;

namespace NavySimulator.Web.Services;

/// <summary>
/// What a side's ships cost to build, in IC (each ship's production cost after the side's equipment bonuses), and what
/// a battle took of it: the ships sunk, and the HP the others lost valued at their cost (repaired in port in the game,
/// so it is shown apart). Carrier air wings, land-based planes and MIO production bonuses are not counted.
/// </summary>
public sealed record FleetCost(int Ships, double Total, double Sunk, double Damaged)
{
    public double SunkShare => Total <= 0 ? 0 : Sunk / Total;

    public static FleetCost Of(IEnumerable<(double Cost, double MaxHp, double Hp)> ships)
    {
        var count = 0;
        var total = 0.0;
        var sunk = 0.0;
        var damaged = 0.0;

        foreach (var (cost, maxHp, hp) in ships)
        {
            count++;
            total += cost;

            if (hp <= 0)
            {
                sunk += cost;
            }
            else if (maxHp > 0)
            {
                damaged += cost * Math.Clamp(1 - hp / maxHp, 0, 1);
            }
        }

        return new FleetCost(count, total, sunk, damaged);
    }

    /// <summary>Ships of a built scenario, in their current state (undamaged before a battle).</summary>
    public static FleetCost Of(IEnumerable<Ship> ships)
    {
        return Of(ships.Select(ship => (ship.GetFinalStats().ProductionCost, ship.GetFinalStats().Hp, ship.CurrentHP)));
    }

    public static FleetCost Of(IEnumerable<ShipRow> ships)
    {
        return Of(ships.Select(ship => (ship.Stats.ProductionCost, ship.Stats.Hp, ship.Hp)));
    }

    /// <summary>The ships of side <paramref name="side"/> (its label) at the end of a battle.</summary>
    public static FleetCost Of(BattleResult result, string side)
    {
        return Of(result.Ships.Where(ship => ship.Side == side).Select(ship => (ship.ProductionCost, ship.MaxHp, ship.FinalHp)));
    }
}
