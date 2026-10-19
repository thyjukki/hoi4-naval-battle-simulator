using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

/// <summary>
/// One ship in a battle: its design, stats after external modifiers, and mutable combat state.
/// Combat rules live in <c>Battles/Mechanics</c>; this type only holds data.
/// </summary>
public class Ship
{
    public string ID;
    public ShipDesign Design;
    public double InitialExperience;
    private readonly List<DamagedPart> damagedParts = [];
    private ShipStats undamagedStats;
    private ShipStats stats;

    public Ship(string id, ShipDesign design, double experience = 0)
    {
        ID = id;
        Design = design;
        InitialExperience = experience;
        MaxManpower = Math.Max(0, design.Hull.Manpower);
        undamagedStats = design.GetFinalStats();
        stats = undamagedStats;
        ResetCombatState();
    }

    public ShipRole Role => Design.Role;
    public int MaxManpower { get; }
    public int CurrentManpower { get; set; }
    public double Experience { get; set; }

    /// <summary>
    /// S-XP [CAL]: the experience the level modifiers read. The game takes it at the start of each combat hour, so
    /// experience lost to hits during the hour changes the level only from the next hour.
    /// </summary>
    public double LevelExperience { get; set; }
    public double CurrentOrganization { get; set; }
    public double CurrentHP { get; set; }
    public ShipStatus CurrentStatus { get; set; }
    public double RetreatProgress { get; set; }
    public bool AttemptedRetreat { get; set; }

    /// <summary>S-STATS-COUNTRY: the country modifiers of the side the ship fights for, set by its <c>BattleParticipant</c>.</summary>
    public ModifierSet CountryModifiers { get; set; } = ModifierSet.Empty;

    /// <summary>Per weapon slot (<c>WeaponSlot</c> index) charge in cooldowns; a slot fires while charge >= cooldown.</summary>
    public double[] SlotCharge { get; } = new double[3];

    /// <summary>S-SUB: hours a submarine stays revealed; 0 = hidden. Always -1 for surface ships.</summary>
    public int RevealTimer { get; set; }

    public bool IsSunk => CurrentHP <= 0;
    public bool IsInBattle => !IsSunk && CurrentStatus != ShipStatus.Retreated;
    public bool IsVisible => RevealTimer != 0;

    /// <summary>S-CRIT-PART: parts damaged by critical hits this battle, in the order they were first damaged.</summary>
    public IReadOnlyList<DamagedPart> DamagedParts => damagedParts;

    /// <summary>
    /// Stats from hull, modules, MIO, researches and spirits, with the penalties of damaged parts (S-STATS-PARTS).
    /// Experience and battle modifiers are applied by the combat rules.
    /// </summary>
    public ShipStats GetFinalStats()
    {
        return stats;
    }

    /// <summary>Times <paramref name="part"/> has been damaged on this ship.</summary>
    public int DamageCount(CriticalPart part)
    {
        return damagedParts.FirstOrDefault(damaged => damaged.Part == part)?.Count ?? 0;
    }

    /// <summary>
    /// S-CRIT-PART [RE] 0xc31170, 0xc3d090: records one more damage of <paramref name="part"/> and recomputes the stats:
    /// each penalized stat × max(0, 1 + count × penalty) over the damaged parts (S-STATS-PARTS).
    /// </summary>
    public void DamagePart(CriticalPart part)
    {
        var damaged = damagedParts.FirstOrDefault(entry => entry.Part == part);

        if (damaged is null)
        {
            damagedParts.Add(new DamagedPart(part));
        }
        else
        {
            damaged.Count++;
        }

        stats = damagedParts.Aggregate(undamagedStats, (current, entry) => current.Scale(entry.Part.StatPenalties.PartPenaltyBonus(entry.Count)));
    }

    /// <summary>Σ over damaged parts of the part's modifier × its count; the rules scale it by 1 + naval_critical_effect_factor.</summary>
    public double PartModifier(string key)
    {
        return damagedParts.Sum(entry => entry.Part.Modifiers.Get(key) * entry.Count);
    }

    /// <summary>
    /// S-STATS-SHIP [RE] 0xc3d090: <c>ship = variant × (1 + tech) + tech</c> for the additive set (org, HP, reliability,
    /// hit chance factors, torpedo factors). Organization comes from the sub-unit in the game, so it is never scaled.
    /// Spirits are applied afterwards as before [LEGACY]; their real effects are combat modifiers (S-STATS-COMBAT).
    /// </summary>
    public void ApplyExternalModifiers(
        IReadOnlyList<ShipStats> equipmentBonuses,
        ShipStats technology,
        ShipStats spiritModifiers,
        ShipStats spiritAverages,
        ShipStats spiritMultipliers)
    {
        var variant = Design.ComputeStats(equipmentBonuses);
        var updated = variant.Scale(technology with { Organization = 0 }).Add(technology.AdditiveSetOnly());
        updated = updated.Add(spiritModifiers).Add(spiritAverages).Scale(spiritMultipliers);

        // S-STATS-CLAMP [RE] 0x142a217.
        undamagedStats = updated with
        {
            Reliability = Math.Max(0, updated.Reliability),
            ProductionCost = Math.Max(0.1, updated.ProductionCost)
        };
        ResetCombatState();
    }

    /// <summary>Restores full HP, organization, manpower, starting experience and undamaged parts so a scenario can be re-run.</summary>
    public void ResetCombatState()
    {
        damagedParts.Clear();
        stats = undamagedStats;
        CurrentHP = stats.Hp;
        CurrentOrganization = stats.Organization;
        CurrentManpower = MaxManpower;
        Experience = InitialExperience;
        LevelExperience = InitialExperience;
        CurrentStatus = ShipStatus.Waiting;
        RetreatProgress = 0;
        AttemptedRetreat = false;
        Array.Clear(SlotCharge);
        RevealTimer = Role == ShipRole.Submarine ? 0 : -1;
    }
}
