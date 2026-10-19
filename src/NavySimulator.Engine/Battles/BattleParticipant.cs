using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles;

public class BattleParticipant
{
    /// <summary>
    /// S-STATS-COUNTRY: the country modifiers the combat rules read so far (the retreat speeds of S-RETREAT-SPEED, the hit
    /// chance terms of S-HIT-MODS). The loader warns about any other key.
    /// </summary>
    public static IReadOnlySet<string> ModelledCountryModifiers { get; } = new HashSet<string>(
    [
        StaticModifiers.NavalRetreatSpeed, StaticModifiers.NavalRetreatSpeedAfterInitialCombat,
        StaticModifiers.SubRetreatSpeed, StaticModifiers.ConvoyRetreatSpeed,
        StaticModifiers.NavalHitChance, StaticModifiers.NavalLightGunHitChanceFactor,
        StaticModifiers.NavalHeavyGunHitChanceFactor, StaticModifiers.NavalTorpedoHitChanceFactor
    ], StringComparer.Ordinal);

    public readonly Fleet Fleet;
    public readonly string Commander;
    public readonly string Doctrine;
    public readonly int ExternalNavalStrikePlanes;
    public readonly List<Research> Researches;
    public readonly List<Spirit> Spirits;
    public readonly Aggressiveness Aggressiveness;

    /// <summary>S-STATS-COUNTRY [RE + CAL]: the country modifiers of the side's researches, summed. Every ship of the side gets them.</summary>
    public readonly ModifierSet CountryModifiers;

    /// <summary>Recorded with the task force setup; not read in combat (see <see cref="NavySimulator.Engine.Battles.RepairPriority"/>).</summary>
    public readonly RepairPriority RepairPriority;

    /// <summary>Plane type of the land-based naval strike planes (<see cref="ExternalNavalStrikePlanes"/>).</summary>
    public readonly string? ExternalPlaneId;

    /// <summary>A-EXTERNAL: air mission efficiency of the land-based planes, multiplies their attack.</summary>
    public readonly double ExternalMissionEfficiency;

    /// <summary>A-STANCE: index into CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO; null uses CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX.</summary>
    public readonly int? CarrierStanceIndex;

    /// <summary>A-SORTIE: the country's carrier_night_traffic (doctrines, spirits), added to the night factor while dark.</summary>
    public readonly double CarrierNightTraffic;

    public BattleParticipant(
        Fleet fleet,
        string commander,
        string doctrine,
        int externalNavalStrikePlanes,
        List<Research> researches,
        List<Spirit> spirits,
        Aggressiveness aggressiveness = Aggressiveness.EngageAtMediumRisk,
        RepairPriority repairPriority = RepairPriority.High,
        string? externalPlaneId = null,
        double externalMissionEfficiency = 1.0,
        int? carrierStanceIndex = null,
        double carrierNightTraffic = 0)
    {
        Fleet = fleet;
        Commander = commander;
        Doctrine = doctrine;
        ExternalNavalStrikePlanes = externalNavalStrikePlanes;
        Researches = researches;
        Spirits = spirits;
        Aggressiveness = aggressiveness;
        RepairPriority = repairPriority;
        ExternalPlaneId = externalPlaneId;
        ExternalMissionEfficiency = externalMissionEfficiency;
        CarrierStanceIndex = carrierStanceIndex;
        CarrierNightTraffic = carrierNightTraffic;
        CountryModifiers = researches.Aggregate(ModifierSet.Empty, (current, research) => current.Add(research.CountryModifiers));

        ApplyFleetModifiers();
    }

    private void ApplyFleetModifiers()
    {
        foreach (var ship in Fleet.Ships)
        {
            var role = ship.Design.Role;
            var hullTypes = ship.Design.Types;

            var applicableResearches = Researches.Where(research => research.AppliesTo(role, hullTypes)).ToList();
            var applicableSpirits = Spirits.Where(spirit => spirit.AppliesTo(role, hullTypes)).ToList();

            // S-STATS-BONUS / S-STATS-SHIP: equipment bonuses go into the design, technologies onto the ship.
            var equipmentBonuses = applicableResearches.Where(research => research.IsEquipmentBonus)
                .Select(research => research.Values)
                .ToList();
            var technology = applicableResearches.Where(research => !research.IsEquipmentBonus)
                .Aggregate(new ShipStats(), (current, research) => current.Add(research.Values));

            var spiritModifiers = applicableSpirits.Aggregate(new ShipStats(), (current, spirit) => current.Add(spirit.StatModifiers));
            var spiritAverages = ShipStats.AverageNonZero(applicableSpirits.Select(spirit => spirit.StatAverages));
            var spiritMultipliers = applicableSpirits.Aggregate(new ShipStats(), (current, spirit) => current.Add(spirit.StatMultipliers));

            ship.ApplyExternalModifiers(equipmentBonuses, technology, spiritModifiers, spiritAverages, spiritMultipliers);

            // S-STATS-COUNTRY [RE]: the ship modifier sum 0xc38760 gathers the country's modifiers for every ship.
            ship.CountryModifiers = CountryModifiers;
        }
    }
}
