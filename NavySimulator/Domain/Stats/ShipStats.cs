namespace NavySimulator.Domain.Stats;

public record ShipStats(
    double Speed = 0,
    double Reliability = 0,
    double Organization = 0,
    double Hp = 0,
    double SurfaceVisibility = 0,
    double SubVisibility = 0,
    double SurfaceDetection = 0,
    double SubDetection = 0,
    double CarrierSubDetection = 0,
    double CarrierSurfaceDetection = 0,
    double CarrierSize = 0,
    double LightAttack = 0,
    double LightPiercing = 0,
    double HeavyAttack = 0,
    double HeavyPiercing = 0,
    double TorpedoAttack = 0,
    double DepthChargeAttack = 0,
    double AntiAir = 0,
    double Armor = 0,
    double TorpedoDamageReductionFactor = 0,
    double TorpedoEnemyCriticalChanceFactor = 0,
    double NavalWeatherPenaltyFactor = 0,
    double LightHitChangeFactor = 0,
    double HeavyHitChangeFactor = 0,
    double ProductionCost = 0)
{

    public ShipStats Add(ShipStats other)
    {
        return new ShipStats(
            Speed + other.Speed,
            Reliability + other.Reliability,
            Organization + other.Organization,
            Hp + other.Hp,
            SurfaceVisibility + other.SurfaceVisibility,
            SubVisibility + other.SubVisibility,
            SurfaceDetection + other.SurfaceDetection,
            SubDetection + other.SubDetection,
            CarrierSubDetection + other.CarrierSubDetection,
            CarrierSurfaceDetection + other.CarrierSurfaceDetection,
            CarrierSize + other.CarrierSize,
            LightAttack + other.LightAttack,
            LightPiercing + other.LightPiercing,
            HeavyAttack + other.HeavyAttack,
            HeavyPiercing + other.HeavyPiercing,
            TorpedoAttack + other.TorpedoAttack,
            DepthChargeAttack + other.DepthChargeAttack,
            AntiAir + other.AntiAir,
            Armor + other.Armor,
            TorpedoDamageReductionFactor + other.TorpedoDamageReductionFactor,
            TorpedoEnemyCriticalChanceFactor + other.TorpedoEnemyCriticalChanceFactor,
            NavalWeatherPenaltyFactor + other.NavalWeatherPenaltyFactor,
            LightHitChangeFactor + other.LightHitChangeFactor,
            HeavyHitChangeFactor + other.HeavyHitChangeFactor,
            ProductionCost + other.ProductionCost);
    }

    public ShipStats Scale(ShipStats percentBonus)
    {
        return new ShipStats(
            Speed * (1 + percentBonus.Speed),
            Reliability * (1 + percentBonus.Reliability),
            Organization * (1 + percentBonus.Organization),
            Hp * (1 + percentBonus.Hp),
            SurfaceVisibility * (1 + percentBonus.SurfaceVisibility),
            SubVisibility * (1 + percentBonus.SubVisibility),
            SurfaceDetection * (1 + percentBonus.SurfaceDetection),
            SubDetection * (1 + percentBonus.SubDetection),
            CarrierSubDetection * (1 + percentBonus.CarrierSubDetection),
            CarrierSurfaceDetection * (1 + percentBonus.CarrierSurfaceDetection),
            CarrierSize * (1 + percentBonus.CarrierSize),
            LightAttack * (1 + percentBonus.LightAttack),
            LightPiercing * (1 + percentBonus.LightPiercing),
            HeavyAttack * (1 + percentBonus.HeavyAttack),
            HeavyPiercing  * (1 + percentBonus.HeavyPiercing),
            TorpedoAttack * (1 + percentBonus.TorpedoAttack),
            DepthChargeAttack * (1 + percentBonus.DepthChargeAttack),
            AntiAir * (1 + percentBonus.AntiAir),
            Armor * (1 + percentBonus.Armor),
            TorpedoDamageReductionFactor * (1 + percentBonus.TorpedoDamageReductionFactor),
            TorpedoEnemyCriticalChanceFactor * (1 + percentBonus.TorpedoEnemyCriticalChanceFactor),
            NavalWeatherPenaltyFactor * (1 + percentBonus.NavalWeatherPenaltyFactor),
            LightHitChangeFactor * (1 + percentBonus.LightHitChangeFactor),
            HeavyHitChangeFactor * (1 + percentBonus.HeavyHitChangeFactor),
            ProductionCost * (1 + percentBonus.ProductionCost));
    }

    public static ShipStats AverageNonZero(IEnumerable<ShipStats> stats)
    {
        var statList = stats.ToList();

        var averages = new ShipStats(
            Speed: AverageNonZero(statList.Select(s => s.Speed)),
            Reliability: AverageNonZero(statList.Select(s => s.Reliability)),
            Organization: AverageNonZero(statList.Select(s => s.Organization)),
            Hp: AverageNonZero(statList.Select(s => s.Hp)),
            SurfaceVisibility: AverageNonZero(statList.Select(s => s.SurfaceVisibility)),
            SubVisibility: AverageNonZero(statList.Select(s => s.SubVisibility)),
            SurfaceDetection: AverageNonZero(statList.Select(s => s.SurfaceDetection)),
            SubDetection: AverageNonZero(statList.Select(s => s.SubDetection)),
            CarrierSubDetection: AverageNonZero(statList.Select(s => s.CarrierSubDetection)),
            CarrierSurfaceDetection: AverageNonZero(statList.Select(s => s.CarrierSurfaceDetection)),
            CarrierSize: AverageNonZero(statList.Select(s => s.CarrierSize)),
            LightAttack: AverageNonZero(statList.Select(s => s.LightAttack)),
            LightPiercing: AverageNonZero(statList.Select(s => s.LightPiercing)),
            HeavyAttack: AverageNonZero(statList.Select(s => s.HeavyAttack)),
            HeavyPiercing: AverageNonZero(statList.Select(s => s.HeavyPiercing)),
            TorpedoAttack: AverageNonZero(statList.Select(s => s.TorpedoAttack)),
            DepthChargeAttack: AverageNonZero(statList.Select(s => s.DepthChargeAttack)),
            AntiAir: AverageNonZero(statList.Select(s => s.AntiAir)),
            Armor: AverageNonZero(statList.Select(s => s.Armor)),
            TorpedoDamageReductionFactor: AverageNonZero(statList.Select(s => s.TorpedoDamageReductionFactor)),
            TorpedoEnemyCriticalChanceFactor: AverageNonZero(statList.Select(s => s.TorpedoEnemyCriticalChanceFactor)),
            NavalWeatherPenaltyFactor: AverageNonZero(statList.Select(s => s.NavalWeatherPenaltyFactor)),
            LightHitChangeFactor: AverageNonZero(statList.Select(s => s.LightHitChangeFactor)),
            HeavyHitChangeFactor: AverageNonZero(statList.Select(s => s.HeavyHitChangeFactor)),
            ProductionCost: AverageNonZero(statList.Select(s => s.ProductionCost)))
        {
            TorpedoHitChangeFactor = AverageNonZero(statList.Select(s => s.TorpedoHitChangeFactor)),
            DepthChargeHitChangeFactor = AverageNonZero(statList.Select(s => s.DepthChargeHitChangeFactor))
        };

        return averages;
    }

    private static double AverageNonZero(IEnumerable<double> values)
    {
        var nonZero = values.Where(value => value != 0).ToList();
        return nonZero.Count == 0 ? 0 : nonZero.Average();
    }

    public double TorpedoHitChangeFactor { get; set; }
    public double DepthChargeHitChangeFactor { get; set; }
}


