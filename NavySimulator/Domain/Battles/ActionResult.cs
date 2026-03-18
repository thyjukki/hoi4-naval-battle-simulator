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
        string skipReason)
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
    }

    public static ActionResult Fire(
        Ship shooter,
        WeaponType weapon,
        SelectedTarget target,
        double damage,
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
            string.Empty);
    }

    public static ActionResult Skip(Ship shooter, WeaponType weapon, string reason)
    {
        return new ActionResult(shooter.ID, weapon, false, null, GroupType.None, 0, 0, 0, false, reason);
    }
}