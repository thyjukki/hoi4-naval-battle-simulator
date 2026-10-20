using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// One air wing in a naval battle; planes are removed as they are shot down. A-MIXED: a wing can hold planes of several
/// variants of its sub-unit (refilled from the stockpile); its stats are their plane-share mix.
/// </summary>
public sealed class AirWing
{
    // A-MIXED [RE] 0xbd1f20: the wing's variants in the game's order, the oldest type first.
    private readonly List<(PlaneEquipment Plane, int Planes)> variants = [];
    private readonly ModifierSet bonus;

    public AirWing(PlaneEquipment plane, int planes, HardcodedValues values, bool executesNavalStrike = true, ModifierSet? bonus = null)
    {
        Plane = plane;
        this.bonus = bonus ?? ModifierSet.Empty;
        variants.Add((plane, planes));
        Size = planes;
        RefreshStats();
        IsStrikeWing = plane.IsInStrikeList(values);
        ExecutesNavalStrike = IsStrikeWing && executesNavalStrike && plane.IsNavalStriker(values);
        IsFighter = plane.IsFighterType(values);
        IsSortieFighter = plane.IsSortieFighterType(values);
    }

    /// <summary>The wing's own plane: its sub-unit, missions and fighter tests are the wing's.</summary>
    public PlaneEquipment Plane { get; }

    /// <summary>
    /// A-STATS / A-MIXED [RE] 0xf661e0: the plane-share mix of the wing's variants with its side's sub-unit bonuses
    /// (doctrines, spirits), as cached at the start of the hour (<see cref="RefreshStats"/>).
    /// </summary>
    public PlaneStats Stats { get; private set; } = new();

    /// <summary>The wing's variants and their planes, the oldest type first.</summary>
    public IReadOnlyList<(PlaneEquipment Plane, int Planes)> Variants => variants;

    /// <summary>
    /// The wing's planes. Lowering it removes the oldest type's planes first (A-MIXED, RemovePlanes 0xf63580); raising it
    /// adds planes of the wing's own plane.
    /// </summary>
    public int Planes
    {
        get => variants.Sum(variant => variant.Planes);
        set
        {
            var change = value - Planes;

            if (change > 0)
            {
                Add(Plane, change);
            }

            for (var i = 0; i < variants.Count && change < 0; i++)
            {
                var removed = Math.Min(variants[i].Planes, -change);
                variants[i] = (variants[i].Plane, variants[i].Planes - removed);
                change += removed;
            }

            variants.RemoveAll(variant => variant.Planes <= 0);

            if (variants.Count == 0)
            {
                variants.Add((Plane, 0));
            }
        }
    }

    /// <summary>A-MIXED: adds planes of a variant, keeping the variants in the game's order (the oldest type first).</summary>
    public void Add(PlaneEquipment plane, int planes)
    {
        if (planes <= 0)
        {
            return;
        }

        var index = variants.FindIndex(variant => variant.Plane.ID == plane.ID);

        if (index >= 0)
        {
            variants[index] = (plane, variants[index].Planes + planes);
            return;
        }

        variants.RemoveAll(variant => variant.Planes <= 0);
        var position = variants.FindIndex(variant => variant.Plane.Order > plane.Order);
        variants.Insert(position < 0 ? variants.Count : position, (plane, planes));
    }

    /// <summary>
    /// A-STATS-CACHE [RE + LOG] 0xf661e0: recomputes the stats from the current mix. The game does this once an hour, at the
    /// start of the hourly air update, so losses and refills during the hour count from the next one.
    /// </summary>
    public void RefreshStats()
    {
        Stats = PlaneStatRules.Mix(variants.Select(variant => (variant.Plane.Stats, variant.Planes)).ToList(), bonus);
    }

    /// <summary>The wing's planes when the battle started: its full strength.</summary>
    public int Size { get; set; }

    /// <summary>A-WINGS-STRIKE: wings in the strike list (their plane allows naval strike, kamikaze or port strike).</summary>
    public bool IsStrikeWing { get; }

    /// <summary>
    /// A-WINGS-STRIKE: strike wings executing naval strike; only these enter the strike averages and take losses. Land
    /// wings join on that mission; carrier wings per <see cref="BattleRules.CarrierWingsExecuteNavalStrike"/>.
    /// </summary>
    public bool ExecutesNavalStrike { get; }

    /// <summary>A-WINGS: fighter-type wings, which defend their side's carriers (A-INTERCEPT). A plane can be both.</summary>
    public bool IsFighter { get; }

    /// <summary>A-WINGS: fighter-type for the sortie bonus of a carrier whose first wing is one (A-SORTIE).</summary>
    public bool IsSortieFighter { get; }

    /// <summary>A-STATS-XP: the wing's experience (wing+0x218); 0 is a fresh wing, which carries the full experience malus.</summary>
    public double Experience { get; set; }

    /// <summary>
    /// A-REGION-LISTS: the wing's mission over the battle's region this air hour (it is on the region's mission list);
    /// null: none (a scrambled-only carrier wing). Its stats then take the mission efficiency and home defence or power
    /// projection (A-STATS-GETTERS).
    /// </summary>
    public RegionMission? RegionMission { get; set; }

    /// <summary>A-REGION: planes the wing lost in this air hour's region air war so far (mission+0x98).</summary>
    public int RegionLosses { get; set; }

    /// <summary>A-REGION: disruption the wing's mission took this air hour (mission+0xb0).</summary>
    public double DisruptionTaken { get; set; }

    /// <summary>
    /// A-REGION-DISRUPT: the disruption left after its side's escorts (mission+0xa8), which lowers its strike efficiency
    /// (0xf78160); reset every air hour.
    /// </summary>
    public double Disruption { get; set; }

    /// <summary>A-ACE: the wing's ace (wing+0x2e8), null without one; a dead ace stays until a new one replaces it.</summary>
    public AirAce? Ace { get; set; }

    /// <summary>
    /// A-ACE-STATS [RE] (0xf61280 and siblings): the live ace's bonus for one modifier, trunc(trunc(effect × min(ACE_WING_SIZE /
    /// planes, ACE_WING_SIZE_MAX_BONUS)) × (1 + ace_effectiveness_factor)); the size factor is 1 for a wing without planes.
    /// </summary>
    public double AceBonus(string modifier, GameDefines defines, double aceEffectivenessFactor = 0)
    {
        if (Ace is not { Alive: true } ace || ace.Template.Effect(modifier) is var effect && effect == 0)
        {
            return 0;
        }

        var air = defines.NAir;
        var size = Planes > 0 ? Math.Min(air.ACE_WING_SIZE / Planes, air.ACE_WING_SIZE_MAX_BONUS) : 1;
        return FixedPoint.Truncate(FixedPoint.Truncate(effect * size) * (1 + aceEffectivenessFactor));
    }
}
