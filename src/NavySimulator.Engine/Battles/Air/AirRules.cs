using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Random;

namespace NavySimulator.Engine.Battles.Air;

/// <summary>
/// A-* [RE] formulas of naval air combat (naval_fire_exchange_air.cpp, hoi4.exe 1.19.3). Spec: docs/mechanics/air.md.
/// </summary>
public static class AirRules
{
    /// <summary>[RE] A-CADENCE: air acts when hourOfDay mod (2 * HOURS_DELAY_AFTER_EACH_COMBAT) equals the region phase.</summary>
    public static bool IsAirHour(int hourOfDay, int regionPhase, GameDefines defines)
    {
        var period = Math.Max(1, (int)Math.Round(2 * defines.NAir.HOURS_DELAY_AFTER_EACH_COMBAT));
        return hourOfDay % period == ((regionPhase % period) + period) % period;
    }

    /// <summary>
    /// [RE] A-SORTIE (0x1976db0, 0x19786c0): carrier sortie efficiency before the final clamp. During the first
    /// SELECTED_SORTIE_INITIAL_TIME hours the stance ratio replaces BASE_CARRIER_SORTIE_EFFICIENCY (the defender uses
    /// 1 - ratio). Weather carrier traffic is not modelled. The group is one wing pool; deck crowding counts all planes
    /// on the carrier (<paramref name="carrierPlanes"/>, A-WINGS-STRIKE).
    /// </summary>
    public static double SortieEfficiency(
        AirGroup group,
        ScreeningSummary ownScreening,
        bool isAttacker,
        int stanceIndex,
        int hour,
        double darkness,
        GameDefines defines,
        double carrierNightTraffic = 0,
        int? carrierPlanes = null)
    {
        var carrier = group.Carrier ?? throw new ArgumentException("Only carrier groups have a sortie efficiency.", nameof(group));
        var navy = defines.NNavy;
        var modifiers = defines.StaticModifiers;
        var ratios = navy.CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO;
        var ratio = ratios.Length == 0 ? 0.5 : ratios[Math.Clamp(stanceIndex, 0, ratios.Length - 1)];

        if (!isAttacker)
        {
            ratio = 1.0 - ratio;
        }

        var baseEfficiency = (hour < navy.SELECTED_SORTIE_INITIAL_TIME ? ratio : navy.BASE_CARRIER_SORTIE_EFFICIENCY);

        // [RE] sortie_efficiency through the member getter: capital_screening_bonus for the carrier line (screening_bonus
        // has none in vanilla), country modifiers (doctrines, spirits) and damaged parts (damaged_flight_deck, S-CRIT-PART).
        baseEfficiency += MemberModifierRules.Get(carrier, StaticModifiers.SortieEfficiency, ownScreening, darkness, defines);

        // [RE] With a fighter wing first, fighter_sortie_efficiency (modifier 0x1b6) through the same getter, and the
        // carrier experience level's.
        if (group.Wings.Count > 0 && group.Wings[0].IsSortieFighter)
        {
            baseEfficiency += CarrierExperienceFighterSortie(carrier, defines) +
                              MemberModifierRules.Get(carrier, StaticModifiers.FighterSortieEfficiency, ownScreening, darkness, defines);
        }

        // [RE] night = clamp(1 + darkness * night.carrier_traffic + (dark ? country carrier_night_traffic : 0), 0, 1).
        var night = Math.Clamp(1.0 + darkness * modifiers.Get(StaticModifiers.Night, StaticModifiers.CarrierTraffic) +
                               (darkness > 0 ? carrierNightTraffic : 0), 0, 1);
        var sortie = Math.Max(0, baseEfficiency * CrowdFactor(carrier, carrierPlanes ?? group.Planes, defines) * night);
        var orgRatio = carrier.CurrentOrganization / CombatStatRules.MaxOrganization(carrier, defines);
        return Math.Max(0, sortie * orgRatio);
    }

    /// <summary>[RE] Deck overcrowding: 1 within capacity, then 1 - CAPACITY_PENALTY * overfill, 0 above 90% overfill.</summary>
    public static double CrowdFactor(Ship carrier, int planes, GameDefines defines)
    {
        var capacity = carrier.GetFinalStats().CarrierSize * defines.NAir.CARRIER_SIZE_STAT_INCREMENT;

        if (planes <= capacity || planes <= 0)
        {
            return 1.0;
        }

        if (capacity <= 0)
        {
            return 0;
        }

        var overfill = (planes - capacity) / capacity;
        return overfill > 0.9 ? 0 : Math.Clamp(1.0 - defines.NAir.CAPACITY_PENALTY * overfill, 0, 1);
    }

    // [RE] carrier_experience_bonus_max / malus_min fighter_sortie_efficiency, scaled like the ship experience modifiers.
    private static double CarrierExperienceFighterSortie(Ship carrier, GameDefines defines)
    {
        var (block, scale) = ExperienceRules.LevelModifier(carrier, defines);
        var carrierBlock = block == StaticModifiers.ShipExperienceMalusMin
            ? StaticModifiers.CarrierExperienceMalusMin
            : StaticModifiers.CarrierExperienceBonusMax;
        return scale * defines.StaticModifiers.Get(carrierBlock, StaticModifiers.FighterSortieEfficiency);
    }

    /// <summary>
    /// [RE] A-STACK (0x1c6a540): planes the enemy fleet allows before the stacking factor applies,
    /// max(NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY, silhouette of visible enemy ships in battle).
    /// </summary>
    public static double PlaneCap(IEnumerable<Ship> enemyShips, GameDefines defines)
    {
        var navy = defines.NNavy;
        var visible = enemyShips.Where(ship => ship.IsInBattle && ship.IsVisible).ToList();
        var capitalValue = navy.SHIP_SILHOUETTE_VALUE_PLANES_CAPITAL * visible.Count(ship => ship.Role == ShipRole.Capital);
        var screenValue = navy.SHIP_SILHOUETTE_VALUE_PLANES_SCREEN * visible.Count(ship => ship.Role == ShipRole.Screen);
        var other = navy.SHIP_SILHOUETTE_VALUE_PLANES_CARRIER * visible.Count(ship => ship.Role == ShipRole.Carrier) +
                    navy.SHIP_SILHOUETTE_VALUE_PLANES_SUBMARINE * visible.Count(ship => ship.Role == ShipRole.Submarine) +
                    navy.SHIP_SILHOUETTE_VALUE_PLANES_CONVOY * visible.Count(ship => ship.Role == ShipRole.Convoy);
        var silhouette = capitalValue + other + Math.Ceiling(screenValue / (1.0 + navy.SCREEN_CAP_REDUCTION_FACTOR * capitalValue));
        return Math.Max(navy.NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY, silhouette);
    }

    /// <summary>[RE] F = 1 / (1 + EFFECT * excess) over the side's carrier planes (land-based planes do not count).</summary>
    public static double StackingFactor(int ownCarrierPlanes, double planeCap, GameDefines defines)
    {
        return ownCarrierPlanes > planeCap
            ? 1.0 / (1.0 + defines.NNavy.NAVAL_COMBAT_PLANE_STACKING_PENALTY_EFFECT * (ownCarrierPlanes - planeCap))
            : 1.0;
    }

    /// <summary>
    /// [RE] A-TARGET (0x197a700): maxHp * (BASE * S + SCALE * (1 - S)) * (1 + STRENGTH * missing HP) + LOW_AA * clamp(5 - AA, 0, 5),
    /// with S the target side's screening efficiency. Hidden submarines are not candidates.
    /// </summary>
    public static double TargetWeight(Ship target, double targetSideScreening, GameDefines defines)
    {
        var navy = defines.NNavy;
        var (baseScore, scaleScore) = target.Role switch
        {
            ShipRole.Submarine => (navy.NAVAL_COMBAT_AIR_SUB_TARGET_BASE, navy.NAVAL_COMBAT_AIR_SUB_TARGET_SCALE),
            ShipRole.Screen => (navy.NAVAL_COMBAT_AIR_SCREEN_TARGET_BASE, navy.NAVAL_COMBAT_AIR_SCREEN_TARGET_SCALE),
            ShipRole.Capital => (navy.NAVAL_COMBAT_AIR_CAPITAL_TARGET_BASE, navy.NAVAL_COMBAT_AIR_CAPITAL_TARGET_SCALE),
            ShipRole.Carrier => (navy.NAVAL_COMBAT_AIR_CARRIER_TARGET_BASE, navy.NAVAL_COMBAT_AIR_CARRIER_TARGET_SCALE),
            _ => (navy.NAVAL_COMBAT_AIR_CONVOY_TARGET_BASE, navy.NAVAL_COMBAT_AIR_CONVOY_TARGET_SCALE)
        };

        var typeScore = baseScore * targetSideScreening + scaleScore * (1.0 - targetSideScreening);
        var stats = target.GetFinalStats();
        var weight = Math.Max(stats.Hp, 0.001) * typeScore *
                     (1.0 + navy.NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE * (1.0 - TargetingRules.HpRatio(target)));
        return weight + navy.NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE * Math.Clamp(5.0 - CombatStatRules.AntiAir(target, defines), 0, 5);
    }

    /// <summary>
    /// [RE] A-INTERCEPT (0x197b380): planes the defending carrier fighters shoot down. Defenders take no losses.
    /// The top-speed bonus term of the speed helper is not decoded and not modelled.
    /// </summary>
    public static int Interception(
        IReadOnlyList<AirWing> attackers,
        IReadOnlyList<AirWing> defenders,
        double defendingShare,
        GameDefines defines,
        IRandomSource random)
    {
        var air = defines.NAir;
        var defendingPlanes = defenders.Sum(wing => wing.Planes);
        var attackingPlanes = attackers.Sum(wing => wing.Planes);

        if (defendingPlanes <= 0 || attackingPlanes <= 0)
        {
            return 0;
        }

        var roll = random.NextDouble(RollKind.Air);
        var defending = air.CARRIER_PERCENTAGE_DEFEND * defendingPlanes * defendingShare;
        var engaged = Math.Min(defending, attackingPlanes * air.COMBAT_MULTIPLANE_CAP);
        var defenderAttack = defenders.Average(wing => wing.Plane.Stats.AirAttack);
        var defenderAgility = defenders.Average(wing => wing.Plane.Stats.Agility);
        var defenderSpeed = defenders.Average(wing => wing.Plane.Stats.Speed);
        var attackerDefence = Math.Max(attackers.Average(wing => wing.Plane.Stats.AirDefense), 0.001);
        var attackerAgility = attackers.Average(wing => wing.Plane.Stats.Agility);
        var attackerSpeed = attackers.Average(wing => wing.Plane.Stats.Speed);

        var damage = engaged * air.CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER * defenderAttack;
        var agilityReduction = attackerAgility > defenderAgility && defenderAgility > 0
            ? (Math.Min(attackerAgility / defenderAgility, air.BIGGEST_AGILITY_FACTOR_DIFF) - 1.0) * damage * air.COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION
            : 0;
        var speedIncrease = defenderSpeed > attackerSpeed && attackerSpeed > 0
            ? (Math.Min(defenderSpeed / attackerSpeed, air.BIGGEST_SPEED_FACTOR_DIFF) - 1.0) * damage * air.COMBAT_BETTER_SPEED_DAMAGE_INCREASE
            : 0;

        var lossesValue = Math.Max(0.001, (damage + speedIncrease - agilityReduction) * 0.01 / attackerDefence);
        var losses = (int)Math.Floor(lossesValue) + (lossesValue - Math.Floor(lossesValue) > roll ? 1 : 0);
        return Math.Min(losses, attackingPlanes);
    }

    /// <summary>
    /// [RE] A-AA-PREEMPT (0x197c8b0): only the target fires; chance ANTI_AIR_TARGETTING_TO_CHANCE * max(1.5 * T - 0.5 * agility, 0.01),
    /// then AA * ANTI_AIR_ATTACK_TO_AMOUNT of the (stacking-scaled) planes, randomly rounded.
    /// </summary>
    public static int PreemptiveAntiAir(double targetAntiAir, double strikeAgility, int planesInReach, GameDefines defines, IRandomSource random)
    {
        var navy = defines.NNavy;
        var targeting = navy.ANTI_AIR_TARGETING;

        if (targeting <= 0 || targetAntiAir <= 0 || planesInReach <= 0)
        {
            return 0;
        }

        var chance = navy.ANTI_AIR_TARGETTING_TO_CHANCE * Math.Max(targeting + 0.5 * (targeting - strikeAgility), 0.01);

        if (random.NextDouble(RollKind.Air) > chance)
        {
            return 0;
        }

        var fraction = Math.Clamp(targetAntiAir * navy.ANTI_AIR_ATTACK_TO_AMOUNT, 0, 1);
        var expected = planesInReach * fraction;
        var kills = (int)Math.Floor(expected);
        return random.NextDouble(RollKind.Air) < expected - kills ? kills + 1 : kills;
    }

    /// <summary>[RE] A-AA-REDUCE (0x196fca0): min(MAX, MULT * (fleet AA term + target AA)^POW).</summary>
    public static double AntiAirReduction(double targetAntiAir, double sideFleetAntiAir, GameDefines defines)
    {
        var navy = defines.NNavy;
        var pool = sideFleetAntiAir + targetAntiAir;
        return pool > 0
            ? Math.Clamp(navy.ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE * Math.Pow(pool, navy.ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE), 0,
                navy.MAX_ANTI_AIR_REDUCTION_EFFECT_ON_INCOMING_AIR_DAMAGE)
            : 0;
    }

    /// <summary>[RE] Side fleet AA term: sum of warship AA * SHIP_TO_FLEET_ANTI_AIR_RATIO * positioning (convoys add nothing).</summary>
    public static double FleetAntiAir(IEnumerable<Ship> sideShips, double positioning, GameDefines defines)
    {
        return sideShips.Where(ship => ship.IsInBattle && ship.Role != ShipRole.Convoy).Sum(ship => CombatStatRules.AntiAir(ship, defines)) *
               defines.NNavy.SHIP_TO_FLEET_ANTI_AIR_RATIO * positioning;
    }

    /// <summary>[RE] A-HIT: hits = round(strikers * clamp(targeting * NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1) * U).</summary>
    public static int Hits(int strikers, double averageTargeting, GameDefines defines, IRandomSource random)
    {
        var fraction = Math.Clamp(averageTargeting * defines.NAir.NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1);
        return (int)Math.Round(strikers * fraction * random.NextDouble(RollKind.Air), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// [RE] A-CRIT (0x197af20): gun crit defines with the piercing bonus always on, × max(0, 1 − the target's
    /// critical_receive_chance) (the opposite sign of the surface rule: the game's behaviour, so a pride of the fleet's −0.5
    /// gives ×1.5); at least 0.001 when the base chance is positive.
    /// </summary>
    public static double CriticalChance(Ship target, double criticalReceiveChance, GameDefines defines)
    {
        var navy = defines.NNavy;
        var reliability = Math.Min(target.GetFinalStats().Reliability, 1);
        var chance = navy.COMBAT_BASE_CRITICAL_CHANCE * (1.0 + navy.COMBAT_ARMOR_PIERCING_CRITICAL_BONUS) * (1.0 - reliability);
        return chance > 0 ? Math.Max(chance * Math.Max(0, 1 - criticalReceiveChance), 0.001) : 0;
    }

    /// <summary>[RE] A-CRIT: one hit's worth is multiplied by 1 + CRIT_MULT * (1 - reliability), i.e. the strike by 1 + that / hits.</summary>
    public static double CriticalMultiplier(Ship target, int hits, GameDefines defines)
    {
        var reliability = Math.Min(target.GetFinalStats().Reliability, 1);
        return 1.0 + (1.0 - reliability) * defines.NNavy.COMBAT_CRITICAL_DAMAGE_MULT / Math.Max(hits, 1);
    }

    /// <summary>[RE] A-EXTERNAL: cap on the land-based planes a side takes in one air hour (the count resets every air hour).</summary>
    public static double ExternalJoinCap(IEnumerable<Ship> enemyShips, int hour, GameDefines defines)
    {
        var air = defines.NAir;
        var enemyHp = enemyShips.Where(ship => ship.IsInBattle && ship.Role != ShipRole.Convoy).Sum(ship => ship.CurrentHP);
        return Math.Max(air.NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP,
            Math.Floor(enemyHp * air.NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO * (1.0 + air.NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO_PER_DAY * (hour / 24))));
    }

    /// <summary>
    /// [RE] Per-plane naval strike stats: the plane stat getters (0xf60f10, 0xf61280, 0xf60bc0) include a x0.1 (agility:
    /// AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY) and x (1 + naval_strike_attack/targetting/agility_factor) of the planes'
    /// country; planes from a carrier also take x (1 + navy_carrier_air_attack/targetting/agility_factor) (A-DMG, A-HIT,
    /// A-AA-PREEMPT). Leader and plane experience terms are not modelled.
    /// </summary>
    public static (double Attack, double Targeting, double Agility) StrikeAverages(
        IReadOnlyList<AirWing> wings,
        GameDefines defines,
        ModifierSet? country = null,
        bool fromCarrier = false)
    {
        var planes = wings.Sum(wing => wing.Planes);

        if (planes <= 0)
        {
            return (0, 0, 0);
        }

        var modifiers = country ?? ModifierSet.Empty;
        double Factor(string strike, string carrier) => (1 + modifiers.Get(strike)) * (fromCarrier ? 1 + modifiers.Get(carrier) : 1);
        double Average(Func<PlaneStats, double> stat) => wings.Sum(wing => stat(wing.Plane.Stats) * wing.Planes) / planes;
        return (0.1 * Average(stats => stats.NavalAttack) * Factor(StaticModifiers.NavalStrikeAttackFactor, StaticModifiers.NavyCarrierAirAttackFactor),
            0.1 * Average(stats => stats.NavalTargeting) * Factor(StaticModifiers.NavalStrikeTargettingFactor, StaticModifiers.NavyCarrierAirTargettingFactor),
            defines.NAir.AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY * Average(stats => stats.Agility) *
            Factor(StaticModifiers.NavalStrikeAgilityFactor, StaticModifiers.NavyCarrierAirAgilityFactor));
    }

    /// <summary>[ASSUMED] Removes lost planes from the wings in order (strike wings first); the game's split is not decoded.</summary>
    public static void RemoveLosses(IEnumerable<AirWing> wings, int losses)
    {
        foreach (var wing in wings)
        {
            if (losses <= 0)
            {
                return;
            }

            var removed = Math.Min(wing.Planes, losses);
            wing.Planes -= removed;
            losses -= removed;
        }
    }
}
