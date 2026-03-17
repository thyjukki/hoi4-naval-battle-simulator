namespace NavySimulator.Domain;

public class ShipStats
{
    public double Speed;
    public double Organization;
    public double HP;
    public double LightAttack;
    public double Armor;

    public ShipStats(
        double speed = 0,
        double organization = 0,
        double hp = 0,
        double lightAttack = 0,
        double armor = 0)
    {
        Speed = speed;
        Organization = organization;
        HP = hp;
        LightAttack = lightAttack;
        Armor = armor;
    }

    public ShipStats Add(ShipStats other)
    {
        return new ShipStats(
            Speed + other.Speed,
            Organization + other.Organization,
            HP + other.HP,
            LightAttack + other.LightAttack,
            Armor + other.Armor);
    }

    public ShipStats Scale(ShipStats percentBonus)
    {
        return new ShipStats(
            Speed * (1 + percentBonus.Speed / 100.0),
            Organization * (1 + percentBonus.Organization / 100.0),
            HP * (1 + percentBonus.HP / 100.0),
            LightAttack * (1 + percentBonus.LightAttack / 100.0),
            Armor * (1 + percentBonus.Armor / 100.0));
    }
}


