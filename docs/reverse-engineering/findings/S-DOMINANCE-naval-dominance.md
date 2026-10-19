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
| 0x1003c50, 0x1005296 | daily dominance gain and loss (read DOMINANCE_DAILY_GAIN_FACTOR / _LOSS_FACTOR; not decoded) |
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
entry), value at `+0x20` (presumably `current`); the region's own value at `+0x78`.

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

**Daily update** [CAL]. `make_save.py` kept that region state when it wrote the `jukki_usa_vs_bc_japan` fleets into
the template, so their battles started at BUL 17377, ROM 17238, against targets of 27349 and 25925. Moving each
country's current value once a day at 02:00 toward the summed dominance of its ships still afloat (escaped ones
included), up by `DOMINANCE_DAILY_GAIN_FACTOR` (TFB 0.03) of the target and down by `DOMINANCE_DAILY_LOSS_FACTOR`
(0.04) of the previous target, predicts the day the logged organization regain moves from BUL (0.22 an hour) to ROM:
hour 133, 157 or 181 in 26 of the 27 battles; in seed 19 the model is a day early. In seed 1, counting only ships
still in battle moves it a day early (hour 109), and an update at 00:00 two hours before the logged change.

## Unresolved

- Mission efficiency functions (0xfae5d0, 0xfadf60, 0xfaff70, 0xfb01c0) and where the mission ratio enters the
  saved target.
- The daily update (0x1003c50, 0x1005296) and why it fell at 02:00 in region 30 (region-dependent hour?).
- Variant flag `+0x408` bit 7 and stat 0x49 (in place of carrier size).
- Intel's reduction of dominance (`NIntel.NAVAL_DOMINANCE_INTEL_*`); the region's own value (naval bases, air bases,
  radars: `NAVAL_BASE_DOMINANCE_FACTOR`, `AIR_BASE_DOMINANCE_FACTOR`, `RADAR_DOMINANCE_FACTOR`).
