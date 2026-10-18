namespace NavySimulator.Engine.Battles.Air;

/// <summary>One air wing in a naval battle; planes are removed as they are shot down.</summary>
public sealed class AirWing
{
    public AirWing(PlaneEquipment plane, AirwingType type, int planes)
    {
        Plane = plane;
        Type = type;
        Planes = planes;
    }

    public PlaneEquipment Plane { get; }
    public AirwingType Type { get; }
    public int Planes { get; set; }

    /// <summary>Wings that strike ships (naval bombers). Fighters fly but only defend.</summary>
    public bool IsStrikeWing => Type == AirwingType.Bomber;
    public bool IsFighter => Type == AirwingType.Fighter;
}
