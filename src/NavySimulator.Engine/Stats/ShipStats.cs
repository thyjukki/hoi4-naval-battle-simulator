namespace NavySimulator.Engine.Stats;

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
    double LightHitChanceFactor = 0,
    double HeavyHitChanceFactor = 0,
    double TorpedoHitChanceFactor = 0,
    double DepthChargeHitChanceFactor = 0,
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
            LightHitChanceFactor + other.LightHitChanceFactor,
            HeavyHitChanceFactor + other.HeavyHitChanceFactor,
            TorpedoHitChanceFactor + other.TorpedoHitChanceFactor,
            DepthChargeHitChanceFactor + other.DepthChargeHitChanceFactor,
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
            LightHitChanceFactor * (1 + percentBonus.LightHitChanceFactor),
            HeavyHitChanceFactor * (1 + percentBonus.HeavyHitChanceFactor),
            TorpedoHitChanceFactor * (1 + percentBonus.TorpedoHitChanceFactor),
            DepthChargeHitChanceFactor * (1 + percentBonus.DepthChargeHitChanceFactor),
            ProductionCost * (1 + percentBonus.ProductionCost));
    }

    /// <summary>
    /// S-STATS-SHIP [RE] 0x1429580: the stats a technology value is also added to flat, on top of
    /// <c>variant × (1 + value)</c>. Depth charges use the light-gun hit chance factor in the game.
    /// </summary>
    public ShipStats AdditiveSetOnly()
    {
        return new ShipStats(
            Organization: Organization,
            Hp: Hp,
            Reliability: Reliability,
            TorpedoDamageReductionFactor: TorpedoDamageReductionFactor,
            TorpedoEnemyCriticalChanceFactor: TorpedoEnemyCriticalChanceFactor,
            NavalWeatherPenaltyFactor: NavalWeatherPenaltyFactor,
            LightHitChanceFactor: LightHitChanceFactor,
            HeavyHitChanceFactor: HeavyHitChanceFactor,
            TorpedoHitChanceFactor: TorpedoHitChanceFactor,
            DepthChargeHitChanceFactor: DepthChargeHitChanceFactor);
    }

    /// <summary>S-STATS-MIO [RE]: the light, heavy and torpedo hit chance factors (stats 0x25-0x27).</summary>
    public ShipStats HitChanceFactorsOnly()
    {
        return new ShipStats(
            LightHitChanceFactor: LightHitChanceFactor,
            HeavyHitChanceFactor: HeavyHitChanceFactor,
            TorpedoHitChanceFactor: TorpedoHitChanceFactor,
            DepthChargeHitChanceFactor: DepthChargeHitChanceFactor);
    }

    /// <summary>
    /// S-STATS-PARTS [RE] 0xc3d340: these stats as a critical part's penalties, damaged <paramref name="count"/> times,
    /// turned into <see cref="Scale"/> bonuses: the stat is multiplied by <c>max(0, 1 + count × penalty)</c>.
    /// [CAL] Only the attacks: heavy ×0.5 and secondaries' light ×0.75 fit every logged shot, while radar short circuits
    /// changed neither positioning's detection (110 of 110 side-hours) nor hit chances. Sub visibility is kept
    /// [ASSUMED]: no submarine battle is logged yet.
    /// </summary>
    public ShipStats PartPenaltyBonus(int count)
    {
        double F(double penalty) => Math.Max(0, 1 + count * penalty) - 1;

        return new ShipStats
        {
            SubVisibility = F(SubVisibility),
            LightAttack = F(LightAttack),
            HeavyAttack = F(HeavyAttack),
            TorpedoAttack = F(TorpedoAttack),
            DepthChargeAttack = F(DepthChargeAttack)
        };
    }

    public ShipStats WithoutHitChanceFactors()
    {
        return this with { LightHitChanceFactor = 0, HeavyHitChanceFactor = 0, TorpedoHitChanceFactor = 0, DepthChargeHitChanceFactor = 0 };
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
            LightHitChanceFactor: AverageNonZero(statList.Select(s => s.LightHitChanceFactor)),
            HeavyHitChanceFactor: AverageNonZero(statList.Select(s => s.HeavyHitChanceFactor)),
            TorpedoHitChanceFactor: AverageNonZero(statList.Select(s => s.TorpedoHitChanceFactor)),
            DepthChargeHitChanceFactor: AverageNonZero(statList.Select(s => s.DepthChargeHitChanceFactor)),
            ProductionCost: AverageNonZero(statList.Select(s => s.ProductionCost)));

        return averages;
    }

    private static double AverageNonZero(IEnumerable<double> values)
    {
        var nonZero = values.Where(value => value != 0).ToList();
        return nonZero.Count == 0 ? 0 : nonZero.Average();
    }
}


