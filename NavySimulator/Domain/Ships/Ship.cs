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
    public int MaxManpower { get; }
    public int CurrentManpower { get; private set; }
    public double CurrentOrganization;
    public double CurrentHP;
    public ShipStatus CurrentStatus;
    public double RetreatProgress;
    public bool AttemptedRetreat;
    private ShipStats? effectiveStats;

    public Ship(string id, ShipDesign design, int experienceLevel = 2)
    {
        ID = id;
        Design = design;
        Experience = GetExperienceForLevel(experienceLevel);
        MaxManpower = Math.Max(0, design.Hull.Manpower);
        CurrentManpower = MaxManpower;
        effectiveStats = design.GetFinalStats();
        CurrentOrganization = effectiveStats.Organization;
        CurrentHP = effectiveStats.Hp;
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

    public void ApplyDailyManpowerRecovery()
    {
        if (CurrentManpower >= MaxManpower || MaxManpower <= 0)
        {
            return;
        }

        var recovery = MaxManpower * Hoi4Defines.DAILY_MANPOWER_GAIN_RATIO;
        CurrentManpower = Math.Min(MaxManpower, CurrentManpower + (int)Math.Round(recovery));
    }

    public (double HpDamage, double OrganizationDamage) ApplyDamage(double damage)
    {
        
        var hpBefore = CurrentHP;
        var orgBefore = CurrentOrganization;

        var hpDamage = damage * Hoi4Defines.COMBAT_DAMAGE_TO_STR_FACTOR;
        CurrentHP = Math.Max(0, CurrentHP - hpDamage);
        var appliedHpDamage = hpBefore - CurrentHP;

        if (appliedHpDamage > 0 && MaxManpower > 0)
        {
            var manpowerBefore = CurrentManpower;
            var maxHp = GetFinalStats().Hp;
            var hpLossRatio = maxHp <= 0 ? 0 : appliedHpDamage / maxHp;
            var manpowerLoss = (int)Math.Round(hpLossRatio * MaxManpower * Hoi4Defines.MANPOWER_LOSS_RATIO_ON_STR_LOSS);
            var minManpower = (int)Math.Round(MaxManpower * Hoi4Defines.MIN_MANPOWER_RATIO_TO_DROP);
            CurrentManpower = Math.Max(minManpower, CurrentManpower - manpowerLoss);

            var appliedManpowerLoss = Math.Max(0, manpowerBefore - CurrentManpower);

            if (appliedManpowerLoss > 0)
            {
                var manpowerLossRatioOfTotal = (double)appliedManpowerLoss / manpowerBefore;
                var experienceLoss = Experience * manpowerLossRatioOfTotal * Hoi4Defines.EXPERIENCE_LOSS_FACTOR;
                Experience = Math.Max(0, Experience - experienceLoss);
            }
        }
        
        var orgDamage = damage * Hoi4Defines.COMBAT_DAMAGE_TO_ORG_FACTOR;
        var maxHpForOrg = GetFinalStats().Hp;
        var hpLossRatioForOrg = 1.0 - (hpBefore / maxHpForOrg);
        orgDamage *= Math.Clamp(hpLossRatioForOrg, 0, 1);
        
        CurrentOrganization = Math.Max(0, CurrentOrganization - orgDamage);
        var appliedOrgDamage = orgBefore - CurrentOrganization;

        return (appliedHpDamage, appliedOrgDamage);
    }
}