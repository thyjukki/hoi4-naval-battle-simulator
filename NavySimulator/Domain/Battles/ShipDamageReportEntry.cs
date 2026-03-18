namespace NavySimulator.Domain.Battles;

public class ShipDamageReportEntry
{
    public string TargetShipID;
    public WeaponType Weapon;
    public double Damage;
    public bool DidKillingBlow;
    public double AttackerPiercing;
    public double AttackerFinalHitChance;
    public double DefenderArmor;
    public double DefenderSpeed;
    public double DefenderVisibility;

    public ShipDamageReportEntry(
        string targetShipId,
        WeaponType weapon,
        double damage,
        bool didKillingBlow,
        double attackerPiercing,
        double attackerFinalHitChance,
        double defenderArmor,
        double defenderSpeed,
        double defenderVisibility)
    {
        TargetShipID = targetShipId;
        Weapon = weapon;
        Damage = damage;
        DidKillingBlow = didKillingBlow;
        AttackerPiercing = attackerPiercing;
        AttackerFinalHitChance = attackerFinalHitChance;
        DefenderArmor = defenderArmor;
        DefenderSpeed = defenderSpeed;
        DefenderVisibility = defenderVisibility;
    }
}

