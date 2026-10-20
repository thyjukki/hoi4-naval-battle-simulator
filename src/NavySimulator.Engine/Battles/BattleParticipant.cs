using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Doctrines;
using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Mios;
using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Battles;

public class BattleParticipant
{
    /// <summary>
    /// S-STATS-COUNTRY: the country modifiers the combat rules read so far: the retreat speeds of S-RETREAT-SPEED, the hit
    /// chance terms of S-HIT-MODS, the critical chance terms of S-CRIT, the part effect factor of S-CRIT-PART, the
    /// S-STATS-COMBAT getters (speed, organization, regain, retreat chance, attack and armor, AA, visibility), the torpedo
    /// cooldown, manpower loss, positioning, screening, torpedo reach and reveal terms, and the air sortie and strike terms.
    /// The loader warns about any other key, except <see cref="CountryModifiersWithoutBattleEffect"/>.
    /// </summary>
    public static IReadOnlySet<string> ModelledCountryModifiers { get; } = new HashSet<string>(
    [
        StaticModifiers.NavalRetreatSpeed, StaticModifiers.NavalRetreatSpeedAfterInitialCombat,
        StaticModifiers.SubRetreatSpeed, StaticModifiers.ConvoyRetreatSpeed,
        StaticModifiers.NavalHitChance, StaticModifiers.NavalLightGunHitChanceFactor,
        StaticModifiers.NavalHeavyGunHitChanceFactor, StaticModifiers.NavalTorpedoHitChanceFactor,
        StaticModifiers.NavalCriticalScoreChanceFactor, StaticModifiers.CriticalReceiveChance,
        StaticModifiers.NavalCriticalEffectFactor, StaticModifiers.NavalSpeedFactor, StaticModifiers.NavyOrg,
        StaticModifiers.NavyOrgFactor, StaticModifiers.NavalMorale, StaticModifiers.NavalMoraleFactor,
        StaticModifiers.NavalRetreatChance, StaticModifiers.NavalRetreatChanceAfterInitialCombat,
        StaticModifiers.SortieEfficiency, StaticModifiers.FighterSortieEfficiency, StaticModifiers.CarrierNightTraffic,
        StaticModifiers.NavalStrikeAttackFactor, StaticModifiers.NavalStrikeTargettingFactor, StaticModifiers.NavalStrikeAgilityFactor,
        StaticModifiers.NavyCarrierAirAttackFactor, StaticModifiers.NavyCarrierAirTargettingFactor, StaticModifiers.NavyCarrierAirAgilityFactor,
        StaticModifiers.Positioning, StaticModifiers.ScreeningEfficiency, StaticModifiers.ScreeningWithoutScreens,
        StaticModifiers.NavalTorpedoScreenPenetrationFactor, StaticModifiers.NavalTorpedoRevealChanceFactor,
        StaticModifiers.NavalTorpedoCooldownFactor, StaticModifiers.SubmarineAttack, StaticModifiers.NavySubmarineAttackFactor,
        StaticModifiers.NavySubmarineDefenceFactor, StaticModifiers.NavyCapitalShipAttackFactor, StaticModifiers.NavyCapitalShipDefenceFactor,
        StaticModifiers.NavyScreenAttackFactor, StaticModifiers.NavyScreenDefenceFactor, StaticModifiers.NavalNightAttack,
        StaticModifiers.NavalDamageFactor, StaticModifiers.NavalDefenseFactor, StaticModifiers.NavyAntiAirAttack,
        StaticModifiers.NavyAntiAirAttackFactor, StaticModifiers.NavyVisibility, StaticModifiers.NavyCasualtyOnHit,
        StaticModifiers.ExperienceGainNavyUnit, StaticModifiers.ExperienceGainNavyUnitFactor, StaticModifiers.NavyWeatherPenalty,
        // A-STATS-MODS, A-DETECT: air keys the naval strike, carrier sorties and the region's air war read.
        StaticModifiers.AirAttackFactor, StaticModifiers.AirDefenceFactor, StaticModifiers.AirAgilityFactor, StaticModifiers.AirEscortEfficiency,
        StaticModifiers.AceEffectivenessFactor, StaticModifiers.AirAceGenerationChanceFactor, StaticModifiers.AirSuperiorityEfficiency,
        StaticModifiers.AirNavEfficiency, StaticModifiers.AirHomeDefenceFactor, StaticModifiers.AirPowerProjectionFactor,
        StaticModifiers.AirSuperiorityDetectFactor, StaticModifiers.AirInterceptionDetectFactor, StaticModifiers.AirUntrainedPilotsPenaltyFactor,
        StaticModifiers.AirNightPenalty, StaticModifiers.AirWeatherPenalty, StaticModifiers.CarrierCapacityPenaltyReduction
    ], StringComparer.Ordinal);

    /// <summary>
    /// S-STATS-COUNTRY: whether combat reads the country modifier: <see cref="ModelledCountryModifiers"/>, or a sub-unit's
    /// experience_gain_&lt;sub-unit&gt;_combat_factor (S-XP-GAIN).
    /// </summary>
    public static bool IsModelledCountryModifier(string key)
    {
        return ModelledCountryModifiers.Contains(key) ||
               (key.StartsWith("experience_gain_", StringComparison.Ordinal) && key.EndsWith("_combat_factor", StringComparison.Ordinal));
    }

    /// <summary>
    /// [RE] Country modifiers with no effect in battle: the game reads them only from an admiral's own modifiers
    /// (fleet-size penalty multiplier, pride of the fleet, enemy retreat chance), only outside combat (casualties on
    /// sinking go to the country's pool), or nowhere in combat (naval_coordination). Spec: S-STATS-COUNTRY.
    /// </summary>
    public static IReadOnlySet<string> CountryModifiersWithoutBattleEffect { get; } = new HashSet<string>(
    [
        StaticModifiers.NavalEnemyFleetSizeRatioPenaltyFactor, "naval_has_potf_in_combat_attack", "naval_has_potf_in_combat_defense",
        StaticModifiers.NavalEnemyRetreatChance, "navy_casualty_on_sink", "naval_coordination",
        // [RE] A-STATS-MODS: air keys with no reader in a naval battle: the country's carrier_traffic (only the weather and
        // night holders count) and air_maximum_speed_factor (only an ace's), fleet spotting before the battle, port
        // strikes, and the land, strategic bombing, mine, intel, cost and research keys of the air doctrines and spirits.
        StaticModifiers.CarrierTraffic, StaticModifiers.AirMaximumSpeedFactor, "spotting_chance", "night_spotting_chance", "port_strike",
        "air_cas_efficiency", "air_cas_present_factor", "army_bonus_air_superiority_factor", "air_close_air_support_org_damage_factor",
        "ground_attack_factor", "air_strategic_bomber_bombing_factor", "air_strategic_bomber_defence_factor", "air_strategic_bomber_night_penalty",
        "strategic_bomb_visibility", "air_training_xp_gain_factor", "experience_gain_air", "experience_gain_air_factor",
        "air_manpower_requirement_factor", "mines_planting_by_air_factor", "mines_sweeping_by_air_factor", "intel_from_combat_factor",
        "army_intel_factor", "navy_intel_factor", "airforce_intel_factor", "civilian_intel_factor", "airforce_intel_to_others",
        "air_advisor_cost_factor", "fighter_aircraft_track_mastery_gain_factor", "strike_aircraft_track_mastery_gain_factor",
        "medium_aircraft_track_mastery_gain_factor", "heavy_aircraft_track_mastery_gain_factor"
    ], StringComparer.Ordinal);

    /// <summary>
    /// S-ADMIRAL [RE]: modifiers the game reads only from an admiral, and the combat rules do: the enemy's fleet-size
    /// penalty factor (S-POS-SIZE), the enemy's retreat chance (S-RETREAT) and the pride of the fleet terms (S-POTF).
    /// </summary>
    public static IReadOnlySet<string> ModelledAdmiralOnlyModifiers { get; } = new HashSet<string>(
    [
        StaticModifiers.NavalEnemyFleetSizeRatioPenaltyFactor, StaticModifiers.NavalEnemyRetreatChance, AdmiralRules.SkillBonusFactor,
        StaticModifiers.NavalHasPotfInCombatAttack, StaticModifiers.NavalHasPotfInCombatDefense
    ], StringComparer.Ordinal);

    /// <summary>
    /// S-ADMIRAL [RE]: admiral modifiers with no effect in battle: naval_coordination (no combat reader), keys the game
    /// reads from the country alone (screening_without_screens; the plane getters' naval strike and carrier air factors,
    /// 0xf60f10, 0x1978e30), and the experience, promotion and reassignment keys of leader bookkeeping.
    /// </summary>
    public static IReadOnlySet<string> AdmiralModifiersWithoutBattleEffect { get; } = new HashSet<string>(
    [
        "naval_coordination", StaticModifiers.ScreeningWithoutScreens, StaticModifiers.NavalStrikeAttackFactor,
        StaticModifiers.NavalStrikeTargettingFactor, StaticModifiers.NavalStrikeAgilityFactor, StaticModifiers.NavyCarrierAirAttackFactor,
        StaticModifiers.NavyCarrierAirTargettingFactor, StaticModifiers.NavyCarrierAirAgilityFactor, "experience_gain_factor",
        "promote_cost_factor", "reassignment_duration_factor", "cannot_use_abilities"
    ], StringComparer.Ordinal);

    public readonly Fleet Fleet;

    /// <summary>S-ADMIRAL: the admiral commanding the side's fleet (the side's commanding admiral); null: none.</summary>
    public readonly Admiral? Admiral;

    /// <summary>S-POTF: the ship of the fleet that is its country's pride of the fleet; null: none.</summary>
    public readonly Ship? PrideOfTheFleet;
    public readonly List<Research> Researches;
    public readonly List<Spirit> Spirits;
    public readonly Aggressiveness Aggressiveness;

    /// <summary>S-DOCTRINE: the side's doctrines, one setup per folder (naval and air).</summary>
    public readonly IReadOnlyList<DoctrineSetup> Doctrines;

    /// <summary>S-MIO: the side's MIOs (unlocked traits and policy). A design's MIO without a setup gives its initial trait only.</summary>
    public readonly IReadOnlyList<MioSetup> Mios;

    /// <summary>
    /// S-MIO: the MIOs the side assigns to designs, by design ID, in place of the design's own (null: none). Designs
    /// left out keep <see cref="ShipDesign.Mio"/>.
    /// </summary>
    public readonly IReadOnlyDictionary<string, Mio?> DesignMios;

    /// <summary>
    /// S-DOCTRINE-EFFECT: the effects the side's doctrines (every folder) and spirits give, each with its source (e.g.
    /// <c>line_of_battle/flagships</c>).
    /// </summary>
    public readonly IReadOnlyList<(string Source, CountryEffects Effects)> ActiveEffects;

    /// <summary>
    /// S-STATS-COUNTRY [RE + CAL]: the country modifiers of the side's researches, doctrines and spirits, summed. Every
    /// ship of the side gets them.
    /// </summary>
    public readonly ModifierSet CountryModifiers;

    /// <summary>Recorded with the task force setup; not read in combat (see <see cref="NavySimulator.Engine.Battles.RepairPriority"/>).</summary>
    public readonly RepairPriority RepairPriority;

    /// <summary>A-EXTERNAL: the side's land-based planes, one entry per air base, in the order they join.</summary>
    public readonly IReadOnlyList<LandBasedAirwing> LandBasedAirwings;

    /// <summary>A-REINFORCE: the side's planes in stock, by plane ID, that can refill its air wings.</summary>
    public readonly IReadOnlyDictionary<string, int> PlaneStockpile;

    /// <summary>A-STANCE: index into CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO; null uses CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX.</summary>
    public readonly int? CarrierStanceIndex;

    /// <summary>
    /// A-SORTIE: the country's carrier_night_traffic, added to the night factor while dark: the scenario's value plus the
    /// country modifier of the side's doctrines and spirits.
    /// </summary>
    public readonly double CarrierNightTraffic;

    /// <summary>
    /// A-REGION-SYNC [RE + LOG]: the side's carrier wings fly a mission over the battle's region (they are on the region's
    /// mission list): every wing then fights in the region's air war and can be disrupted; otherwise they are only
    /// scrambled (their fighters attack, every wing defends, none is disrupted). The game gives the wings the region at
    /// the task force's naval mission tick out of combat (0xfbe7f0), so the side whose tick starts the battle has them and
    /// a side already in combat does not. Null: the game's case for a battle-start save, the attacker yes, the defender no.
    /// </summary>
    public readonly bool? CarrierWingsOnRegionMission;

    /// <summary>
    /// A-REGION-SYNC [RE + LOG]: the carrier wings' air supply ratio and fuel in their mission effectiveness (0xf78500:
    /// the base's received / asked supply × the fuel factor; 1 when supplied). The effectiveness also takes the deck
    /// crowding and air_mission_efficiency (<see cref="NavySimulator.Engine.Battles.Air.RegionAirWarRules.Effectiveness"/>).
    /// The logged CL3-template attacker had 0.734.
    /// </summary>
    public readonly double CarrierMissionEfficiency;

    /// <summary>
    /// A-DETECT-RAD [RE]: the share of the battle region's provinces the side's and its friends' radars cover, each radar
    /// times its state's control share (0..1); with DETECT_CHANCE_FROM_RADARS it raises the side's air detection.
    /// </summary>
    public readonly double RadarCoverage;

    /// <summary>A-COVERAGE: the side's air bases, one per state its land-based wings fly from.</summary>
    public readonly IReadOnlyList<AirBaseSetup> AirBases;

    public BattleParticipant(
        Fleet fleet,
        Admiral? admiral,
        List<Research> researches,
        List<Spirit> spirits,
        Aggressiveness aggressiveness = Aggressiveness.EngageAtMediumRisk,
        RepairPriority repairPriority = RepairPriority.High,
        int? carrierStanceIndex = null,
        double carrierNightTraffic = 0,
        IReadOnlyList<DoctrineSetup>? doctrines = null,
        GameDefines? defines = null,
        IReadOnlyList<MioSetup>? mios = null,
        IReadOnlyDictionary<string, Mio?>? designMios = null,
        Ship? prideOfTheFleet = null,
        IReadOnlyList<LandBasedAirwing>? landBasedAirwings = null,
        IReadOnlyDictionary<string, int>? planeStockpile = null,
        bool? carrierWingsOnRegionMission = null,
        double carrierMissionEfficiency = 1.0,
        double radarCoverage = 0,
        IReadOnlyList<AirBaseSetup>? airBases = null)
    {
        if (prideOfTheFleet is not null && !fleet.Ships.Contains(prideOfTheFleet))
        {
            throw new ArgumentException("The pride of the fleet must be a ship of the fleet.", nameof(prideOfTheFleet));
        }

        Fleet = fleet;
        Admiral = admiral;
        PrideOfTheFleet = prideOfTheFleet;
        Researches = researches;
        Spirits = spirits;
        Aggressiveness = aggressiveness;
        RepairPriority = repairPriority;
        LandBasedAirwings = landBasedAirwings ?? [];
        PlaneStockpile = planeStockpile ?? new Dictionary<string, int>(StringComparer.Ordinal);
        CarrierStanceIndex = carrierStanceIndex;
        CarrierWingsOnRegionMission = carrierWingsOnRegionMission;
        CarrierMissionEfficiency = carrierMissionEfficiency;
        RadarCoverage = Math.Clamp(radarCoverage, 0, 1);
        AirBases = airBases ?? [];
        Doctrines = doctrines ?? [];
        Mios = mios ?? [];
        DesignMios = designMios ?? new Dictionary<string, Mio?>();

        // S-DOCTRINE: naval and air doctrines alike; the air folder's effects reach the planes (A-STATS).
        var gameDefines = defines ?? GameDefines.Vanilla();
        ActiveEffects = Doctrines
            .SelectMany(setup => DoctrineRules.ActiveEffects(setup, gameDefines))
            .Concat(spirits.Select(spirit => (Source: spirit.ID, spirit.Effects)))
            .ToList();
        CountryModifiers = researches.Select(research => research.CountryModifiers)
            .Concat(ActiveEffects.Select(effect => effect.Effects.CountryModifiers))
            .Aggregate(ModifierSet.Empty, (current, modifiers) => current.Add(modifiers));
        CarrierNightTraffic = carrierNightTraffic + CountryModifiers.Get(StaticModifiers.CarrierNightTraffic);
        PlaneBonuses = ActiveEffects.SelectMany(effect => effect.Effects.PlaneBonuses).ToList();

        ApplyFleetModifiers();
    }

    /// <summary>S-MIO: the MIO the side's ships of this design are assigned to: the side's assignment, else the design's.</summary>
    public Mio? MioOf(ShipDesign design)
    {
        return DesignMios.TryGetValue(design.ID, out var assigned) ? assigned : design.Mio;
    }

    /// <summary>S-STATS-MIO: the bonus the side's state of the design's MIO (<see cref="MioOf"/>) gives the design.</summary>
    public ShipStats MioBonus(ShipDesign design)
    {
        var assigned = MioOf(design);
        return MioRules.EquipmentBonus(design, assigned, Mios.FirstOrDefault(setup => setup.Mio.ID == assigned?.ID));
    }

    /// <summary>The naval doctrine folder.</summary>
    public const string NavalFolder = "naval";

    /// <summary>A-STATS: the plane bonus blocks of the side's doctrines and spirits.</summary>
    public readonly IReadOnlyList<PlaneBonus> PlaneBonuses;

    private readonly Dictionary<string, PlaneStats> planeStats = new(StringComparer.Ordinal);

    /// <summary>A-STATS: the plane's stats with the side's bonuses for its sub-unit and categories (<see cref="PlaneStatRules"/>).</summary>
    public PlaneStats PlaneStatsOf(PlaneEquipment plane)
    {
        if (!planeStats.TryGetValue(plane.ID, out var stats))
        {
            var bonus = PlaneBonuses.Where(entry => entry.AppliesTo(plane))
                .Aggregate(ModifierSet.Empty, (current, entry) => current.Add(entry.Values));
            stats = planeStats[plane.ID] = PlaneStatRules.WithBonuses(plane.Stats, bonus);
        }

        return stats;
    }

    /// <summary>
    /// The inputs combat ignores: effects of the active doctrines and spirits with no simulator counterpart (unmodelled
    /// country modifiers, stats and scripted effects) and MIO bonus stats the simulator does not have. Keys the game itself does not use in battle
    /// (<see cref="CountryModifiersWithoutBattleEffect"/>) are not listed.
    /// </summary>
    public IReadOnlyList<string> IgnoredEffects()
    {
        var ignored = ActiveEffects.SelectMany(effect => effect.Effects.CountryModifiers.Values.Keys
                .Where(key => !IsModelledCountryModifier(key) && !CountryModifiersWithoutBattleEffect.Contains(key))
                .Concat(effect.Effects.NotSimulated))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        ignored.AddRange(Mios.SelectMany(setup => (setup.Mio.InitialTrait is null ? [] : new[] { setup.Mio.InitialTrait })
                .Concat(setup.UnlockedTraits)
                .SelectMany(trait => trait.NotSimulated)
                .Concat(setup.Policy?.NotSimulated ?? [])
                .Select(key => $"{key} (MIO {setup.Mio.ID})"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));
        ignored.AddRange(IgnoredAdmiralEffects());
        return ignored;
    }

    /// <summary>
    /// S-ADMIRAL: the admiral's trait and skill effects combat ignores: modifier keys the rules do not read (e.g.
    /// spotting_chance, pride of the fleet), terrain adjusters (naval terrain is not modelled) and scripted effects.
    /// </summary>
    public IReadOnlyList<string> IgnoredAdmiralEffects()
    {
        if (Admiral is null)
        {
            return [];
        }

        var subUnitKeys = Admiral.Traits.SelectMany(trait => trait.SubUnitModifiers.Values).SelectMany(modifiers => modifiers.Values.Keys);
        var keys = Admiral.CombatModifiers.Values.Keys.Concat(Admiral.TraitModifiers.Values.Keys).Concat(subUnitKeys)
            .Where(key => !IsModelledAdmiralModifier(key) && !AdmiralModifiersWithoutBattleEffect.Contains(key))
            .Select(key => $"{key} (admiral)");
        var terrains = Admiral.Traits.SelectMany(trait => trait.Adjusters.Keys.Where(key => key != LeaderTrait.Night)
            .Select(terrain => $"{terrain} adjuster (admiral trait {trait.ID})"));
        var scripted = Admiral.Traits.SelectMany(trait => trait.Scripted.Select(key => $"{key} (admiral trait {trait.ID})"));
        return keys.Concat(terrains).Concat(scripted).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>S-ADMIRAL: whether combat reads the key from an admiral (some keys the country has, it does not).</summary>
    // [RE] 0xc3b240: experience gain has no admiral term.
    private static readonly HashSet<string> ExperienceGainKeys =
        new([StaticModifiers.ExperienceGainNavyUnit, StaticModifiers.ExperienceGainNavyUnitFactor], StringComparer.Ordinal);

    public static bool IsModelledAdmiralModifier(string key)
    {
        return (ModelledCountryModifiers.Contains(key) || ModelledAdmiralOnlyModifiers.Contains(key)) && !AdmiralModifiersWithoutBattleEffect.Contains(key) &&
               !ExperienceGainKeys.Contains(key);
    }

    private void ApplyFleetModifiers()
    {
        var bonuses = Researches.Concat(ActiveEffects.SelectMany(effect => effect.Effects.StatBonuses)).ToList();

        foreach (var ship in Fleet.Ships)
        {
            var role = ship.Design.Role;

            // S-STATS-BONUS / S-STATS-SHIP: equipment bonuses go into the design (matched against its equipment types
            // too), technology-like bonuses onto the ship.
            var equipmentBonuses = bonuses.Where(bonus => bonus.IsEquipmentBonus && bonus.AppliesTo(role, ship.Design.EquipmentBonusTypes))
                .Select(bonus => bonus.Values)
                .ToList();
            var technology = bonuses.Where(bonus => !bonus.IsEquipmentBonus && bonus.AppliesTo(role, ship.Design.Types))
                .Aggregate(new ShipStats(), (current, bonus) => current.Add(bonus.Values));

            // S-STATS-MIO: the side's state of the MIO the side assigns the design to.
            ship.ApplyExternalModifiers(equipmentBonuses, technology, MioBonus(ship.Design));

            // S-STATS-COUNTRY [RE]: the ship modifier sum 0xc38760 gathers the country's modifiers for every ship.
            ship.CountryModifiers = CountryModifiers;

            // S-ADMIRAL-MOD [RE] 0xc38760: and the fleet admiral's container A and sub-unit modifiers.
            ship.Admiral = Admiral;
            ship.PrideOfTheFleet = PrideOfTheFleet;
            ship.AdmiralModifiers = Admiral is null
                ? ModifierSet.Empty
                : Admiral.CombatModifiers.Add(Admiral.SubUnitModifiers(ship.Design.ShipType.ID));
        }
    }
}
