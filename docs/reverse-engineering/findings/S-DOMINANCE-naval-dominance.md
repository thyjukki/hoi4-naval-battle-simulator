# S-DOMINANCE: naval dominance (hoi4.exe 1.19.3)

What a ship contributes to naval dominance, how a task force's dominance reaches a region, and when a country
controls a region, which is what the organization regain of S-ORG-REGAIN asks. Static analysis plus two calibrations
from saves and logged battles. Spec: [surface.md](../../mechanics/surface.md) S-DOMINANCE.

## Function map

| RVA | What |
|---|---|
| 0xbd5d60 (chunk 0xbd5dae) | variant dominance `(variant, out, unused)`: the formula below; 0 unless `variant+0x3f0` (equipment type) is set and passes 0xc97b90 |
| 0xc97b90 | equipment type check along the archetype chain (`type+0x4d8`): keyword mask `type+0x540` (the archetype's when 0) `\| type+0x548` → 0xf8b020 |
| 0xc959b0 | `*out = type+0x3f0`: the equipment type's `naval_dominance_factor` (leaf) |
| 0xc33240 | ship dominance `(ship, out)`: variant 0x100ff30(`ship+0x40`), then 0xbd5d60(variant, out, `ship+0x808`); 0 without a variant |
| 0xfafda0 | a mission's dominance in a region `(mission, out, region)` |
| 0x1003f50 | region control for a country `(region dominance, country)`: 0 none, 1 the country's side, 2 the enemies' |
| 0x143c720 | minimum dominance `(out, t, own value)`: `own × t / (1 − t)`, t ≥ 1 taken as 0.99 |
| 0xc395a0 (0xc398f0..0xc39943) | organization regain: adds NAVAL_DOMINANCE_ORG_RECOVERY when 0x1003f50 returns 1 for the ship's country |
| 0x1003c50 | a region's daily dominance update (S-DOMINANCE-DAILY below); from `gamestate.daily` 0x1d4810 ("daily.strategic_region_update", 0x1d50b6) and 0x1beae0 (0x1bed88, 0x1bedd8), through the region thunk 0xe22660 (`region+0xe8`) |
| 0x1002fe0 | targets: for every country 0x10033e0; a country with a target > 0 gets its entry (created if missing, 0x19dd1c0 / 0x1000980), 0x19de3c0(target, base), and joins the gain set |
| 0x1002d60 | loss set: every country with an entry, minus the gain set |
| 0x10033e0 | a country's target in the region `(region dominance, out, country)`: `base = Σ 0xfb3220` over its missions of kind 1, `target = base × (multiplier 0x19dd350 + Σ strike-force terms 0x1003760)`; out +8 base, +0x10 the allies' strike term, +0x18, +0x20 the multiplier |
| 0xfb3220 | a mission's dominance `(mission, out, region)`: 0 without ships, inactive (+0x30) or with the region not in its list (+0xc8, count +0xd4); else `Σ 0xc33240(ship)` over the task force's ships (0xd6eeb0) `× efficiency`: 1.0 for type 1 with `mission+0x70 == region`, types 2..5 0xfb2980 / 0xfae5d0 / 0xfadf60 (not decoded). No MISSION_DOMINANCE_RATIOS |
| 0x1003760 | strike-force term (NAVAL_DOMINANCE_STRIKE_FORCE_FRACTION, _MULTIREGION_DECAY; not decoded in detail) |
| 0x19dd350 | a country's multiplier: `(1 + 0x19dd240) × (1 + 0x19dd440) × (1 + 0x19dd550)`, building levels of the country and its allies in the region × AIR_BASE (0x19dd303) / NAVAL_BASE (0x19dd503) / RADAR_DOMINANCE_FACTOR (0x19dd613); medium: which buildings each counts |
| 0x19dd200 | value step `(V, delta)`: `previous = current; current += delta`; a positive delta is capped at the target, the result floored at 0 |
| 0x19de3c0 | set target `(V, target, base)`: `V.target = target`, `V.base_target = base`, `V.decline_from = target` if target > 0 |
| 0x1006280 | the control snapshot: copies every entry (country and value block) to the array at `+0x40` (stride 0x50) that 0x1003f50 reads |
| 0x1005280 (0x1005296) | the loss step for the loss set: `current -= DOMINANCE_DAILY_LOSS_FACTOR × decline_from` (0x24ef6f0 negates), then target and base 0; the entry is removed at 0 |
| 0x1780f80, 0x1d61b90, 0x178f54e | other readers of the per-ship defines / callers of 0xbd5d60 (tooltips, not decoded) |

## Ship dominance (0xbd5d60)

Fixed point (fx5) throughout; `stat(s)` is the variant's final stat `variant+0x100+8s` (S-STATS-DESIGN).

```
if variant.type == null or !0xc97b90(variant.type): return 0
S = DOMINANCE_PER_SHIP_PER_SPEED_NEUTRAL; R = DOMINANCE_PER_SHIP_PER_RANGE_NEUTRAL
speed = (2S == 0) ? 0 : (stat(0x21 naval_speed) - S) / (2S)       // fx divide, truncating
range = (2R == 0) ? 0 : (stat(0x22 naval_range) - R) / (2R)
heavy = stat(0x1b hg_attack) × DOMINANCE_PER_SHIP_PER_HEAVY_GUN_ATTACK
carrier = stat(variant+0x408 bit 7 ? 0x49 : 0x48 carrier_size) × DOMINANCE_PER_SHIP_PER_CARRIER_SIZE
return (1 + speed + range + heavy + carrier) × type.naval_dominance_factor   // type+0x3f0 via 0xc959b0
```

No clamp (the define comments' "min value is 0" is not in this code). The third argument (`ship+0x808`, manpower) is
overwritten before use: dominance does not depend on manpower, HP or organization. The stats are the variant's, so
sub-unit (technology, doctrine) bonuses do not count; equipment bonuses do.

## A mission's dominance (0xfafda0)

```
ships = 0xd6eeb0(mission+8)                 // the task force's ships
if ships empty: return 0
ratio = MISSION_DOMINANCE_RATIOS[mission+0x14]
if ratio <= 0 or !mission+0x30 or region not in mission+0xc8 (count +0xd4): return 0
efficiency = switch mission type 1..9: 1.0 when mission+0x70 == region for one type, else 0xfae5d0 / 0xfadf60 /
             0xfaff70 / 0xfb01c0 (mission efficiency functions, not decoded)
if efficiency <= 0: return 0
return Σ 0xc33240(ship) × efficiency × ratio
```

## Region control (0x1003f50)

`region+0xe8` → region dominance; entries at `+0x40` (count `+0x4c`, stride 0x50): country (0xbb48f0 on the
entry), value at `+0x20`: the entry's `current`. The array is the snapshot 0x1006280 copies from the value table at the
end of every daily update, so control changes only once a day. The region's own value is at `+0x78`.

```
friendly = Σ value of entries whose country is the asking country or allied (country+0xf88 relation, 0xd25410)
enemy    = Σ value of entries at war with it (relation +0x2e8 set, +0x49 == 0)
t = DOMINANCE_CONTROLLED_THRESHOLD_RATIO
minimum = 0x143c720(t, own value)            // own × t / (1 − t): 1.5 × own at 0.6
// a ratio check friendly/enemy ≥ t or enemy/friendly ≥ t comes first; one of them always holds
if friendly < minimum and enemy < minimum: return 0
return enemy > friendly ? 2 : 1              // a tie counts for the asking side, so both sides control
```

The regain (0xc395a0) asks with `tf+0x1dc` if > 0, else `tf+0x1d8` (operating country, else owner), of the region
`[tf+0x1f0]+0xc8` (current province → strategic region).

## Calibration

**Ship formula** [CAL]. The CL3 battle's save (`light_attack_vs_torp_cruiser_battle_start.hoi4`, both task forces on
patrol, `mission=1`, in region 30, the Black Sea) holds:

```
region={ 30={ dominance={ countries={ ROM BUL } values={
    { "ROM" { current=17238 target=17238 base_target=17238 previous=17238 decline_from=17238 individual_ratio=0.9857 } }
    { "BUL" { current=17376.9375 target=17376.9375 ... individual_ratio=0.98581 } } } } } }
```

The simulator's sums of the formula over the two fleets (`NavySimulator list-ships` gives `navalDominance` per
design) are 17376.9375 and 17238.0. TFB's patrol ratio is 1.5, so the saved target is not `Σ × ratio` as 0xfafda0
computes it; which path writes `target` (and whether patrol efficiency is 2/3 here) is open. `individual_ratio` fits
`current / (current + own value)` with an own value of 250.1 for both countries.

## S-DOMINANCE-DAILY: the daily update (0x1003c50) [RE + CAL]

It runs once a day for every strategic region, in the tick where the day changes (`gamestate.daily`, after that
tick's hourly update). The region keeps a value table at `+0x20` (robin hood, entries 0x58, the value block `V` at
entry+0x10): `+0x8` country, `+0x18 current`, `+0x20 base_target`, `+0x28 target`, `+0x30 individual_ratio`,
`+0x38 previous`, `+0x40 decline_from` (named after the save's fields). Fixed point fx5; products truncate toward zero.

```
for each country c:                                       // 0x1002fe0
    (target, base) = 0x10033e0(region, c)                 // Σ mission dominance × multiplier, below
    if target > 0:
        V = entry(c), or a new one (current 0)
        V.target = target; V.base_target = base; V.decline_from = target     // 0x19de3c0
        gain set += c
loss set = countries with an entry − gain set             // 0x1002d60
for c in gain set:                                        // 0x1003c50
    V.previous = V.current
    V.current = clamp(V.current + trunc(V.target × DOMINANCE_DAILY_GAIN_FACTOR), 0, V.target)   // 0x19dd200
for c in loss set:                                        // 0x1005296
    V.previous = V.current
    V.current = max(0, V.current − trunc(V.decline_from × DOMINANCE_DAILY_LOSS_FACTOR))
    V.target = 0; V.base_target = 0
    if V.current == 0: remove the entry
for every entry: V.individual_ratio = current / (current + 0x1003830(...))   // 1.0 when that sum is 0
snapshot for the control check                            // 0x1006280

target(c)     = base × (multiplier(c) + Σ strike-force terms)                 // 0x10033e0
base          = Σ over c's naval missions of kind 1 covering the region: Σ ship dominance (0xc33240) of the task
                force's ships × mission efficiency (a patrol in its own region: 1.0)          // 0xfb3220
multiplier(c) = (1 + air bases × AIR_BASE_DOMINANCE_FACTOR) × (1 + naval bases × NAVAL_BASE_DOMINANCE_FACTOR)
                × (1 + radars × RADAR_DOMINANCE_FACTOR), the country's and its allies' in the region   // 0x19dd350
```

So the 4 % loss applies only to a country with no task force left in the region. A country whose ships are sunk
keeps a target, and its current value falls **at once** to the new target at the next day change (the gain step's
cap); rising toward a higher target takes `GAIN × target` a day. The task force's ships (0xd6eeb0) are those still in
it: sunk ships leave, escaped and retreating ones stay. HP and organization do not count. `MISSION_DOMINANCE_RATIOS`
(TFB patrol 1.5) is not on this path, which is why the saves hold the plain sums.

**Check** [CAL]. Model (`dominance_model.py`, session scratchpad): start from the save's `current` per country
(`jukki_usa_vs_bc_japan_battle_start`: ROM 17238 and BUL 17376.9375, the CL3 template's; `..._no_doctrines_battle_start`
and `..._mio_admiral_battle_start`: the fleets' sums, as `make_save.py` writes them). At each day change the target is
the summed dominance of each side's ships not sunk (escaped included; multiplier 1, no strike forces), and the gain step
applies. The side with the higher current (both on a tie) has NAVAL_DOMINANCE_ORG_RECOVERY from that hour's regain on.
Against the logged regain (0.22 instead of 0.20 an hour on ships out of the fight) it gets every observed hour right in
all 127 logged runs: 100 of `jukki_nd_batch100`, 11 of `jukki_usa_vs_bc_japan_battle_start`, the 10 of the MIO + admiral
batch and 6 constant-roll runs (S-REPLAY-CONSTANT), 20,260 observed hours. The earlier calibrated model (down by 4 % of
the previous target a day, floored at the target) got 101 of them right; in the others the game hands control to Japan
a day earlier, when the USA's losses take its current value straight down to its target. In these saves the day
changes at the start of battle hour h with `(13 + h) % 24 == 0`; the earlier "02:00" came from reading an older log
with a one-hour-late hour index.

## Unresolved

- Mission efficiency functions (0xfae5d0, 0xfadf60, 0xfaff70, 0xfb01c0) and where the mission ratio enters the
  saved target.
- The daily update is decoded (S-DOMINANCE-DAILY). Open there: the strike-force term 0x1003760 and the allies'
  strike term in 0x10033e0 (out +0x10), the individual ratio's denominator 0x1003830, and which buildings 0x19dd240 /
  0x19dd440 / 0x19dd550 count (the multiplier is 1 in region 30 for these saves). The loss step (a country with no
  task force left in the region) is decoded but not seen in a logged battle.
- Variant flag `+0x408` bit 7 and stat 0x49 (in place of carrier size).
- Intel's reduction of dominance (`NIntel.NAVAL_DOMINANCE_INTEL_*`); the region's own value (naval bases, air bases,
  radars: `NAVAL_BASE_DOMINANCE_FACTOR`, `AIR_BASE_DOMINANCE_FACTOR`, `RADAR_DOMINANCE_FACTOR`).
