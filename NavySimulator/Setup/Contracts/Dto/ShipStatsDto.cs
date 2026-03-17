using NavySimulator.Domain;

namespace NavySimulator.Setup.Contracts;

public class ShipStatsDto
{
    public double Speed { get; set; }
    public double Organization { get; set; }
    public double HP { get; set; }
    public double SurfaceVisibility { get; set; }
    public double SubVisibility { get; set; }
    public double LightAttack { get; set; }
    public double HeavyAttack { get; set; }
    public double TorpedoAttack { get; set; }
    public double Armor { get; set; }

    public ShipStats ToDomain()
    {
        return new ShipStats(
            speed: Speed,
            organization: Organization,
            hp: HP,
            surfaceVisibility: SurfaceVisibility,
            subVisibility: SubVisibility,
            lightAttack: LightAttack,
            heavyAttack: HeavyAttack,
            torpedoAttack: TorpedoAttack,
            armor: Armor);
    }
}


