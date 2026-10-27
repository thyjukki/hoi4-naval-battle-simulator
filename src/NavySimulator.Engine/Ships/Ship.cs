using NavySimulator.Engine.Leaders;
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
        // S-STATS-MANPOWER [LOG]: hull crew plus the modules' manpower (a carrier's deck spaces).
        MaxManpower = Math.Max(0, design.Hull.Manpower + design.Modules.Sum(module => module.Manpower));
        VariantStats = design.GetFinalStats();
        undamagedStats = VariantStats;
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

    /// <summary>S-ADMIRAL: the admiral commanding the ship's fleet (and its side), set by its <c>BattleParticipant</c>; null: none.</summary>
    public Admiral? Admiral { get; set; }

    /// <summary>
    /// S-ADMIRAL-MOD [RE]: what the admiral adds to every modifier read of this ship (0xc38760): container A plus the traits'
    /// <c>sub_unit_modifiers</c> for the ship's sub-unit.
    /// </summary>
    public ModifierSet AdmiralModifiers { get; set; } = ModifierSet.Empty;

    /// <summary>S-POTF: the pride of the fleet of the side the ship fights for (possibly this ship), set by its <c>BattleParticipant</c>; null: none.</summary>
    public Ship? PrideOfTheFleet { get; set; }

    /// <summary>S-POTF: whether this ship is its country's pride of the fleet.</summary>
    public bool IsPrideOfTheFleet => ReferenceEquals(PrideOfTheFleet, this);

    /// <summary>Per weapon slot (<c>WeaponSlot</c> index) charge in cooldowns; a slot fires while charge >= cooldown.</summary>
    public double[] SlotCharge { get; } = new double[3];

    /// <summary>S-SUB: hours a submarine stays revealed; 0 = hidden. Always -1 for surface ships.</summary>
    public int RevealTimer { get; set; }

    /// <summary>
    /// S-RECOVERY: the ship was recovered when it sank. It is out of the battle as a sunk ship is (the game's member state
    /// stays sunk), with the HP it was recovered with.
    /// </summary>
    public bool IsRecovered { get; set; }

    public bool IsSunk => CurrentHP <= 0 || IsRecovered;
    public bool IsInBattle => !IsSunk && CurrentStatus != ShipStatus.Retreated;
    public bool IsVisible => RevealTimer != 0;

    /// <summary>S-CRIT-PART: parts damaged by critical hits this battle, in the order they were first damaged.</summary>
    public IReadOnlyList<DamagedPart> DamagedParts => damagedParts;

    /// <summary>
    /// Stats from hull, modules, MIO, researches and doctrines, with the penalties of damaged parts (S-STATS-PARTS).
    /// Experience and battle modifiers are applied by the combat rules.
    /// </summary>
    public ShipStats GetFinalStats()
    {
        return stats;
    }

    /// <summary>
    /// The equipment variant's stats: the design with the side's equipment bonuses, before technology and doctrine
    /// sub-unit bonuses and part damage (the game's <c>variant+0x100</c> stats, S-STATS-DESIGN).
    /// </summary>
    public ShipStats VariantStats { get; private set; }

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
    /// Technology here means every sub-unit bonus of the side: researches and doctrines (S-DOCTRINE-EFFECT).
    /// <paramref name="mioBonus"/> is the side's bonus of the design's MIO (S-STATS-MIO); null: its initial trait only.
    /// </summary>
    public void ApplyExternalModifiers(IReadOnlyList<ShipStats> equipmentBonuses, ShipStats technology, ShipStats? mioBonus = null)
    {
        var variant = Design.ComputeStats(equipmentBonuses, mioBonus);
        VariantStats = variant;
        var updated = variant.Scale(technology with { Organization = 0 }).Add(technology.AdditiveSetOnly());

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
