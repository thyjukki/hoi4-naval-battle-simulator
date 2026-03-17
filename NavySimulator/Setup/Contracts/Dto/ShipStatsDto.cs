using NavySimulator.Domain;

namespace NavySimulator.Setup.Contracts;

public class ShipStatsDto
{
    public double Speed { get; set; }
    public double Organization { get; set; }
    public double HP { get; set; }
    public double LightAttack { get; set; }
    public double Armor { get; set; }

    public ShipStats ToDomain()
    {
        return new ShipStats(
            speed: Speed,
            organization: Organization,
            hp: HP,
            lightAttack: LightAttack,
            armor: Armor);
    }
}


