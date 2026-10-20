# Naval air combat spec

Status: **implemented** in `src/NavySimulator.Engine/Battles/Air/` (`AirRules`, `AirPhase`, `AirGroup`, `AirWing`). The rules come from the reverse-engineering findings in [A-air-combat.md](../reverse-engineering/findings/A-air-combat.md) and [A-WINGS](../reverse-engineering/findings/A-WINGS-carrier-wings-and-reinforcement.md) (hoi4.exe 1.19.3, `naval_fire_exchange_air.cpp`); each rule below names the function it was decoded from. Tags are explained in [README.md](README.md).

Planes do not use hit profiles, armor or piercing. [RE]

## Overview of one air step

An air step runs for each side **after that side's surface fire**, in air hours only (A-CADENCE). Within the step, each air group in turn does the following:
1. Planes flying: `round(pool planes × clamp(sortie, 0, 1))` for a carrier group (A-SORTIE); all planes of a joined land-based group (A-EXTERNAL).
2. Pick one target (A-TARGET).
3. Enemy carrier fighters intercept (A-INTERCEPT).
4. The target's AA fires once before the strike (A-AA-PREEMPT).
5. Hits (A-HIT) and damage (A-DMG), reduced by fleet AA (A-AA-REDUCE), with a possible crit (A-CRIT).

Air groups ("air objects"): one per **wing pool** on each carrier, i.e. per plane sub-unit (a carrier with fighters and naval bombers has two), plus one per entry of the side's `landBasedAirwings` (A-WINGS). A group without strike wings (a fighter pool) never strikes. A sunk or escaped carrier's groups stop flying. Before the side turns of an air hour the region's air war runs (A-REGION: losses and disruption for every wing over the region, carrier fighters and land-based air superiority wings included), then land-based planes join (A-EXTERNAL); after the 00:00 hour, the stockpile refills land-based wings (A-REINFORCE).

## A-PLANES: plane equipment [PDX]

Planes are the game's fixed plane equipment, imported by `NavySimulator import-planes` (GUI: Settings → Import planes) into `data/planes/planes.json`; there is no plane designer. The import takes every non-archetype entry of `common/units/equipment` whose archetype is an air archetype (`interface_category = interface_category_air`) without `module_slots` (so the designer airframes are left out) and is not a missile. Keys a variant leaves out are its archetype's: stats (`air_attack`, `air_defence`, `air_agility`, `maximum_speed`, `naval_strike_attack`, `naval_strike_targetting`, `reliability`, `air_range`, `build_cost_ic`, `air_superiority`), `type`, `allow_mission_type` and `carrier_capable`. Each plane also gets the air sub-unit whose `need` names its archetype (`common/units/air.txt`, e.g. `cv_nav_bomber`): its `type` (which the fighter tests read) and wing sizes (`carrier_air_wing_size` 10, `land_air_wing_size` 100 in vanilla and TFB). With the TFB dev checkout over vanilla: 62 planes, 15 carrier capable (`cv_fighter_equipment_0..4`, `cv_CAS_equipment_1..4`, `cv_nav_bomber_equipment_1..4`, the two scout planes).

Each plane also records its sub-unit's `categories` (e.g. `cv_nav_bomber`: `category_nav_bomber`, `category_carrier_nav_bomber`), which doctrine and spirit bonuses name (A-STATS).

Not modelled yet: plane equipment upgrades (TFB's `plane_naval_upgrade`, `cv_plane_naval_upgrade`, ...), plane MIOs, equipment bonuses of technologies and ideas on planes.

## A-STATS: a wing's stats [RE]

From [A-STATS](../reverse-engineering/findings/A-STATS-plane-stats-and-air-modifiers.md). The wing stat cache (0xf661e0) multiplies each equipment stat by `1 + Σ` of the owner's sub-unit bonuses for the plane's sub-unit **and every one of its categories** (air doctrines and spirits: `category_carrier_nav_bomber = { naval_strike_attack = 0.05 }`; technologies the same way), as for ships (S-STATS-SHIP): `stat = trunc((1 + Σ bonus) × equipment stat)`. Reliability is the one plane stat on the additive list: `× (1 + b) + b` (only accidents read it). The stats then stay fixed for the battle. Code: `BattleParticipant.PlaneStatsOf`, `PlaneStatRules`, `AirWing.Stats`.

The combat getters add their own `(1 + Σ)` on top (A-STATS-GETTERS):
- **Naval strike** (A-DMG): attack and agility × (1 + `naval_strike_*_factor` + ace + M), targeting × (1 + `naval_strike_targetting_factor` + ace). M is the mission efficiency of the wing's mission *type*: carrier wings have none (M = 0, so `air_nav_efficiency` never raises carrier strikes); land-based naval-strike wings take `air_nav_efficiency`.
- **Naval interception** (A-INTERCEPT): the cached air attack, defence and agility as they are; speed × (1 + ace). The country's `air_attack/defence/agility_factor` and `air_maximum_speed_factor` are not read here.
- **Region air war** (A-REGION, 0xf5e890): attack, defence and agility × (M + 1 + Σ `air_*_factor` of the country and the wing's experience holder (+ ace)), truncated. M (by the *executing* mission: `air_superiority_efficiency`, `air_nav_efficiency`, interception's `air_intercept_efficiency`) and `air_home_defence_factor` (a carrier wing in its task force's region) or `air_power_projection_factor` (a land wing over a sea region) to attack and defence come in only for a wing with a region mission, in the fight and the strike efficiency, not in the attackers' sort or the escorts.
- **Experience holder** (A-STATS-XP, 0xf65f20): with `v = clamp(xp / L0 − 1, −1, 0)` below the first `AIR_WING_XP_LEVELS` level (else `clamp((xp − L0) / (Llast − L0), 0, 1)`), truncated to 0.01, the wing carries `air_wing_experience_bonus_max × v` or `air_wing_experience_malus_min × (−v) × (1 + max(air_untrained_pilots_penalty_factor, −1))`. A fresh wing (xp 0, as `make_save.py` writes them) carries the whole malus: TFB −0.15 air attack, defence and agility and +0.15 `air_night_penalty`. In a naval battle it acts only in the region war and the night and weather factors. The simulator's wings start at experience 0 [LOG: the region replay's disruption and the strikes of disrupted wings match the game far better with it].

[LOG] Air doctrines in game (`air_cv_doctrines`: Battlefield Support with Naval Aviation and Precision Naval Bombing complete against Operational Integrity with Torpedo Swarm Tactics and two Naval Aviation rewards, each side with a carrier as pride of the fleet; 10 roll-logged runs): 1,151 of 1,173 naval strikes replay exactly, air damage +0.13 %, every surface shot matches; 100 simulator seeds give the game's means within noise (hours 410 vs 453, HP lost 2,185 / 1,669 vs 2,173 / 1,645).

Which air country keys act in a naval battle: the table in A-STATS-MODS. No effect: the country's `carrier_traffic` (only the weather and night holders count, A-SORTIE) and `air_maximum_speed_factor` (only an ace's), `spotting_chance` (fleet spotting before the battle), `port_strike`, and the land, strategic, mine, intel and cost keys. `air_mission_efficiency` multiplies the effectiveness, clamped to 1 (so it only helps a wing below 1); the effectiveness is an input here. `carrier_capacity_penalty_reduction` scales `CAPACITY_PENALTY` (A-SORTIE).

## A-WINGS: air objects, strike wings and fighters [RE]

From [A-WINGS](../reverse-engineering/findings/A-WINGS-carrier-wings-and-reinforcement.md):
- **Air objects per wing pool** (0x1c6a7c0): a pool is the wings of one sub-unit on one base, so each carrier has one air object per plane sub-unit, in order of its first wing. Sortie, flying planes, stacking wings, interception attackers, AA and hits are per air object; deck crowding (A-SORTIE) and the positioning fill (S-POS) count every plane on the carrier.
- **Strike list** (`wing+0x30 & 0x190`): wings whose plane allows `naval_bomber`, `port_strike` or `naval_kamikaze` (the game adds kamikaze to fighter-type sub-units of kamikaze-capable countries; not modelled). It does not depend on the mission the player set. TFB: carrier CAS and carrier naval bombers strike; carrier fighters do not.
- **Executing mission** (0x1978e30, 0x197a1dc): only strike wings executing naval strike or kamikaze enter the strike averages (A-DMG) and take losses (A-LOSSES). Land-based wings join on that mission. What sets it for carrier wings is not decoded, but the game's own state shows it: in 10 logged TFB battles (2026-10-20, `output/ingame/jukki_usa_vs_bc_japan_carriers_battle_start_*`) every carrier naval bomber wing had `executing_mission = 0x10` (naval strike) with no assigned mission, and every carrier fighter wing `0x1` (air superiority) [LOG]. `rules.carrierWingsExecuteNavalStrike` (default true) does this; false (carriers do no damage) is kept for tests.
- **Fighter-type** (0xf63100, `& 0x100000c00`): the plane's **sub-unit** `type` has `fighter`, `interceptor` or `heavy_fighter`; these wings defend in A-INTERCEPT. The A-SORTIE fighter bonus (`& 0x100000400`) needs `fighter` or `heavy_fighter`. TFB's CAS (equipment `{ cas fighter }`, sub-unit `{ cas strategic_bomber }`) is not a fighter. [ASSUMED] A plane without a known sub-unit uses its equipment `type`.

Carrier wings are set per carrier in the force composition (`carrierAirwings`: per design, one loadout per ship in ship order; each wing entry is `airwings` wings of `planeCount` planes, 0 meaning 10). Planes that are not carrier capable are allowed with a loader warning. The game does not check deck capacity when it refills a wing; the simulator takes the wings as given.

## A-CADENCE: when planes strike [RE]

`airHour ⇔ hourOfDay mod (2 × HOURS_DELAY_AFTER_EACH_COMBAT) == regionPhase` (`0xe24530`). With the vanilla 4 h delay, every air group in the battle acts at the three hours of each day where `hour % 8 == regionId % 8`. The phase follows the game clock and the strategic region, not the battle start. Hour 0 counts.
- `rules.airRegionPhase` (default 4, which gives the wiki's 04/12/20) and `rules.startHourOfDay` set the phase and the clock.
- `CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT` **has no effect**: the cooldown it sets is written but read only by a dead eligibility list. Land-based groups also strike at every air hour (their "struck once" flag is dead too).
- The air step does not check S-ACTIVATE.

## A-SORTIE: how many carrier planes fly [RE]

`0x1976db0` / `0x19786c0`:
```
ratio   = CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[stance]; defender uses 1 − ratio
base    = (hour < SELECTED_SORTIE_INITIAL_TIME ? ratio : BASE_CARRIER_SORTIE_EFFICIENCY)
        + the carrier's sortie_efficiency (country: doctrines, spirits; damaged flight deck)
        + screeningEff × screening_bonus.sortie_efficiency
        + carrierScreening × capital_screening_bonus.sortie_efficiency     (vanilla 0.1)
        + fighter_sortie_efficiency (carrier experience, country), if the pool's first wing is a fighter or heavy fighter
crowd   = planes ≤ cap ? 1 : (r = (planes − cap)/cap) > 0.9 ? 0 : clamp(1 − CAPACITY_PENALTY × r, 0, 1)
          with planes = all planes on the carrier, cap = deck size × CARRIER_SIZE_STAT_INCREMENT
night   = clamp(1 + darkness × night.carrier_traffic + (dark ? carrierNightTraffic : 0), 0, 1)
          (vanilla −1: no sorties at night; TFB −0.75, so 25 %, +0.15 with its carrier doctrine)
sortie  = max(0, base × crowd × night) × org / maxOrg
flying  = round(poolPlanes × clamp(sortie, 0, 1))
```
- The clamp comes only at the end, so a sortie efficiency above 1 offsets low org.
- The crowding counts every plane on the carrier; `flying` only the air object's pool (A-WINGS).
- [RE + LOG] `hour` is the game's combat hour counter, which is one ahead of the battle hour during the update (S-HOUR): the stance base ends after battle hour `SELECTED_SORTIE_INITIAL_TIME − 2` (22). In logged TFB carrier battles the carrier-ratio positioning switched to the base efficiency at battle hour 23, not 24.
- The stance does not split planes into offence and defence (A-STANCE).
- `night.carrier_traffic` comes from the loaded static modifiers (pass the mod folder with `--defines`). The country's `carrier_night_traffic` is the side's doctrines' and spirits' plus the participant input `carrierNightTraffic`.
- Weather: × `clamp(1 + carrier_traffic, 0, 1)` of the region's weather holder, whole (0x1976f84; S-WEATHER: heavy rain −0.8, blizzard −1), beside the night factor.
- `carrier_capacity_penalty_reduction` of the country scales `CAPACITY_PENALTY` by `1 + value` (A-STATS-MODS, 0xc57a89).
- Not modelled: leader modifiers.

## A-STANCE [RE]

The stance index comes from `carrierStanceIndex` per participant, with default `CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX`. It is used in two places:
1. As the sortie base for the first 24 h (A-SORTIE).
2. In interception: `defShare = 1 − ratio` of the defending side's stance.

## A-STACK: stacking factor [RE]

`0x161e9a0` / `0x1c6a540`:
```
cap = max(NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY,
          10·capitals + 16·carriers + 7·subs + 4·convoys + ceil(5·screens / (1 + SCREEN_CAP_REDUCTION_FACTOR × 10·capitals)))
F   = ownCarrierPlanes > cap ? 1 / (1 + NAVAL_COMBAT_PLANE_STACKING_PENALTY_EFFECT × excess) : 1
```
- [RE + LOG] `ownCarrierPlanes` is the sum of the air objects' counted planes (`airfx+0x5c`), taken before they recount at this air step: planes lost in the region's air war since the last air step are still counted (`AirGroup.CountedPlanes`).
- The cap counts the enemy's visible ships that are still in the battle; hidden subs do not count. Land-based planes are not in `ownCarrierPlanes`.
- The game also counts support ships at 3 (`SHIP_SILHOUETTE_VALUE_PLANES_SUPPORT`). The simulator has no support role.
- F does **not** reduce the planes that fly. It shrinks two things: the strike wings used for the stat averages (`max(1, round(n × F))`), and the AA base (`ceil((flying − intercepted) × F)`).

## A-EXTERNAL: land-based planes [RE]

Each entry of a side's `landBasedAirwings` is one air base's wing on a naval strike mission over the region: one air object. From [A-WINGS-EXT](../reverse-engineering/findings/A-WINGS-carrier-wings-and-reinforcement.md#a-wings-ext-land-based-planes-joining-a-naval-battle) (0xc57d30, 0x16205c0), at the start of every air hour, before the combat:
```
joined = 0                                                   (each side; the count resets every air hour)
cap    = max(NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP,
             floor(Σ enemy warship current HP × JOIN_RATIO × (1 + JOIN_RATIO_PER_DAY × floor(hour / 24))))
for each wing, in listed order [ASSUMED] while joined < cap:
    budget  = round(wing planes × missionEfficiency)
    add     = min(budget, cap − joined);  joined += add
    the wing is in the battle from now on; it may lose at most add planes in its strikes until its next join
```
- A wing that has joined strikes at every air hour with **all its planes**, not only the joined count (read from code; worth an in-game check).
- `missionEfficiency` is the mission's efficiency (air base, range, coverage, none simulated; an input): it only scales the offered planes. `disruptionFactor` (airfx+0xc0, 0xf78160) multiplies their attack: 1 unless set; the region's air war computes the disruption itself (A-REGION), so leave it at 1 when the scenario has the enemy's air superiority wings.
- [LOG] The disruption counts **twice**: the mission efficiency 0xf78160 weighs each wing's attack inside the strike average (A-DMG), and the air object's `airfx+0xc0`, the same value, multiplies the result again. In 10 logged TFB battles (`air_land_vs_fleet`: 100 land tactical bombers against cruisers covered by 100 land fighters on air superiority) the game's damage per hit was the undisrupted value × factor² in every strike (factor 0.45–0.51); the replays match within 0.5 % with it.
- [LOG] Only wings in range join: in game, land CAS from Burgas or Dobrogea never joined a battle in province 7906, tactical bombers did (mission efficiency 0.6 from Burgas, 1.0 from Dobrogea). The simulator has no range or coverage model; leave out-of-range wings out of the scenario and set `missionEfficiency` to the game's.
- Each entry has a `mission`: `naval_bomber` (naval strike, the default) or `air_superiority`. Air superiority wings never join the naval battle (A-WINGS-EXT); they fight in the region's air war (A-REGION), which disrupts and shoots down the enemy's strike wings.
- Land-based planes skip the sortie step and get no carrier multiplier.
- Not modelled: port strikes, naval patrol wings (they join but have no strike wings), carrier wings of task forces outside the battle joining it (their budget also takes the stance ratio).

## A-REGION: the region's air war [RE + LOG]

From [A-REGION](../reverse-engineering/findings/A-REGION-carrier-wings-in-the-region-air-war.md). In each air hour, before the naval combat hour (the hourly strategic-air update 0xc57d30), every wing over the battle's region fights the region's ordinary air war: carrier wings of task forces in combat (retreating and escaped carriers included, sunk ones not: [RE + LOG] an escaped ship stays in its task force, which stays in the combat, so its wings stay on both lists and only stop striking, [A-REGION-ESCAPE](../reverse-engineering/findings/A-REGION-ESCAPE-escaped-carrier-wings.md)) and land-based wings. This is where carrier fighters matter in TFB (they never intercept, A-INTERCEPT), and where planes disappear between naval strikes. Code: `AirPhase.RegionAirWar`, `RegionAirWarRules`.

Lists per side:
- **Scrambled**: every carrier wing (0xc66480). Its fighters (air superiority) attack; all its wings defend; **none is ever disrupted**.
- **Region missions**: land-based wings, and the carrier wings when they fly a mission over the region (`carrierWingsOnRegionMission`): all of them attack (bombers too), defend and can be disrupted. A carrier fighter of a side with region missions is on both lists (it attacks twice).
- **A-REGION-SYNC** [RE + LOG] ([finding](../reverse-engineering/findings/A-REGION-SYNC-carrier-wing-region.md)): a task force's carrier wings get the region at its hourly naval mission tick out of combat (0xd79900 → 0xfbe7f0 → 0xfa9e00; hold, strike force, convoy, mine and training missions: the location's region; patrol: only in its spotting region; reserve fleet and invasion support: none). The side whose tick starts the battle has them, a side already in combat never gets them; a region change carries an existing region along (0xc647b0) but never makes one. So in a battle-start save the attacker's wings are on the list and the defender's are not, which is the default when `carrierWingsOnRegionMission` is not set (the saves' `hours_in_mission` showed ROM's one tick and none for BUL).
- Effective planes per wing: `trunc(round(planes × effectiveness) × CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[stance])` for carrier wings, `round(planes × effectiveness)` for land wings. Effectiveness (0xf78500) = `clamp(base × crowding × (1 + air_mission_efficiency), 0, 1)`: carrier wings take no coverage and no region change penalty; their base is the air supply ratio × fuel (`carrierMissionEfficiency`, default 1; the logged 0.734 was ROM's supply), crowding is the deck's (A-SORTIE, every plane on the carrier); `air_mission_efficiency` sums the country's, the fleet admiral's and (wings with a region mission) the region weather's (rain −0.1 / −0.3, snow −0.1, ...). Land wings: base = `missionEfficiency` (coverage, base crowding, supply, region change penalty and fuel, none modelled), × (1 + the country's and the weather's `air_mission_efficiency`). The effectiveness is recomputed every air hour and does not decay.

Each side in turn attacks the other (the combat's attacker first, [ASSUMED]); per attacking wing, in order of normalised attack × agility:
```
defender weight  d.left = max(1, round(trunc(n × k)))     k = 1 for a carrier wing, the attacker's detection (A-DETECT) for a land wing;
                                                          n × k in fixed point (100 planes at 0.028 count 3)
eng      = min(Σ d.left × COMBAT_MULTIPLANE_CAP, a.n);    share = d.left / Σ d.left
damage   = share × eng × attack × COMBAT_DAMAGE_STATS_MULTILPIER − agility edge + speed edge (+ top speed bonus)
x        = max(0.001, trunc(trunc(scale × damage) × 0.01) / defence)     scale = COMBAT_DAMAGE_SCALE_CARRIER (both carrier wings) | COMBAT_DAMAGE_SCALE
losses   = x < cap ? floor(x) + (U < frac(x)) : cap        cap = min(d.left, d.n − losses so far)
strike defenders: disruption += trunc(d.left / Σ strike d.left × trunc(DISRUPTION_FACTOR(_CARRIER) × a.n × (1 + speed × DSF) × (1 + agility × DAgF) × (1 + attack × DAtF) × (1 + detection × DDetF)))
                  (d.left as before this attacker's decrement, [RE] 0xf7c282; the attacker's own detection)
d.left  -= round(share × eng / COMBAT_MULTIPLANE_CAP)
```
The stats are those of A-STATS (region war getter). Then each side's air superiority wings escort: `escort = n × (1 + speed × ESF) × (1 + agility × EAgF) × (1 + attack × EAtF) × (1 + own detection × DDetF) × ESCORT_FACTOR × (1 + air_escort_efficiency)`; the side's region-mission strike wings keep `trunc((1 − min(1, Σ escort / Σ disruption)) × disruption)`. The losses come off the wings after the whole air war; a carrier wing left without planes is deleted from its pool [LOG]. Disruption resets every air hour. A disrupted wing strikes at `e = clamp(1 − trunc(sqrt(disruption / den) × 0.316228), 0.001, 1)`, `den = n_i × (1 + defence × DISRUPTION_DEFENCE_DEFENCE_FACTOR) × (1 + attack × DISRUPTION_DEFENCE_ATTACK_FACTOR) × (1 + speed × DISRUPTION_DEFENCE_SPEED_FACTOR)` (stats as shares of `AIR_WING_MAX_STATS_*`), inside the strike average (A-DMG); a land wing's join factor (`airfx+0xc0`) is the same with its offered planes.

[LOG] Checked against 40 logged TFB battles (`air_cv_mirror`, `air_cv_bombers_vs_fighters`, 2026-10-20):
- The efficiency formula reproduced the game's strike averages of disrupted carrier bombers (0.67 at disruption 14.4) and land bombers (0.956 at 2.9).
- With only the scrambled carrier fighters of the side without region missions attacking, the predicted disruption of the other side's carrier bombers was 22.4; the game's was 22.0. With escorts (a fighter mirror) nobody was disrupted, as in the game.
- Planes lost in the region per air hour: predicted about 13 and 8 for the two sides of the mirror, logged 10–16 and 6–11.
- The free-running engine matches the game's battle length, losses and air damage within noise in both carrier scenarios (see [calibration](../calibration.md#air-tests-2026-10-20)).
- Wing by wing (`NavySimulator replay --region <file>`: the engine's region air war as a dry run from the game's wings at the start of each air hour, against the game's wings at their strike), in 8 replayed carrier battles: planes lost per wing and air hour 0.090 / 0.066 (engine) vs 0.087 / 0.066 (game) for the mirror attacker's bombers / fighters, 0.351 / 0.388 vs 0.382 / 0.376 for the defender's; bombers against fighters: disruption 10.2 vs 10.0, losses 0.163 vs 0.187 (the game's accidents add a little).
- [LOG] Land-based bombers as defenders (2026-10-20, after A-DETECT and A-STATS): the engine read the defender's capacity after the attacker's decrement, so a lone land wing (weight 1) took no disruption; with the game's order, the decoded detection and the fresh wings' experience malus, `air_land_vs_fleet` (20 runs) matches the game's disruption of Japan's land bombers within 1 % in 596 of 600 air hours, with no hand-set `disruptionFactor` and the USA's land fighters at `missionEfficiency` 0.46 (the land effectiveness, coverage of the region from the base, is not modelled). Strikes of the disrupted wings: 422 of 620 match, air damage exact. [RE + LOG] Escaped carriers stay in the region war (A-REGION-ESCAPE): at the first air hour after a side's first escape the game's disruption follows the engine with them in, in all 13 `air_cv_bombers_vs_fighters` and 18 `air_cv_land_mix` runs with an escape. The replay's `--region` dry run takes its own region losses off escaped carriers' wings, which the logger no longer lists (their air object moved to `group+0x88`); with that, the USA's land bombers' disruption over the 18 runs is 192k against the game's 179k and Japan's carrier bombers' 139k against 141k. The land fighters' `missionEfficiency` in `air_cv_land_mix` is 0.343 (4 of the region's 7 provinces in Fighter II range from Burgas × the base's supply factor 0.6, A-DETECT-MIX), its land bombers' 0.6. For free runs the template's stockpiles refill land wings with older variants whose stats the game averages into the wing (not modelled: the simulator counts stock as the wing's own plane).
- In the CL3 template the combat's attacker's carrier wings were on the region list (effectiveness 0.734, its supply ratio) and the defender's were not, as A-REGION-SYNC predicts. `make_save.py` puts a side's carrier wings on the list when its `carrierWingsOnRegionMission` is true (an active mission over `--air-region`); the attacker's cannot be kept off it. [LOG] Checked in game (`air_cv_doctrines`, 10 roll-logged runs, `output/ingame/air_batch7`): with the defender's wings written on the list, the replay matches the game's disruption of the attacker's bombers within 1 % in 9,314 of 9,548 wing hours (142k in all); with them off the list the engine gives 7.7k and a third of the game's losses.

Not modelled: the land wings' effectiveness (coverage from their base's range, base crowding: the input `missionEfficiency`), the weather and night terms of the stats (none in TFB; vanilla night `air_attack_factor` −0.5), the distance weight for wings over other regions, accidents (a separate hourly loss of land wings; carrier wings in battle have none, A-STATS-ACCIDENT), aces in air-to-air combat, the region's air superiority, interception missions.

## A-DETECT: a side's air detection in the region [RE]

From [A-DETECT](../reverse-engineering/findings/A-DETECT-region-air-detection.md) (0xf77730, the game's tooltip "Aircraft detection"). Each air hour, for each side (its country) over the battle's region:
```
AIR   = trunc(clamp(Σ trunc((1 + t + h) × n) / DETECT_CHANCE_FROM_AIRCRAFTS_EFFECTIVE_COUNT, 0, 1) × DETECT_CHANCE_FROM_AIRCRAFTS)
        over the side's own region-list missions (scrambled-only carrier wings do not count), n = effective planes;
        t = air_superiority_detect_factor (air superiority) | air_interception_detect_factor (interception) | NO_SEARCH_MISSION_DETECT_FACTOR;
        h = air_home_defence_factor (carrier wings, always home) | air_power_projection_factor (land wings: never home over a sea region)
RAD   = trunc(radarCoverage × DETECT_CHANCE_FROM_RADARS)        radar-covered share of the region's provinces (own and friends' radars)
BASE  = trunc(controlled land share × DETECT_CHANCE_FROM_OCCUPATION)   0 in a sea region
NIGHT = trunc(trunc(mean darkness of the region's provinces × DETECT_CHANCE_FROM_NIGHT) × plane-weighted night factor)
WEA   = trunc(the region weather's air_detection × plane-weighted weather factor)       rain −0.1 / −0.2, snow, ...
det   = clamp(trunc((AIR + RAD + BASE) × (1 + NIGHT + WEA)), 0.01, 1)
```
Night factor `clamp(1 + air_night_penalty (country, experience holder), 0, 2)`, weather factor the same with `air_weather_penalty` (0xf5bc80, 0xf5c9d0; the plane's night stat, ace and carrier-mission terms are not modelled). The region's darkness is the mean over its provinces (`data/sea-regions`) at the battle's clock, read like the battle province's (S-NIGHT; weather off means no night).

`det` is the attacker's detection in its turn: the weight of the other side's land wings (A-REGION), its disruption power, and (its own) its escorts. A country's own `air_detection` (spirits) is not read here; targeted modifiers (`air_detection`, `air_intercept_efficiency` against a country) are not modelled (no vanilla or TFB script sets them). In the logged CL3-template battles nobody had radar in region 30, so `det` was `0.8 × planes / 3000` or the floor 0.01. Code: `RegionAirWarRules.Detection`, `AirPhase.RegionDetection`. In game, the console command `lock_air_det <tag> [value]` fixes a country's value.

## A-INTERCEPT: carrier fighters vs strike planes [RE]

`0x197b380`. The defenders are all fighter-type wings (A-WINGS, by sub-unit) on every carrier of the target's group that is still afloat (0x197bb9d: escaped carriers and wings without planes included, sunk carriers not); land-based fighters are not used. The roll is made whenever that list is not empty [RE + LOG: in logged battles the game rolled the interception (line 0x27d) for strikes whose defenders were only escaped carriers' or empty wings].
- [LOG] With TFB's stance ratios of 1.0, `defShare = 0`, so carrier fighters never shoot down strike planes in TFB (in 40 logged battles not one plane was intercepted); they still matter for the region's air war (A-REGION).
```
D       = CARRIER_PERCENTAGE_DEFEND × Σ defender planes × defShare
engaged = min(D, Σ attacking strike-wing planes × COMBAT_MULTIPLANE_CAP)
dmg     = engaged × CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER × avg defender air attack
agiRed  = aAgi > dAgi ? (min(aAgi/dAgi, BIGGEST_AGILITY_FACTOR_DIFF) − 1) × dmg × COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION : 0
spdInc  = dSpd > aSpd ? (min(dSpd/aSpd, BIGGEST_SPEED_FACTOR_DIFF) − 1) × dmg × COMBAT_BETTER_SPEED_DAMAGE_INCREASE : 0
x       = max(0.001, (dmg + spdInc − agiRed) × 0.01 / avg attacker air defence)
losses  = floor(x) + (frac(x) > U ? 1 : 0)
```
- The averages are unweighted means per wing (wings without planes count).
- Defenders take no losses, and there is no disruption.
- The top-speed term of the speed helper is not decoded and not modelled. [RE, partial]
- `COMBAT_DAMAGE_SCALE_CARRIER` and `DISRUPTION_FACTOR_CARRIER` are not read in naval combat.

## A-TARGET: target selection [RE]

`0x197a700`, a weighted random draw over the visible enemy ships that are in the battle:
```
t      = TYPE_BASE × S + TYPE_SCALE × (1 − S)          S = target side's screening efficiency
weight = maxHP × t × (1 + NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE × (1 − HP/maxHP))
       + NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE × clamp(5 − AA, 0, 5)
```
The low-AA term is additive and, at real weights, negligible. Each group picks one target per strike.

[RE + LOG] The pick (0x197ac19 → 0x24f04f0) is an **integer** draw `n` (random int31, line 0x43f, the same generator as the other rolls; logged since 2026-10-20): `p = trunc(n / 2^31 × Σw)` over the fx5 weights, and the target is the first candidate **in list order** whose running sum exceeds `p` (the surface pick puts the heaviest first; this one does not). Each product of the weight is truncated to fx5. Code: `AirRules.PickTarget`. In 40 replayed TFB battles every air target matched.

## A-AA-PREEMPT: preemptive shoot-down [RE]

`0x197c8b0`. Only the target fires.
```
chance = ANTI_AIR_TARGETTING_TO_CHANCE × max(1.5 × ANTI_AIR_TARGETING − 0.5 × agi, 0.01)
         agi = AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY × average agility of the selected strike wings × A-STRIKE-MODS (agility)
fires if U ≤ chance
kills  = randomly rounded ceil((flying − intercepted) × F) × clamp(targetAA × ANTI_AIR_ATTACK_TO_AMOUNT, 0, 1)
```
Convoys have no AA. Kamikaze is not modelled.

## A-AA-REDUCE: fleet AA damage reduction [RE]

`0x196fca0`:
```
fleetAA   = Σ warship AA on the target side × SHIP_TO_FLEET_ANTI_AIR_RATIO × side positioning
            (ship AA = (anti_air_attack + navy_anti_air_attack) × (1 + navy_anti_air_attack_factor), S-STATS-COMBAT; the target's AA too)
reduction = min(MAX_ANTI_AIR_REDUCTION_EFFECT_ON_INCOMING_AIR_DAMAGE,
                ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE × (fleetAA + targetAA)^ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE)
```
Convoys add nothing, either to the side total or as the target.
- [LOG] The side term counts every warship of the side that is afloat, **escaped ones included** (they stay in the group's member list); sunk ships drop out. It is set in the side update with positioning at the start of the hour (S-HOUR), so ships sunk during the hour still count. In the logged `jukki_usa_vs_bc_japan_ingame` battles the game's side value matched "in battle + escaped" in every hour; counting only ships in battle made late strikes up to 15 % too strong. Code: `BattleSide.FleetAntiAir`.

## A-HIT: how many planes hit [RE]

```
strikers = flying − intercepted − aaKills
hits     = round(strikers × clamp(avgTargeting × NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1) × U)
```
- `avgTargeting` is `0.1 × naval targeting`, plane-weighted over the selected strike wings that execute naval strike (A-WINGS), × A-STRIKE-MODS (targeting).
- The uniform U halves the expected hits.
- `flying` counts the air object's pool, so carrier fighters (their own pool) are not strikers.
- `tMult = 1 + naval_strike(region weather) + (1 + airfx+0xe8) × naval_strike(night holder)` (0x197bf38; `AirRules.TargetingMultiplier`): the weather's whole (light rain −0.2, heavy rain −0.4); no vanilla or TFB night block has `naval_strike`, and `airfx+0xe8` is taken as 0 [ASSUMED]. [LOG] With the logged weather the replayed air damage of the TFB rain battles matches the game's (it was 20 % high without it).

## A-DMG: strike damage [RE]

```
n_i    = round(planes_i × pool planes / Σ planes of the selected wings)          per selected wing i
attack = Σ(attack_i × n_i × e_i) / Σ n_i   × (1 + navy_carrier_air_attack_factor for a carrier group)
         attack_i = 0.1 × naval attack × (1 + naval_strike_attack_factor + the wing's ace bonus)    (A-STRIKE-MODS, A-ACE)
         e_i      = the wing's mission efficiency 0xf78160 (1 unless its mission is disrupted, A-REGION)
       × (NAVAL_STRIKE_CARRIER_MULTIPLIER for a carrier group | disruptionFactor for land-based)
base   = hits × attack × (1 − reduction)
HP    -= base × NAVAL_STRIKE_DAMAGE_TO_STR;  org -= base × NAVAL_STRIKE_DAMAGE_TO_ORG
```
Targeting and agility are averaged the same way, without `e_i`. [LOG] The logger records the game's three averages per strike (0x1978e30, `averages` in raw.jsonl).
There is no damage randomness and no HP-scaled org damage. Sinking and retreat follow the surface rules (`DamageRules.Apply`).

## A-STRIKE-MODS: country modifiers of the strike stats [RE]

The plane stat getters (`0xf60f10`, `0xf61280`, `0xf60bc0`) multiply naval attack, targeting and agility by `1 + naval_strike_attack_factor`, `1 + naval_strike_targetting_factor` and `1 + naval_strike_agility_factor` of the planes' country; planes from a carrier also take `1 + navy_carrier_air_attack_factor`, `1 + navy_carrier_air_targetting_factor` and `1 + navy_carrier_air_agility_factor` (TFB's carrier subdoctrines and Base Strike milestone). The side's doctrines and spirits supply them (S-DOCTRINE-EFFECT). Air doctrine plane bonuses are in the plane stats (A-STATS); a land-based naval strike wing adds `air_nav_efficiency` to attack and agility (M). Leader terms are not modelled. Code: `AirRules.StrikeAverages`.

## A-CRIT: crits from air [RE]

```
base   = COMBAT_BASE_CRITICAL_CHANCE × (1 + COMBAT_ARMOR_PIERCING_CRITICAL_BONUS) × (1 − min(rel, 1))
chance = base > 0 ? max(0.001, base × max(0, 1 − critical_receive_chance)) : 0
mult   = 1 + COMBAT_CRITICAL_DAMAGE_MULT × (1 − rel) / hits
```
- Air crits use the gun crit defines, not the torpedo ones.
- [RE] A part hit replaces the multiplier (0x1975150): the same path as gun crits (S-CRIT-PART: chance × the ship type's `critical_part_damage_chance_mult` / reliability, roll line 0x2f4, part pick 0x78f, the part's multipliers and additions), with `CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR` as the base. Simulated since 2026-10-20 (`CriticalPartRules` with the air chance).
- [RE] (0x197af20) The base chance is multiplied by `max(0, 1 − critical_receive_chance)` of the target, read through its member getter, before the 0.001 floor (which applies only when the base chance is positive). The sign is the opposite of the surface rule (S-CRIT), so a negative value (damage control, the pride of the fleet's −0.5) raises the chance: the game's behaviour, reproduced. Code: `AirRules.CriticalChance`.

## A-ACE: aces [RE + LOG]

From [A-ACE](../reverse-engineering/findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md). After a strike that reached its target (also when no striker survived the AA), every wing of the air object's strike list, in pool order, gets one call with the air object's losses (intercepted + AA) and the target's HP lost, truncated (`damage`):
```
wing with a live ace:     if losses > 0: dies if U(line 0xb52) ≤ ACE_DEATH_CHANCE_BASE + trunc(trunc(losses × ACE_DEATH_CHANCE_PLANES_MULT) × 0.1)
otherwise:                if damage > 0 and losses < damage:
                              earns one if U(line 0xb68) ≤ trunc((ACE_EARN_CHANCE_BASE + trunc(trunc(damage × ACE_EARN_CHANCE_PLANES_MULT) × 0.1))
                                                               × (1 + air_ace_generation_chance_factor))
ace bonus (per modifier) = trunc(trunc(effect × min(ACE_WING_SIZE / wing planes, ACE_WING_SIZE_MAX_BONUS)) × (1 + ace_effectiveness_factor))
```
- The template is drawn by `chance` among the `common/aces` templates whose `type` meets the wing's sub-unit's (TFB carrier naval bombers: `support_good` / `unique` / `genius` at 0.9 / 0.4 / 0.05: naval targeting +0.05 / +0.10 / +0.15, doubled on wings of 50 planes or fewer). The game draws it from its own hash generator, outside the logged rolls; a replay takes the logged ace (the logger reads each wing's ace and its template's chance since 2026-10-20).
- The bonus counts only for its wing's term in the strike averages (A-DMG): naval attack, targeting and agility keys only, so TFB support aces raise naval targeting and nothing else at sea.
- Wings gain no experience in naval strikes; experience levels have no naval effect.
- [LOG] The roll order matches the logged battles (0xb68 per wing without a live ace, 0xb52 per wing with one when planes were lost, 0xb72 after an ace is earned: the promotion event, no effect).
- [ASSUMED] A new ace replaces a dead one on its wing. [LOG] When the region's air war deletes a wing, its ace can move to another wing of the pool (the game then skips that wing's earn roll); the free-running engine drops it.
- Data: `aces/aces.json`, written by `NavySimulator import-planes` from `common/aces`.

## A-LOSSES [RE + ASSUMED]

- Planes are lost to interception and to preemptive AA, only from strike wings executing naval strike (0x197a1dc); a land-based wing loses at most the planes it offered at its last join. Air groups persist for the whole battle.
- [ASSUMED] Losses come off the wings in order. The game's split across wings is not decoded.
- Plane experience is not modelled.

## A-REINFORCE: planes from the stockpile [RE]

From [A-WINGS-REINFORCE](../reverse-engineering/findings/A-WINGS-carrier-wings-and-reinforcement.md#a-wings-reinforce-planes-from-the-stockpile): once a day, in the daily update after the 00:00 combat hour (with S-MANPOWER), the country's stockpile refills air wings, **all missing planes at once** up to the wing size, the wing with the highest `priority × 1000 + 500 (has a mission) + missing planes` first (the simulator's wings have equal priority and missions, so the one missing most).
- **Carrier wings get no planes while their task force is in combat** (0xf5cda2), so in a battle only land-based wings refill. The new planes fly from the next air hour. Confirmed in game [LOG]: with 1000 carrier naval bombers in stock, the carriers' bomber pools only lost planes over 8 day changes in each of 10 battles.
- A wing takes any stockpiled variant of its sub-unit with the same allowed missions. The simulator takes the wing's own plane first, then [ASSUMED] the others, counted as the wing's plane (the game mixes variants in a wing).
- A land-based wing's size is its entry's planes. Manpower, the 48 h transfer timer and reinforcement preferences are not modelled.
- Input: `planeStockpile` per side (plane ID → count); each refill is logged in the hourly log.

## Experience from sorties [RE]

Carriers gain experience per sortie as part of S-XP (`ExperienceRules`), from the sortie efficiency of their first air object. [LOG] The efficiency is **not clamped** to 1: TFB carriers at a sortie efficiency of 1.55 in the first day gained 0.24789 an hour in game (0.16 per unit), the engine clamped to 0.16. The game's counter-0 update before battle hour 0 (S-HOUR) already gives one hour of it.

## Not modelled

Leader modifiers, kamikaze, port strikes, support-ship silhouettes, the top-speed interception term, how losses are split across wings, accidents (A-REGION), range and coverage of land-based missions (inputs), plane equipment upgrades and plane MIOs, wing experience other than 0 (no input yet) and its gain in battle.

## Inputs

| Field | Where | Meaning |
|---|---|---|
| `carrierAirwings` | force composition | wings per carrier: per design, one loadout per ship (plane ID, wings, planes per wing) |
| `landBasedAirwings` | participant | land-based planes, one entry per air base: plane ID, planes, mission (`naval_bomber` or `air_superiority`), mission efficiency (offered planes), disruption factor (attack) |
| `planeStockpile` | participant | planes in stock by plane ID (A-REINFORCE) |
| `carrierStanceIndex` | participant | index into `CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO` |
| `carrierNightTraffic` | participant | extra `carrier_night_traffic` on top of the side's doctrines and spirits |
| `carrierWingsOnRegionMission` | participant | A-REGION-SYNC: the carrier wings fly a mission over the region (all fight in the region's air war and can be disrupted); default: the attacker yes, the defender no |
| `carrierMissionEfficiency` | participant | A-REGION-SYNC: the carrier wings' supply ratio and fuel in their effectiveness (default 1; the logged CL3 attacker had 0.734); crowding and `air_mission_efficiency` are computed |
| `radarCoverage` | participant | A-DETECT: radar-covered share of the battle region's provinces (0..1, default 0) |
| `doctrines`, `spiritIDs` | participant | S-DOCTRINE, S-SPIRIT: naval and air doctrines; sortie, strike, night traffic, region war and detection modifiers, plane stat bonuses (A-STATS) |
| `rules.airRegionPhase`, `rules.startHourOfDay`, `rules.nightEnabled` | scenario | clock phase and night |
| `rules.weatherEnabled` | scenario | weather off as with `disable_weather`, which also means no night (S-WEATHER, S-NIGHT) |
| `weather`, `regionID`, `provinceID`, `startDate` | scenario | the region's weather (`carrier_traffic`, `naval_strike`) and the province's darkness (S-WEATHER, S-NIGHT) |
| `rules.carrierWingsExecuteNavalStrike` | scenario | A-WINGS [LOG]: carrier strike wings execute naval strike (default true) |
| `rules.navalDominanceEnabled` | scenario | S-DOMINANCE: the side with more naval dominance at the start regains organization faster (not used by air) |

Each strike is reported as an `AirStrikeRecord`: hour, side, source, target, planes flying, intercepted, shot down, hits, AA reduction, crit, HP and org damage, and sunk.
