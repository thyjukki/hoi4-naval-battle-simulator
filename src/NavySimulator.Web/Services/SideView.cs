using NavySimulator.Engine.Battles;

namespace NavySimulator.Web.Services;

/// <summary>One side of a <see cref="BattleView"/>.</summary>
public sealed class SideView(
    string label,
    string fleetId,
    Aggressiveness aggressiveness,
    RepairPriority repairPriority,
    IReadOnlyList<DesignGroup> designs,
    double positioning,
    double screening,
    double carrierScreening,
    DamageBreakdown damageDealt)
{
    public string Label { get; } = label;
    public string FleetId { get; } = fleetId;
    public Aggressiveness Aggressiveness { get; } = aggressiveness;
    public RepairPriority RepairPriority { get; } = repairPriority;
    public IReadOnlyList<DesignGroup> Designs { get; } = designs;

    /// <summary>Positioning and screening the shown hour was fought with (1 before the first hour).</summary>
    public double Positioning { get; } = positioning;

    public double Screening { get; } = screening;
    public double CarrierScreening { get; } = carrierScreening;

    /// <summary>Damage dealt up to the shown hour, by weapon, ship role and design (land-based planes included).</summary>
    public DamageBreakdown DamageDealt { get; } = damageDealt;

    public IEnumerable<ShipRow> Ships => Designs.SelectMany(design => design.Ships);
    public int Count => Designs.Sum(design => design.Count);
    public int InBattle => Designs.Sum(design => design.InBattle);
    public int Sunk => Designs.Sum(design => design.Sunk);
    public int Escaped => Designs.Sum(design => design.Escaped);
    public int Retreating => Designs.Sum(design => design.Retreating);
    public double Hp => Designs.Sum(design => design.Hp);
    public double MaxHp => Designs.Sum(design => design.MaxHp);
    public double HpDamageDealt => DamageDealt.HpDamage;
    public int ShotsFired => Designs.Sum(design => design.ShotsFired);
    public int Hits => Designs.Sum(design => design.Hits);
}
