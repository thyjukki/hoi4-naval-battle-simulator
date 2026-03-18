using NavySimulator.Domain;
using NavySimulator.Domain.Stats;

namespace NavySimulator.Setup.Contracts;

public class ShipStatsDto
{
    public double Speed { get; set; }
    public double Organization { get; set; }
    public double HP { get; set; }
    public double SurfaceVisibility { get; set; }
    public double SubVisibility { get; set; }
    public double LightAttack { get; set; }
    public double LightPiercing { get; set; }
    public double HeavyAttack { get; set; }
    public double HeavyPiercing { get; set; }
    public double TorpedoAttack { get; set; }
    public double Armor { get; set; }
    public double LightHitChangeFactor { get; set; }
    public double HeavyHitChangeFactor { get; set; }
    public double ProductionCost { get; set; }

    public ShipStats ToDomain()
    {
        return new ShipStats(
            Speed: Speed,
            Organization: Organization,
            Hp: HP,
            SurfaceVisibility: SurfaceVisibility,
            SubVisibility: SubVisibility,
            LightAttack: LightAttack,
            LightPiercing: LightPiercing,
            HeavyAttack: HeavyAttack,
            HeavyPiercing: HeavyPiercing,
            TorpedoAttack: TorpedoAttack,
            Armor: Armor,
            LightHitChangeFactor: LightHitChangeFactor,
            HeavyHitChangeFactor: HeavyHitChangeFactor,
            ProductionCost: ProductionCost);
    }
}


