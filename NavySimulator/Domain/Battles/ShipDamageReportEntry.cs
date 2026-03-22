namespace NavySimulator.Domain.Battles;

public class ShipDamageReportEntry
{
    public int HourTick;
    public string TargetShipID;
    public WeaponType Weapon;
    public bool DidHit;
    public double Damage;
    public double AppliedHpDamage;
    public double AppliedOrganizationDamage;
    public bool DidKillingBlow;
    public double AttackerPiercing;
    public double AttackerFinalHitChance;
    public double DefenderArmor;
    public double DefenderSpeed;
    public double DefenderVisibility;

    public ShipDamageReportEntry(
        int hourTick,
        string targetShipId,
        WeaponType weapon,
        bool didHit,
        double damage,
        double appliedHpDamage,
        double appliedOrganizationDamage,
        bool didKillingBlow,
        double attackerPiercing,
        double attackerFinalHitChance,
        double defenderArmor,
        double defenderSpeed,
        double defenderVisibility)
    {
        HourTick = hourTick;
        TargetShipID = targetShipId;
        Weapon = weapon;
        DidHit = didHit;
        Damage = damage;
        AppliedHpDamage = appliedHpDamage;
        AppliedOrganizationDamage = appliedOrganizationDamage;
        DidKillingBlow = didKillingBlow;
        AttackerPiercing = attackerPiercing;
        AttackerFinalHitChance = attackerFinalHitChance;
        DefenderArmor = defenderArmor;
        DefenderSpeed = defenderSpeed;
        DefenderVisibility = defenderVisibility;
    }
}

