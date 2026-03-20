using NavySimulator.Domain;
using NavySimulator.Domain.Stats;

namespace NavySimulator.Setup.Contracts;

public class ShipStatsDto
{
    public double Speed { get; set; }
    public double Reliability { get; set; }
    public double Organization { get; set; }
    public double HP { get; set; }
    public double SurfaceVisibility { get; set; }
    public double SubVisibility { get; set; }
    public double SurfaceDetection { get; set; }
    public double SubDetection { get; set; }
    public double CarrierSubDetection { get; set; }
    public double CarrierSurfaceDetection { get; set; }
    public double CarrierSize { get; set; }
    public double LightAttack { get; set; }
    public double LightPiercing { get; set; }
    public double HeavyAttack { get; set; }
    public double HeavyPiercing { get; set; }
    public double TorpedoAttack { get; set; }
    public double DepthChargeAttack { get; set; }
    public double AntiAir { get; set; }
    public double Armor { get; set; }
    public double TorpedoDamageReductionFactor { get; set; }
    public double TorpedoEnemyCriticalChanceFactor { get; set; }
    public double TorpedoHitChanceFactor { get; set; }
    public double NavalWeatherPenaltyFactor { get; set; }
    public double LightHitChanceFactor { get; set; }
    public double HeavyHitChanceFactor { get; set; }
    public double ProductionCost { get; set; }

    public ShipStats ToDomain()
    {
        return new ShipStats(
            Speed: Speed,
            Reliability: Reliability,
            Organization: Organization,
            Hp: HP,
            SurfaceVisibility: SurfaceVisibility,
            SubVisibility: SubVisibility,
            SurfaceDetection: SurfaceDetection,
            SubDetection: SubDetection,
            CarrierSubDetection: CarrierSubDetection,
            CarrierSurfaceDetection: CarrierSurfaceDetection,
            CarrierSize: CarrierSize,
            LightAttack: LightAttack,
            LightPiercing: LightPiercing,
            HeavyAttack: HeavyAttack,
            HeavyPiercing: HeavyPiercing,
            TorpedoAttack: TorpedoAttack,
            DepthChargeAttack: DepthChargeAttack,
            AntiAir: AntiAir,
            Armor: Armor,
            TorpedoDamageReductionFactor: TorpedoDamageReductionFactor,
            TorpedoEnemyCriticalChanceFactor: TorpedoEnemyCriticalChanceFactor,
            NavalWeatherPenaltyFactor: NavalWeatherPenaltyFactor,
            LightHitChanceFactor: LightHitChanceFactor,
            HeavyHitChanceFactor: HeavyHitChanceFactor,
            ProductionCost: ProductionCost)
        {
            TorpedoHitChanceFactor = TorpedoHitChanceFactor
        };
    }
}


