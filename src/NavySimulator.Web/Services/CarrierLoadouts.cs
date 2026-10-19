using NavySimulator.Engine;
using NavySimulator.Engine.Defines;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>
/// A-WINGS: editing the air wings of each carrier of a fleet (<see cref="FleetDto.CarrierAirwings"/>: per design, one
/// loadout per ship in ship order).
/// </summary>
public static class CarrierLoadouts
{
    /// <summary>Planes per wing when a wing gives none (as the loader).</summary>
    public const int DefaultWingPlanes = 10;

    /// <summary>The loadout of a design's carrier (0-based), or null when it has none.</summary>
    public static CarrierLoadoutDto? Find(FleetDto fleet, string designId, int index)
    {
        return fleet.CarrierAirwings.TryGetValue(designId, out var loadouts) && index < loadouts.Count ? loadouts[index] : null;
    }

    /// <summary>The loadout of a design's carrier (0-based), created (with empty ones before it) when missing.</summary>
    public static CarrierLoadoutDto Get(FleetDto fleet, string designId, int index)
    {
        if (!fleet.CarrierAirwings.TryGetValue(designId, out var loadouts))
        {
            loadouts = [];
            fleet.CarrierAirwings[designId] = loadouts;
        }

        while (loadouts.Count <= index)
        {
            loadouts.Add(new CarrierLoadoutDto());
        }

        return loadouts[index];
    }

    public static int Wings(CarrierLoadoutDto? loadout)
    {
        return loadout?.Wings.Sum(wing => wing.Airwings) ?? 0;
    }

    public static int Planes(CarrierLoadoutDto? loadout)
    {
        return loadout?.Wings.Sum(wing => wing.Airwings * PlanesPerWing(wing)) ?? 0;
    }

    public static int PlanesPerWing(CarrierAirwingAssignmentDto wing)
    {
        return wing.PlaneCount == 0 ? DefaultWingPlanes : wing.PlaneCount;
    }

    public static void AddWing(FleetDto fleet, string designId, int index, string planeId)
    {
        Get(fleet, designId, index).Wings.Add(new CarrierAirwingAssignmentDto { PlaneID = planeId, Airwings = 1, PlaneCount = DefaultWingPlanes });
    }

    public static void RemoveWing(FleetDto fleet, string designId, int index, CarrierAirwingAssignmentDto wing)
    {
        Find(fleet, designId, index)?.Wings.Remove(wing);
        Trim(fleet, designId);
    }

    /// <summary>Gives every carrier of the fleet (of <paramref name="carrierDesignIds"/>) a copy of one carrier's loadout.</summary>
    public static void CopyToAll(FleetDto fleet, string designId, int index, IEnumerable<string> carrierDesignIds)
    {
        var source = Find(fleet, designId, index)?.Wings ?? [];

        foreach (var carrierDesign in carrierDesignIds.ToList())
        {
            var count = fleet.ShipDesigns.GetValueOrDefault(carrierDesign);
            fleet.CarrierAirwings[carrierDesign] = Enumerable.Range(0, count)
                .Select(_ => new CarrierLoadoutDto { Wings = source.Select(Copy).ToList() })
                .ToList();
            Trim(fleet, carrierDesign);
        }
    }

    /// <summary>Drops loadouts beyond the design's ship count and trailing empty ones; removes the design when none is left.</summary>
    public static void Trim(FleetDto fleet, string designId)
    {
        if (!fleet.CarrierAirwings.TryGetValue(designId, out var loadouts))
        {
            return;
        }

        var count = fleet.ShipDesigns.GetValueOrDefault(designId);

        if (loadouts.Count > count)
        {
            loadouts.RemoveRange(count, loadouts.Count - count);
        }

        while (loadouts.Count > 0 && loadouts[^1].Wings.Count == 0)
        {
            loadouts.RemoveAt(loadouts.Count - 1);
        }

        if (loadouts.Count == 0)
        {
            fleet.CarrierAirwings.Remove(designId);
        }
    }

    /// <summary>A-WINGS: what a plane does in a naval battle: strikes, defends as a fighter, both, or neither.</summary>
    public static string Role(PlaneEquipment plane, HardcodedValues values)
    {
        return (plane.IsNavalStriker(values), plane.IsFighterType(values)) switch
        {
            (true, true) => "strike + fighter",
            (true, false) => "strike",
            (false, true) => "fighter",
            _ => "no naval role"
        };
    }

    private static CarrierAirwingAssignmentDto Copy(CarrierAirwingAssignmentDto wing)
    {
        return new CarrierAirwingAssignmentDto { PlaneID = wing.PlaneID, Airwings = wing.Airwings, PlaneCount = wing.PlaneCount };
    }
}
