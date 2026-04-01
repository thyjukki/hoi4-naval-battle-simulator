using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class Ship
{
    public string ID;
    public ShipDesign Design;
    private double experience;
    public double Experience
    {
        get => experience;
        set => experience = ClampExperience(value);
    }

    public int ExperienceLevel => GetExperienceLevelFromXp(Experience);
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
        Experience = GetExperienceForLevel(experienceLevel);
        effectiveStats = design.GetFinalStats();
        CurrentOrganization = effectiveStats.Organization;
        CurrentHP = effectiveStats.Hp;
    }

    public Ship(string id, ShipDesign design)
        : this(id, design, 2)
    {
    }

    public Ship(ShipDesign design)
        : this(Guid.NewGuid().ToString("N"), design, 2)
    {
    }

    public bool IsSunk => CurrentHP <= 0;

    public ShipStats GetFinalStats()
    {
        var stats = effectiveStats ?? Design.GetFinalStats();
        var attackMultiplier = Math.Max(0, 1.0 + GetShipExperienceAttackModifier());

        return stats with
        {
            LightAttack = stats.LightAttack * attackMultiplier,
            HeavyAttack = stats.HeavyAttack * attackMultiplier,
            TorpedoAttack = stats.TorpedoAttack * attackMultiplier,
            DepthChargeAttack = stats.DepthChargeAttack * attackMultiplier
        };
    }

    public double GetShipExperienceAttackModifier()
    {
        var level = ExperienceLevel;
        var maxLevel = Hoi4Defines.UNIT_EXP_LEVELS.Length;

        if (maxLevel <= 0)
        {
            return Hoi4Defines.ShipExperienceBonusMinNavalDamageFactor;
        }

        var step = (Hoi4Defines.ShipExperienceBonusMaxNavalDamageFactor - Hoi4Defines.ShipExperienceBonusMinNavalDamageFactor) / maxLevel;
        return Hoi4Defines.ShipExperienceBonusMinNavalDamageFactor + step * level;
    }

    private static int GetExperienceLevelFromXp(double experience)
    {
        var normalizedExperience = ClampExperience(experience) / Hoi4Defines.NAVY_MAX_XP;
        var level = 0;

        foreach (var threshold in Hoi4Defines.UNIT_EXP_LEVELS)
        {
            if (normalizedExperience < threshold)
            {
                break;
            }

            level++;
        }

        return level;
    }

    public static double GetExperienceForLevel(int experienceLevel)
    {
        var clampedLevel = Math.Clamp(experienceLevel, 0, Hoi4Defines.UNIT_EXP_LEVELS.Length);

        if (clampedLevel <= 0)
        {
            return 0;
        }

        return Hoi4Defines.UNIT_EXP_LEVELS[clampedLevel - 1] * Hoi4Defines.NAVY_MAX_XP;
    }

    public void SetExperienceLevel(int experienceLevel)
    {
        Experience = GetExperienceForLevel(experienceLevel);
    }

    private static double ClampExperience(double experience)
    {
        return Math.Clamp(experience, 0, Hoi4Defines.NAVY_MAX_XP);
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