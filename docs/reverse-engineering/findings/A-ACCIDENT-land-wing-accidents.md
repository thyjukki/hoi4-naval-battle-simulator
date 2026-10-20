# A-ACCIDENT: air accidents during a naval battle

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-REGION (A-REGION-ACCIDENT is replaced by this finding), A-STATS-ACCIDENT, A-WINGS-EXT
  (land wings), new A-ACCIDENT
- **Confidence:** high for the roll path, the local generator, the chance and the loss formula (read from code,
  every branch); medium for the meaning of the air manager's three base lists and of `gamestate+0xa35`. The logs
  agree with the formula (4 single-plane losses of land wings outside air hours against about 7 expected), but the
  sample is small and the logs do not record the rolls.

Method: `tools/re/` (`hoi4re.py`, `ctx.py`, `defmap.json`) and throw-away disassembly scripts. Logs:
`output/ingame/air_batch3` and `air_batch5` (`air_land_vs_fleet`, `air_cv_land_mix`; 39 runs, `raw.jsonl`
`airObjects` / `air`); saves `air_cv_land_mix_battle_start.hoi4`, `air_land_vs_fleet_battle_start.hoi4`.
Conventions as in [A-REGION](A-REGION-carrier-wings-in-the-region-air-war.md): RVAs, fixed point ×1e5 (raw integer =
value × 100000), every product and quotient truncated toward zero; `wing` = CAirWing; `mission` = `wing+0x90`
(`wing+0xa0` its `type`, `wing+0xa4` its `executing_mission`).

## A-ACCIDENT-PASS: when and over which wings

Step 4 of the hourly strategic-air update 0xc57d30 (called at 0xc58129, every hour, not only air hours, before the
region air war of step 5 and before the naval combat hour) is **0xc5efc0(airManager)**:

```
bases = airManager.list(+0x90, n +0x9c) ++ list(+0xa8, n +0xb4) ++ list(+0xd8, n +0xe4)   // the last: carrier bases
(a parallel job over `bases`, vtable 0x2959fe0; draws nothing)
if gamestate+0xa35 == 0:
    seed = random_int(strategicair.cpp, 0x1be8) & 0x7fffffff       // ONE draw of the game generator per hour
    state = { c = seed, k = 0x5ea6ba9f − seed }                     // uint32
    for base in bases:
        for entry in base.countryEntries(+0x48, n +0x54):            // one per country at the base (pools +0xb8 / +0xc4)
            0xc60290(entry, &state)
```

**The local generator** (inlined three times in 0xc60290; all uint32 arithmetic, wrapping):

```
next():
    x  = k − c × 0x4ad685b3
    x ^= x >> 8
    x += 0x68e31da4
    x ^= x << 8
    x *= 0x1b56c4e9
    x ^= x >> 8
    x *= 0x92d68ca2
    x ^= x >> 8
    c += 1
    return x & 0x7fffffff
```

`next() % 100000` is the usual "U" (raw fixed in 0..99999). Only the seed comes from the game generator; every other
accident draw is local.

**0xc60290(entry, state)**, per (base, country) entry:

```
if entry.pools(+0xc4) == 0: return                         // no draw at all
u1 = next() % 100000
if u1 >= 10000: return                                     // 90 % of hours: nothing at this entry
start = next() % pools                                     // signed idiv, x is non-negative
for pool in pools[start..end) ++ pools[0..start):          // circular
    for wing in pool.wings(+0x28, n +0x34):
        if wing.type & 0x800 (training):
            eligible = planes > 0 and !wing+0x79 and ready (+0x64 >= +0x60) and no transfer target (+0x48)
                       and not (base is a carrier (base+0x68 == 0) and its ship's task force InCombat)
                       and 0xf80a40(mission)               // the training mission is valid (has an air base)
        else:
            eligible = executing != 0 and not executing & (0x20 drop_nuke | 0x10000 barrage | 0x20000 sam)
                       and not (main variant (0x100ff30) type keyword bit 16)        // missiles
        if !eligible: continue                             // no draw
        p  = trunc(0xf61d70(wing) × ACCIDENT_CHANCE_BALANCE_MULT)
        u2 = next() % 100000                               // drawn for every eligible wing, even when p == 0
        if p > 0 and p > trunc(u2 × 0.1):                  // raw: p_raw > u2 / 10
            0xf5dd80(wing, next() % 100000)                // u3
            return                                         // at most one accident per entry and hour
```

So with p in fixed point, an eligible wing that is reached has an accident with probability `p / 0.1` (exactly
`p_raw / 10000`), i.e. `chance × BALANCE_MULT / 0.1` = **`chance`** with the vanilla / TFB `BALANCE_MULT` 0.1.

**Which wings in a naval battle qualify.**
- Land wings with an assigned region mission that is active (`type ≠ 0`, `executing ≠ 0`): naval strike (0x10),
  air superiority (0x1), interception (0x4) and the rest, whether or not they join the battle this hour. The
  logged land wings execute their mission in every hour (`executing` 16 in all 8 hour phases).
- Carrier wings scrambled in a battle: `type = 0` → 0xf61d70 returns 0, so **no accident**; but they are eligible
  (executing ≠ 0) and **consume one local draw** each in an entry's accident hour. Carrier wings of carriers not in
  combat with a player mission (`type ≠ 0`) can have accidents, with CARRIER_MULT.
- Training wings: not on a carrier in combat.

## A-ACCIDENT-CHANCE: 0xf61d70 → 0xc4daf0

**0xf61d70(wing, out, tooltip)**:
```
if wing.type == 0 or wing.planes <= 0: return 0
if !0xf80920(mission):                         // mission+0x30 (region) set and mission+0x20 (active)
    if !(type & 0x800): return 0
    mask = 0x800
else:
    mask = type & 0x800 ? 0x800 : (executing != 0 ? executing : lowest set bit of type among bits 0..17)
rel     = 0xf60970(wing, stat 0x41 reliability, mask)    // the mission's array, else the cache wing+0x390
xpF     = wing+0x238 (experience holder).Get(0x65 air_accidents_factor)
owner   = country of tag wing+0x9c4 if set, else the pool's country ([wing+0x38]+0x40 → +0x90)
base    = the pool's air base (handle [wing+0x38]+0x18)
return 0xc4daf0(out, base, owner, rel, xpF, training = type & 0x800, tooltip)
```

**0xc4daf0** (tooltip strings `RANDOM_AIR_ACCIDENT_CHANCE_*`):
```
R = trunc((1 − min(rel, 1)) × ACCIDENT_CHANCE_RELIABILITY_MULT)
B = ACCIDENT_CHANCE_BASE + owner.air_accidents (0x64, country+0x5b8)
M = 1
region = base is a carrier (base+0x68 == 0) ? region of the ship's task force location (tf+0x1f0 → +0xc8)
                                             : region of the base's province (0xc53910 → province +0xc8)
w = weatherManager(gamestate+0x688).regionHolder(region.id)(0xf19fd0)+0x10 .Get(0x64 air_accidents)
if w > 0: M = 1 + w
if base is a carrier: M += ACCIDENT_CHANCE_CARRIER_MULT
if training:          M += AIR_WING_XP_TRAINING_MISSION_ACCIDENT_FACTOR
M += xpF + owner.air_accidents_factor (0x65)
chance = trunc( trunc(B × R) × M )
```
- All factors **add** into one multiplier `M`; CARRIER_MULT 1.5 makes it `× 2.5` (not `× 1.5`) on a carrier.
- The weather is that of the **base's** strategic region, not the mission's or the battle's.
- `M` is not clamped: with a large negative `air_accidents_factor` the chance is ≤ 0 and no accident happens (vanilla
  `air_wing_experience_bonus_max` −1.0 makes veteran wings accident-free; TFB −0.5).
- Reliability above 1 gives `R = 0`; the wing's reliability is its cached, plane-share weighted stat
  ([A-MIXED](A-MIXED-plane-variants-in-a-wing.md)), so a mixed wing's chance follows its mix.

Example (TFB, `tac_bomber_equipment_2` reliability 0.8, land base, clear weather, XP 0 so `air_wing_experience_malus_min`
`air_accidents_factor` +0.10, no country terms): `R = 0.4`, `B × R = 0.04`, `M = 1.1`, chance 0.044, p = 0.0044 →
an accident in about 0.1 × 4.4 % = 0.44 % of hours for a wing alone at its base.

## A-ACCIDENT-EFFECT: planes lost, 0xf5dd80

```
owner = country of wing+0x9c4 (else the pool's)
x     = wing.planes × u3                          // raw fixed = planes × U
n     = 0x715310(owner, x):
          owner.accidents_score(+0x14e8) += trunc(x × ACCIDENT_EFFECT_MULT)
          n = trunc(accidents_score); accidents_score −= n
if n > 0:
    RemovePlanes(wing, n, killer = 0xbb3e00 tag)  // 0xf63580: oldest equipment entries first (A-MIXED-REMOVE)
    wing+0x84 += n                                // accident loss counter
    0xc5cb60(airManager, wing, n, …, kind 3)      // loss statistics only
    if the wing has a live ace and u3 <= ACE_DEATH_CHANCE_BASE: the ace dies (0x61bba0, kind 2)   // A-ACE
```
- **How many:** the integer part of a **per-country** accumulator `accidents_score` (saved with the country, token
  0x37fc; the writer 0x7191b0 reads `country+0x14e8`). Each accident adds `planes × U × ACCIDENT_EFFECT_MULT`
  (0.007 vanilla and TFB): a 100-plane wing adds 0.35 planes on average, so a whole plane is lost only every few
  accidents, from the wing whose accident crosses the next integer. Every wing of the country, anywhere on the map,
  feeds the same accumulator. The `tools/ingame` saves have `accidents_score=0` for every country.
- **Experience:** yes, through RemovePlanes (0xf63580 → 0xf63e00):
  ```
  f        = share of the mission region's provinces controlled by the owner or its allies (each 1/count)
  lossF    = clamp( (1 − trunc(f × AIR_WING_XP_LOSS_REDUCTION_OVER_FRIENDLY_TERRITORY_FACTOR))
                    × (1 + owner modifier 6, air_wing_xp_loss_when_killed_factor), 0, 1 )   // negative → 0
  xpKilled = trunc(lossF × AIR_WING_XP_LOSS_WHEN_KILLED)
  xp(+0x218) = max(0, (oldPlanes × xp − n × xpKilled) / newPlanes)
  ```
  i.e. the dead pilots are assumed to have `xpKilled` experience; below that the average falls, above it rises.
- **Manpower:** `wing+0x80 −= round(Σ k × variant manpower × MANPOWER_LOSS_RATIO_PLANE_SHOT)`.
- RemovePlanes does **not** refresh the wing's stat cache (refreshed at the next hourly update).

## Defines and constants involved

| Name | Vanilla / TFB | Role |
|---|---|---|
| `NAir.ACCIDENT_CHANCE_BASE` | 0.1 / 0.1 | `B` (0xc4db86) |
| `NAir.ACCIDENT_CHANCE_RELIABILITY_MULT` | 2.0 / 2.0 | `R` (0xc4db47) |
| `NAir.ACCIDENT_CHANCE_CARRIER_MULT` | 1.5 / 1.5 | added to `M` on a carrier (0xc4e272) |
| `NAir.AIR_WING_XP_TRAINING_MISSION_ACCIDENT_FACTOR` | 0.2 / 0.2 | added to `M` for training (0xc4e389) |
| `NAir.ACCIDENT_CHANCE_BALANCE_MULT` | 0.1 / 0.1 | `p = chance × BALANCE` (0xc605a9) |
| `NAir.ACCIDENT_EFFECT_MULT` | 0.007 / 0.007 | accumulator step (0x715310) |
| `NAir.ACE_DEATH_CHANCE_BASE` | 0.005 / 0.005 | ace death `u3 ≤` (0xf5df61) |
| `NAir.MANPOWER_LOSS_RATIO_PLANE_SHOT` | 0.1 / 0.1 | manpower lost (0xf636dc) |
| `NAir.AIR_WING_XP_LOSS_REDUCTION_OVER_FRIENDLY_TERRITORY_FACTOR` | 0.3 / **0.2** | XP loss (0xf63a02) |
| `NAir.AIR_WING_XP_LOSS_WHEN_KILLED` | 300 / 300 | XP loss (0xf63aad) |
| hard-coded `10000` (0.1), `100000` | — | the 10 % hour gate; the U range |
| hard-coded `× 0.1` (raw `u2 × 10000 / 1e5`) | — | the threshold scale in the chance test |
| hard-coded 0x5ea6ba9f, 0x4ad685b3, 0x68e31da4, 0x1b56c4e9, 0x92d68ca2 | — | the local hash generator |
| modifiers `air_accidents` (0x64), `air_accidents_factor` (0x65) | country, region weather (0x64), XP holder (0x65) | `B`, `M` |
| static `weather_*` `air_accidents` | vanilla rain 0.1 / 0.3, snow 0.1, blizzard 0.3, sandstorm 0.6; TFB 0.1 / 0.3, 0.25, 0.5, 0.6 | `M = 1 + w` |
| static `air_wing_experience_bonus_max` / `_malus_min` `air_accidents_factor` | vanilla −1.0 / +0.5; TFB −0.5 / +0.10 | XP term of `M` |

## How it was verified

- **Static:** every branch of 0xc5efc0, 0xc60290, 0xf61d70, 0xc4daf0, 0xf5dd80, 0x715310, 0xf63580, 0xf63e00 read
  from the disassembly; `accidents_score` ↔ `country+0x14e8` from the token table (0x831c0, token 0x37fc) and the
  country writer (0x71a570); the country ctor zeroes it (0x6caa95).
- **Logs record no accident roll:** the logger hooks `random_int` only on the combat thread while a combat update is
  open, so `rolls.csv` has no `strategicair.cpp` line (no 0x1be8, nor the region war's 0x12c9); checked in
  `air_batch3/5/7` and `air_check`.
- **Plane counts (39 runs, 18,066 land-wing hours):** land wings lose planes outside air hours (no region war, no
  strike) exactly 4 times, always one plane (`air_cv_land_mix` s2 h185, s3 h524, s4 h97 BUL bombers; s10 h249 ROM
  bombers); none in `air_land_vs_fleet`. A Monte Carlo of this finding's rules over the logged hours and plane
  counts (gate 10 %, chance 0.040–0.044, BUL's fighters at the same entry, accumulator from 0) expects 0.9–1.2 /
  1.6–1.9 / 3.8–4.3 such losses for lvf ROM / mix ROM / mix BUL (observed 0 / 1 / 3; total ≈ 7 vs 4, Poisson
  p ≈ 0.17). The per-country accumulator explains why the short `air_land_vs_fleet` runs (one wing, ~250 h,
  ~0.35 planes accumulated per run) almost never lose a whole plane.
- Carrier wings (all `air_batch*` runs, 19,607 drops) never lose planes outside air hours, as `type = 0` predicts
  (weak evidence alone: their accidents would be rare).

## Rules for the engine

1. Every hour (air hour or not), before the region air war: for each side and each air base the side's land wings
   use (one entry per country and base), with probability 0.1 (`u1 < 10000`) pick a random start pool (sub-unit pool
   at the base) and walk the eligible wings circularly; each eligible wing has an accident with probability
   `trunc(chance × ACCIDENT_CHANCE_BALANCE_MULT)_raw / 10000`; the first accident ends the entry's hour.
2. `chance` as in A-ACCIDENT-CHANCE (reliability of the wing's current mix, the base region's weather, the XP holder
   value from A-STATS-XP).
3. Planes lost: `acc[country] += planes × U × ACCIDENT_EFFECT_MULT`; lose `floor(acc)`, keep the fraction (start
   from the save's `accidents_score`, 0 in the `tools/ingame` saves). Remove them oldest equipment entry first
   (A-MIXED-REMOVE), recompute XP as above; ace death if `U ≤ ACE_DEATH_CHANCE_BASE` (same U).
4. Carrier wings in a battle: none. Rolls: the game generator gives one `random_int(strategicair.cpp, 0x1be8)` per
   hour (step 4, before the region war's draws); everything else is the local generator.
5. Size: about `0.1 × chance × 0.0035 × planes` planes per wing-hour, ≈ 0.5 planes over a 300-hour battle for a
   100-plane wing; a `BattleRules` switch (own `RollKind`) is enough. An exact replay is not possible from the seed:
   the counter `c` at the battle's entries depends on every base and wing in the world processed before them. A
   replay needs the logger to hook 0xf5dd80 (wing handle, u3, planes before/after, `accidents_score`) and apply the
   accidents as scripted events.

## Differences from the current spec

- New spec item A-ACCIDENT (`docs/mechanics/air.md`): nothing in the engine models accidents yet (no `ACCIDENT_*`
  define in `GameDefines`).
- A-REGION-ACCIDENT's outline is replaced: the chance test is `p > 0.1 × U` (as written there), but carrier wings in
  battle have none (A-STATS-ACCIDENT, confirmed here) and still consume a local draw; the carrier factor is additive.
- A-STATS-ACCIDENT's "combination not decoded" is settled by A-ACCIDENT-CHANCE.

## UNRESOLVED

- What the air manager's second base list (`+0xa8`) holds (first: air bases, third: carrier bases, by the
  `pCarrierBase` assert); what `gamestate+0xa35` is (it also skips an organisation reset, S-DOCTRINE).
- The friendly share `f` for a sea region (provinces without a controller): read as "no friendly province" (f = 0),
  not traced through 0xbb48f0 for tag 0.
- The killer tag 0xbb3e00 and the statistics kind 3 of 0xc5cb60 (no effect on the battle).
- `wing+0x150` (blended by 0xf63e00 when planes are added, with `EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR`).
