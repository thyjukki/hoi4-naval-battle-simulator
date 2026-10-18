using NavySimulator.Engine;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Web.Services;

/// <summary>One ship as shown at a given battle hour: its state then and its fire totals up to then.</summary>
public sealed record ShipRow(
    int Index,
    string Side,
    string Id,
    string Number,
    string DesignId,
    ShipRole Role,
    ShipStats Stats,
    double Hp,
    double Organization,
    double Experience,
    ShipStatus Status,
    int? SunkHour,
    int? RetreatedHour,
    int ShotsFired,
    int Hits,
    double HpDamageDealt,
    double OrganizationDamageDealt,
    double HpDamageTaken,
    int Kills)
{
    public bool Sunk => Hp <= 0;
    public bool InBattle => !Sunk && Status != ShipStatus.Retreated;

    /// <summary>"sunk", "escaped", "retreating", "fighting" or "waiting".</summary>
    public string StatusName => Sunk
        ? "sunk"
        : Status switch
        {
            ShipStatus.Retreated => "escaped",
            ShipStatus.Retreating => "retreating",
            ShipStatus.Active => "fighting",
            _ => "waiting"
        };
}
