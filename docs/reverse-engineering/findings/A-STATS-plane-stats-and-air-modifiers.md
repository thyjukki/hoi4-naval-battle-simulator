# A-STATS: plane wing stats and the air country modifiers in a naval battle

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-STATS (plane stats with doctrine and spirit bonuses), A-DMG / A-HIT / A-AA-PREEMPT /
  A-INTERCEPT (the per-wing stats), A-REGION (wing stats 0xf5e890, accidents), A-EXTERNAL (join budget), A-SORTIE
  (traffic), S-DOCTRINE-EFFECT (where air doctrine and spirit effects act) (`docs/mechanics/air.md`, `surface.md`)
- **Confidence:** read from code only (static analysis). None of the logged in-game battles had an air doctrine or a
  plane bonus, so nothing here is confirmed in game. Per section below.

Method: `tools/re/` (`hoi4re.py`, `ctx.py`, `who.py`) plus throw-away scripts: an emulation of the stat token switch
0x13e81b0 over the static token map (0x831c0) to rebuild the full `EEquipmentStats` enum, an imm32 scan of `.text`
for every modifier id loaded in front of a modifier getter (0x55e360, 0xc38760, 0x15c70b0, 0x1972570, 0x61a930), and
a disp32 scan for wing fields. Modifier ids → script keys from the registration function 0x55f7f0 joined with the token
map (as in [S-STATS-COUNTRY](S-STATS-COUNTRY-modifier-sources.md)). Conventions as in [A-air-combat](A-air-combat.md):
RVAs, fixed point ×1e5, every product and quotient truncated toward zero. `wing` = CAirWing, `pool` = `[wing+0x38]`,
`mission` = the air mission embedded at `wing+0x90` (`type` = `wing+0xa0`, `executing_mission` = `wing+0xa4`), `owner` =
the wing's country (0xf5aad0: `wing+0x9c4` tag if set, else the pool's). Defines as vanilla / TFB.

## Summary

1. **A plane wing's stats are built exactly like a ship's (S-STATS-SHIP).** The wing caches a stat array at
   `wing+0x390` (0x4e stats, `EEquipmentStats`): for every equipment variant `v` in the wing, weighted by its share
   `w_v` of the wing's planes, `stat[s] += variant_v.final[s] × (1 + sub[s])`, where `sub` is the owner's copy of the
   pool's sub-unit (`cv_nav_bomber`, …) with every technology and doctrine/spirit sub-unit block that names the
   sub-unit or one of its `categories` summed in. **A sub-unit bonus `naval_strike_attack = 0.1` is a +10 % factor on
   the equipment stat, not a flat value**; no air combat stat is in the flat (additive) list.
2. Equipment bonuses (idea `equipment_bonus`, doctrine `equipment_bonus`, `add_equipment_bonus`, MIO, upgrades) act one
   level lower, in the variant's own `(1 + Σ)` (S-STATS-DESIGN), so they compound with the sub-unit factor.
3. Country modifiers are **not** in the cache. They are added by the combat getters, each in its own `(1 + Σ)` that
   multiplies the cached stat: naval attack / targeting / agility (`naval_strike_*_factor`, ace, mission efficiency,
   weather, night), region-war attack / defence / agility (`air_*_factor`, experience, ace, mission efficiency, home
   defence / power projection, weather, night). The naval interception uses the raw cached stats; its speed only the
   ace.
4. The cache is rebuilt every hour by the wing update (0xf62750) and whenever the executing mission is chosen, so a
   doctrine change reaches the planes within the hour. For the simulator the stats are fixed for the battle.
5. Air country modifiers that act in or around a naval battle: `naval_strike_targetting_factor` (and attack / agility),
   `sortie_efficiency`, `carrier_night_traffic`, `carrier_capacity_penalty_reduction`, `ace_effectiveness_factor`,
   `air_ace_generation_chance_factor`, `air_escort_efficiency`, the four mission efficiencies (`air_nav_efficiency`
   only for wings whose mission `type` has naval strike, i.e. **not** for carrier wings, whose `type` is 0),
   `air_mission_efficiency` (effectiveness, clamped to ≤ 1), `air_home_defence_factor` / `air_power_projection_factor`
   (region war), `air_attack/defence/agility_factor` (region war only), `air_weather_penalty` / `air_night_penalty` /
   `air_carrier_night_penalty_reduction_factor` (scale weather and night terms that are 0 in TFB for every naval
   reader), `air_untrained_pilots_penalty_factor` (experience malus, region war). **No reader in combat:** country
   `carrier_traffic` and country `air_maximum_speed_factor` (only an ace's own value is read). Full table below.

## A-STATS-ENUM: the stat indices

The full enum (78 stats, emulated from 0x13e81b0); the plane-relevant part:

| Idx | Stat | Idx | Stat |
|---|---|---|---|
| 0x2e | air_range | 0x35 | naval_strike_targetting |
| 0x2f | air_defence | 0x36 | air_ground_attack |
| 0x30 | air_attack | 0x37 | **air_visibility_factor** (the `stat37` of A-REGION's defender weight) |
| 0x31 | air_agility | 0x3e | maximum_speed |
| 0x32 | air_bombing | 0x41 / 0x42 | reliability / reliability_factor |
| 0x33 | air_superiority | 0x45 / 0x46 | fuel_consumption / _factor |
| 0x34 | naval_strike_attack | 0x4c | **night_penalty** (`rawStat(0x4c)` of the night factor; `airfx+0xe8`) |

Ship stats as in [S-STATS](S-STATS-design-stats.md) (0x13..0x2a, 0x3c..0x3f, 0x48, 0x4d); 0x2a is
`naval_weather_penalty_factor` and 0x39 `railway_gun_attack_range_index_in_define` (the two S-STATS left unnamed).

**Confidence:** high (every token of the static map run through the switch).

## A-STATS-CACHE: the wing stat cache 0xf661e0

`0xf661e0(wing, opt{missionMask})` (mask = the argument if present, else `wing+0xa0` = the mission `type`):

```
sub   = pool+0x20                       // the owner's copy of the sub-unit (0xf661a0: [country+0xf70] → 0xd11ae0)
block = wing+0x2f0; clear (0x101a120)   // stat array at block+0xa0 = wing+0x390
table = wing+0x968; reset to one entry per bit of missionMask (0xf66be0)   // per-mission arrays, 0x270 bytes each
total = Σ amount over the wing's equipment (0x1012920, wing+0x1d8)
for (variant v, amount a) in wing+0x1f8:
    w = a / total                                                      // fixed, truncated
    for s in 0..0x4d with v.final[s] != 0:                              // 0x1019ef0 → 0x142a580
        stat[s] += trunc( trunc((1 + sub[s]) × w) × v.final[s] )
    for bit in missionMask with an entry in v.mission_type_stats (v+0x370):
        for (s, x) in that entry:  table[bit][s] += f(x) − f(v.final[s])  // f(x) = x == 0 ? 0 : trunc(trunc((1+sub[s])×w)×x)
for each table entry e: e[s] += stat[s] for all s; then e += sub's reliability, mines, fuel terms (0x1019f30 → 0x142aaa0)
block += sub additive list × 1.0 (0x1019ea0 → 0x1429d30 base path → 0x1429580); clamps:  // as for ships
    build_cost_ic >= 0.1, reliability >= 0, supply_consumption >= 0
0xf667b0(wing)                          // available missions
```

- The additive list 0x1429580 (sub-unit value added flat, in addition to the factor) is the ship list: default morale,
  max organisation / strength, recon … suppression, mines, convoy raiding coordination, the hit chance and torpedo
  factors, `naval_weather_penalty_factor`, railway gun stats, reliability (+factor), weight, acclimatization, fuel.
  **None of 0x2e..0x37, 0x3e, 0x4c.** So for plane combat stats the sub-unit value is a pure factor.
- `mission_type_stats` (designer modules, e.g. vanilla interception agility) give per-mission arrays; the raw getter
  0xf60970 returns the array of the asked mission bit if the wing has one, else `wing+0x390`. TFB's fixed carrier
  planes (`cv_*_equipment_N` in `single_engine_airframe.txt`) have no modules, so the per-mission array equals the base.
- **Refresh:** 0xf62750 (wing hourly update, every hour from the strategic-air job 0xc46030, A-REGION-ORDER step 1),
  0xc49e80 / 0xc4a2f0 (with mask = the executing mission just chosen, steps 2), wing creation 0xc505f0 / 0xc508f0, load
  0xf62bc0, 0xf674e0. `pool+0x20` is pointed at the owner's copy by 0xf661a0 (per pool, from the country function
  0xc62ba0); the copy is rebuilt in place when a technology or doctrine source changes (0x101b1b0, S-DOCTRINE).

**Same as ships.** 0x142a580 is the wing twin of the ship's 0x142b5f0 ("AddWithMultiplier", S-STATS-SHIP): variant
stat × (1 + sub-unit stat), then the same additive list and clamps. The only differences: a wing averages over its
variants by plane share, and it keeps per-mission arrays.

**Evidence.** 0xf661e0..0xf667a3; 0x142a580 (`(sub + 0x186a0) × w`, then `× variant[+0x100]`, `add [dst]`, 78 stats);
0x142aa30; 0x1429580 (list rebuilt with the enum); 0x142aaa0 (only stats 0x23, 0x24, 0x41, 0x42, 0x45, 0x46);
0x1429d30 base path at 0x142a004; 0xf661a0; 0xf60970 (`wing+0x968` lookup 0xf5e400, else `wing+0x390`).

**Confidence:** high for the formula, the factor nature and the refresh points; medium that the country copy is
updated in place (the pointer is only re-resolved by 0xc62ba0).

## A-STATS-SUBUNIT: what `sub[s]` holds

```
sub[s] = definition[s]                                                   // TFB air.txt: no plane stats → 0
       + Σ researched technologies: level × (block[sub-unit name][s] + Σ_category block[category][s])
       + Σ doctrine / spirit bonus sources (grand doctrine, subdoctrines, rewards, milestones; [country+0xf70]+0x08):
             1.0 × (block[sub-unit name][s] + Σ_category block[category][s])        // S-DOCTRINE-EFFECTS
```

The categories come from `common/units/air.txt` (TFB `cv_nav_bomber`: `category_nav_bomber category_carrier_nav_bomber`;
`cv_fighter`: `category_fighter category_carrier_fighter`), so a carrier naval bomber gets both
`category_nav_bomber` and `category_carrier_nav_bomber` blocks, summed. The block keys go to the same stat indices
(the sub-unit stat array is the `EEquipmentStats` array, see the additive list's names). TFB technologies carry no
plane combat bonuses (only `category_nav_bomber` mines planting / sweeping in `MTG_naval_Support.txt`).

**Confidence:** high (S-DOCTRINE-EFFECTS, S-STATS-SHIP, same builder 0x101b1b0).

## A-STATS-VARIANT: equipment bonuses

`variant.final[s]` is S-STATS-DESIGN unchanged: `(base + module/upgrade adds + average) × (1 + Σ module mult + Σ idea
equipment_bonus + Σ named equipment bonus (doctrine `equipment_bonus`, `add_equipment_bonus`) + MIO (hit chance factors
excepted))`. For a fixed TFB plane the base is the equipment's own value (`cv_nav_bomber_equipment_2` naval strike
attack 14). The TFB air doctrines and spirits in `data/` have no `equipment_bonus`.

So the order is: **equipment bonuses (variant) → sub-unit factor (wing cache) → getter `(1 + Σ country, ace, …)`**,
three separate multipliers, each truncated.

## A-STATS-GETTERS: how the combat reads the cache

`stat(i)` = 0xf60970(wing, i, missionFlags) (cache, no modifiers).

**Naval strike** (0x1978e30, per strike wing, `prov` = battle province, flags = `executing_mission`):
```
attack    0xf60f10 = trunc( (1 + ace(0x16) + owner.naval_strike_attack_factor + M + R(0x16)) × trunc(stat(0x34) × 0.1) )
                     (the base × AIR_NAVAL_KAMIKAZE_DAMAGE_MULT when flags == 0x80 kamikaze and prov > 0)
targeting 0xf61280 = trunc( (1 + ace(0x17) + owner.naval_strike_targetting_factor + R(0x17)) × trunc(stat(0x35) × 0.1) )
agility   0xf60bc0 = trunc( (1 + ace(0x18) + owner.naval_strike_agility_factor + M + R(0x18))
                            × trunc(stat(0x31) × AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY) )          // 0.02
M = mission efficiency by the mission TYPE (wing+0xa0), R = weather/night terms; both only if prov > 0 and the
    mission has a strategic region (A-ACE-STATS)
```
**Naval interception** (0x197b380): air attack 0x30, defence 0x2f, agility 0x31 are `stat(i)` as is; speed is
0xf60760 = `trunc(stat(0x3e) × (1 + ace(0x2) + [ace template has 0x2] owner.air_ace_bonuses_factor))`, read from
`wing+0x580` directly (never the per-mission array). **The country's `air_maximum_speed_factor` is never read**
(0xf607f3 reads the ace template's container only to test the flag). The AirFX averages `airfx+0xc8 / d0 / d8 / e0 /
e8` (0x197d4a0) are the plane-weighted `stat` 0x31 / 0x34 / 0x35 / 0x3e / 0x4c (agility, naval attack, targeting,
speed, **night_penalty**) of the strike wings.

**Region air war and the disruption efficiency** (0xf5e890(wing, out, prov, missionBits), A-REGION-COMBAT):
```
mA, mD, mG = 1 + Σ air_attack/defence/agility_factor over: owner (+0x5a8 holder), experience holder (wing+0x228/+0x238),
             ace × sizeF (+ owner.air_ace_bonuses_factor per positive ace entry)                        // 0xf5d3b0
if prov > 0 and mission region:
    M  = missionBits ? 0xf7ed40(missionBits) : 0xf7ed40(type);  missionBits == 8: mD += air_strategic_bomber_defence_factor
    mA/mD/mG += regionWeather(0x6f/0x70/0x71) × weatherF + [darkness > DAY_NIGHT_COVERAGE_FACTOR] night(0x6f..0x71) × nightF
    base region == prov's region ? mA, mD += air_home_defence_factor : mA, mD += air_power_projection_factor
            (land base: the base province's region; carrier: the region of the fleet's location, 0xd6d160)
attack = trunc((M + mA) × stat(0x30)), defence = trunc((M + mD) × stat(0x2f)), agility = trunc((M + mG) × stat(0x31))
```
Call sites: attacker 0xf7be70 and defender 0xf862a0 with `prov` and the executing mission; `e_i` 0xf78160 with the battle
province and the executing mission; escorts 0xf77360 and the attacker sort with `prov = 0` (no M, no weather, no home).

**Range** 0xf5e5c0 = `stat(0x2e) × (1 + ace(0x5) + bonuses + owner.air_range_factor)` (coverage of land missions only).

**Worked example (TFB).** `cv_nav_bomber_equipment_2`, naval strike attack 14, no equipment bonus; air doctrine
`naval_torpedo_tactics` (`category_carrier_nav_bomber` +0.05) and its reward `specialized_torpedoes` (+0.04):
`sub = 0.09`, cache `stat(0x34) = trunc(1.09 × 14) = 15.26`, per-plane naval attack `trunc(15.26 × 0.1) = 1.526`, then
`× (1 + naval_strike_attack_factor + …)` in the getter, `× (1 + navy_carrier_air_attack_factor)` on the AirFX average
(A-DMG), `× NAVAL_STRIKE_CARRIER_MULTIPLIER`. Without the doctrine: 1.4.

**Confidence:** high for the getters (0xf60f10 and 0xf5e890 read in full again; 0xf60760; 0x197d4a0 offsets); high
that the country `air_maximum_speed_factor` has no reader (imm scan: only 0xf607da / 0xf607f3).

## A-STATS-MISSION-EFF: which mission efficiency (0xf7ed40)

`M = first match on the bits b: b & 0x4 → air_intercept_efficiency; b & 0x1 → air_superiority_efficiency; b & 0x2 →
air_cas_efficiency; b & 0x110 → air_nav_efficiency; else 0` (owner's country container only).

- Naval strike getters pass the mission **type** (`wing+0xa0`, the assigned missions), not the executing mission.
  Carrier wings have `type = 0` (game save and the logged battles: `"type": 0, "executing": 16`), so **M = 0 for
  every carrier wing in the naval strike: `air_nav_efficiency` does not raise carrier strikes.** A land-based wing on a
  naval strike mission (`type = 0x10`) gets `air_nav_efficiency` in attack and agility (not targeting). A land wing
  whose `type` also has interception, air superiority or CAS gets that efficiency instead (first match).
- The region war and `e_i` pass the **executing** mission: carrier fighters executing air superiority get
  `air_superiority_efficiency`, interception `air_intercept_efficiency`, carrier naval bombers executing 0x10
  `air_nav_efficiency`, in attack, defence and agility.

**Confidence:** high.

## A-STATS-EFFECTIVENESS: air keys in the mission effectiveness 0xf78500

Only the keys are listed here; the carrier branch is decoded elsewhere.
```
dist = isCarrier ? 0 : coverage(+0x50) − 1                // coverage from range (air_range, air_range_factor)
crowd = 0xc53fd0(base) − 1                               // base crowding (carrier: deck, carrier_capacity_penalty_reduction, A-SORTIE)
sup  = a4 ? 0 : 0xc54ed0(base) − 1                       // supply
mEff = regionWeather(mission region).air_mission_efficiency          // weather blocks: rain −0.1 / −0.3, snow −0.1, …
     + owner.air_mission_efficiency
     + (carrier base, fleet admiral alive: admiral container A (+0x418).air_mission_efficiency)
e = (1 + dist) × (1 + crowd) × (1 + sup);  if !isCarrier and !(type & 0x60): e ×= 1 − region_change_penalty
e ×= 1 + mEff
fuel ratio f < 1: e ×= MISSION_EFFICIENCY_MULT_AT_LACK_OF_FUEL + (1 − MULT) × f      // 0.25
if mission region: e ×= 1 + owner's per-strategic-region modifier air_mission_efficiency (country+0x4d8, 0x6f2650)
effectiveness = clamp(e, 0, 1)
```
So `air_mission_efficiency` only helps a wing whose effectiveness is below 1 for another reason (rain, crowding, low
coverage, supply, fuel); with nothing wrong it is capped at 1. It reaches the naval battle only through
`effective_planes_count` (A-REGION: the region war plane count; A-EXTERNAL: a land wing's join budget). It is not in
the naval strike damage.

**Confidence:** high for the terms and the clamp; medium for the meaning of `a4` and 0xc54ed0 (supply), and for the
per-region container `country+0x4d8`.

## A-STATS-XP: the experience holder and `air_untrained_pilots_penalty_factor` (0xf65f20)

```
L = NAir.AIR_WING_XP_LEVELS (vanilla {100, 300, 700, 900}); xp = wing+0x218
v = xp < L[0] ? clamp(xp / L[0] − 1, −1, 0) : clamp((xp − L[0]) / (L[last] − L[0]), 0, 1);  v truncated to 0.1
                                       // corrected (was 0.01): 0xf6602d, A-ORDER-air-objects-leaving-the-strike-list.md
u = wing+0x9c4 tag set ? 1 + max(owner.air_untrained_pilots_penalty_factor, −1) : 1        // cached wing+0xa10
holder(wing+0x238) = v > 0 ? air_wing_experience_bonus_max × v : air_wing_experience_malus_min × (−v × u)
```
Skipped for missile sub-units. Rebuilt every hour (0xf62750), at creation and load. `wing+0x9c4` (save key `tag`) is
set at creation to the creating country and written in every save (tools/ingame writes it too), so the factor
normally applies. A fresh wing (xp 0) carries the full malus: TFB `air_attack/defence/agility_factor −0.15`,
`naval_strike_attack_factor −0.15`, `air_night_penalty +0.15`. In a naval battle the holder is read only by 0xf5e890
(region war) and by weatherF / nightF; the naval strike getters ignore it (A-ACE-STATS).

**Confidence:** high for the formula; medium for the static block names (+0xc8 / +0xd0).

## A-STATS-ACCIDENT: no accidents for carrier wings without a mission type

The accident chance 0xf61d70 returns 0 at once when the mission `type` (`wing+0xa0`) is 0 (and when the wing has no
planes). Otherwise it is 0xc4daf0(base, owner, reliability stat 0x41 of the mission, experience `air_accidents_factor`,
training flag), which reads `ACCIDENT_CHANCE_BASE + owner.air_accidents`, `(1 − min(rel, 1)) ×
ACCIDENT_CHANCE_RELIABILITY_MULT`, the weather `air_accidents` of the base's region, `ACCIDENT_CHANCE_CARRIER_MULT`,
`AIR_WING_XP_TRAINING_MISSION_ACCIDENT_FACTOR` and `air_accidents_factor` (owner and experience holder); how the terms
combine was not decoded. **Carrier wings in a naval battle (`type = 0`) therefore never have
accidents**, which corrects A-REGION-ACCIDENT; land wings joining a battle do.

**Confidence:** high for the `type == 0` exit (0xf61d7b); low for the full chance.

## A-STATS-MODS: air country modifiers in a naval battle

"Naval strike" = 0x1978e30 / 0x197b890; "region war" = the strategic-air hour before the naval hour (A-REGION);
"join" = a land wing joining (A-EXTERNAL). Id = modifier id (0x55f7f0).

| Key (id) | Reader (RVA) | Formula | Effect in a naval battle |
|---|---|---|---|
| `naval_strike_attack_factor` (0x16) | 0xf60f10 | `+` in the attack getter's `(1 + Σ)` | strike damage (in engine) |
| `naval_strike_targetting_factor` (0x17) | 0xf61280 | `+` in the targeting getter's `(1 + Σ)` | strike hits |
| `naval_strike_agility_factor` (0x18) | 0xf60bc0 | `+` in the agility getter's `(1 + Σ)` | preemptive AA chance |
| `air_nav_efficiency` (0x10) | 0xf7ed40 ← 0xf60f10, 0xf60bc0, 0xf5e890 | M, `+` in `(1 + Σ)` / `(M + m)` | land naval-strike wings' attack and agility (not targeting); region war and `e_i` of carrier bombers executing 0x10; **not carrier strikes** (`type` 0) |
| `air_superiority_efficiency` (0xb) | 0xf7ed40 ← 0xf5e890 | M | region war of carrier fighters executing AS (attack, defence, agility) |
| `air_intercept_efficiency` (0xd) | 0xf7ed40 ← 0xf5e890; targeted 0xf75690 | M; disruption `× (1 + targeted value)` | region war of wings executing interception; the **targeted** (`targeted_modifier`) value vs the defender scales disruption (A-REGION `T13`) |
| `air_cas_efficiency` (0xf) | 0xf7ed40 | M | only wings whose type / executing has CAS |
| `air_mission_efficiency` (0x11) | 0xf78500 (0xf788c4, 0xf788f2, 0xf7896e) | `e ×= 1 + Σ`, effectiveness clamped ≤ 1 | effective planes in the region war and a land wing's join budget, only if e < 1 otherwise |
| `air_attack_factor` / `air_defence_factor` / `air_agility_factor` (0x6f/0x70/0x71) | 0xf5d3b0 ← 0xf5e890 | `m = 1 + Σ` (country, experience, ace) | region war, escorts, `e_i` denominator; **not** naval interception or strike |
| `air_maximum_speed_factor` (0x2) | 0xf60760 (ace only) | — | **none** from a country (only an ace's own value) |
| `air_escort_efficiency` (0xc) | 0xf77360 | escort `× (1 + v)` | escorts against disruption (A-REGION-DISRUPT) |
| `air_home_defence_factor` / `air_power_projection_factor` (0x74/0x75) | 0xf5e890; 0xf77730 | `+` to mA, mD (home: the province's region is the base's; carrier: the fleet's region) | region war and `e_i`; carrier wings in their battle region get home defence; detection weights (A-DETECT) |
| `air_weather_penalty` (0x106) | 0xf5c9d0 | weatherF = `clamp(1 + owner + xp + ace, 0, 2)` × weather-holder value | scales weather `air_*_factor` / `naval_strike_*_factor`: **0 in vanilla and TFB**; not the weather `naval_strike` of A-HIT; detection (A-DETECT) |
| `air_night_penalty` (0x105) | 0xf5bc80 | nightF = `clamp(1 + owner + xp + stat 0x4c + ace − carrier reduction, 0, 2)` × night-holder value | region war at night in **vanilla** (night `air_attack_factor −0.5`); TFB night has none → no effect; naval getters: no night values; detection |
| `air_carrier_night_penalty_reduction_factor` (0x10d) | 0xf5bc80 (0xf5bded) | `−` in nightF, carrier base whose task force has a naval mission (`tf+0x374 ≠ 0`) | as `air_night_penalty` (vanilla region war only) |
| `air_strategic_bomber_night_penalty` (0x104) | 0xf5bc80 | nightF, type & 0x8 | none (strategic bombers) |
| `air_strategic_bomber_defence_factor` (0x76) | 0xf5e890 | mD, missionBits == 8 | none |
| `air_untrained_pilots_penalty_factor` (0x73) | 0xf65f5a (0xf660b5) | malus weight `× (1 + max(v, −1))` | experience malus of inexperienced wings in the region war (and nightF / weatherF) |
| `air_ace_generation_chance_factor` (0x7) | 0xf80f60, 0xf7c6b1 | `× (1 + v)` | ace earn after a strike / in the region war (A-ACE) |
| `ace_effectiveness_factor` (0x25a) | 0x61a930 | ace value `× (1 + v)` | ace bonuses (A-ACE) |
| `air_ace_bonuses_factor` (0x72) | 0xf5d3b0, 0xf60760, 0xf5e5c0 | `+` once per ace entry | ace wings (A-ACE) |
| `sortie_efficiency` (0x24) | 0x1976db0 via 0x1972570 | `+` to the carrier sortie base (ship sum includes the country) | carrier sortie (A-SORTIE, in engine) |
| `fighter_sortie_efficiency` (0x1b6) | 0x1976db0 | same, fighter first wing | A-SORTIE |
| `carrier_traffic` (0x22) | 0x1976db0 (weather and night holders only) | — | **none** from a country (TFB `all_weather_operations` +0.05, `unceasing_airpower` +0.15 do nothing in combat) |
| `carrier_night_traffic` (0x23) | 0x1976db0 (0x1976f4b) | `+` in the night factor when dark | A-SORTIE (in engine) |
| `carrier_capacity_penalty_reduction` (0x10c) | 0xc57a89 (crowd), 0x12cb230 (UI) | `CAPACITY_PENALTY × (1 + v)` | deck crowding: sortie (A-SORTIE) and effectiveness |
| `air_range_factor` (0x5) | 0xf5e5c0 | range `× (1 + Σ)` | coverage → effectiveness of land wings (join budget); none for carriers |
| `air_accidents_factor` / `air_accidents` (0x65/0x64) | 0xc4daf0, 0xf61e5f | accident chance | land wings only (carrier wings `type` 0 → chance 0) |
| `air_mission_xp_gain_factor` (0x1b5) | 0xf5dfa0 | XP gain `× (1 + v + ace)` | wing XP in the region war → experience holder next hour (minor) |
| `air_wing_xp_loss_when_killed_factor` (0x6) | 0xf63a56 (RemovePlanes 0xf63580) | XP loss per killed plane | wing XP after losses, also naval interception / AA (0x197a1dc) (minor) |
| `air_superiority_detect_factor` / `air_interception_detect_factor` (0x9/0x8) | 0xf77730 | detection weights | region detection `det` (A-DETECT) |
| `port_strike` (0x10b) | 0xbb8380 (from 0xf82ca0) | external efficiency `× (1 + v)`, ≥ 0.001 | port-strike combats only |
| `spotting_chance` (0x55), `night_spotting_chance` (0x108) | 0xfaa680 (navalmission.cpp) | fleet spotting | before the battle only (whether it starts); none inside |
| `air_fuel_consumption_factor` (0x19e) | 0xf5f95a | fuel use | indirect: fuel stock → effectiveness / XP; none in the simulator |
| `air_cas_present_factor`, `army_bonus_air_superiority_factor`, `air_close_air_support_org_damage_factor`, `ground_attack_factor`, `air_strategic_bomber_bombing_factor`, `strategic_bomb_visibility`, `air_training_xp_gain_factor`, `experience_gain_air(_factor)`, `air_manpower_requirement_factor`, `mines_*_by_air_factor`, intel, design-cost, mastery-gain, advisor and doctrine cost keys | land, strategic, mine, intel, economy code | — | none in a naval battle |

Sub-unit block stats (`category_* = { … }`, A-STATS-SUBUNIT): `air_attack`, `air_defence`, `air_agility`,
`maximum_speed`, `naval_strike_attack`, `naval_strike_targetting`, `air_range`, `air_bombing`, `air_ground_attack` are
all factors on the cached stat. In a naval battle: `naval_strike_attack` / `_targetting` and `air_agility` (strike,
AA chance, interception, region war), `air_attack` / `air_defence` (interception, region war), `maximum_speed`
(interception, region war speed terms), `air_range` (land coverage only); `air_bombing` / `air_ground_attack` none.

## Resolved open items of other findings

- A-REGION `stat37` (`wing+0x548`) = stat 0x37 `air_visibility_factor`: defender weight `× (1 + air_visibility_factor)`.
- A-REGION targeted modifier slots: slot 1 = `air_detection` (0x1) and slot 0xd = `air_intercept_efficiency`, both from
  a targeted-modifier entry looked up by the defender's country (entry +0x130 via 0xd23780, the layout of the
  `country+0xf88` table; that the table is the attacker's is read from the call sites, medium). Only `targeted_modifier`
  blocks set these two terms; the plain country `air_intercept_efficiency` acts only as M.
- A-air-combat `airfx+0xe8` = plane-weighted stat 0x4c `night_penalty` of the strike wings (and `+0xc8..+0xe0` =
  agility, naval attack, targeting, speed).
- A-REGION-ACCIDENT: carrier wings with `type = 0` have no accidents (A-STATS-ACCIDENT).

## Rules for the engine

1. Plane stats: `stat = equipmentStat × (1 + Σ equipment bonuses)` (variant) then `× (1 + Σ sub-unit bonuses)` where
   the sum takes every active doctrine / spirit / technology block naming the plane's sub-unit or any of its
   categories (`PlaneBonus.AppliesTo`), each added once. Apply it to air_attack, air_defence, air_agility,
   maximum_speed, naval_strike_attack, naval_strike_targetting, air_range. Reliability is `× (1 + b) + b` (additive
   list; only accidents). This is what `PlaneStatRules.WithBonuses` does (keep it; reliability aside).
2. Getters on top, separate multipliers: naval attack / agility `× (1 + naval_strike_*_factor + ace + M)`, targeting
   `× (1 + naval_strike_targetting_factor + ace)`; `M = 0` for carrier wings, `air_nav_efficiency` for land-based
   naval-strike wings. Country `air_attack/defence/agility_factor` only in the region war; interception uses the
   cached stats; speed `× (1 + ace)` only.
3. Region war (`RegionAirWarRules`): add `M` by the executing mission (AS → `air_superiority_efficiency`, interception
   → `air_intercept_efficiency`, naval strike → `air_nav_efficiency`), `+ air_home_defence_factor` to attack and
   defence (carrier wings in their battle region; land wings: home if their base is in the battle's region, else
   `air_power_projection_factor`), and the experience malus `× (1 + air_untrained_pilots_penalty_factor)` for wings
   below the first XP level. Escorts use none of these.
4. Ignore (record only) country `carrier_traffic`, country `air_maximum_speed_factor`, `air_weather_penalty`,
   `air_night_penalty`, `air_carrier_night_penalty_reduction_factor` (no nonzero target in TFB; vanilla night
   `air_attack_factor −0.5` in the region war is the one exception), `spotting_chance`, `port_strike`.
5. `air_mission_efficiency`: only in the effectiveness `clamp(… × (1 + Σ), 0, 1)`; with the simulator's hand-set land
   efficiency it can raise a value below 1, never above.

## Defines and constants involved

| Name | Vanilla / TFB | Role |
|---|---|---|
| `NAir.AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY` | 0.02 / 0.02 | naval agility base |
| `NAir.AIR_NAVAL_KAMIKAZE_DAMAGE_MULT` | 20 / 10 | kamikaze naval attack |
| `NAir.AIR_WING_MAX_STATS_ATTACK / DEFENCE / AGILITY / SPEED` | 100/100/100/800, 500/300/400/1100 | normalised region-war stats |
| `NAir.DAY_NIGHT_COVERAGE_FACTOR` | 0.5 / 0.5 | night term gate |
| `NAir.AIR_WING_XP_LEVELS` | {100, 300, 700, 900} (TFB same) | experience holder |
| `NAir.MISSION_EFFICIENCY_MULT_AT_LACK_OF_FUEL` | 0.25 / 0.25 | effectiveness fuel term |
| `NAir.ACCIDENT_CHANCE_BASE / _RELIABILITY_MULT / _CARRIER_MULT` | 0.1 / 2 / 1.5 (TFB same) | accident chance |
| `NAir.FIELD_EXPERIENCE_FACTOR`, `AIR_WING_XP_MAX`, `AIR_WING_XP_LOSS_WHEN_KILLED` | 0.7, 1000, 300 (TFB same) | wing XP |
| hard-coded 0.1 (`imul 0x2710`) | | naval attack / targeting base |
| hard-coded clamp [0, 2] | | weatherF / nightF |
| hard-coded clamp [0, 1] | | effectiveness |

## How it was verified

Static reading only; no in-game battle so far had a plane bonus (TFB technologies have none for naval bombers, and the
logged battles had no air doctrine), and the logger does not record the strike's stat averages. Cross-checks: the
wing cache uses the same helpers (0x1019ef0 / 0x1019ea0 → 0x142a580 / 0x1429d30) as the ship's 0xc3d090; the logged
carrier wings have `type = 0` (`air_batch1` raw.jsonl), which fixes M = 0 and the accident exit; the per-hit damage of
the logged carrier mirrors matched the simulator without any sub-unit factor (A-REGION), as expected with no bonus.

Suggested in-game check (tools/ingame): give one side `air_subdoctrine_naval_torpedo_tactics` (+0.05
`category_carrier_nav_bomber` naval_strike_attack) and `naval_expertise` (+0.05 `naval_strike_targetting_factor`), log
at 0x1979245 / 0x1979313 the getter returns (`[r14]` after the call) or the AirFX averages after 0x197d4a0
(`airfx+0xc8..+0xe8`) and the wing cache `wing+0x390+8·0x34`: expected `trunc(1.05 × 14) × 0.1 = 1.47` per plane for
`cv_nav_bomber_equipment_2`, targeting `1.0 × 1.05`.

## Differences from the current spec

- A-STATS: confirmed (was [ASSUMED]): sub-unit bonuses are factors on the equipment stat, summed over the sub-unit and
  all its categories, compounding with equipment bonuses and with the getters' country factors.
- A-DMG / A-HIT: the mission efficiency M is 0 for carrier wings; `air_nav_efficiency` acts only for land-based
  naval-strike wings (attack and agility).
- A-REGION: add M by the executing mission, home defence / power projection and the experience malus (with
  `air_untrained_pilots_penalty_factor`) to the wing stats; name `stat37` and the targeted slots; carrier wings have
  no accidents.
- A-SORTIE: country `carrier_traffic` has no effect (only the weather and night holders).
- A-INTERCEPT: the country's `air_maximum_speed_factor` has no effect.

## UNRESOLVED

- The full accident chance 0xc4daf0 (terms listed, combination not decoded).
- 0xf78500's `a4` argument, the supply term 0xc54ed0 and the per-region container `country+0x4d8` (who fills it).
- Whether 0xc62ba0 (pool → country sub-unit copy) runs again after a doctrine change, or the copy is always updated in
  place (needed only for mid-game changes).
- The exact XP-loss formula in RemovePlanes (0xf63a02..0xf63ae4, 0xf63e00).
- 0xd6d160 (the fleet's region for home defence) was read only to its first branch.
