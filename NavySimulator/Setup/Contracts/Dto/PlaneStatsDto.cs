using NavySimulator.Domain;

namespace NavySimulator.Setup.Contracts;

public class PlaneStatsDto
{
    public double AirDefense { get; set; }
    public double AirAttack { get; set; }
    public double NavalAttack { get; set; }
    public double Speed { get; set; }
    public double Agility { get; set; }
    public double AirSuperiority { get; set; }
    public double NavalTargeting { get; set; }
    public double Reliability { get; set; }
    public double ProductionCost { get; set; }

    public PlaneStats ToDomain()
    {
        return new PlaneStats(
            AirDefense,
            AirAttack,
            NavalAttack,
            Speed,
            Agility,
            AirSuperiority,
            NavalTargeting,
            Reliability,
            ProductionCost);
    }
}


