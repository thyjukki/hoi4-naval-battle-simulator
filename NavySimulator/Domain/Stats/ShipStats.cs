namespace NavySimulator.Domain.Stats;

public record ShipStats(
    double Speed = 0,
    double Organization = 0,
    double Hp = 0,
    double SurfaceVisibility = 0,
    double SubVisibility = 0,
    double LightAttack = 0,
    double LightPiercing = 0,
    double HeavyAttack = 0,
    double HeavyPiercing = 0,
    double TorpedoAttack = 0,
    double Armor = 0,
    double LightHitChangeFactor = 0,
    double HeavyHitChangeFactor = 0,
    double ProductionCost = 0)
{

    public ShipStats Add(ShipStats other)
    {
        return new ShipStats(
            Speed + other.Speed,
            Organization + other.Organization,
            Hp + other.Hp,
            SurfaceVisibility + other.SurfaceVisibility,
            SubVisibility + other.SubVisibility,
            LightAttack + other.LightAttack,
            LightPiercing + other.LightPiercing,
            HeavyAttack + other.HeavyAttack,
            HeavyPiercing + other.HeavyPiercing,
            TorpedoAttack + other.TorpedoAttack,
            Armor + other.Armor,
            LightHitChangeFactor + other.LightHitChangeFactor,
            HeavyHitChangeFactor + other.HeavyHitChangeFactor,
            ProductionCost + other.ProductionCost);
    }

    public ShipStats Scale(ShipStats percentBonus)
    {
        return new ShipStats(
            Speed * (1 + percentBonus.Speed),
            Organization * (1 + percentBonus.Organization),
            Hp * (1 + percentBonus.Hp),
            SurfaceVisibility * (1 + percentBonus.SurfaceVisibility),
            SubVisibility * (1 + percentBonus.SubVisibility),
            LightAttack * (1 + percentBonus.LightAttack),
            LightPiercing * (1 + percentBonus.LightPiercing),
            HeavyAttack * (1 + percentBonus.HeavyAttack),
            HeavyPiercing  * (1 + percentBonus.HeavyPiercing),
            TorpedoAttack * (1 + percentBonus.TorpedoAttack),
            Armor * (1 + percentBonus.Armor),
            LightHitChangeFactor * (1 + percentBonus.LightHitChangeFactor),
            HeavyHitChangeFactor * (1 + percentBonus.HeavyHitChangeFactor),
            ProductionCost * (1 + percentBonus.ProductionCost));
    }

    public double TorpedoHitChangeFactor { get; set; }
    public double DepthChargeHitChangeFactor { get; set; }
}


