using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class Ship
{
    public string ID;
    public ShipDesign Design;
    public int ExperienceLevel;
    public double CurrentOrganization;
    public double CurrentHP;
    public ShipStatus CurrentStatus;
    public double RetreatProgress;
    public bool AttemptedRetreat;
    private ShipStats? effectiveStats;

    public Ship(string id, ShipDesign design, int experienceLevel)
    {
        ID = id;
        Design = design;
        ExperienceLevel = experienceLevel;
        effectiveStats = design.GetFinalStats();
        CurrentOrganization = effectiveStats.Organization;
        CurrentHP = effectiveStats.Hp;
    }

    public Ship(string id, ShipDesign design)
        : this(id, design, Hoi4Defines.SHIP_EXPERIENCE_LEVEL_REGULAR)
    {
    }

    public Ship(ShipDesign design)
        : this(Guid.NewGuid().ToString("N"), design, Hoi4Defines.SHIP_EXPERIENCE_LEVEL_REGULAR)
    {
    }

    public bool IsSunk => CurrentHP <= 0;

    public ShipStats GetFinalStats()
    {
        var stats = effectiveStats ?? Design.GetFinalStats();
        var attackMultiplier = Math.Max(0, 1.0 + Hoi4Defines.GetShipExperienceAttackModifier(ExperienceLevel));

        return stats with
        {
            LightAttack = stats.LightAttack * attackMultiplier,
            HeavyAttack = stats.HeavyAttack * attackMultiplier,
            TorpedoAttack = stats.TorpedoAttack * attackMultiplier,
            DepthChargeAttack = stats.DepthChargeAttack * attackMultiplier
        };
    }

    public void ApplyExternalModifiers(ShipStats statModifiers, ShipStats statAverages, ShipStats statMultipliers)
    {
        var stats = Design.GetFinalStats();
        stats = stats.Add(statModifiers);
        stats = stats.Add(statAverages);
        stats = stats.Scale(statMultipliers);
        effectiveStats = stats;
        CurrentOrganization = stats.Organization;
        CurrentHP = stats.Hp;
    }

    public (double HpDamage, double OrganizationDamage) ApplyDamage(double damage)
    {
        // Damage is applied against both the target's HP (by 60%[33]) and organization (by 100%[34]).
        // Additionally ships start out immune against organization damage, scaling up to the nominal value as the ship's HP level goes down.
        // As an example, assume a ship with 100/50 HP/Org receives multiple hits of 50 damage each.
        // The consecutive hits will do 30/0, 30/15, 30/30, and 30/45 HP/Org damage, with the fourth hit sinking the ship.
        
        var hpBefore = CurrentHP;
        var orgBefore = CurrentOrganization;

        var hpDamage = damage * Hoi4Defines.COMBAT_DAMAGE_TO_STR_FACTOR;
        CurrentHP = Math.Max(0, CurrentHP - hpDamage);
        var appliedHpDamage = hpBefore - CurrentHP;
        
        var orgDamage = damage * Hoi4Defines.COMBAT_DAMAGE_TO_ORG_FACTOR;
        var maxHp = GetFinalStats().Hp;
        var hpLossRatio = maxHp <= 0 ? 1.0 : 1.0 - (CurrentHP / maxHp);
        orgDamage *= Math.Clamp(hpLossRatio, 0, 1);
        
        CurrentOrganization = Math.Max(0, CurrentOrganization - orgDamage);
        var appliedOrgDamage = orgBefore - CurrentOrganization;

        return (appliedHpDamage, appliedOrgDamage);
    }
}