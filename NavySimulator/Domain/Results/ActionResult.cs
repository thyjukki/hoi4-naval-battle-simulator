using NavySimulator.Domain;
using NavySimulator.Domain.Battles;

internal class ActionResult
{
    public readonly string ShooterId;
    public readonly WeaponType Weapon;
    public readonly bool Fired;
    public readonly Ship? Target;
    public readonly GroupType TargetGroup;
    public readonly double Damage;
    public readonly double FinalHitChance;
    public double HitRoll;
    public readonly bool DidHit;
    public readonly string SkipReason;
    public readonly double PiercingValue;
    public readonly double DefenderArmor;
    public readonly double DefenderSpeed;
    public readonly double DefenderVisibility;
    public bool DidKillingBlow;
    public readonly bool DidCriticalHit;
    public readonly double CriticalDamageMultiplier;
    public readonly int Hour;
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
        bool didCriticalHit,
        double criticalDamageMultiplier,
        int hour,
        double appliedHpDamage,
        double appliedOrganizationDamage)
    {
        ShooterId = shooterId;
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
        DidCriticalHit = didCriticalHit;
        CriticalDamageMultiplier = criticalDamageMultiplier;
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
        bool didHit,
        bool didCriticalHit,
        double criticalDamageMultiplier)
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
            didCriticalHit,
            criticalDamageMultiplier,
            hour,
            appliedHpDamage: 0,
            appliedOrganizationDamage: 0);
    }

    public static ActionResult Skip(Ship shooter, WeaponType weapon, int hour, string reason)
    {
        return new ActionResult(shooter.ID, weapon, false, null, GroupType.None, 0, 0, 0, false, reason, 0, 0, 0, 0, false, false, 1, hour, 0, 0);
    }
}