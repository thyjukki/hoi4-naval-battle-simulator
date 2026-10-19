using NavySimulator.Engine.Defines;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>One air wing in a naval battle; planes are removed as they are shot down.</summary>
public sealed class AirWing
{
    public AirWing(PlaneEquipment plane, int planes, HardcodedValues values, bool executesNavalStrike = true)
    {
        Plane = plane;
        Planes = planes;
        Size = planes;
        IsStrikeWing = plane.IsInStrikeList(values);
        ExecutesNavalStrike = IsStrikeWing && executesNavalStrike && plane.IsNavalStriker(values);
        IsFighter = plane.IsFighterType(values);
        IsSortieFighter = plane.IsSortieFighterType(values);
    }

    public PlaneEquipment Plane { get; }
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
}
