using NavySimulator.Domain;
using NavySimulator.Domain.Battles;

internal class ActionResult
{
    public string ShooterID;
    public WeaponType Weapon;
    public bool Fired;
    public Ship? Target;
    public GroupType TargetGroup;
    public double Damage;
    public double FinalHitChance;
    public double HitRoll;
    public bool DidHit;
    public string SkipReason;
    public double PiercingValue;
    public double DefenderArmor;
    public double DefenderSpeed;
    public double DefenderVisibility;
    public bool DidKillingBlow;
    public int Hour;
    public double AppliedHpDamage;
    public double AppliedOrganizationDamage;

    private ActionResult(
        string shooterId,
        WeaponType weapon,
        bool fired,
        Ship? target,
        GroupType targetGroup,
        double damage,
        double finalHitChance,
        double hitRoll,
        bool didHit,
        string skipReason,
        double piercingValue,
        double defenderArmor,
        double defenderSpeed,
        double defenderVisibility,
        bool didKillingBlow,
        int hour,
        double appliedHpDamage,
        double appliedOrganizationDamage)
    {
        ShooterID = shooterId;
        Weapon = weapon;
        Fired = fired;
        Target = target;
        TargetGroup = targetGroup;
        Damage = damage;
        FinalHitChance = finalHitChance;
        HitRoll = hitRoll;
        DidHit = didHit;
        SkipReason = skipReason;
        PiercingValue = piercingValue;
        DefenderArmor = defenderArmor;
        DefenderSpeed = defenderSpeed;
        DefenderVisibility = defenderVisibility;
        DidKillingBlow = didKillingBlow;
        Hour = hour;
        AppliedHpDamage = appliedHpDamage;
        AppliedOrganizationDamage = appliedOrganizationDamage;
    }

    public static ActionResult Fire(
        Ship shooter,
        WeaponType weapon,
        SelectedTarget target,
        double damage,
        double piercingValue,
        double defenderArmor,
        double defenderSpeed,
        double defenderVisibility,
        int hour,
        double finalHitChance,
        double hitRoll,
        bool didHit)
    {
        return new ActionResult(
            shooter.ID,
            weapon,
            true,
            target.Target,
            target.Group,
            damage,
            finalHitChance,
            hitRoll,
            didHit,
            string.Empty,
            piercingValue,
            defenderArmor,
            defenderSpeed,
            defenderVisibility,
            didKillingBlow: false,
            hour,
            appliedHpDamage: 0,
            appliedOrganizationDamage: 0);
    }

    public static ActionResult Skip(Ship shooter, WeaponType weapon, int hour, string reason)
    {
        return new ActionResult(shooter.ID, weapon, false, null, GroupType.None, 0, 0, 0, false, reason, 0, 0, 0, 0, false, hour, 0, 0);
    }
}