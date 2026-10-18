using NavySimulator.Engine;

namespace NavySimulator.Web.Services;

/// <summary>The ships of one design (a "ship type") on one side.</summary>
public sealed class DesignGroup(string side, string designId, ShipRole role, IReadOnlyList<ShipRow> ships)
{
    public string Side { get; } = side;
    public string DesignId { get; } = designId;
    public ShipRole Role { get; } = role;
    public IReadOnlyList<ShipRow> Ships { get; } = ships;

    public int Count => Ships.Count;
    public int InBattle => Ships.Count(ship => ship.InBattle);
    public int Sunk => Ships.Count(ship => ship.Sunk);
    public int Escaped => Ships.Count(ship => !ship.Sunk && ship.Status == ShipStatus.Retreated);
    public int Retreating => Ships.Count(ship => !ship.Sunk && ship.Status == ShipStatus.Retreating);
    public double Hp => Ships.Sum(ship => Math.Max(0, ship.Hp));
    public double MaxHp => Ships.Sum(ship => ship.Stats.Hp);
    public double Organization => Ships.Where(ship => !ship.Sunk).Sum(ship => Math.Max(0, ship.Organization));
    public double MaxOrganization => Ships.Where(ship => !ship.Sunk).Sum(ship => ship.Stats.Organization);
    public int ShotsFired => Ships.Sum(ship => ship.ShotsFired);
    public int Hits => Ships.Sum(ship => ship.Hits);
    public double HpDamageDealt => Ships.Sum(ship => ship.HpDamageDealt);
    public double HpDamageTaken => Ships.Sum(ship => ship.HpDamageTaken);
    public int Kills => Ships.Sum(ship => ship.Kills);
}
