using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Stats;

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
    /// 1 - ratio). The region's weather and the night are separate clamped factors (S-WEATHER: carrier_traffic of the
    /// region's weather holder, whole). The group is one wing pool; deck crowding counts all planes on the carrier
    /// (<paramref name="carrierPlanes"/>, A-WINGS-STRIKE).
    /// </summary>
    public static double SortieEfficiency(
        AirGroup group,
        ScreeningSummary ownScreening,
        bool isAttacker,
        int stanceIndex,
        int hour,
        BattleEnvironment environment,
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

        // [RE + LOG] The game compares its hour counter (combat+0x44), which is one ahead of the battle hour during the
        // update (S-HOUR), so the stance base ends after battle hour SELECTED_SORTIE_INITIAL_TIME - 2.
        var baseEfficiency = (hour + 1 < navy.SELECTED_SORTIE_INITIAL_TIME ? ratio : navy.BASE_CARRIER_SORTIE_EFFICIENCY);

        // [RE] sortie_efficiency through the member getter: capital_screening_bonus for the carrier line (screening_bonus
        // has none in vanilla), country modifiers (doctrines, spirits) and damaged parts (damaged_flight_deck, S-CRIT-PART).
        baseEfficiency += MemberModifierRules.Get(carrier, StaticModifiers.SortieEfficiency, ownScreening, environment, defines);

        // [RE] With a fighter wing first, fighter_sortie_efficiency (modifier 0x1b6) through the same getter, and the
        // carrier experience level's.
        if (group.Wings.Count > 0 && group.Wings[0].IsSortieFighter)
        {
            baseEfficiency += CarrierExperienceFighterSortie(carrier, defines) +
                              MemberModifierRules.Get(carrier, StaticModifiers.FighterSortieEfficiency, ownScreening, environment, defines);
        }

        // [RE] night = clamp(1 + darkness * night.carrier_traffic + (dark ? country carrier_night_traffic : 0), 0, 1).
        var night = Math.Clamp(1.0 + environment.Darkness * modifiers.Get(StaticModifiers.Night, StaticModifiers.CarrierTraffic) +
                               (environment.IsDark ? carrierNightTraffic : 0), 0, 1);
        // [RE] 0x1976f84: weather = clamp(1 + carrier_traffic of the region's weather holder, 0, 1).
        var weather = Math.Clamp(1.0 + environment.WeatherModifier(StaticModifiers.CarrierTraffic), 0, 1);
        var sortie = Math.Max(0, baseEfficiency * CrowdFactor(carrier, carrierPlanes ?? group.Planes, defines) * weather * night);
        var orgRatio = carrier.CurrentOrganization / CombatStatRules.MaxOrganization(carrier, defines);
        return Math.Max(0, sortie * orgRatio);
    }

    /// <summary>
    /// [RE] Deck overcrowding: 1 within capacity, then 1 - CAPACITY_PENALTY × (1 + the country's
    /// carrier_capacity_penalty_reduction) × overfill (A-STATS-MODS 0xc57a89), 0 above 90% overfill.
    /// </summary>
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
        var penalty = defines.NAir.CAPACITY_PENALTY * (1 + carrier.CountryModifiers.Get(StaticModifiers.CarrierCapacityPenaltyReduction));
        return overfill > 0.9 ? 0 : Math.Clamp(1.0 - penalty * overfill, 0, 1);
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

        // Each product truncated to the game's fx5, as for the surface target weight (the pick walks fx5 integers).
        var typeScore = FixedPoint.Truncate(FixedPoint.Truncate(baseScore * targetSideScreening) +
                                            FixedPoint.Truncate(scaleScore * (1.0 - targetSideScreening)));
        var stats = target.GetFinalStats();
        var missing = 1.0 - FixedPoint.Divide(target.CurrentHP, Math.Max(stats.Hp, 0.001));
        var weight = FixedPoint.Truncate(FixedPoint.Truncate(Math.Max(stats.Hp, 0.001) * typeScore) *
                                         FixedPoint.Truncate(1.0 + navy.NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE * missing));
        return weight + FixedPoint.Truncate(navy.NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE *
                                            Math.Clamp(5.0 - CombatStatRules.AntiAir(target, defines), 0, 5));
    }

    /// <summary>
    /// [RE] A-TARGET pick (0x197ac19, 0x24f04f0): an integer draw n (line 0x43f) gives <c>p = trunc(n / 2^31 × Σw)</c>
    /// over the fx5 weights, and the first candidate in list order whose running sum exceeds p is the target. Unlike the
    /// surface pick (S-TARGET) the heaviest candidate does not come first.
    /// </summary>
    public static Ship? PickTarget(IReadOnlyList<(Ship Target, double Weight)> candidates, IRandomSource random)
    {
        var weights = candidates.Select(candidate => Math.Max(0, FixedPoint.Raw(candidate.Weight))).ToList();
        var total = weights.Sum();

        if (total <= 0)
        {
            return null;
        }

        var point = (long)(random.NextDouble(RollKind.AirTargetPick) * total);
        var cumulative = 0L;

        for (var i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];

            if (weights[i] > 0 && point < cumulative)
            {
                return candidates[i].Target;
            }
        }

        return null;
    }

    /// <summary>
    /// [RE] A-INTERCEPT (0x197b380): planes the defending carrier fighters shoot down. Defenders take no losses.
    /// The top-speed bonus term of the speed helper is not decoded and not modelled. [RE + LOG] The roll is made whenever
    /// there is a defending wing, with or without planes; the averages are per wing.
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

        if (defenders.Count == 0 || attackers.Count == 0)
        {
            return 0;
        }

        var roll = random.NextDouble(RollKind.AirInterception);
        var defending = air.CARRIER_PERCENTAGE_DEFEND * defendingPlanes * defendingShare;
        var engaged = Math.Min(defending, attackingPlanes * air.COMBAT_MULTIPLANE_CAP);
        var defenderAttack = defenders.Average(wing => wing.Stats.AirAttack);
        var defenderAgility = defenders.Average(wing => wing.Stats.Agility);
        var defenderSpeed = defenders.Average(wing => wing.Stats.Speed);
        var attackerDefence = Math.Max(attackers.Average(wing => wing.Stats.AirDefense), 0.001);
        var attackerAgility = attackers.Average(wing => wing.Stats.Agility);
        var attackerSpeed = attackers.Average(wing => wing.Stats.Speed);

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
    /// then AA * ANTI_AIR_ATTACK_TO_AMOUNT of the (stacking-scaled) planes, randomly rounded. In fx5, products truncated:
    /// [CAL] TFB's T 0.4 against agility 0.7 makes the chance exactly 0.05, which the constant-roll battles at U 0.05 hit
    /// (the game fires on equality; in doubles the chance was 0.04999999999999999).
    /// </summary>
    public static int PreemptiveAntiAir(double targetAntiAir, double strikeAgility, int planesInReach, GameDefines defines, IRandomSource random)
    {
        var navy = defines.NNavy;
        var targeting = navy.ANTI_AIR_TARGETING;

        if (targeting <= 0 || targetAntiAir <= 0 || planesInReach <= 0)
        {
            return 0;
        }

        var agility = FixedPoint.Truncate(strikeAgility);
        var chance = FixedPoint.Truncate(navy.ANTI_AIR_TARGETTING_TO_CHANCE *
                                         Math.Max(targeting + FixedPoint.Truncate(0.5 * (targeting - agility)), 0.01));

        if (random.NextDouble(RollKind.AirAntiAirChance) > chance)
        {
            return 0;
        }

        var fraction = Math.Clamp(FixedPoint.Truncate(targetAntiAir * navy.ANTI_AIR_ATTACK_TO_AMOUNT), 0, 1);
        var expected = FixedPoint.Truncate(planesInReach * fraction);
        var kills = (int)Math.Floor(expected);
        return random.NextDouble(RollKind.AirAntiAirKills) < expected - kills ? kills + 1 : kills;
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

    /// <summary>
    /// [RE + LOG] A-AA-REDUCE side fleet AA term (side+0x170, set in the side update with positioning): sum of warship AA
    /// * SHIP_TO_FLEET_ANTI_AIR_RATIO * positioning over the side's members that are afloat, escaped ships included
    /// (they stay in the group's member list; sunk ones do not count). Convoys add nothing.
    /// </summary>
    public static double FleetAntiAir(IEnumerable<Ship> sideShips, double positioning, GameDefines defines)
    {
        return sideShips.Where(ship => !ship.IsSunk && ship.Role != ShipRole.Convoy).Sum(ship => CombatStatRules.AntiAir(ship, defines)) *
               defines.NNavy.SHIP_TO_FLEET_ANTI_AIR_RATIO * positioning;
    }

    /// <summary>
    /// [RE] A-HIT: hits = round(strikers * clamp(targeting * tMult * NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1) * U), tMult
    /// from <see cref="TargetingMultiplier"/>.
    /// </summary>
    public static int Hits(int strikers, double averageTargeting, GameDefines defines, IRandomSource random, double targetingMultiplier = 1.0)
    {
        var fraction = Math.Clamp(averageTargeting * targetingMultiplier * defines.NAir.NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1);
        return (int)Math.Round(strikers * fraction * random.NextDouble(RollKind.AirHits), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// [RE] A-HIT 0x197bf38: <c>tMult = 1 + naval_strike(region weather holder) + (1 + airfx+0xe8) × naval_strike(night
    /// holder)</c>, both whole (S-WEATHER: rain light −0.2). airfx+0xe8 (a plane-weighted equipment field) is not
    /// identified and taken as 0 [ASSUMED]; neither vanilla's nor TFB's night block has naval_strike.
    /// </summary>
    public static double TargetingMultiplier(BattleEnvironment environment, GameDefines defines)
    {
        return 1.0 + environment.WeatherModifier(StaticModifiers.NavalStrike) +
               environment.Darkness * defines.StaticModifiers.Get(StaticModifiers.Night, StaticModifiers.NavalStrike);
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
    /// [RE] Naval strike stat averages (0x1978e30) over the selected wings. Per wing, the plane stat getters (0xf60f10,
    /// 0xf61280, 0xf60bc0) give 0.1 x the stat (agility: AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY) x (1 + the country's
    /// naval_strike_attack/targetting/agility_factor + the wing's live ace bonus, A-ACE-STATS); each wing weighs
    /// n = round(planes x poolPlanes / selected planes), and its attack also its mission efficiency (0xf78160:
    /// 1 unless disrupted; <paramref name="efficiency"/>). Planes from a carrier then take x (1 +
    /// navy_carrier_air_attack/targetting/agility_factor) (A-DMG, A-HIT, A-AA-PREEMPT). Leader terms, the weather and
    /// night region terms and air doctrines are not modelled.
    /// </summary>
    public static (double Attack, double Targeting, double Agility) StrikeAverages(
        IReadOnlyList<AirWing> wings,
        GameDefines defines,
        ModifierSet? country = null,
        bool fromCarrier = false,
        int? poolPlanes = null,
        double efficiency = 1.0)
    {
        var selected = wings.Sum(wing => wing.Planes);

        if (selected <= 0)
        {
            return (0, 0, 0);
        }

        var modifiers = country ?? ModifierSet.Empty;
        var aceFactor = modifiers.Get(StaticModifiers.AceEffectivenessFactor);
        var total = poolPlanes ?? selected;
        double weights = 0, attack = 0, targeting = 0, agility = 0;

        foreach (var wing in wings)
        {
            // A-STATS-MISSION-EFF [RE] 0xf7ed40: the mission efficiency by the wing's mission type joins attack and agility;
            // carrier wings have no mission type (M = 0), land-based naval strike wings take air_nav_efficiency.
            var mission = fromCarrier || !wing.ExecutesNavalStrike ? 0 : modifiers.Get(StaticModifiers.AirNavEfficiency);
            double Stat(double value, string modifier, double missionEfficiency = 0) =>
                value * (1 + modifiers.Get(modifier) + wing.AceBonus(modifier, defines, aceFactor) + missionEfficiency);

            var n = Math.Round((double)wing.Planes * total / selected, MidpointRounding.AwayFromZero);
            var stats = wing.Stats;
            // A-REGION-DISRUPT: a disrupted mission strikes at the efficiency of 0xf78160 with the wing's weight as planes.
            var wingEfficiency = wing.Disruption > 0 ? RegionAirWarRules.StrikeEfficiency(wing.Disruption, n, wing, defines, modifiers) : efficiency;
            attack += Stat(0.1 * stats.NavalAttack, StaticModifiers.NavalStrikeAttackFactor, mission) * n * wingEfficiency;
            targeting += Stat(0.1 * stats.NavalTargeting, StaticModifiers.NavalStrikeTargettingFactor) * n;
            agility += Stat(defines.NAir.AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY * stats.Agility, StaticModifiers.NavalStrikeAgilityFactor, mission) * n;
            weights += n;
        }

        if (weights <= 0)
        {
            return (0, 0, 0);
        }

        double Carrier(string modifier) => fromCarrier ? 1 + modifiers.Get(modifier) : 1;
        return (attack / weights * Carrier(StaticModifiers.NavyCarrierAirAttackFactor),
            targeting / weights * Carrier(StaticModifiers.NavyCarrierAirTargettingFactor),
            agility / weights * Carrier(StaticModifiers.NavyCarrierAirAgilityFactor));
    }

    /// <summary>
    /// [RE] A-ACE-ROLLS (0xf80f60): the chance a wing without a live ace earns one after a strike that dealt
    /// <paramref name="damage"/> (HP lost, truncated) while losing fewer planes: trunc((ACE_EARN_CHANCE_BASE +
    /// trunc(trunc(damage × ACE_EARN_CHANCE_PLANES_MULT) × 0.1)) × (1 + air_ace_generation_chance_factor)).
    /// </summary>
    public static double AceEarnChance(int damage, double generationFactor, GameDefines defines)
    {
        var air = defines.NAir;
        var perDamage = FixedPoint.Truncate(FixedPoint.Truncate(damage * air.ACE_EARN_CHANCE_PLANES_MULT) * defines.Hardcoded.AceChancePlanesScale);
        return FixedPoint.Truncate((air.ACE_EARN_CHANCE_BASE + perDamage) * (1 + generationFactor));
    }

    /// <summary>[RE] A-ACE-ROLLS: the chance a live ace dies after a strike that lost planes.</summary>
    public static double AceDeathChance(int losses, GameDefines defines)
    {
        var air = defines.NAir;
        return air.ACE_DEATH_CHANCE_BASE +
               FixedPoint.Truncate(FixedPoint.Truncate(losses * air.ACE_DEATH_CHANCE_PLANES_MULT) * defines.Hardcoded.AceChancePlanesScale);
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
