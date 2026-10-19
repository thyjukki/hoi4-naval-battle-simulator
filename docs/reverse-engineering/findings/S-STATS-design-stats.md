# Findings: ship design stats and per-ship stats (HOI4 1.19.3)

Area: how a naval equipment variant (hull + modules) gets its final stats, how technology, MIO, idea and named
equipment bonuses enter, how the ship's own stat block is built from the variant, and which modifiers are only
applied by the combat getters. Proposed spec IDs: S-STATS-DESIGN, S-STATS-AVG, S-STATS-BONUS, S-STATS-MIO,
S-STATS-SHIP, S-STATS-PARTS, S-STATS-CLAMP, S-STATS-COMBAT.

Method: static analysis of hoi4.exe 1.19.3.0 (SHA-256 `7DC947BE…A031D5`) with `tools/re/`, plus TFB
(workshop 2559317737) and vanilla script files. Fixed point 1e5, truncating multiply and divide (see the findings
README).

## Function map (RVAs)

| RVA | Role |
|---|---|
| 0xbe0260 | `CEquipmentVariant::RecalculateStats(bool)`: rebuilds the variant, calls 0xbdfc80 (normal path) or 0xbdfa70 |
| 0xbdfc80 | Final variant stats with per-source breakdown: `final[s] = (add + avgSum/avgCnt) × (1 + mult)` for all 0x4e stats, written to `variant+0x100+8·s` |
| 0xbdfa70 | Same formula without the breakdown (second caller path of 0xbe0260) |
| 0x193cdf0 | Asserts `Components.GetSize() >= NUMBER_OF_EQUIPMENT_STATS` (stat_helper.cpp), calls 0x193b080 |
| 0x193b080 | Gathers the four components `{add, avgSum, avgCnt, mult}` of every stat from all sources (frame, idea bonuses, named bonuses, MIO, upgrades, modules) |
| 0x193ade0 | Single-stat version of 0x193b080 (called through 0x193cc10) |
| 0x193d170 | Adds one stat container (`add_stats`, `multiply_stats`, `add_average_stats`) × weight to all components |
| 0x193ca70 | Single-stat version of 0x193d170 |
| 0x6f3920 | `variant+0xd0`: list of idea equipment bonuses (leaf getter) |
| 0xbd5b40 | Resolves the variant's named-equipment-bonus handles (`variant+0xe8`) in the country (`+0xf68`); "Failed to find named equipment bonus by handle." |
| 0xbd3b80 | Refreshes the MIO bonus of a variant: `variant+0x498 = 0xdb7a10(MIO, variant.type)`, then 0xbe0260 |
| 0xdb7800 (0xdb7a10 mode 1 = equipment, 0xdb7a30 mode 2 = production) | Sums `equipment_bonus` of the MIO's unlocked traits that pass `limit_to_equipment_type` (0xa4fe60), the initial trait and the country's MIO policy (0xa4b260) |
| 0xe5f600 | Country adds an idea equipment bonus (`country+0x160`); if `instant` (`bonus+0x70`) also adds it to every existing variant (`+0xd0`) that it applies to (0x631e70) and recalculates |
| 0xe5f740 | Same without the country list (re-application path) |
| 0xe65f30 | Removes the country's idea bonuses: only `instant` ones are removed from existing variants |
| 0x631e70 | `bonus.AppliesTo(equipmentType)` |
| 0x13e81b0 | Script token id → `EEquipmentStats` index (switch; see the enum table below) |
| 0xc3d090 | `CShip::RefreshStats`: builds the ship stat block `ship+0x120+8·s` from the variant, the country's sub-unit definition and damaged parts; rescales current HP; max organization |
| 0x1019e50 → 0x1429cb0 → 0x142b5f0 | Adds the variant's final stats into the ship block, each × `(1 + subUnitStat[s])` ("AddWithMultiplier") |
| 0x1019ea0 → 0x1429d30 | Adds a sub-unit stat set to a block; with the "base" flag it goes through 0x1429580 (additive list), then clamps |
| 0x1429580 | Adds the sub-unit's own stats for a fixed list of stats (org, hit chance factors, HP, reliability, …) |
| 0xd11ae0 | Country's per-sub-unit stat object (`country+0xf70`, by sub-unit index); falls back to the database definition (0xac4f00) |
| 0xd0e200 | Creates the country copy of a sub-unit (0x3c77d0) and applies all technology bonuses to it (0x101b1b0) |
| 0x101b1b0 / 0x193d370 / 0x193d2a0 | For every researched technology (level `+0x174`), add its bonus block for the sub-unit type (0x643110) and for each sub-unit category (0x6430e0), × level |
| 0xee3e70 | On technology researched: enables sub-units (`tech+0x98`) through 0xd0e200 |
| 0xc34a50 | Combat attack/armor getter: `max(0, stat × (1 + Σ factors))`, kind 0 light, 1 heavy, 2 sub attack, 3 torpedo, 4 armor |
| 0xc3a5d0 / 0xc3a580 / 0xc3a890 / 0xc3ac00 / 0xc3a520 | Light attack / heavy attack / sub attack / torpedo attack / armor (call 0xc34a50 with kind 0/1/2/3/4) |
| 0xc3a740 | Combat speed: `stat[naval_speed] × max(0.1, 1 + naval_speed_factor + leader + fuel penalty)` |
| 0x55f7f0 | Modifier registration (modifier id → script name), used to name the ids below |
| 0x831c0 | Static token registration (script keyword → token id); used with 0x13e81b0 to rebuild the stat enum |

### Struct fields

| Object | Offset | Meaning |
|---|---|---|
| equipment type (hull) | `+0x148 + 8·s` | base stat `s` of the frame (hull) |
| variant | `+0x3f0` | equipment type (hull) |
| variant | `+0x68` (list at `+0x70`, count `+0x7c`) | equipment upgrades: `{upgrade*, uint8 level}`; upgrade stat container at `upgrade+0x40` |
| variant | `+0xb8` (count `+0xc4`) | installed modules, one entry per filled slot `{slot, module*}` |
| variant | `+0xd0` (count `+0xdc`) | idea equipment bonuses (stat list at `bonus+0x40`, count `+0x4c`) |
| variant | `+0xe8` (count `+0xf4`) | named equipment bonus handles (stat list at `+0x48`, count `+0x54`) |
| variant | `+0x100 + 8·s` | final stats (0x4e entries) |
| variant | `+0x414` | Σ upgrade levels |
| variant | `+0x488` | MIO (design team) handle |
| variant | `+0x498` (count `+0x4a4`) | summed MIO equipment bonus `{token, value}`, token → stat via 0x13e81b0 |
| module | `+0x128` | stat container: `+0x138` add_stats, `+0x158` add_average_stats, `+0x178` multiply_stats (each a list of `{int stat, int64 value}`, 16 bytes) |
| bonus | `+0x70` | `instant` flag |
| country | `+0x160` | idea equipment bonuses; `+0xa0` the country's variants; `+0xf68` named bonuses / MIO policies; `+0xf70` per-country sub-units |
| ship | `+0x120 + 8·s` | ship stat block (`ship+0x80` is the sub-unit stats object, its stat array starts at `+0xa0`) |
| ship | `+0x6f8` | current HP; `+0x700` current organization |
| ship | `+0x918` (count `+0x924`) | damaged critical parts `{part*, int count}`, part stat list at `part+0x58` (count `+0x64`) |
| sub-unit stats | `+0x60 + 8·s` | sub-unit stat array (definition values + technology bonuses) |

### `EEquipmentStats` (rebuilt from 0x13e81b0, token ids from 0x831c0)

`0x13` surface_detection, `0x14` sub_detection, `0x15` surface_visibility, `0x16` sub_visibility, `0x17`/`0x18`
carrier sub/surface detection, `0x19` lg_attack, `0x1a` lg_armor_piercing, `0x1b` hg_attack, `0x1c`
hg_armor_piercing, `0x1d` torpedo_attack, `0x1e` sub_attack, `0x1f` anti_air_attack, `0x21` naval_speed, `0x22`
naval_range, `0x23`/`0x24` mines planting/sweeping, `0x25` naval_light_gun_hit_chance_factor, `0x26`
naval_heavy_gun_hit_chance_factor, `0x27` naval_torpedo_hit_chance_factor, `0x28`
naval_torpedo_damage_reduction_factor, `0x29` naval_torpedo_enemy_critical_chance_factor, `0x3c`
max_organisation, `0x3d` max_strength, `0x3f` armor_value, `0x41` reliability, `0x45` fuel_consumption, `0x48`
carrier_size, `0x4d` build_cost_ic (0x4e stats in all). `0x2a` and `0x39` have no matching token (probably
naval_weather_penalty_factor and naval_dominance_factor). These match the ship fields used by the other findings:
`+0x1c8` surface visibility = stat 0x15, `+0x228` speed = 0x21, `+0x248/+0x250/+0x258` light/heavy/torpedo hit
chance factor = 0x25/0x26/0x27.

---

## S-STATS-DESIGN: variant stats from hull and modules

**What the game does** (0x193b080 gathers, 0xbdfc80 combines):

```
for each stat s:                                  // components: add, avgSum, avgCnt, mult
    add    = hull.base[s]                          // type+0x148
    mult   = Σ idea bonuses[s]                     // variant+0xd0, every stat multiplicative
           + Σ named equipment bonuses[s]          // add_equipment_bonus, every stat multiplicative
    if s in {light, heavy, torpedo hit chance factor (0x25..0x27)}: add  += MIO[s]
    else:                                                            mult += MIO[s]
    for each upgrade u with level L:  add += L·u.add[s];  mult += L·u.mult[s]
                                      if u declares avg[s]: avgSum += L·u.avg[s]; avgCnt += 1
    for each filled slot (module m):  add += m.add_stats[s];  mult += m.multiply_stats[s]
                                      if m declares add_average_stats[s]: avgSum += m.avg[s]; avgCnt += 1
    a = add + (avgCnt > 0 ? avgSum / avgCnt : 0)
    final[s] = (s == carrier_size) ? floor(a + mult)        // see S-STATS-CLAMP
                                   : a × (1 + mult)
```

- `multiply_stats` multiplies **hull base + all add_stats + the average**, not only the base.
- All multipliers (modules, MIO, idea and named bonuses) are **one additive sum** in a single `(1 + Σ)`. Nothing
  compounds at the design level.
- Repeated modules are separate slot entries, each with weight 1, so 3 identical turrets add their `add_stats` 3×
  and `multiply_stats` 3×.
- No clamp, floor or rounding apart from the 1e-5 fixed-point truncation (and carrier size).

**Evidence.** 0x193b080: hull loop 0x193b0b0..0x193b199 (`add [comp+0], type[+0x148+8s]`); idea loop
0x193b1c0..0x193b206 (`add [comp+0x18]`); named 0x193b230..0x193b276 (`add [comp+0x18]`); MIO 0x193b2e0..0x193b317
(`sub eax,0x25 / je … / add [rdx]` else `add [rdx+0x18]`); upgrades 0x193b340 (`imul r8, level, 0x186a0`, call
0x193d170 on `upgrade+0x40`); modules 0x193b380 (`module+0x128`, weight `0x186a0`). 0x193d170: add list `+0x10`
→ `[comp]`, mult list `+0x50` → `[comp+0x18]`, avg list `+0x30` → `[comp+8]` and `inc dword [comp+0x10]`.
Combine: 0xbdff4a..0xbe0085 (`idiv` avg, then `mult + 0x186a0`, `imul`, magic `/1e5`). Module container offsets
confirmed by the module tooltip 0x6317f0 (reads `+0x30` next to `EQUIPMENT_MODULE_STAT_AVERAGED`) and the module
description 0x152d4f0 (`lea rcx,[r13+0x128]`).

**Verdict.** MATCHES the simulator's module part (`ShipDesign.GetFinalStats`: base + Σ statModifiers + average,
× (1 + Σ statMultipliers)). The worked examples below are identical to 4 decimals without MIO or bonuses.
DIFFERS for MIO (S-STATS-MIO).

**Confidence.** High.

## S-STATS-AVG: how the average is taken

`avg = Σ value / count`, where `count` is the number of module **slots whose module declares the stat** in
`add_average_stats` (a declared 0 counts). Modules without the stat are not counted, empty slots are not counted, no
weighting. Equipment upgrades are the exception: `avgSum += level × value` but `avgCnt += 1` per upgrade.

**Verdict.** MATCHES the simulator's `AverageNonZero` for all TFB ship modules (no module declares an average of 0;
checked over all 122 modules). It would differ only for a declared 0. **Confidence.** High.

## S-STATS-BONUS: idea and named equipment bonuses, `instant`

- Ideas, advisors and leader traits with `equipment_bonus = { <type> = { stat = x … instant = yes } }` create
  bonus objects in `country+0x160` (0xe5f600). With `instant` the bonus is also pushed into every existing variant
  it applies to (0x631e70: equipment type/archetype match) and the variant is recalculated. Without `instant` it
  is not added to existing variants. On removal, only `instant` bonuses are taken off existing variants (0xe65f30).
  So a non-instant bonus reaches only variants created while it is active, and stays on them (the creation path
  itself was not traced, see Unresolved).
- `add_equipment_bonus = { name = … bonus = { <type> = { … } } }` (effects, e.g. TFB special projects) are named
  bonuses held by the country and referenced by handle from the variant (0xbd5b40).
- Both are **multiplicative for every stat**, in the same `(1 + Σ)` as `multiply_stats`. That includes the hit
  chance factors: a named `naval_torpedo_hit_chance_factor = 0.05` multiplies the design's torpedo hit chance factor
  by 1.05; it does not add 0.05.

**Verdict.** NEW / DIFFERS. The simulator models `sp_torpedo_cruiser_eq_bonus_torpedo` and
`sp_torpedo_cruiser_eq_bonus_light_attack` as researches (ship level) with additive hit-chance factors.
**Confidence.** High for the arithmetic; medium for the non-instant timing.

## S-STATS-MIO: MIO bonuses

```
MIO[s] = Σ equipment_bonus[s] over the MIO's unlocked traits whose limit_to_equipment_type matches the hull
       + initial_trait.equipment_bonus[s] + country MIO policy bonus[s]           // 0xdb7800, mode 1
```
The sum is applied as in S-STATS-DESIGN: light, heavy and torpedo hit chance factors (stats 0x25–0x27) go to the
**add** bucket (before the multiplier), every other stat goes to the **same multiplier sum** as the modules. So
`naval_torpedo_damage_reduction_factor`, `naval_torpedo_enemy_critical_chance_factor`, armor, HP etc. from an MIO
are percentages of the design value; on a hull/module set that has 0 of that stat they do nothing (the MIO
documentation's "requires modules").

**Evidence.** 0xbd3b80 (`variant+0x498 ← 0xdb7a10(MIO, type)`), 0xdb7800 (trait loop over `mio+0x140`, filter
0xa4fe60, sum 0x63f3c0 of `trait+0x130`), 0x193b2c0..0x193b317 (token → stat 0x13e81b0, `0x25/0x26/0x27` → add).

**Verdict.** DIFFERS. The simulator applies the MIO after the module multiplier as a separate step
(`(… × (1+Σmodule)) + mioAdd) × (1+Σmio)`), so MIO percentages compound with module percentages, and it adds the
MIO's `torpedoEnemyCriticalChanceFactor` as a flat value. **Confidence.** High.

## S-STATS-SHIP: technology bonuses and the ship's stat block

Technology blocks like `battleship = { lg_attack = 0.05 }` or `destroyer = { naval_torpedo_hit_chance_factor =
0.05 }` are **sub-unit bonuses**, not equipment bonuses. They are summed (× tech level, normally 1) into the
country's copy of the sub-unit (0xd0e200 → 0x101b1b0 → 0x193d2a0 → 0x1429d30 direct path), for the sub-unit type
and for each of its categories. `naval_range` is added without the level factor; `fuel_capacity` and
`fuel_consumption` are added unscaled. The sub-unit's own script values (e.g. TFB `battleship.max_organisation =
50`, `supply_consumption = 0.8`) are in the same array. There is no `instant` concept: every ship of that sub-unit
picks the bonus up on its next refresh.

The ship block (0xc3d090) is then:

```
sub = country sub-unit stats (definition + Σ tech bonuses)              // 0xd11ae0
for each stat s with variant[s] != 0:
    ship[s] += variant[s] × (1 + sub[s])                                 // 0x142b5f0 (reliability / equipment count)
for s in ADDITIVE_SET:                                                   // 0x1429580
    ship[s] += sub[s]
ADDITIVE_SET = { max_organisation, max_strength, reliability, reliability_factor,
                 naval_light/heavy/torpedo_hit_chance_factor, naval_torpedo_damage_reduction_factor,
                 naval_torpedo_enemy_critical_chance_factor, stat 0x2a, mines_planting, mines_sweeping,
                 convoy_raiding_coordination, supply_consumption(_factor), default_morale, … }
clamp: ship[build_cost_ic] >= 0.1, ship[reliability] >= 0, ship[supply_consumption] >= 0   // 0x142a217
ship[s] ×= max(0, 1 + count × part[s])  for each damaged critical part        // S-STATS-PARTS
```

So a technology percentage on an equipment stat (attack, piercing, armor, speed, AA, detection, visibility) is
`variant × (1 + Σtech)`, one multiplicative step after the design. For the additive-set stats the same value is
both a percentage of the variant value and a flat addition: `homing_torpedo` gives a light cruiser
`thcf = variant_thcf × 1.05 + 0.05`.

Max organization (0xc3d4a3..0xc3d5fb): `maxOrg = max(0.001, (ship[max_organisation] + navy_org) × (1 + navy_org_factor
+ max_organisation(modifier)))`, × `NNavy.TRAINING_ORG` on a training mission (`navy+0x374 == 7`). Current
organization is clamped to it. Current HP is rescaled when max HP changes: `hp = hp × newMax / oldMax`, clamped to
`newMax` (0xc3d4a3..0xc3d517).

**Verdict.** MATCHES the simulator's structure for equipment stats (`Ship.ApplyExternalModifiers` multiplies the
design stats by `1 + Σ research multipliers`). DIFFERS for the additive set: the game adds the value **and**
multiplies the variant value by it; the simulator's research JSON splits values into `statModifiers` (add) and
`statMultipliers` (multiply), which the game does not do. Organization comes from the sub-unit, not the hull (the
JSON puts it on the hull, which gives the same number).

**Confidence.** High for the ship formula and the additive list; medium that the technology values land in the
same array as the definition values (the direct-add path 0x1429d30 and the fallback to the plain definition both
point to it).

## S-STATS-PARTS: damaged critical parts

`ship[s] ×= max(0, 1 + count × partStat[s])` for each `{part, count}` in `ship+0x918`, after the sub-unit step
(0xc3d340..0xc3d487, log string `_EquipmentModifiers[i]: %s`). This is the "stat penalties stay on the ship" of
S-CRIT-PART. **Confidence.** High for the formula, medium for the list being the damaged parts (count = times
damaged).

## S-STATS-CLAMP: caps, floors, rounding

- Variant: no caps or floors on any stat. `carrier_size` is the exception: `floor(add + avg + mult)`; its
  `multiply_stats` value is **added** as a flat number, and the result is truncated to an integer (0xbdff98..0xbe005d,
  assert "Stat should be integer but isn't").
- Ship: `build_cost_ic >= 0.1`, `reliability >= 0` (no upper cap at 1), `supply_consumption >= 0` (0x142a217..
  0x142a24e); max org `>= 0.001`; max HP `>= 0.001` for the HP rescale.
- Speed: no floor at design or ship level; the combat getter floors the factor (S-STATS-COMBAT).
- Everything is int64 fixed point 1e-5 with truncating multiply/divide; there is no other rounding.

**Verdict.** The simulator has no clamps; only `reliability >= 0` could matter with large negative reliability
modifiers. **Confidence.** High.

## S-STATS-COMBAT: design-time vs combat-time

Design time (variant, S-STATS-DESIGN): hull, modules, equipment upgrades, idea `equipment_bonus`, named
`add_equipment_bonus`, MIO traits and policies.

Ship refresh (S-STATS-SHIP/PARTS): technology sub-unit bonuses, sub-unit definition values, damaged parts,
`navy_org`, `navy_org_factor`, `max_organisation`, `TRAINING_ORG`.

Combat getters (country, admiral, doctrine, spirit and experience modifiers, read live through the modifier sum
0xc38760):

```
attack(kind)   = max(0, stat × (1 + Σ))                                     // 0xc34a50, kind 0..3
  Σ = (sub ? submarine_attack + navy_submarine_attack_factor + …_against : 0)
    + (capital ? navy_capital_ship_attack_factor + …_against : 0)
    + (screen ? navy_screen_attack_factor + …_against : 0)
    + naval_night_attack × darkness(province)
    + naval_damage_factor                       // also used in the hit multiplier, see S-DMG
    + admiral attack skill terms + naval_has_potf_in_combat_attack
    + (fuel < 1 ? (1 − fuel) × OUT_OF_FUEL_ATTACK_FACTOR (torpedo: OUT_OF_FUEL_TORPEDO_FACTOR) : 0)
armor          = max(0, armor_value × (1 + Σdef))                          // 0xc34a50 kind 4 (0xc3a520)
  Σdef = navy_submarine/capital_ship/screen_defence_factor (+ _against) + naval_defense_factor
       + admiral defense terms + naval_has_potf_in_combat_defense           // no night, no fuel term
speed          = naval_speed × max(0.1, 1 + naval_speed_factor + leader term
                                   + (fuel < 1 ? (1 − fuel) × OUT_OF_FUEL_SPEED_FACTOR : 0))   // 0xc3a740
hit chance factors, visibility: see S-HIT (ship stat + modifier sum).
piercing (0xc3a570, 0xc3a5c0): the raw ship stat, no modifiers.
```

Modifier ids (0x55f7f0): 0x38 submarine_attack, 0xc2/0xc3 navy_submarine_attack/defence_factor, 0xc4/0xc5
navy_capital_ship_attack/defence_factor, 0xc6/0xc7 navy_screen_attack/defence_factor, 0x107 naval_night_attack,
0x1b7 naval_damage_factor, 0x1b8 naval_defense_factor, 0x1c8–0x1cd the `_against` versions, 0x1d1/0x1d2
naval_has_potf_in_combat_attack/defense, 0x29 naval_speed_factor, 0x46 navy_org, 0x47 navy_org_factor, 0x27e
max_organisation.

NEW: `naval_defense_factor` (and so the experience-level defense bonus) also scales **armor**, not only the
damage multiplier.

**Verdict.** The simulator's `Spirit` data (`attack`, `hitChance`, `positioning`, …) are these combat-time
modifiers; they should not be folded into the design stats. **Confidence.** High for the structure; medium for the
admiral terms (0xd6b1b0, 0xd6b590, 0x143e4a0, 0xc19c40 not decoded).

---

## Worked examples

Computed by a throwaway Python script (not in the repo): game formula from the TFB script values (`00_ship_modules.txt`,
hull values from `data/hulls`, which match `ship_hull_heavy_3` and `ship_hull_cruiser_3`), against a Python copy
of `GetFinalStats` + `ApplyExternalModifiers` on the JSON data. All module JSON values used by these designs match
the script exactly. Stats not listed are equal or zero.

**39_battleship_balanced** (heavy_hull_3; 3× heavy_battery_3, fire_control_3, radar_4, engine_heavy_3, 3× anti_air_4,
3× light_secondaries_2, armor_bb_3): **identical** in every stat (speed 29.07, HP 500, armor 44.2, HA 60, HP
pierce 44, LA 12, LP 6, AA 20.7, light/heavy hit factor 0.175, cost 13587.25).

**39_battleship_balanced + MIO battle_line_armor_armor_la**

| stat | game | simulator | diff |
|---|---|---|---|
| Hp | 540.00 | 550.00 | +10.00 |
| Armor | 51.00 | 53.04 | +2.04 |
| AntiAir | 23.40 | 23.805 | +0.405 |
| TorpedoEnemyCriticalChanceFactor | 0 | −0.05 | −0.05 |
| ProductionCost | 14178.00 | 14266.61 | +88.61 |
| LightAttack / HeavyAttack / LightPiercing / LightHitChanceFactor | 13.8 / 63 / 6.3 / 0.225 | same | 0 |

**cl3_armor4_full_la** (light_cruiser_hull_3; medium_light_battery_3, fire_control_2, radar_2, engine_cruiser_3,
armor_cruiser_3, anti_air_3, light_secondaries_2, 5× light_battery_3): **identical** (speed 31.44, HP 132, armor 8.4,
LA 28, LP 6.571, AA 5.375, hit factors 0.1/0.1, torpedo DR 0.85, enemy crit −0.2, cost 5248.125).

**cl3_armor4_full_la + MIO battle_line_armor_armor_la + light_upgrade_1, light_upgrade_2,
fire_control_methods_3_cruiser**: identical except AntiAir 6.125 (game) vs 6.181 (simulator), +0.056.

**cl3_armor4_mix + torpedo_upgrade_1, torpedo_upgrade_2 (homing_torpedo)**: identical except
TorpedoHitChanceFactor 0.0815 (game: 0.03 × 1.05 + 0.05) vs 0.0800 (simulator), −0.0015.

**torpedo_cruiser_torpattack + named bonus sp_torpedo_cruiser_eq_bonus_torpedo**: identical except
TorpedoHitChanceFactor 0.063 (game: 0.06 × 1.05) vs 0.110 (simulator: 0.06 + 0.05), +0.047. TorpedoAttack is equal
(146.2) because no torpedo module has a torpedo-attack multiplier.

## Recommended changes

1. `ShipDesign.GetFinalStats` (S-STATS-DESIGN/MIO): one multiplier sum. `final = (base + Σmodule.add + avg +
   mioAdd) × (1 + Σmodule.mult + Σmio + Σnamed/idea bonuses)`, where `mioAdd` holds only the three hit chance
   factors and every other MIO stat goes into the sum. Drop the MIO `StatAverages` (the game has none).
2. Data: give MIOs and equipment bonuses one `equipmentBonus` map per modifier (the script form) instead of
   `statModifiers`/`statMultipliers`; let the engine route by stat. Same for researches: one map, with the engine
   applying `variant × (1 + Σ)` to every stat and an extra `+ Σ` for the additive set (HP, reliability,
   organization, the three hit chance factors, torpedo damage reduction, enemy crit chance).
3. Move `sp_torpedo_cruiser_eq_bonus_*` (and any other `add_equipment_bonus` / idea `equipment_bonus`) from
   `researches` to a design-level bonus list that feeds the multiplier sum of item 1. Their hit chance factors must
   multiply, not add.
4. Organization: keep it on the hull JSON for now (it equals the sub-unit value), but compute max org as
   `(org + navy_org) × (1 + navy_org_factor + max_organisation)`.
5. Clamps: ship reliability `>= 0`, production cost `>= 0.1`; carrier size `floor(add + avg + mult)`.
6. Combat getters (not design stats): attack `× max(0, 1 + Σ)` with naval_damage_factor, the capital/screen/sub
   attack factors and night × darkness; armor `× max(0, 1 + Σdef)` with naval_defense_factor and the defence
   factors; speed `× max(0.1, 1 + naval_speed_factor + …)`. The spirit/doctrine JSON belongs here.
7. Spec: add the S-STATS-* items to `docs/mechanics/surface.md` with [RE] tags.

## Unresolved

- How the hull's base array (`type+0x148`) is filled from `archetype` and `parent` (assumed: archetype values,
  overridden by the hull's own; the JSON hulls match this reading for heavy_hull_3 and light_cruiser_hull_3).
  2026-10-19: the hull import (S-DESIGN-HULL) uses this reading and reproduces all 27 hand-entered TFB hulls. Reading
  stats along `parent` instead would give TFB's `ship_hull_heavy_1..4` the pre-dreadnought's `naval_speed = 18` (and
  `_1` its HP, reliability and manpower), against 30 in the hand-entered data, so the archetype reading is the likely one.
- The variant-creation path that copies non-instant idea bonuses from `country+0x160` (only the instant add and
  the removal were traced).
- Whether the script parser merges duplicate stat keys or repeated `add_stats` blocks in one module (the
  aggregator itself would count every entry).
- Admiral and leader terms in 0xc34a50 / 0xc3a740 (0xd6b1b0, 0xd6b590, 0xd6eee0, 0x143e4a0, 0xc19c40) and the
  doctrine-like `[+0x270/+0x278] × 0xf19660(province)` term.
- Whether `naval_damage_factor` is really counted twice (attack getter and hit multiplier; see S-DMG).
- Names of stats 0x2a and 0x39 (no token maps to them).
- The plane-only branch of 0xbe0260 (variant flags `+0x408`, best `naval_strike_attack × naval_strike_targetting`
  module pick) and `USE_SINGLE_NAVAL_ARMAMENT_CATEGORY`.

## Index entries

| RVA / field | Role |
|---|---|
| 0xbe0260 | CEquipmentVariant::RecalculateStats |
| 0xbdfc80 | variant final stats with breakdown → `variant+0x100` |
| 0xbdfa70 | variant final stats, no breakdown |
| 0x193cdf0 | stat_helper: size assert, calls 0x193b080 |
| 0x193b080 | gather stat components from all variant sources |
| 0x193ade0 / 0x193cc10 | single-stat gather |
| 0x193d170 | add stat container × weight (add/avg/mult) |
| 0x193ca70 | single-stat version of 0x193d170 |
| 0x6f3920 | variant idea bonuses (`+0xd0`) |
| 0xbd5b40 | variant named equipment bonuses (handles `+0xe8`) |
| 0xbd3b80 | refresh variant MIO bonus (`+0x498`) |
| 0xdb7800 / 0xdb7a10 / 0xdb7a30 | MIO trait/initial/policy bonus sum (equipment / production) |
| 0xa4fe60 | MIO trait limit_to_equipment_type check |
| 0xe5f600 / 0xe5f740 | country add idea equipment bonus (instant → existing variants) |
| 0xe65f30 | country remove idea equipment bonuses |
| 0x631e70 | equipment bonus applies to type |
| 0x13e81b0 | token id → EEquipmentStats |
| 0x831c0 | static token registration (name → id) |
| 0xc3d090 | CShip::RefreshStats |
| 0x1019e50 / 0x1429cb0 / 0x142b5f0 | add variant stats to ship × (1 + sub-unit stat) |
| 0x1019ea0 / 0x1429d30 | add sub-unit stat set (direct or base path) + clamps |
| 0x1429580 | sub-unit additive stat list |
| 0x1019fb0 | ship stat block finalize (not decoded) |
| 0xd11ae0 | country per-sub-unit stats lookup (`country+0xf70`) |
| 0xd0e200 | create country sub-unit copy, apply techs |
| 0x101b1b0 / 0x193d370 / 0x193d2a0 | apply technology sub-unit bonuses (type and categories, × level) |
| 0xee3e70 | technology researched handler (enables sub-units) |
| 0xc34a50 | combat attack/armor getter (kind 0 light, 1 heavy, 2 sub, 3 torpedo, 4 armor) |
| 0xc3a5d0 / 0xc3a580 / 0xc3a890 / 0xc3ac00 / 0xc3a520 | light / heavy / sub / torpedo attack, armor |
| 0xc3a740 | combat speed getter |
| 0x152d4f0 | module stat description (container `+0x128`) |
| 0x6317f0 | stat container tooltip (EQUIPMENT_MODULE_STAT_AVERAGED) |
| type `+0x148+8s` | hull base stats |
| variant `+0x3f0` | equipment type |
| variant `+0x68`/`+0x70`/`+0x7c` | upgrades {upgrade*, level} |
| variant `+0xb8`/`+0xc4` | modules, one per filled slot |
| variant `+0xd0`/`+0xdc` | idea bonuses |
| variant `+0xe8`/`+0xf4` | named bonus handles |
| variant `+0x100+8s` | final stats |
| variant `+0x414` | Σ upgrade levels |
| variant `+0x488` | MIO handle |
| variant `+0x498`/`+0x4a4` | MIO bonus list |
| module `+0x128` (`+0x138` add, `+0x158` avg, `+0x178` mult) | module stat container |
| upgrade `+0x40` | upgrade stat container |
| bonus `+0x40` (idea) / `+0x48` (named) | bonus stat lists |
| bonus `+0x70` | instant flag |
| country `+0x160` | idea equipment bonuses |
| country `+0xa0` | country variants |
| country `+0xf70` | per-country sub-units |
| ship `+0x120+8s` | ship stat block |
| ship `+0x6f8` / `+0x700` | current HP / current org |
| ship `+0x918`/`+0x924` | damaged critical parts {part*, count} |
| sub-unit stats `+0x60+8s` | sub-unit stat array |
