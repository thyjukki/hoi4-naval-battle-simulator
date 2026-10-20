# A-DETECT: the region air detection `det` of the region air war

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-REGION (defender weights, disruption power, escorts; `docs/mechanics/air.md`),
  `HardcodedValues.RegionAirDetection`
- **Confidence:** read from code only (static analysis); the formula is the game's own tooltip
  `AIR_DETECTION_IN_REGION_DETAILS` (built in the same function, term by term). Checked against logged TFB battles
  (`air_cv_land_mix`, `air_land_vs_fleet`): the defender weight it predicts for land bombers fits the logged
  disruption ratios. Per section below.

Extends [A-REGION](A-REGION-carrier-wings-in-the-region-air-war.md) (its UNRESOLVED item "the detection value `det`").
Conventions as there: RVAs, fixed point ×1e5, every product and quotient truncated toward zero, `0x24ed730` rounds half
away from zero. Modifier ids → loc keys from the registration function 0x55f7f0 (script keys matched by name against
`documentation/modifiers_documentation.md`). Defines are given as vanilla / TFB.

## Summary

`det(country C, strategic region R)` is the "Aircraft detection" total of the air region tooltip:

```
det = clamp( (AIR + RAD + BASE) × (1 + NIGHT + WEA), 0.01, 1 )
```

- **AIR**: C's own air missions over R (region mission list `strategicAir+0x98[R]`, the same list as the region air war),
  plane counts weighted by mission type (air superiority / interception detect factors, `NO_SEARCH_MISSION_DETECT_FACTOR`
  for every other mission) and home defence / power projection, divided by `DETECT_CHANCE_FROM_AIRCRAFTS_EFFECTIVE_COUNT`,
  clamped to 1, × `DETECT_CHANCE_FROM_AIRCRAFTS`.
- **RAD**: radar coverage of R's provinces by radar stations of C **and its friends** (allies / subject relations),
  × `DETECT_CHANCE_FROM_RADARS`.
- **BASE**: share of R's **land** provinces controlled by C (or the same original tag), × `DETECT_CHANCE_FROM_OCCUPATION`.
  0 in every all-sea region.
- **NIGHT**: mean darkness of R's provinces × `DETECT_CHANCE_FROM_NIGHT` × the plane-weighted wing night factor.
- **WEA**: R's weather `air_detection` modifier (static modifiers `weather_*`) × the plane-weighted wing weather factor.
- 0.01 is a hard-coded floor (`0x3e8`). A console command `lock_air_det <tag> [value]` overrides it (debug table).

For the logged BUL / ROM test battles (sea region 30, TFB, 1936): RAD = BASE = 0, so `det` is the AIR term alone, about
0.01–0.06 (table at the end). It does **not** explain the engine's hand-set land `disruptionFactor` 0.48; two engine
deviations found while checking (defender weight rounding, the disruption share read after the decrement) do.

## A-DETECT-TOT: 0xf77730(out, rad, region, tag, tooltip)

Called by 0xf81a20 (region war: `tag` = the attacking country), 0xf77360 (escorts: the escort mission's owner
0xf615b0, region 0xf7ee40), 0xf7d5e4 (naval strike / naval recon detection, not decoded), 0xbb3800 / 0xe0d390 /
0x1880500 (map record and tooltips). Every caller first gets `rad = 0xf7a460(region, tag)` (A-DETECT-RAD).

```
if tag in debug lock table (0x332f5f0, count 0x332f5fc; 16-byte entries {int country index, ..., fixed value +8}):
    return the locked value                                     // tooltip "DEBUG: Locked to: X%"
BASE = trunc(0xe21800(region, tag) × DETECT_CHANCE_FROM_OCCUPATION)            // 0xf777de
RAD  = trunc(rad × DETECT_CHANCE_FROM_RADARS)                                   // 0xf7780c
country = 0xbb48f0(tag);  M = country modifiers (country+0x5b8)
mI = M[0x8]  air_interception_detect_factor;   mS = M[0x9]  air_superiority_detect_factor
mH = M[0x74] air_home_defence_factor;          mP = M[0x75] air_power_projection_factor
cnt = 0; planes = 0; nightW = 0; weaW = 0
for m in strategicAir(country).+0x98[region.id]  (0xc546e0):  if m.executing (+0x14) == 0: continue
    n = m.effective_planes_count (+0xe0)  (int);  planes += n
    nightW += trunc(nightFactor(m.wing) × n)      // 0xf5bc80: clamp(1 + air_night_penalty … , 0, 2) (A-ACE)
    weaW   += trunc(weatherFactor(m.wing) × n)    // 0xf5c9d0: clamp(1 + air_weather_penalty …, 0, 2)
    home   = baseRegion(m.wing) (0xf5e520) == m.region (+0x30 → +0x58)
             // air base: the base province's strategic region; carrier: the region of its task force's location
    t = m.executing & 1 (air superiority) ? mS : m.executing & 4 (interception) ? mI : NO_SEARCH_MISSION_DETECT_FACTOR
    cnt += trunc((1 + t + (home ? mH : mP)) × n)
ratio = clamp(cnt / DETECT_CHANCE_FROM_AIRCRAFTS_EFFECTIVE_COUNT, 0, 1)        // 1 if the define is 0
AIR   = trunc(ratio × DETECT_CHANCE_FROM_AIRCRAFTS)                             // 0xf77d15
avgN  = planes ? nightW / planes : 0;    avgW = planes ? weaW / planes : 0
NIGHT = trunc(trunc(0xe21640(region) × DETECT_CHANCE_FROM_NIGHT) × avgN)       // mean darkness of the region's provinces
WEA   = weather holder of the region (0xf19fd0(weather manager, region.id)) ? trunc(holder[0x1] air_detection × avgW) : 0
TOT   = trunc((AIR + RAD + BASE) × (1 + NIGHT + WEA))
return TOT < 0.01 ? 0.01 : min(TOT, 1)                                          // 0x3e8, 0x186a0
```

Tooltip labels (`AIR_DETECTION_IN_REGION_DETAILS`, `localisation/english/air_l_english.yml`): TOT "Aircraft detection
Total", BASE "From controlled territory", RAD "Radars in range", AIR "Active airplanes in region", DETECT_RED "reduced by a
factor" (= 1 + NIGHT + WEA), NIGHT "Night", WEA "Bad weather".

Notes:
- Only C's own missions count in AIR (allies' planes do not); only missions on the region list `+0x98` (land missions
  over R, and carrier wings that have R as their mission region, A-REGION-LISTS). Scrambled-only carrier wings (`+0x140`)
  are not counted.
- A carrier wing is always "home" (its base region is where its task force is); a land wing over a sea region never is
  (air bases are on land), so it gets `air_power_projection_factor`.
- The country's own `air_detection` (e.g. spirits `JAP_air_defence_surveilance_corps_idea`, Sweden's) is **not** read
  here: the only reads of modifier 1 through the getter 0x55e360 are the region weather holder (0xf77eee), the targeted
  holder (0xf75926) and the map record 0xbb3b1b (medium: inlined getters were not searched).
- Darkness 0 and no weather give `1 + NIGHT + WEA = 1`.

**Confidence:** high (every term read off the code and matched to its tooltip key); medium for the names of the wing
night / weather factors (from [A-ACE]) and for "home" on carriers (0xf5e520's carrier branch via 0xd6d160).

## A-DETECT-RAD: radar coverage 0xf7a460 → 0xf861d0

```
0xf7a460(out, region, tag):
    own = country(tag)
    sum = 0xf861d0(region, own)
    for every other existing country X (gamestate+0x310[i], i ≥ 1, X+0x484 > 0) with 0x702780(own, X):  sum += 0xf861d0(region, X)
    return region.provinceCount (+0x24) ? clamp(sum / provinceCount, 0, 1) : 1
0xf861d0(region, X):  Σ over X's radar records r (X+0x1120, count +0x112c):
                          covered(r, region) × r.controlShare          // r+0x30[i] where r+0x18[i] is the region; × r+0x68
```
`0x702780(own, X)` is the "friendly" test also used by 0xf860e0 (not an enemy): X's tag in a set at
`own+0xf88 → +0x290` (0xd8c980), or the relation entry for X has a subject relation (`+0x288` type 0x30d1, overlord /
subject tag at +0x10 / +0x14, 0xd25830 / 0xd25880) or a non-null `+0x210` (0xd24860). Read as "allied / faction member /
overlord or subject"; the exact relation names are not decoded (medium-low).

Radar records (radar.cpp, 0x70 bytes, built by **0x10a83f0** from 0x6fe1e0): one per state in `country+0x460` (the
country's states) that has a radar building with level > 0 (building types of `0x161d30()+0x2b0`, building `+0x40`):
```
r+0x48 = state;  r+0x60 = owner;  r+0x68 = controlShare = 0x9d4190(state, owner) =
         share of the state's provinces whose controller (+0x188) is the owner or of the same original tag (0xbb52f0)
0x10a3f20 (coverage):
    L    = Σ over radar building types of the state's building level (+0x42);  r+0x58 = L;  if L ≤ 0: range 0, no coverage
    Lmax = max level of those types (0x1177310; fallback data:3333dc8)
    R    = RADAR_RANGE_BASE + RADAR_RANGE_MIN + (RADAR_RANGE_MAX − RADAR_RANGE_MIN) × (Lmax > 1 ? (L − 1)/(Lmax − 1) : 1)
           // NBuildings globals 0x3334040 / 0x33340c8 / 0x3334178; map pixels; r+0x50 = R
    for every province p of the map (1 … gamestate+0x2bc):
        covered if p.state (+0xc0) is the radar's state, or |p.x − c.x| ≤ R and |p.y − c.y| ≤ R and dist²(state, p)
        (0x9d37a0) < R²   (c = state+0x30 → +0x148 / +0x14c, p = p+0xb8 → +0xb8 / +0xbc)
        covered provinces → r+0x00; per strategic region (p+0xc8): r+0x18 regions, r+0x30 counts
```
So RAD = `DETECT_CHANCE_FROM_RADARS × min(1, Σ_radars (covered provinces of R) × controlShare / provinces of R)`:
overlapping radars add up; the radar level only sets the range. No technology modifier is read in the range (the define
comment's "excluding techs").

**Confidence:** high for 0xf7a460 / 0xf861d0 / 0x10a3f20 / 0x9d4190; medium for the field names of the state centre and
for which state list `country+0x460` is (owned vs controlled); medium-low for the meaning of 0x702780.

## A-DETECT-BASE: controlled territory 0xe21800(region, out, tag)

```
land = ctrl = 0
for p in region provinces (+0x18, +0x24):
    if p+0xb8 → +0xd2 bit 0 (land; the same flag S-WEATHER uses for "never at sea"):
        land++;  if p.controller (+0x188) == tag or (both set and 0xbb52f0 same original tag): ctrl++
return land ? ctrl / land : 0
```
**Confidence:** high. Sea provinces have no controller, so the result is 0 for an all-sea region under either reading of
the flag.

## A-DETECT-NIGHT: region darkness 0xe21640(region, out)

Mean of the province darkness 0xf19660 (S-NIGHT, record +0x240) over the region's provinces: `Σ dark / provinceCount`.
**Confidence:** high.

## A-DETECT-USE: where `det` enters (fills in A-REGION)

- **Defender weights 0xf80540** (attacker's `det`): `k = 0xf75820(cache, …)`; non-carrier defender
  `k = clamp(det + T1, 0, 1)`, carrier defender `k = clamp(1 + T1, 0, 1)`.
  `d.n = d.left = max(1, round(n × k × (1 + stat37) [× w]))` with `n × k` a **fixed-point** product (truncated at 1e-5,
  not to an integer: 0xf80731..0xf80751, then × (1 + wing+0x548) and 0x24ed730). So 100 land bombers at `det` 0.028 count
  `round(2.8) = 3`.
- **T1** (0xf75820) = the attacking country's **targeted** modifier id 0x1 = `air_detection` against the defender's
  country (its targeted-modifier table `country+0xf88`, holder via 0xd23780 → +0x10; ids share the global modifier space,
  as the `navy_*_factor_against` ids 0x1c8..0x1cd read by S-STATS). Cached per target tag with the two bases (1 and
  `det`). 0 unless a script gives `targeted_modifier = { tag = … air_detection = … }`.
- **T13** (0xf75690) = targeted modifier id 0xd = `air_intercept_efficiency` against the defender's country: disruption ×
  (1 + it).
- **Disruption power 0xf7be70**: `P = … × (1 + det × DISRUPTION_DETECTION_FACTOR)` (attacker's `det`).
- **Escorts 0xf77360**: `… × (1 + det' × DISRUPTION_DETECTION_FACTOR)` with `det'` of the **escort's owner** in the
  escort mission's region (its own call to 0xf7a460 / 0xf77730).
- **Disruption share** (0xf7be70, checked while reading the use of `det`): the defender's `left` used for
  `trunc(left / sameStrike × dis)` is read at 0xf7c282 (`rdi = entry+0xc`) **before** 0xf862a0 decrements it
  (0xf7c4d9) and used at 0xf7c5a2; `totLeft` / `sameStrike` are summed once per attacker before the defender loop
  (0xf7bfa7 / 0xf7bfd2). The disruption factor is `DISRUPTION_FACTOR_CARRIER` only when both defender and attacker are
  carrier wings (`cv2`), else `DISRUPTION_FACTOR`.

**Confidence:** high for the weights, T1 / T13 slots and the share order; medium for the names of T1 / T13 (id space
shared with the global modifiers, no script uses them in vanilla or TFB).

## A-DETECT-LOCK: debug override

Console command `lock_air_det` (registered in 0x36f0, handler 0x268ad0, help "Locks Air Detection for a country, omit
detection value to reset") adds / removes `{country index, value}` in the table 0x332f5f0; 0xf77730 returns the locked
value for that country. Useful for an in-game check: lock BUL to 1.0 and its land wings' targets weigh fully.
How the handler parses the value (fraction or percent) was not read (medium-low; the tooltip prints value × 100 %).

## Defines and constants involved

| Name | Vanilla / TFB | Role |
|---|---|---|
| `NAir.DETECT_CHANCE_FROM_OCCUPATION` | 0.1 / 0.1 | BASE (0xf777de) |
| `NAir.DETECT_CHANCE_FROM_RADARS` | 0.5 / **0.7** | RAD (0xf7780c) |
| `NAir.DETECT_CHANCE_FROM_AIRCRAFTS_EFFECTIVE_COUNT` | 3000 / 3000 | AIR divisor (0xf77cc1) |
| `NAir.DETECT_CHANCE_FROM_AIRCRAFTS` | 0.8 / 0.8 | AIR (0xf77d15) |
| `NAir.DETECT_CHANCE_FROM_NIGHT` | −0.2 / −0.2 | NIGHT (0xf77d81) |
| `NAir.NO_SEARCH_MISSION_DETECT_FACTOR` | −0.5 / −0.5 | AIR weight of planes not on air superiority / interception (0xf77869) |
| `NAir.DISRUPTION_DETECTION_FACTOR` | 1 / 1 | disruption power and escorts (0xf7c159, 0xf77450) |
| `NBuildings.RADAR_RANGE_BASE / _MIN / _MAX` | 20 / 20 / 200, **25 / 50 / 250** | radar range (0x10a3fe5..0x10a3ff3) |
| static modifiers `weather_rain_light / _rain_heavy / _snow / _blizzard / _sandstorm` `air_detection` | −0.1 / −0.2 / −0.15 / −0.3 / −0.9; TFB −0.1 / −0.2 / **−0.4 / −0.75** / −0.9 | WEA (region weather holder, modifier 0x1) |
| modifier 0x1 `air_detection` | — | region weather holder; targeted (T1) |
| modifiers 0x8 / 0x9 `air_interception_detect_factor` / `air_superiority_detect_factor` | TFB air doctrines +0.05 … +0.1 / +0.05 … +0.075 (vanilla double) | AIR weights |
| modifiers 0x74 / 0x75 `air_home_defence_factor` / `air_power_projection_factor` | doctrines, decisions | AIR weights (also wing attack / defence, A-REGION) |
| modifier 0xd `air_intercept_efficiency` (targeted) | — | T13 |
| hard-coded 0.01 | | floor of `det` (`0x3e8`) |
| hard-coded 1.0 | | RAD when the region has no provinces |

## `det` in the logged TFB test battles (CL3 template: BUL vs ROM, region 30, province 7906, 1936.5.6)

Region 30 ("Black Sea", provinces 5412 7906 8131 8256 8282 8308 8334) is all sea: **BASE = 0**. The template save has
radar stations only in states 123, 125, 126, 127 (English, level 1, range 75 px in TFB), about 500 px from the Black
Sea; neither BUL nor ROM has a friend with radar: **RAD = 0**. The battles ran with weather off and logged darkness 0:
**NIGHT = WEA = 0**. No air doctrines: all detect / home / projection factors 0. So `det = max(0.01, 0.8 × cnt / 3000)`
with `cnt` = the country's region-list planes, naval strike planes at ½:

| Scenario | ROM (combat attacker) | BUL (combat defender) |
|---|---|---|
| CL3 / `jukki_*` without planes | no wings: unused (0.01) | unused (0.01) |
| `air_cv_mirror` (carrier wings of 10 planes at effectiveness 0.734 → 7) | 4 × (5 × 7 × ½ + 3 × 7) = 154 → **0.0411** | carrier wings only scrambled (+0x140): **0.01** |
| `air_cv_bombers_vs_fighters` | 4 × 8 × 7 × ½ = 112 → **0.0299** | **0.01** |
| `air_land_vs_fleet` | 100 tac bombers × ½ = 50 → **0.0133** | 100 land fighters at ≈ 45 effective planes → **≈ 0.012** |
| `air_cv_land_mix` | 154 + 50 = 204 → **0.0544** | ≈ 45 fighters + 60 × ½ bombers = 75 → **≈ 0.020** |
| `air_cv_mirror_weather` (light rain) | 0.0411 × (1 − 0.1 − 0.2 × mean darkness) | 0.01 |

Evidence from the logs (`output/ingame/air_batch3`, `air_batch5`):
- `air_cv_land_mix`, all 18 runs: ROM's 100 land bombers end each air hour with 0.201 of the disruption of a 7-plane ROM
  carrier bomber wing (hours 2, 10, 18: 0.2010 / 0.2003 / 0.1987), and in a few hours (seeds 7, 11, 16, 18) with half of
  it (0.099). Carrier wings defend on both lists (2 × 7 = 14) and their disruption factor is
  `DISRUPTION_FACTOR_CARRIER` 2 against BUL's carrier fighters but 4 against its land wings, while the land bombers get 4
  from everyone, so `ratio = W/14 × 4ΣP / (2P_cv + 4P_land)` lies between W/14 and 2W/14: only **W = 2** (normal) and
  **W = 1** (half hours) fit. `W = round(100 × det_BUL)` gives `det_BUL` in [0.015, 0.025), i.e. `cnt` 56–93 = 30 (BUL's
  60 bombers × ½) + 26–63 fighters; the half hours are hours with `cnt` < 56 (e.g. BUL's bomber wing not executing).
  With the engine's `det` 0 the weight is 1 in every hour; with a floor-only 0.01 it is 1 as well.
- `air_land_vs_fleet`: ROM's single land bomber wing gets the whole disruption of BUL's fighters (361.0, later 362.0 in
  every run): `4 × n × (1 + speed × 1.5)(1 + agility)(1 + attack)(1 + det)`, so BUL's fighters fly about 45 effective
  planes (Fighter II: factor 1.99), the same 26–63 as above. `det` (≈ 0.012) enters only as `1 + det`; the weight
  (1–2) does not matter with one defender. The step 361.0 → 362.0 after hour 10 is 0.28 % and is not explained (an XP
  stat change or `cnt` change; open).

**Does `det` explain the hand-set numbers?** No:
- Land `disruptionFactor` 0.48 (`air_land_vs_fleet`): the game's factor is the efficiency of the 361 disruption
  (0.51 at 100 planes, falling as the wing loses planes; logged 0.45–0.51). The engine gets no disruption there because
  it takes the share **after** the attacker's decrement (`AirPhase.FightRegion`: `defender.Left` is lowered before
  `defender.Left / sameStrike`): one defender with `left = 1` and 3 engaged planes ends at 0. With the share read before
  the decrement (A-DETECT-USE) the engine would compute ≈ 361 and the factor itself; `disruptionFactor` would be 1.
- Land fighters' `missionEfficiency` (0.33 in the mixed test, ≈ 0.45 here) is the land mission's effectiveness
  (`effective_planes_count`, 0xf78500 / 0xf78360), not detection; it stays an input until 0xf78500's land branch is
  decoded. The 0.33 was fitted with the share bug and is probably low.
- The engine's defender weight `(int)Math.Truncate(planes × detection)` should be `round(planes × det)` (fixed-point
  product), with `det` ≥ 0.01.

## Engine inputs to compute `det` (proposal)

Per side and air hour, `RegionAirWarRules.Detection(...)`:
- AIR from the side's region-list entries the engine already builds in `RegionAirWar` (land wings; carrier wings when
  `carrierWingsOnRegionMission`): `Σ planes × (1 + t + home ? mH : mP)`, `t` = `air_superiority_detect_factor` for air
  superiority, `NO_SEARCH_MISSION_DETECT_FACTOR` for naval strike (no interception missions in the engine). Needs the
  country modifiers 0x8 / 0x9 / 0x74 / 0x75 from **air** doctrines and spirits (now recorded, not applied) and, for land
  wings, a flag **`LandBasedAirwing.baseInRegion`** (default false; never true for a sea region).
- RAD: new side input **`radarCoverage`** (0..1, default 0): the coverage of the region by the side's and its friends'
  radars (before × `DETECT_CHANCE_FROM_RADARS`). Importable later from game data: radar station levels per state
  (`history/states`, or a save's `buildings`), state centres and province positions (map), `NBuildings.RADAR_RANGE_*`,
  state control; in 1936 only ENG / GER states have radars, so 0 for most test setups.
- BASE: new side input **`regionControlShare`** (0..1, default 0), the share of the region's land provinces the side
  controls; 0 for every sea region (`data/sea-regions` are all sea), so it can be left out until land regions matter.
- NIGHT: mean darkness over the region's provinces (`data/sea-regions` has every province's position; the S-NIGHT
  darkness model gives each) × `DETECT_CHANCE_FROM_NIGHT` × the plane-weighted wing night factor.
- WEA: the region weather's `air_detection` (the static modifiers the engine already loads for `RegionWeather`) × the
  plane-weighted wing weather factor.
- Optional **`airDetectionOverride`** per side (mirrors `lock_air_det`) and targeted `air_detection` (T1, default 0).
- Defines: add `NAir.DETECT_CHANCE_FROM_OCCUPATION`, `DETECT_CHANCE_FROM_RADARS`,
  `DETECT_CHANCE_FROM_AIRCRAFTS_EFFECTIVE_COUNT`, `DETECT_CHANCE_FROM_AIRCRAFTS`, `NO_SEARCH_MISSION_DETECT_FACTOR`
  (`DETECT_CHANCE_FROM_NIGHT` exists); `NBuildings.RADAR_RANGE_*` only if coverage is computed. Replace
  `HardcodedValues.RegionAirDetection` (0) by the floor `RegionAirDetectionMin` = 0.01 [RE].

## A-DETECT-MIX: `air_cv_land_mix`, BUL's land bombers as defenders (follow-up, LOG + RE)

Engine replays (A-DETECT implemented, the share read before the decrement) gave BUL's land tactical bombers about a third
of the game's disruption (58.8k vs 179.3k over 18 runs). That gap is not a detection problem. It comes from escaped
carriers (rule 1); a second input, the land coverage (rule 2), settles the rest.

**1. Escaped carriers leave the region air war (LOG, high; static mechanism medium).**
- Before any carrier of either side reaches state 0x10 (escaped), the engine and the game agree. With rule 2's
  efficiencies (replay variant `fitout_d`, all 18 runs, hours before the first escape):

  | Wings | Engine | Game | Ratio |
  |---|---|---|---|
  | BUL land bombers | 7,872 | 7,957 | 0.989 (200 / 311 air hours within 1 %) |
  | ROM carrier bombers | 72.2k | 71.3k | 1.013 |
  | ROM land bombers | 733 | 725 | 1.011 |
- After the first escape the game/engine ratio of BUL's land bomber disruption climbs to 2–8. It starts at the hour a BUL
  carrier goes from state 8 (retreating, still counted) to 0x10, in every run checked (s4, s5, s16). Sunk carriers
  (0x20) the engine already drops.
- A hand model of the game's rules at s5 hour 393 gives 535–594 with BUL's escaped carriers left out (game 559, engine
  28). The model uses ROM's logged wings, BUL's land fighters at 34 effective planes, the bombers' weight
  `round(53 × det_ROM 0.034) = 2`, the share before the decrement and the BUL fighters' escort.
- Why the engine is low: `AirPhase.RegionAirWar` keeps every carrier that is not sunk ("escaped carriers' too"), and in
  the replay their wings keep stale plane counts. They add strike defenders (`sameStrike`) and escorts, so BUL's land
  bomber gets a small share and is fully escorted.
- Static side: when a ship becomes escaped (0x19739bd), a carrier's AirFX is moved out of the side group's air-object
  list `+0x58` into `+0x88` (0x1c6cc50; this is why the logger no longer lists its wings). The scramble list 0xc66480
  only tests `InCombat(ship+0x728)`, so the escaped ship's task force is presumably no longer the combat's. Not traced.
- **Rule:** carrier wings of escaped carriers (state 0x10) take no part in the region air war: no attack, no defence, no
  escort, no detection count. Retreating carriers (state 8) still do.

**2. Land mission effectiveness 0xf78500: coverage × the base's supply factor (RE + LOG).**
- The land branch, from [A-REGION-SYNC-EFF](A-REGION-SYNC-carrier-wing-region.md): `e = coverage × capacity_penalty ×
  supply × (1 − region_change_penalty) × (1 + air_mission_efficiency) × fuel`.
- Coverage is UpdateCoverage 0xf87480: `min(1, provinces of the region in range / region provinces)`.
  - Range in map pixels: `trunc(wing range / divisor)`, where the divisor is the global at 0x3335f50, most likely
    `MAP_SCALE_PIXEL_TO_KM` 7.114 (not confirmed by name).
  - The in-range test is 0x1012f80: a bounding-box precheck, then distance² from the air base position to each
    province.
- Burgas (state 211, base 329, TFB `map/buildings.txt` air_base at 3229.2, 1383.1) to region 30's provinces, from the
  positions in `data/sea-regions`: 7906 30.8 px, 5412 45.9, 8256 71.8, 8131 75.4, 8308 129.9, 8282 159.3, 8334 185.5.
  - Fighter II (900 km → 126 px) covers **4/7**; Fighter 0 (550 km → 77 px) covers the same 4.
  - Tactical bomber II (2000 km → 281 px) covers 7/7, as do ROM's bombers from Dobrogea (base 350).
- The save gives `capacity_penalty = 1` on every base (level 10, capacity 1000) and no fuel consumption, so the rest is
  the base entry's supply ratio × (1 + AME). BUL's land bombers log effectiveness 0.600 (0.602 at times), so that
  factor is **0.6** at Burgas with 200 planes.
- Hence BUL's land fighters: `4/7 × 0.6 = 0.343` → 34 effective planes in `air_cv_land_mix`. In `air_land_vs_fleet`
  (100 fighters alone) 0.46 = 4/7 × 0.805.
  - The 0.6 / 0.805 split between supply and `air_mission_efficiency` is not separable from the logs.
  - The fitted 0.31 / 0.33 were low because of rule 1.

**Numbers for the engine (this scenario):**

| Wings | `missionEfficiency` |
|---|---|
| BUL land fighters | **0.343** |
| BUL land bombers | 0.6 |
| ROM land bombers | 1.0 |
| `air_land_vs_fleet`'s BUL fighters | 0.46 |

The `det` values are unchanged (ROM 0.034–0.054, so BUL's 53–60 bomber planes weigh 2–3).

**3. For a free-running engine (LOG + save):** the template save's stockpiles refill land wings at each day tick
(A-REINFORCE), with any variant of the sub-unit that has the same missions. The wing's stats are then the plane-share
average of its variants (0xf661e0, A-STATS-CACHE).

| Country | Stockpile |
|---|---|
| BUL | 10,004 `fighter_equipment_0` (+ 7 GRE-made), 18 GRE `tac_bomber_equipment_0` |
| ROM | 10,005 `fighter_equipment_0` (+ 18 ITA-made), 3 `tac_bomber_equipment_0` |

- So BUL's fighter wing stays at 100 planes and turns into Fighter 0 over the battle (lower escort and attack).
- BUL's land bombers were refilled (87 → 100 in s5).
- The scenario needs `planeStockpile` with these planes for free runs; replays do not need it because they take planes
  from the log.

## How it was verified

Static reading of 0xf77730, 0xf7a460, 0xf861d0, 0xe21800, 0xe21640, 0x10a83f0, 0x10a3f20, 0x9d4190, 0xf75820, 0xf75690,
0xf80540 and the defender loop of 0xf7be70; modifier ids from the 0x55f7f0 registration table (ids 0x1, 0x2, 0x7, 0xb,
0xd, 0x15, 0x24, 0x57 match the names already in the index); define globals from `defmap.json`, the NBuildings globals
from their loaders (0x7efa30 / 0x7efe50 / 0x7efc40). The formula matches the tooltip text term by term. Log check above
(defender weight 2 / 1 for 100 land bombers in `air_cv_land_mix`, ≈ 45 effective fighters in both land scenarios).

Suggested in-game checks (tools/ingame, Frida): the return of 0xf77730 at 0xf81b08 (`[rsp+0x38]` after the call) per
attacking country; 0xf80540's `d.n` per defender; or the `lock_air_det BUL 1` console command with `air_land_vs_fleet`
(ROM's land bombers then weigh 100, so BUL's fighters engage 100 × 3 planes and shoot down far more).

## Differences from the current spec

- A-REGION: `det` is no longer [ASSUMED] 0; it is at least 0.01 and in the test battles 0.01–0.06 (table above).
- Defender weight: `max(1, round(n × k × (1 + stat37) × w))`, not `trunc` to an integer.
- Disruption share: `left` before the attacker's own decrement (`AirPhase.FightRegion` reads it after).
- With both fixed, land bombers' `disruptionFactor` should be 1 in `air_land_vs_fleet` (and the land fighters'
  `missionEfficiency` re-fitted).

## UNRESOLVED

- The land branch of the mission effectiveness 0xf78500 (`effective_planes_count` of land wings: BUL's 100 fighters fly
  ≈ 45 from base 329).
- `stat37` (wing+0x548; the equipment stat index order is built at runtime, not mapped). The `air_cv_land_mix` ratios
  are proportional to carrier wing planes (6/7 exactly), so it is 0 for carrier naval bombers.
- 0x702780 relation names; which state list `country+0x460` is; 0x9d37a0 (distance²) details and map wrap.
- The 0.28 % step of BUL's disruption power after hour 10 in `air_land_vs_fleet`.
- 0xf7d5e4 (naval strike / naval recon detection that also uses `det`); the `lock_air_det` value parsing.
- A-DETECT-MIX: how an escaped carrier leaves the scramble list (the task force of `ship+0x728` after 0x19739bd); the
  range divisor global 0x3335f50 by name; the split of the 0.6 / 0.805 base factor between supply and
  `air_mission_efficiency`.
