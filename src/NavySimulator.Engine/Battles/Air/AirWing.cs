using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>One air wing in a naval battle; planes are removed as they are shot down.</summary>
public sealed class AirWing
{
    public AirWing(PlaneEquipment plane, int planes, HardcodedValues values, bool executesNavalStrike = true, PlaneStats? stats = null)
    {
        Plane = plane;
        Stats = stats ?? plane.Stats;
        Planes = planes;
        Size = planes;
        IsStrikeWing = plane.IsInStrikeList(values);
        ExecutesNavalStrike = IsStrikeWing && executesNavalStrike && plane.IsNavalStriker(values);
        IsFighter = plane.IsFighterType(values);
        IsSortieFighter = plane.IsSortieFighterType(values);
    }

    public PlaneEquipment Plane { get; }

    /// <summary>A-STATS: the plane's stats with its side's sub-unit bonuses (doctrines, spirits).</summary>
    public PlaneStats Stats { get; }
    public int Planes { get; set; }

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
