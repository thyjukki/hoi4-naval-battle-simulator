namespace NavySimulator.Domain;

public class ShipStats
{
    public double Speed;
    public double Organization;
    public double HP;
    public double SurfaceVisibility;
    public double SubVisibility;
    public double LightAttack;
    public double LightPiercing;
    public double HeavyAttack;
    public double HeavyPiercing;
    public double TorpedoAttack;
    public double Armor;

    public ShipStats(
        double speed = 0,
        double organization = 0,
        double hp = 0,
        double surfaceVisibility = 0,
        double subVisibility = 0,
        double lightAttack = 0,
        double lightPiercing = 0,
        double heavyAttack = 0,
        double heavyPiercing = 0,
        double torpedoAttack = 0,
        double armor = 0)
    {
        Speed = speed;
        Organization = organization;
        HP = hp;
        SurfaceVisibility = surfaceVisibility;
        SubVisibility = subVisibility;
        LightAttack = lightAttack;
        LightPiercing = lightPiercing;
        HeavyAttack = heavyAttack;
        HeavyPiercing = heavyPiercing;
        TorpedoAttack = torpedoAttack;
        Armor = armor;
    }

    public ShipStats Add(ShipStats other)
    {
        return new ShipStats(
            Speed + other.Speed,
            Organization + other.Organization,
            HP + other.HP,
            SurfaceVisibility + other.SurfaceVisibility,
            SubVisibility + other.SubVisibility,
            LightAttack + other.LightAttack,
            LightPiercing + other.LightPiercing,
            HeavyAttack + other.HeavyAttack,
            HeavyPiercing + other.HeavyPiercing,
            TorpedoAttack + other.TorpedoAttack,
            Armor + other.Armor);
    }

    public ShipStats Scale(ShipStats percentBonus)
    {
        return new ShipStats(
            Speed * (1 + percentBonus.Speed / 100.0),
            Organization * (1 + percentBonus.Organization / 100.0),
            HP * (1 + percentBonus.HP / 100.0),
            SurfaceVisibility * (1 + percentBonus.SurfaceVisibility / 100.0),
            SubVisibility * (1 + percentBonus.SubVisibility / 100.0),
            LightAttack * (1 + percentBonus.LightAttack / 100.0),
            LightPiercing * (1 + percentBonus.LightPiercing / 100.0),
            HeavyAttack * (1 + percentBonus.HeavyAttack / 100.0),
            HeavyPiercing  * (1 + percentBonus.HeavyPiercing / 100.0),
            TorpedoAttack * (1 + percentBonus.TorpedoAttack / 100.0),
            Armor * (1 + percentBonus.Armor / 100.0));
    }
}


