# A-COVERAGE: land-based mission effectiveness (coverage, air base crowding, supply, fuel)

- **Game version:** 1.19.3
- **Executable SHA-256:** 7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5
- **Spec items affected:** A-REGION-SYNC-EFF (the land branch of 0xf78500), A-DETECT-MIX rule 2, A-REGION (land wings'
  `missionEfficiency`), S-NIGHT (province positions)
- **Confidence:** coverage and crowding read in full from the code (high) and reproduce every logged land
  effectiveness; supply ratio: formula of the asked supply high, the received supply needs the live supply network
  (not reproducible from game files); fuel: high for the factor, the country's fuel ratio is an input.

Builds on [A-REGION-SYNC-EFF](A-REGION-SYNC-carrier-wing-region.md#a-region-sync-eff-mission-effectiveness-0xf78500mission-out-iscarrier-nosupply-tooltip)
(the factors and their order) and corrects [A-DETECT-MIX](A-DETECT-region-air-detection.md) rule 2: **the 0.6 at
Burgas is the air base's crowding (`capacity_penalty`), not its supply.** The template save already has 10 BUL fighter
wings of 100 at Burgas (capacity 1000); the scenario's 100 fighters and 100 bombers make 1200 planes.

## What the game does

Land branch of 0xf78500 (unchanged, from A-REGION-SYNC-EFF; fixed point ×1e5, each product truncated):
```
e = coverage(mission+0x50) × capacity_penalty(entry+0xd8) × supply(entry+0x30)
    × (1 − region_change_penalty)        // if !(type & 0x60)
    × (1 + air_mission_efficiency)       // region weather + country
    × fuelTerm × (1 + targeted)          // clamp [0, 1]
```
`entry` is the air base's record for the wing's country (`base+0x30[countryIdx]`).

### A-COVERAGE-RANGE: wing range in map pixels (0xf5e5c0)
```
G      = NGame.MAP_SCALE_PIXEL_TO_KM (global 0x3335f50, fixed ×1e5: 7.114 → 711400)
px     = trunc(stat_air_range(wing+0x500) × 1e5 / G)      // fixed; stat = plane-share average of the wing's variants
mult   = 1 + owner.air_range_factor (modifier 5, country holder +0x5b8; owner = wing+0x9c4 tag, else the pool's)
         + [alive ace (wing+0x2e8, +0x158)] ace modifier 5 (+ owner.air_ace_bonuses_factor 0x72 if that is nonzero)
range  = trunc(px × mult)                                 // fixed px
R      = trunc(range / 1e5)                               // whole px, stored in mission+0x78 by UpdateCoverage
```
The index's "whole km" for `mission+0x78` is wrong: the division by `MAP_SCALE_PIXEL_TO_KM` happens in 0xf5e5c0, so
+0x78 holds whole map pixels. Fighter II 900 km → 126 px; Fighter 550 → 77; Tactical Bomber II 2000 → 281.

### A-COVERAGE-COUNT: in-range provinces 0x10132d0 → 0x1012f80(list, region, base, rangeFixed, limit = 0)
```
R2 = R × R                                                  // int32
if base+0x60 (carrier handle) resolves: origin = the carrier's task force location province (0xe212d0, 0xe7d130)
else origin = state = base+0x68 (the air base's state, saved as `state=`); none → 0
// region precheck (region template = region+0x30; boxes +0xf0, count +0xfc, 16 bytes {x, y, w, h})
W  = Σ box.w;  H = min(box.y + box.h) − max(box.y)  (min starts at the map height);  H ≤ 0 → return 0
hd2 = (W/2)² + (H/2)²                                       // int, /2 truncates toward 0
if d2(regionCentre, origin) > R2 + hd2: return 0            // note: R² + hd², not (R + hd)²
count = 0
for p in region provinces (region+0x18, count +0x24):
    if d2(origin, p) <= R2: count++, list += {p.id, trunc(sqrt(d2)) px}
d2(a, b): dx = b.x − a.x wrapped by 0x118f660 (dx > W/2 → dx − W; dx < −W/2 → dx + W; W = map width), dy = b.y − a.y;
          dx² + dy² (int32). No wrap in y.
```
Positions:
- **Province** `[province+0xb8]+0xb8 / +0xbc` = the truncated mean pixel of the province in `map/provinces.bmp`,
  x from the left, y counted from the bottom row (file row order of the bottom-up BMP). Written by 0x140a3c0 from the
  province's pixel runs (+0x58, count +0x64): `x = Σx / n`, `y = Σrow / n` (int division), n → +0xc0. Not read from
  `unitstacks.txt` (its loader 0x164f680 only reads +0xb8 / +0xbc). Settles the S-NIGHT open question.
- **State (land air base origin)** `[state+0x30]+0x148 / +0x14c` (state+0x30 = the CStateTemplate) = the centre of the
  bounding box of the state's provinces, set in the state-database pass 0xabce00 (loop body 0xabce20) after the
  provinces.bmp scan 0xa5f7b0:
  ```
  rect(p) (map province entry +0x88, 0x140a610): x0 = minx & ~1, y0 = miny & ~1,
           w = (maxx | 1) − x0 + 1, h = (maxy | 1) − y0 + 1          // 2-pixel aligned bbox, y from bottom
  A = union of rect(p) over the state's provinces
  B = the same with x0 + W for rects with x0 < W/2                     // seam-shifted copy
  box = B if A.w > B.w else A
  cx = box.x + box.w/2, wrapped into [0, W);  cy = box.y + box.h/2;  +0x150 = max(w, h)
  ```
  **Not** the `air_base` position of `map/buildings.txt` (the A-DETECT-MIX assumption); the buildings loader does not
  touch the state template.
- **Region centre** `[region+0x30]+0xe0 / +0xe4` (0x15a43c0): box1 = union of the rects of the provinces on the same
  map half as the first province, box2 = the others (only once one is more than W/2 away in x); boxes pushed to +0xf0;
  centre = box1 centre; if box2 exists, `cx = (box on the right).x + (w1 + w2)/2 − W`. Without the seam: the bbox
  centre of the region's provinces.

### A-COVERAGE-UPDATE: UpdateCoverage 0xf87480(mission, force)
```
R = trunc(range(wing) / 1e5)
if !force and R == mission+0x78: return
mission+0x78 = R; mission+0x44 = 0
if no region (+0x30) or type & 0x800 (training): return       // coverage keeps its old value
n = 0x10132d0(list +0x38, region, wing, limit 0)
coverage(+0x50) = min(1, trunc(n × 1e5 / region.provinceCount(+0x24)))
```
Callers: the hourly wing update 0xf62750 (not forced: recomputed when the integer range changes, e.g. when stockpile
refills mix older variants into the wing's average range), InitPostRead 0xf62bc0 (forced, on load), the wing refresh
0xf643a0 (forced; no province in range → a land wing's mission region is cleared).

### A-COVERAGE-CROWD: capacity_penalty 0xc579f0(base) → 0xc57b70(entry, base, f)
```
P = Σ over the base's country entries (base+0x48, count +0x54)
      Σ pools (entry+0xb8, count +0xc4) Σ wings (pool+0x28, count +0x34) wing.planes (+0x7c)   // 0xc4d910, 0xf5c4d0
    // every wing at the base, idle or on any mission, of every country
C = base+0x78 (capacity, int)
f = 1 if P <= 0 or P <= C
else if C == 0: r = 1
else r = trunc((P − C) × 1e5 / C); if r > 0.9: f = 0; if r == 0: f = 1
CP = NAir.CAPACITY_PENALTY; carrier base (base+0x68 == 0 with a ship): CP = trunc(CP × (1 + carrier_capacity_penalty_reduction))
f = clamp(1 − trunc(CP × r), 0, 1)
per country entry: fuel_consumption (+0xe0) = Σ pools 0xf5ba20, base_fuel_consumption (+0xe8) = Σ 0xf5b720;
                   capacity_penalty (+0xd8) = f, or 0 if the entry's operational_status (+0xd0) <= 0 or state+0x864 is set
```
Capacity 0xc65490: land airfield (`base+0x7c` kind 0) `C = Σ over air-base building types of the state's building
level (+0x42) × NBuildings.AIRBASE_CAPACITY_MULT` (int); rocket site (kind 1) × `ROCKETSITE_CAPACITY_MULT`; kind 2: 1;
carrier: 0xc4cae0. When: every hour, all air bases, in a parallel job (task vtable 0x2959fe0, slot 0xc670f0 →
0xc44530 → 0xc45d50 / 0xc67630) launched from 0xc5efc0 inside the hourly strategic-air update 0xc57d30. The saved
`capacity_penalty` is stale until the first hour.

### A-COVERAGE-SUPPLY: the base entry's supply ratio (entry+0x30)
```
asked  (vtable slot 0x50, 0xc4d770) = trunc(Σ this country's wings at the base trunc(planes × NAir.SUPPLY_NEED_FACTOR × 0.01)
                                             × NSupply.BASE_AIR_SUPPLY_MULT_FOR_TRUCK_BUFFER)          // 0xf625b0
location (slot 0x58, 0xc4d490) = the base's supply province (base+0x70, else 0xc53790/0xc53910); carrier: task force location
received: country_supply.cpp hands each province's supply to its consumers (divisions and air bases) in a proportional
          cumulative share (0x121a7ae: to_give = min(need − received, share)); 0x1a0afb0 adds it, clamps to asked and sets
ratio  = received / asked (1 if asked = 0); the ctor starts it at 1; not saved
```
So the ratio is about `min(1, the province's available supply / the total need in that province)`. It depends on the
live supply network (hub flow, railways, the army's consumption), which is not in the game files.

### A-COVERAGE-FUEL and region change penalty
- Fuel term (0xc543e0): `consumption(+0xe0) > 0 ? clamp(received(+0xf0) / consumption, 0, 1) : 1`, and
  `e ×= MISSION_EFFICIENCY_MULT_AT_LACK_OF_FUEL + (1 − MULT) × f` when f < 1. Consumption (0xf5ba20) = Σ executing
  wings `effective planes (+0x170) × fuel stat × NAir.FUEL_COST_MULT × NAir.MISSION_FUEL_COSTS[mission]`;
  received is set by the country's fuel distribution (0x10f3080 → 0xc4bb10) as `need × the country's fuel ratio`. A
  country with fuel in stock gives 1. In all logged runs f = 1.
- Region change penalty: make_save writes `region_change_penalty=0`. Load does not call SetRegion with a change, so it
  stays 0 (the logged bombers fly at exactly the crowd factor from the first air hour). It comes back with
  reinforcement: refilled planes enter at `EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR`, blended by plane count
  (0xf63e00), and the penalty decays every hour by `trunc(gain / 24)` (0.008 for naval bombers, 0.037 for fighters);
  exact rule and timing in A-COVERAGE-REFILL below.

### A-COVERAGE-REFILL: the region change penalty of refilled planes

[RE + LOG, 2026-10-26] The dip of a land wing's effectiveness after a stockpile refill is the mission's
`region_change_penalty` (`mission+0xc0` = `wing+0x150`), which the land branch of 0xf78500 applies as `× (1 − pen)`
(carrier wings skip it, A-REGION-SYNC). New planes bring it in; every hour takes some off. Fixed point ×1e5, raw integers:
```
on every plane add (0xf63e00(wing, old, xpNew, p, flag), called after wing+0x7c = n, the new count):
    if n > old:  pen = trunc((old × pen + (n − old) × min(p, EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR)) / n)
    // p = EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR (0x33382d0) from every plane-adding path: wing creation 0xf59760,
    // AddAirplanes 0xf5ac50 / 0xf5add0, the stockpile and production refill (0xf66890, 0xf683b8, 0xf63aa3), planes
    // returned to a wing 0xf66dd0; planes moved from another wing (0xf63af0 ← 0xf674e0) bring the source's own pen.
    // Removing planes never changes pen. One call per variant offered, so a refill of two variants blends twice.
every hour (0xf80490 from the wing update 0xf62750, hourly strategic-air job), for a wing with a mission
(mission+0x10 type != 0) that is not training (type & 0x800 = 0):
    if pen > 0:  pen = clamp(pen − trunc(gain / 24), 0, 1)
    gain (0xf7ec90, the pool sub-unit's `type` keywords, sub-unit+0x5a8): CAS bit 14 → _DAILY_GAIN_CAS,
         naval bomber bit 15 → _NAVAL_BOMBER, tactical bomber bit 12 → _TACTICAL_BOMBER, mask 0x100000400 (fighter,
         heavy fighter) → _FIGHTER, strategic bomber bit 13 → _STRATEGIC_BOMBER, bit 21 → _MARITIME_PATROL_PLANE,
         else _DEFAULT   (vanilla / TFB 0.888, 0.192, 0.192, 0.888, 0.072 / TFB 0.1, 1, 1 a day)
effectiveness (land wings, in the air hours): e = … × (1 − pen) × …, as in the land branch above.
```
Timing [LOG]: the daily refill (A-REINFORCE, the day-change tick) comes before that tick's hourly decay, so a wing
refilled at the day change has `trunc(old × pen + added × 0.9) / n` reduced once in the tick of the refill and once in
every following tick: an air hour h ticks after the refill reads `pen_refill − h × trunc(gain / 24)`. Example
(`constant_roll_air_clean/u007/air_cv_land_mix`): BUL's land tactical bombers, 76 planes, get the 18 old
`tac_bomber_equipment_0` of the stock at the day change → `pen = 18 × 90000 / 94 = 17234`; 6 hours later (air hour
17) `17234 − 6 × 800 = 12434` → e = 0.87566; air hour 25: 6034 → 0.93966; air hour 33: 0 → 1. With TFB's gains a
tactical bomber needs `0.9 / 0.008 = 112.5` hours to recover a fully new wing, a fighter 24.3.

**Engine state per land wing:** `pen` (start: the save's `region_change_penalty`, 0 from make_save.py); the blend on
every plane add (the daily refill per variant, the creation of a wing); the hourly decay by the wing's sub-unit type
(every hour, not only air hours, only while the wing has a mission); `× (1 − pen)` in the land effectiveness (also the
region air war's effective planes `round(planes × e)`, A-REGION). Carrier wings take none.

**Validation** (`penalty_model.py` in the session scratchpad): every logged land wing effectiveness (`raw.jsonl` →
`air[].wings[].effectiveness`, the land bombers; the land fighters on air superiority are not in the combat log):
- the clean saves (only the scenario's planes): the 8 constant-roll runs (`constant_roll_air_clean/u005`, `u007`) and the
  20 random seeds (`air_clean_batch`): **2,232 of 2,232** values exact to 1e-5, the 47 with a penalty included;
- the template-plane saves (crowding from the CL3 template's 1,000 BUL fighters at Burgas, + 100 AS fighters assumed
  full): `constant_roll_air` 103 of 112, `air_batch3` / `air_batch5` 2,185 of 2,282; every miss has pen = 0 and is the
  unlogged crowding (±1 plane of the fighters, or late hours of one constant-roll battle with more planes at Burgas).
  The parent analysis that read the hour-17 dip at Burgas as 38 extra template planes was this penalty: with it the
  template's planes stay at 1000 (1000 + 94 + 100 → crowd 0.612 × 0.87566 = 0.5359, the logged value).

**Confidence:** high for the blend and the decay (read in full, every logged value reproduced); high that all plane-adding
paths pass the factor (operand resolved at each call); medium for the sub-unit type bit names (from A-REGION-SYNC); the
fighters' decay (0.037 a hour) is not checked in a log (their effectiveness is not logged).

## Worked numbers (TFB map, region 30 Black Sea)

| Item | Value |
|---|---|
| Burgas (state 211: 649 6967 9769 9783 9888 9902 9919) bbox | x 3212..3245, y 1358..1395 → centre **(3229, 1377)** |
| Dobrogea (state 77: 9614 11597) bbox | x 3218..3253, y 1386..1401 → centre **(3236, 1394)** |
| Region 30 centre / hd² | (3346, 1397) / 112² + 67² = 17033 |
| d² Burgas → 7906, 5412, 8256, 8131, 8308, 8282, 8334 | 986, 1745, 5077, 6205, 17044, 26170, 34325 |
| d² Dobrogea → 7906, 5412, 8131, 8256, 8308, 8282, 8334 | 720, 2601, 4157, 4625, 15154, 22280, 32413 |

Coverage of region 30 by range, no `air_range_factor` (Burgas / Dobrogea): 550 km (77 px) 3/7 / 4/7; 700 (98) 4/7 /
4/7; 900 (126) **4/7** / 5/7; 1100 (154) 5/7 / 6/7; 1400 and 2000 (196, 281) 7/7 / 7/7.

Planes at the base (template save + scenario): Burgas in `air_cv_land_mix` 10 × 100 template fighters + 100 AS fighters
+ 100 tactical bombers = 1200 (capacity 10 × 100 = 1000) → r = 0.2 → **f = 0.6**; in `air_land_vs_fleet` 1100 → r =
0.1 → **f = 0.8**; Dobrogea 100 (one tactical bomber wing; ROM's fighters are at base 68) → 1.

| Wings | Coverage | Crowd | Effectiveness | Game |
|---|---|---|---|---|
| BUL land tactical bombers (Tac II, 2000 km), mix | 7/7 | 0.6 | 0.6 | logged 0.600 |
| BUL land fighters (Fighter II, 900 km), mix | 4/7 = 0.57142 | 0.6 | 0.34285 | 0.343 (A-DETECT-MIX fit) |
| BUL land fighters, `air_land_vs_fleet` | 4/7 | 0.8 | 0.45713 | 0.46 (calibration fit) |
| ROM land tactical bombers (Dobrogea) | 7/7 | 1.0 | 1.0 | logged 1.000 |

The logged changes follow the same rules (the mission's effectiveness is refreshed in air hours, ticks ≡ 2 mod 8):
- `s9` tick 74: the bomber wing at 87 planes, P = 1187 → f = 1 − 2 × 0.187 = **0.626** (logged 0.62600).
- Daily refill of 13 planes at tick 85: penalty 13 × 0.9 / 100 = 0.117, −0.008 per hour.
  - Tick 90: 0.6 × (1 − 0.069) = **0.5586** (logged 0.55860).
  - Tick 98: 0.6 × (1 − 0.005) = **0.597** (logged 0.59700).
- `s12`: refill of 12 at tick 37 → tick 42 0.6 × (1 − 0.06) = **0.564** (logged 0.56400).

## Defines and constants involved

| Name | Vanilla / TFB | Role |
|---|---|---|
| `NGame.MAP_SCALE_PIXEL_TO_KM` (global 0x3335f50) | 7.114 / 7.114 | km → map px in 0xf5e5c0 |
| `NBuildings.AIRBASE_CAPACITY_MULT` (0x3333e68, int) | 200 / **100** | capacity per air base level |
| `NBuildings.ROCKETSITE_CAPACITY_MULT` (0x3333ef4) | 100 / 100 | rocket site capacity |
| `NAir.CAPACITY_PENALTY` | 2 / 2 | crowd slope |
| `NAir.SUPPLY_NEED_FACTOR` | 0.28 / **0.25** | air base asked supply per plane (× 0.01) |
| `NSupply.BASE_AIR_SUPPLY_MULT_FOR_TRUCK_BUFFER` (0x3337100) | 1.0 / 1.0 | multiplies the base's asked supply |
| `NAir.MISSION_EFFICIENCY_MULT_AT_LACK_OF_FUEL` | 0.25 / 0.25 | fuel term |
| `NAir.FUEL_COST_MULT`, `NAir.MISSION_FUEL_COSTS` | 0.35 / **0.7**; table | fuel consumption |
| `NAir.EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR`, `..._DAILY_GAIN_*` | 0.9; see A-REGION-SYNC | penalty of refilled planes, hourly decay |
| modifier 5 `air_range_factor`, 0x72 `air_ace_bonuses_factor` | — | range multiplier |
| hard-coded 0.9 | | crowd excess above which f = 0 |
| hard-coded 2-pixel grid | | province rect alignment (0x140a610) |

## How it was verified

- Static: 0xf5e5c0, 0xf87480, 0x10132d0, 0x1012f80, 0x9d37a0, 0xe21330, 0x118f660, 0xabce20, 0x15a43c0, 0x140a3c0,
  0x140a610, 0xa5f7b0, 0xc579f0, 0xc57b70, 0xc4d910, 0xf5c4d0, 0xc65490, 0xc4d770, 0xf625b0, 0xc4d490, 0x1a0afb0,
  0x121a7ae, 0xc543e0, 0xf5ba20, 0x10f3080 read. Define names from their registration sites (`MAP_SCALE_PIXEL_TO_KM`
  0x8407d0, `BASE_AIR_SUPPLY_MULT_FOR_TRUCK_BUFFER` 0x9a4ab9, `AIRBASE_CAPACITY_MULT` 0x7ee330, `ROCKETSITE_CAPACITY_MULT`
  0x7f0220). CAirBase writer 0xc667b0 (RTTI `CAirBase`, vtable 0x2958780): +0x68 → `state` (state+0x58 id), else +0x60
  `carrier`, +0x78 `capacity`, +0x48 `countries`, +0x7c `base`, +0x84 `level`, +0x88 `allow_equipment_type`.
- Geometry recomputed from TFB `map/provinces.bmp` + `definition.csv` (5632 × 2048): region 30's mean pixels equal the
  repo's `data/sea-regions` points for all 7 provinces.
- Saves: `air_cv_land_mix_battle_start.hoi4` / `air_land_vs_fleet_battle_start.hoi4`: base 329 `state=211 capacity=1000
  level=10`, BUL fighter pool at 329 with 10 + 1 wings of 100, the scenario's tactical bomber wing at 329 (mix only);
  ROM's tactical bomber pool at 350 with one wing.
- Logs (`output/ingame/air_batch3`, `air_batch5`, `land_check`; `raw.jsonl` → `airObjects[side][].wings[].effectiveness`
  = mission+0xd8, logged only for wings in the naval combat's air objects, so the land bombers and not the AS
  fighters): ROM land bombers 100000 in every record. BUL land bombers 60000 at full strength; every other value matches
  the crowd change from plane losses or the refill penalty, as in the worked numbers.

## Differences from the current spec

- A-DETECT-MIX rule 2: the 0.6 / 0.805 "supply × air_mission_efficiency" factor is the crowd factor: 0.6 at 1200 planes,
  0.8 at 1100 (0.805 was the fit). Supply and `air_mission_efficiency` were 1 and 0.
- The land base origin is the state's bounding-box centre, not the `buildings.txt` air base. Both give 4/7 for
  Fighter II here, but Fighter (550 km) differs: 3/7 from the state centre, 4/7 from the building.
- Province positions: the game uses the provinces.bmp mean pixel. `import-regions` reads `unitstacks.txt`, which
  matches for 2741 of the 3133 sea provinces in `data/sea-regions` and is off by up to 35 px for the other 392 (all 7 of
  region 30 match). This also affects S-NIGHT darkness.
- Engine inputs for a land wing: the base's state (provinces → centre), the base's capacity (state air base level ×
  `AIRBASE_CAPACITY_MULT`), every plane at the base (other wings and countries included), the wing's range and
  `air_range_factor`, the region's provinces and their mean pixels; supply ratio and the country's fuel ratio as inputs
  defaulting to 1.

## UNRESOLVED

- The received supply (needs the live supply network; minimal input: the base entry's supply ratio, shown in the air
  base tooltip `AIRBASE_SUPPLY_PENALTY_DETAIL`). The exact proportional rule in 0x121a7ae (rounding, order of consumers)
  is only skimmed.
- `state+0x864` (forces capacity_penalty 0) and `entry.operational_status` semantics.
- The seam case of the region and state boxes (only read, no test).
- The fighters' coverage is checked only through the fitted 0.343 / 0.46 (their effectiveness is not logged). The
  state-centre vs building-position choice is code-only: no logged case tells them apart.
