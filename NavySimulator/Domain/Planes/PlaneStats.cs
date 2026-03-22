namespace NavySimulator.Domain;

public record PlaneStats(
    double AirDefense = 0,
    double AirAttack = 0,
    double NavalAttack = 0,
    double Speed = 0,
    double Agility = 0,
    double AirSuperiority = 0,
    double NavalTargeting = 0,
    double Reliability = 0,
    double ProductionCost = 0);

