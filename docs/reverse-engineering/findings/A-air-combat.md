# Findings: naval air combat (A-*), hoi4.exe 1.19.3

Module `naval\naval_fire_exchange_air.cpp`. Each carrier (and each group of land-based planes that joined) is an **air object** ("AirFX") that belongs to a fire-exchange group ("group"; the group fights one opposing group, `group+0x20`). All RVAs are relative to the image base.

## Conventions and helpers

- Fixed point: int64 with scale 1e5. A product is `trunc(a*b/1e5)` (magic multiply 0x29f16b11c6d1e109, `sar 0xe`, plus the sign bit, so it truncates toward zero). A division is `a*1e5/b` with `idiv`. Every intermediate value below is truncated at 5 decimals.
- `0x24ed730`: fixed to int, **rounds half away from zero**.
- `0x2233db0` `random_fixed`: U in {0, 0.00001, …, 0.99999}. `0x2233fa0`: random int31.
- Modifier ids come from `scratch_dmg_modidx.json` (registration table found by the damage agent): 0x15 NAVAL_STRIKE (the weather `naval_strike`), 0x16/0x17/0x18 NAVAL_STRIKE_ATTACK/TARGETTING/AGILITY_FACTOR, 0x22 CARRIER_TRAFFIC, 0x23 CARRIER_NIGHT_TRAFFIC, 0xc9/0xca/0xcb NAVAL_CARRIER_AIR_ATTACK/TARGETTING/AGILITY_FACTOR, 0x10c CARRIER_CAPACITY_PENALTY_REDUCTION, 0x1b6 CARRIER_FIGHTER_SORTIE_EFFICIENCY_FACTOR, 0x1cf CARRIER_SORTIE_HOURS_DELAY, 0x18c NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR. 0x24 is not in that table. It is read as the carrier's own stat in the sortie sum, so it is `sortie_efficiency`. 0x22 and 0x23 are confirmed by the sortie tooltip, which prints `[rbp+0x78]` (mod 0x23) under `MODIFIER_CARRIER_NIGHT_TRAFFIC`.
- Plane stats: `0xf60970(wing, out, statIdx, missionFlags)` returns the raw equipment stat (`wing[0x390+8*idx]` or a per-mission override). Indices: 0x2f air defence, 0x30 air attack, 0x31 agility, 0x34 naval strike attack, 0x35 naval strike targeting. Getters with modifiers:
  - `0xf60f10` naval attack per plane = `stat34 × 0.1 × (1 + Σ naval_strike_attack_factor…)`, × AIR_NAVAL_KAMIKAZE_DAMAGE_MULT for kamikaze missions.
  - `0xf61280` naval targeting per plane = `stat35 × 0.1 × (1 + Σ naval_strike_targetting_factor…)`.
  - `0xf60bc0` naval strike agility = `stat31 × AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY (0.02) × (1 + Σ naval_strike_agility_factor…)`.
  - `0xf60760` speed with modifiers.
  - The `Σ` sums come from ace, country, region and day/night (DAY_NIGHT_COVERAGE_FACTOR); I did not decode them in detail.
- Ship fields: +0x6f8 HP, +0x308 max HP (used as `max(maxHP, 0.001)`), +0x700 org, +0x328 reliability, +0x728 → fleet, fleet +0x4ec = carrier stance index. Member +0x12c is the submarine "revealed" timer (SUBMARINE_REVEALED_TIMEOUT; surface ships hold −1). Member +0xe0 & 0xb means alive / in battle.
- Combat (`CCombatNaval`) fields: +0x44 hours since the battle started, +0x10c port-strike flag, +0x120 an "air every hour" flag (see A-CADENCE), +0xb0 weather/region modifier holder, `0x15c3080(combat)` second holder (night/darkness), +0xb8 > 0 means darkness. Combatant (side) fields: +0xd8 = ctor flag, true for the side stored at combat+0x28 (very likely the attacker; `0x13e05f0`, `0x15c28f0`); +0x148 screening efficiency, +0x150 the second screening value, +0x158 positioning, +0x170 fleet AA term, +0x3e8 external planes joined so far.
- AirFX fields: +0x08 group, +0x10 carrier member, +0x1c → carrier base (+0x60 ship), +0x28/+0x34 list of strike wings (mission flags & 0x190: 0x10 naval strike, 0x80 naval kamikaze, 0x100 port strike), +0x40/+0x4c list of all wings, +0x5c total planes of all wings (recomputed each air step by `0x1976ca0`), +0x64 country, +0x80 "already struck" byte, +0x81 external (land-based) flag, +0xc0 external efficiency, +0xc8..+0xe8 plane-weighted equipment averages (`0x197d4a0`), +0xf8 cooldown hours.

## Call graph of one air step

`0x1c6b560` (group hourly update) decrements every AirFX cooldown `+0xf8` by 1 (if > 0) and calls `0x1c6c580` (group air step):
1. Gate: `combat[0x120] != 0` **or** `0xe24530(strategicRegion)` (the air clock). If false, nothing happens this hour.
2. `0x161e9a0`: the stacking factor F for this side (A-STACK).
3. It builds a list of eligible wings: carrier wings with cooldown ≤ 0, and external wings with `+0x80 == 0`. **The list is never used.** The loop below runs over all AirFX (`[group+0x58]`), see A-CADENCE.
4. For each AirFX (only if the group has an opposing group): `0x1976ca0` (planes), `0x197d4a0` (averages), then `0x197b890(airfx, enemyGroup, combat, F)` (main strike). If that returns true and the AirFX has a carrier, `0x197d770` sets `cooldown = ceil(carrier_sortie_hours_delay modifier) + CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT`.
5. Every external AirFX gets `+0x80 = 1`.

`0x197b890` main strike, in order:
1. `nWings = max(1, round(count(strikeWings) × F))`.
2. Carrier check: `0x1c69dc0(group, carrierShip)` gives the carrier's member in this group. If the member is found but dead, return false. If it is not found, carry on without the carrier multiplier.
3. `0x1978e30`: average naval attack, targeting and agility over the first nWings strike wings (A-DMG).
4. Carrier AirFX only: `flying = round(planes × clamp(0x19786c0 sortie, 0, 1))`. External AirFX: `flying = planes`.
5. `0x197a700`: pick the target (A-TARGET).
6. Find the defending fighters and the stance share; `0x197b380` interception (A-INTERCEPT).
7. `0x197c8b0`: preemptive AA (A-AA-PREEMPT).
8. Damage multiplier for targeting, then `0x197ac80` strike (A-HIT, A-DMG, A-AA-REDUCE, A-CRIT).
9. Port-strike extras when `combat[0x10c]`. Then `0xf80f60` per wing (losses and damage, i.e. experience).

---

## A-CADENCE: when planes strike

**What the game does.**
- `0xe24530(region)`: `airHour ⇔ (date.hours − 43 800 000) mod (2 × HOURS_DELAY_AFTER_EACH_COMBAT) == region[0x140]`. Here `date.hours = gamestate[0x468]`, and 43 800 000 = 5000 years × 8760 h, so the value is hours since year 0. `region[0x140] = region[0x60] mod (2 × HOURS_DELAY_AFTER_EACH_COMBAT)` is set in `0xe23bb0` (strategicregion.cpp, 0xe2449c..0xe244ab); `region[0x60]` is most likely the strategic region id. 8760 and 24 are both multiples of 8, so this is `hourOfDay mod 8 == regionId mod 8`.
- With the vanilla 4 h delay, **every air object (carrier and land-based) in a battle in strategic region R acts at the three hours of each day with `hour % 8 == R % 8`**. The phase is tied to the clock and the region. It does not depend on the battle start. The first strike comes at the first such hour after the battle starts (hour 0 counts).
- Bypass: if `combat[0x120] != 0`, the air step runs every hour. The constructor sets it to 0 (`0x15c2422`); I found no writer that sets it to 1 in the naval modules, and `0x161b5f0` uses it next to the port-strike flag. In normal sea battles it is false.
- `CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT` **has no effect**. `0x197d8a9` writes `cooldown = ceil(CARRIER_SORTIE_HOURS_DELAY mod) + 3` to `+0xf8`, and `0x1c6ba78` counts it down. But the only reader, `0x197a1b0` (`cooldown <= 0`), feeds the eligibility list in `0x1c6c580` (built at 0x1c6c6b0..0x1c6c705), and that list is dead: the strike loop at 0x1c6c710 reads `[group+0x58]` / `[group+0x64]`, not the list. The external "struck once" byte `+0x80` (set at 0x1c6c799) is likewise read only there (and in save code `0x197d990`). Even if the filter were live, a 3 h cooldown is shorter than the 8 h gate.
- The air step does not check S-ACTIVATE.

**Evidence.** 0xe24638..0xe24654; 0xe2449c..0xe244ab; 0x1c6c5a4..0x1c6c5d5 (gate); 0x1c6c6b0..0x1c6c705 (dead filter); 0x1c6c710..0x1c6c772 (strike loop); 0x197d770/0x197d8a9 (cooldown write); 0x1c6ba70..0x1c6ba95 (decrement).

**Verdict.** The second candidate is right (8 h, aligned to the clock), with a precise phase: `regionId mod 8`. The 6 h carrier candidate is **wrong**: the define is written but never acts, which matches the community report. Land-based planes use the same clock (the wiki's 04/12/20 is the phase of one region, `id % 8 == 4`). Night still matters, through the sortie traffic factor (A-SORTIE).
**Confidence.** High for "every 8 h on the region clock" and for the cooldown having no effect. Medium that `region[0x60]` is the region id and that the date offset makes the phase equal to the hour of day.

## A-SORTIE: how many carrier planes fly

**What the game does** (`0x1976db0`, wrapped by `0x19786c0`):
```
stance  = CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[carrierShip.fleet.stanceIdx]
if !side.flag_d8: stance = 1 - stance            // the side at combat+0x30 (defender?) uses the complement
base    = (combat.hours < SELECTED_SORTIE_INITIAL_TIME) ? stance : BASE_CARRIER_SORTIE_EFFICIENCY
base   += screeningValue(carrierMember, sortie_efficiency)                 // 0x1972570, stat 0x24
if firstWingOfAirFX is fighter-type (equip flags & 0x100000400):
    base += screeningValue(carrierMember, CARRIER_FIGHTER_SORTIE_EFFICIENCY_FACTOR 0x1b6)
crowd   = carrierBase cached factor (A-SORTIE crowd, below)
weather = clamp(1 + carrier_traffic(region holder combat+0xb0), 0, 1)
night   = clamp(1 + carrier_traffic(night holder) + (darkness>0 ? country.carrier_night_traffic : 0), 0, 1)
sortie  = max(0, base × crowd × weather × night)                         // 0x19770a4..0x197711c
eff     = max(0, sortie × carrierOrg / carrierMaxOrg)                     // 0x19786c0: 0xc39530, tooltip CARRIER_EFFICIENCY_ORG
flying  = round(airfx.planes × clamp(eff, 0, 1))                          // 0x197ba43..0x197ba71; must be > 0
```
- `screeningValue(member, stat)` (`0x1972570`) = the ship's modifier `stat` + (combat box 2 or 3) `side.screeningEff × static screening_bonus[stat]` + (box 3, the carrier line) `0x161ece0(side) × static capital_screening_bonus[stat]`. In vanilla, `capital_screening_bonus` has `sortie_efficiency = 0.1` and `screening_bonus` has none. So the "+10 % from screening" is `0.1 × capital-screening efficiency` (`0x161ece0` = `1 − (1 − side+0x148) × (1 − side+0x150)`, the combined screening and carrier-screening value; rechecked) for carriers in the carrier line. Leader traits, doctrines and spirits enter through the ship's `sortie_efficiency`.
- Crowd (`0xc579f0` chain, cached per country at base+0xd8 and read by `0xc53fd0`): `planes = 0xc4d910(base)`, `cap = base[0x78]`. If `planes ≤ cap` (or planes ≤ 0): 1. If `cap == 0`: 0. Otherwise `r = (planes − cap)/cap`; if `r > 0.9`: 0; else `clamp(1 − CAPACITY_PENALTY × (1 + carrier_capacity_penalty_reduction(owner)) × r, 0, 1)`. The reduction modifier applies only when `base[0x68] == 0` and the base has a ship.
- The clamp to [0,1] comes only at the end, after the org multiplication. A sortie efficiency above 1 therefore offsets overcrowding and low org.
- `airfx.planes` (`+0x5c`) counts **all** wings on the carrier, fighters included.

**Verdict.** DIFFERS from air.md in several ways:
1. The org factor **exists**: × carrier org/maxOrg (the old simulator was right on this).
2. The stance does not split planes into offence and defence. During the first 24 h it **replaces** the 0.5 base (attacker: offensive ratio; other side: 1 − ratio).
3. The screening bonus is `0.1 × capital-screening efficiency`, and only in the carrier line.
4. Overcrowding is a separate multiplicative factor, set to 0 above 90 % overfill.
5. Weather and night are separate clamped factors. Night uses `carrier_traffic` from the night modifier (−1 × darkness) plus the country's `carrier_night_traffic`.

**Confidence.** High for the structure. Medium for the meaning of the combatant flag (attacker) and for `0x161ece0` being capital-screening efficiency.

## A-STANCE

**What the game does.** The stance index is per fleet (`ship+0x728 → +0x4ec`). It is used:
1. In A-SORTIE as the base for the first `SELECTED_SORTIE_INITIAL_TIME` hours.
2. In A-INTERCEPT: `defShare = 1 − RATIO[target ship's fleet stance]` (0x197bde1..0x197be0e). If the target is not a warship (a convoy): `defShare = 0.5` (0xc350), or `1 − RATIO` of the defending carrier with the most fighter wings, if there is one (0x197be18..0x197be54).

**Verdict.** DIFFERS: there is no offensive/defensive split of the flying planes. The offensive share is only a time-limited base sortie efficiency. The defensive share only scales how many enemy fighters intercept. `CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX` is read only at 0xd6343b (fleet default).
**Confidence.** High.

## A-STACK: stacking factor

**What the game does** (`0x161e9a0` and `0x1c6a540`):
```
cap      = max(NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY, Σ_enemyGroups silhouette(group))    // max, not sum
silhouette(group) = capVal + other + ceil(screenVal / (1 + SCREEN_CAP_REDUCTION_FACTOR × capVal))
    capVal    = 10 × #capitals          (SHIP_SILHOUETTE_VALUE_PLANES_CAPITAL; c3af10)
    screenVal = 5 × #screens            (c3b210)
    other     = 16 × #carriers (c3af20 && !sub) + 7 × #subs (c3b220) + 3 × #support (c3b230) + 4 × #convoys
    (only members alive (&0xb) and with member+0x12c != 0, i.e. hidden subs do not count)
ownPlanes = Σ_ownGroups Σ_carrierAirFX airfx.planes        // external (land-based) planes are not counted
F = ownPlanes > cap ? 1e10 / (1e5 + trunc(EFFECT × (ownPlanes − cap)))  = 1 / (1 + 0.005 × excess) : 1
```
The enemy side is `combat+0x30` if `side.flag_d8`, else `combat+0x28`.

Where F is used (in `0x197b890`):
- `nWings = max(1, round(#strikeWings × F))`: only the first nWings strike wings feed the attack/targeting/agility averages (`0x1978e30`).
- The AA base: `aaPlanes = ceil((flying − intercepted) × F)` (0x197be90..0x197bf02).
- F does **not** scale `flying` or the number of planes that strike.

**Verdict.**
- Cap: DIFFERS. It is `max(80, silhouette)`, not `80 + silhouette`. The screen divisor uses the capital *silhouette sum* (1 + 0.02 × 10 × caps = 1 + 0.2 per capital), not the capital count. Support ships (3) count.
- Curve: the `1/(1 + 0.005 × excess)` candidate is right.
- Effect: NEW and surprising. F does not reduce takeoffs. It shrinks the set of wings used for stat averages and the AA kill base (so stacking actually *reduces* AA losses). Land-based planes do not count.

**Confidence.** High for the formulas. Medium-high for "F does not scale strikers": I traced r15d from 0x197ba71 to 0x197c00b with no use of F.

## A-EXTERNAL: land-based planes

**What the game does.** Join cap (`0x161b5f0`, part 0x161b653..0x161b7b3):
`cap = max(NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP, trunc(Σ enemy warships' current HP × JOIN_RATIO × (1 + JOIN_RATIO_PER_DAY × floor(hours/24))))`. Whole days count (`/24` with `0x2aaaaaab`, `sar 2`); only ships, not convoys (`0x1622d40`); current HP `+0x6f8`.
Port strikes (`combat[0x10c]` and not `[0x120]`) use another cap (`0xc54980`).

Join (`0x16205c0`): `room = max(0, cap − arg5)`. `add = clamp(min(offered, room − side.joinedTotal), 0, …)`, then `side.joinedTotal(+0x3e8) += add`. The planes go into the existing external AirFX for that air base (`0x197a6a0`) or a new one (`0x1976630`, `0x1979770`). `+0x3e8` is never decreased, so **the cap applies to the cumulative total joined during the battle**. Lost planes do not free it.

In the strike, external AirFX skip the sortie step (`flying = planes`). Their attack is multiplied by `airfx+0xc0` (an external efficiency, probably mission efficiency; 0x1979527). They get no carrier multiplier.

**Verdict.** Cap formula MATCHES with whole days. NEW: the cap is cumulative, and the HP is the current HP of enemy warships only. External groups stay in the battle and strike at every air hour (the "once" flag is dead, see A-CADENCE). Mission efficiency is `+0xc0` (its source is UNRESOLVED; the air-mission code is out of scope). The 5th argument `arg5` of `0x16205c0` is UNRESOLVED (caller found only through a vtable).
**Confidence.** High for the cap. Medium for the join bookkeeping.

## A-INTERCEPT: carrier fighters vs bombers

**What the game does** (`0x197bb9d..0x197bdc6` collect defenders; `0x197b380` resolve):
- Defenders: every wing with fighter-type equipment (`0xf63100`: flags & 0x100000c00) on any carrier in the **target's group**, regardless of mission. Land-based fighters are not used.
- `D = CARRIER_PERCENTAGE_DEFEND × Σ defenders' planes × defShare` (fractional fixed; defShare from A-STANCE). The defenders' own sortie efficiency is not used.
- `A = Σ planes of the attacking AirFX's strike wings` (all of them, not just `flying`).
- Averages over the wing count (unweighted mean per wing): defender air attack `dAtk` (0x30), agility `dAgi` (0x31), speed `dSpd`; attacker defence `aDef` (0x2f), agility `aAgi`, speed `aSpd`.
- Then:
```
engaged = min(D, A × COMBAT_MULTIPLANE_CAP (3))
dmg     = engaged × CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER (0.5 in 1.19.3) × dAtk
agiRed  = aAgi > dAgi ? (min(aAgi/dAgi, BIGGEST_AGILITY_FACTOR_DIFF 4) − 1) × dmg × COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION 0.45 : 0   // 0xf77630
spdInc  = dSpd > aSpd > 0 ? (min(dSpd/aSpd, BIGGEST_SPEED_FACTOR_DIFF 3.5) − 1) × dmg × 0.65 + topSpeed term(TOP_SPEED_DAMAGE_BONUS_FACTOR) : 0  // 0xf7ad60
x       = max(0.001, (dmg + spdInc − agiRed) × 0.01 / aDef)    // 0x197b724..0x197b76f
losses  = floor(x) + (frac(x) > U ? 1 : 0)                      // U drawn before (0x197b686)
```
- Losses are removed from the attacking wings (`0x197a1c0`, type 1) and logged.
- Defenders lose nothing. There is no escort and no "disrupted" state: interception only kills.
- `COMBAT_DAMAGE_SCALE_CARRIER` and `DISRUPTION_FACTOR_CARRIER` are not read in this module (their readers 0xf862a0/0xf7be70 are in the generic air-combat code).

**Verdict.** NEW: the formula was missing from the spec; this is it. The spec's outline (normal air-combat math with COMBAT_DAMAGE_STATS_MULTILPIER) is wrong for naval air combat. The 0.01 factor and division by bomber defence are hard-coded (`imul 0x3e8`).
**Confidence.** High for the structure and the defines. Medium for the stat-index names (0x2f defence, 0x30 attack, 0x31 agility, `0xf60760` speed) and the top-speed term of the speed helper (that helper is shared with land air combat and I decoded it only partly).

## A-TARGET: target selection

**What the game does** (`0x197a700`). For each member of the enemy group, the weight is:
```
skip (weight 0) if !(alive & 0xb) or member+0x12c == 0          // hidden submarine
S = enemySide.screeningEff (+0x148, from the first member's side)
t(TYPE) = BASE_TYPE × S + SCALE_TYPE × (1 − S)                  // e.g. carrier: 10·S + 200·(1−S)
warship: w = max(maxHP, 0.001)
          × t(SUB)     if submarine (0x1973d30)
          × t(SCREEN)  if screen
          × t(CAPITAL) if capital
          × t(CARRIER) if carrier (c3af20)
          × (1 + NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE × (1 − HP/maxHP))
          + NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE × clamp(5 − shipAA, 0, 5)      // additive
convoy:  w = convoyBase(0x1ad7f30) × t(CONVOY) × (1 + STRENGTH × (1 − hpRatio)) + LOW_AA × clamp(5 − convoyAA, 0, 5)
```
`shipAA` is `0xc3a470(ship, side+0x130)`, the AA stat in the battle context. The pick is a weighted random draw (`0x24f08a0` with a random int, line 0x43f).

**Verdict.** DIFFERS from both candidates. `typeScore = BASE·S + SCALE·(1 − S)` (an interpolation, not `BASE + SCALE·(1 − S)`). One S for all types (ordinary screening efficiency of the target side). Max HP still multiplies. The low-AA term is **additive** and linear, `5 × max(0, 5 − AA)` capped at 25; at real weights (maxHP × 10..200) it is negligible. The wiki's `5 × (5 − AA)` multiplier is wrong. `NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE` is read at 0x197abc9 (who.py listed only a false positive). Hidden submarines are excluded (confirmed).
**Confidence.** High.

## A-AA-PREEMPT: preemptive shoot-down

**What the game does** (`0x197c8b0`, args: airfx, target, agility, aaPlanes):
```
T      = ANTI_AIR_TARGETING (0.9)
agi    = average naval-strike agility (0.02 × agility × (1+mods) × (1+naval_carrier_air_agility_factor if carrier))
chance = ANTI_AIR_TARGETTING_TO_CHANCE × max(T + 0.5 × (T − agi), 0.01)     // 0x197c9c2..0x197ca18
if T <= 0 or targetAA <= 0: no AA
if U > chance (U < chance is a success; equal also succeeds): no AA          // line 0x212
frac   = clamp(targetAA × ANTI_AIR_ATTACK_TO_AMOUNT, 0, 1)
kills  = floor(aaPlanes × frac) + (U2 < frac-remainder ? 1 : 0)              // line 0x21c
kamikaze wing present (wing flags bit 0x80): kills = max(1, kills) × AIR_NAVAL_KAMIKAZE_LOSSES_MULT
```
`aaPlanes = ceil((flying − intercepted) × F)` (A-STACK). `targetAA` = AA of the target ship only, in the battle context (`side+0x130`); convoys use `0x1ad8060`. Kills come off the wings (`0x197a1c0`, type 2, or 6 for kamikaze).

**Verdict.** DIFFERS: the chance is `0.2 × max(1.5·T − 0.5·agi, 0.01)`, not `0.2 × max(T − agi, 0.01)`. At agi 0 that is 27 %; at agility 60 (agi 1.2) it is 15 %. Only the target fires (confirmed). The plane count is the stacking-scaled survivors, not the wing. Random rounding is confirmed. Positioning does not appear here (the AA stat may already contain it; see A-AA-REDUCE).
**Confidence.** High.

## A-AA-REDUCE: fleet AA damage reduction

**What the game does** (`0x196fca0`; fleet term from `0x161e400` / `0x161e4d9`):
```
side.fleetAA(+0x170) = Σ_allGroups Σ_warships AA(ship, noContext) × SHIP_TO_FLEET_ANTI_AIR_RATIO × side.positioning(+0x158)
pool      = side.fleetAA + (target is warship ? AA(target, noContext) : 0)
reduction = pool > 0 ? clamp(ANTI_AIR_MULT × pool^ANTI_AIR_POW, 0, MAX_ANTI_AIR_REDUCTION) : 0
```
The side update recomputes it each hour.

**Verdict.** MATCHES the form, with two differences. The fleet term is also **multiplied by the side's positioning**. Convoys add nothing to the side total (only `0x1622d40` warships are summed) and nothing as the target. The side is all ships in all groups of that side; the target is counted once more inside the total.
**Confidence.** High.

## A-HIT: how many planes hit

**What the game does** (`0x197ac80`, 0x197ad4e..0x197ade7):
```
strikers  = flying − intercepted − aaKills            // must be > 0
tMult     = 1 + naval_strike(weather holder) + (1 + airfx[+0xe8]) × naval_strike(night holder)   // 0x197bf38..0x197bfb9
hitFrac   = clamp(avgTargeting × tMult × NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1)
hits      = round(strikers × hitFrac × U)             // U uniform [0,1), line 0x45d
if hits <= 0: no damage
```
`avgTargeting = Σ(targeting_i × n_i)/Σ n_i` over the first nWings strike wings, and already contains the `× 0.1` of the stat getter. With a carrier present it is also × (1 + naval_carrier_air_targetting_factor). `airfx+0xe8` is the plane-weighted average of an equipment field +0x260 (`0x197d646`); its name is UNRESOLVED and the vanilla night modifier has no `naval_strike`.

**Verdict.** DIFFERS:
1. **The hit count is multiplied by a uniform roll U**, so on average hits are **half** of `planes × hitFrac`.
2. Weather `naval_strike` (rain −0.2, heavy rain/blizzard −0.4, snow −0.3) multiplies targeting.
3. Strikers include fighters, because `flying` counts all carrier planes.

The `/10` of the spec is right (the stat getter's `× 0.1`). Target speed, visibility and armor do not matter (confirmed).
**Confidence.** High.

## A-DMG: strike damage

**What the game does** (`0x1978e30` stats; `0x197bfcf..0x197c017`; `0x197ac80` 0x197adfa..0x197ae82):
```
per strike wing i (first nWings): n_i = round(planes_i × airfx.planes / Σ planes_sel)
                                  e_i = 0xf78160 efficiency (1.0 unless out of fuel; planes lost are reported)
avgAttack = Σ(attack_i × n_i × e_i) / Σ n_i               // attack_i = 0.1 × naval_strike_attack × (1+mods)
if carrier present: avgAttack ×= (1 + naval_carrier_air_attack_factor(country))
if external:        avgAttack ×= airfx[+0xc0]
atk = avgAttack × (carrier member alive in this group ? NAVAL_STRIKE_CARRIER_MULTIPLIER (12) : 1)
base = hits × atk × (1 − reduction)
strDmg = base × NAVAL_STRIKE_DAMAGE_TO_STR (1.0)
orgDmg = base × NAVAL_STRIKE_DAMAGE_TO_ORG (1.5)
crit step (A-CRIT), then 0xc3e0e0(ship, strDmg, orgDmg) (convoy: 0x1ad8610)
```
- No damage randomness factor. No `(1 − hpRatio)` org scaling: unlike guns, full-HP ships take the full org damage.
- Sinking and kill credit are as for guns (`0xc3e680`, `0x1975480`), and the damage is recorded (`0x196fa40`, `0x15c6250`).
- `NAVAL_STRIKE_CARRIER_MULTIPLIER` is 12 in 1.19.3. "Carrier present" means the AirFX's carrier ship is a live member of the same fire-exchange group (`0x1c69dc0`).

**Verdict.** MATCHES the main formula. NEW details:
- **The `× 0.1` on naval attack does exist** (in the plane stat getter `0xf60f10`, `imul 0x2710`). The spec's plan to drop the old `× 0.1` would be wrong if the engine uses the designer stat.
- Attack is the per-plane average (efficiency-weighted).
- Org damage is not scaled by HP lost, and there is no randomness. The randomness lives in the hit count.
- The multiplier needs the carrier in the same group, not merely in the battle.

**Confidence.** High.

## A-CRIT: crits from air

**What the game does** (0x197ae86..0x197b06c):
```
rel     = min(target reliability (+0x328; convoy 0x1ad8180), 1)
chance  = COMBAT_BASE_CRITICAL_CHANCE × (1 + COMBAT_ARMOR_PIERCING_CRITICAL_BONUS) × (1 − rel)
chance ×= max(0, 1 − screeningValue(target, NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR 0x18c))
if chance > 0: chance = max(chance, 0.001)
crit if U < chance
mult    = 1 + (1 − rel) × COMBAT_CRITICAL_DAMAGE_MULT / hits
if crit and mult > 1:
    if part damage (0x1975150 with CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR) succeeds: no multiplier
    else strDmg ×= mult, orgDmg ×= mult
```
With vanilla values: chance = 0.1 × (1 − rel), e.g. 2 % at reliability 0.8. On a crit, one hit's worth is multiplied by `1 + 5(1 − rel)`, so the strike total rises by `5(1 − rel)/hits`.

**Verdict.** DIFFERS: air does not use the torpedo constants. It uses the gun crit defines, with the piercing bonus always on. Likely bug: the receive-chance modifier is applied as `(1 − v)`, so `critical_receive_chance = −0.5` (pride of the fleet) **raises** air crit chance ×1.5. The part chance uses the `_FROM_AIR` define (0.1).
**Confidence.** High for the formula. Medium for the `screeningValue` sum for 0x18c (the same helper as A-SORTIE; it includes the ship's own modifier).

## A-LOSSES

**What the game does.**
- Planes are lost to interception (`0x197a1c0` type 1) and to preemptive AA (type 2; kamikaze type 6 with the ×4 multiplier). `0xf78160` reports fuel-related non-flying planes (`0xc5c5c0`).
- Defenders take no losses in naval interception.
- AirFX objects persist for the battle, and their plane count is recomputed from the wings each step.
- After each strike every strike wing gets `0xf80f60(losses, damage)` (experience and stats; not decoded).

**Verdict.** MATCHES the outline. NEW: the loss types above, and the fact that defenders take no losses.
**Confidence.** Medium.

---

## Recommended engine changes (for the milestone-2 air phase)

1. **Cadence.** Run the air phase in hours where `hour % (2 × HOURS_DELAY_AFTER_EACH_COMBAT) == regionPhase`. `regionPhase` is a scenario input (strategic region id mod 8; default 4, which gives the wiki's 04/12/20). It applies to carriers and land-based groups alike, from hour 0. Ignore `CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT` (keep it as a `BattleRules` variant "CarrierCooldownEnforced", default off).
2. **Sortie.** `eff = clamp(max(0, base × crowd × weather × night) × org/maxOrg, 0, 1)`.
   - `base` = stance ratio (attacker) or 1 − ratio (defender) for the first 24 h, then 0.5. Add the carrier's `sortie_efficiency` modifiers, `+0.1 × capitalScreeningEff` in the carrier line, and `+ fighter_sortie_efficiency` if the first wing is a fighter.
   - `crowd = r > 0.9 ? 0 : clamp(1 − 2(1 + capPenRed) r, 0, 1)`.
   - `weather = clamp(1 + carrier_traffic_weather, 0, 1)`; `night = clamp(1 − darkness + carrier_night_traffic·[dark], 0, 1)`.
   - `flying = round(allCarrierPlanes × eff)`.
3. **Stacking.** `cap = max(80, Σ silhouette)` with `screens ceil(5n / (1 + 0.2·caps))`, support 3. Use `F = 1/(1 + 0.005·excess)` over own carrier planes only. Apply F only to `nWings` and to the AA base. Keep a variant if calibration disagrees.
4. **Target.** Weight `maxHP × Π(BASE·S + SCALE·(1 − S)) × (1 + 5·(1 − hp)) + 5·clamp(5 − AA, 0, 5)`, where S is the target side's screening efficiency. Exclude hidden subs. Draw by weighted random.
5. **Interception.** Implement the formula in A-INTERCEPT. Inputs: enemy carrier fighters in the target's group (planes, air attack, agility, speed), defShare from the target fleet's stance, and attacker strike wings (planes, defence, agility, speed).
6. **Preemptive AA.** `chance = 0.2 × max(1.35 − 0.5·agi, 0.01)`. Kills = random-rounded `ceil((flying − intercepted)·F) × clamp(0.01·AA, 0, 1)`.
7. **Hits.** `hits = round(strikers × clamp(avgTgt·(1 + naval_strike_weather)·0.3, 0, 1) × U)`, with `avgTgt = 0.1 × designer targeting × (1 + mods)`. Make the U factor explicit in `BattleRules` (expected-value mode = 0.5).
8. **Damage.** `base = hits × 0.1 × designerAttack × (1 + mods) × (12 if own carrier alive in the same group) × (1 − AAred)`. HP −= base, org −= 1.5·base. No HP-scaled org, no randomness.
   - `AAred = min(0.75, 0.18 × (shipAA + 0.25 × positioning × ΣsideWarshipAA)^0.225)`.
9. **Crit.** `chance = 0.1(1 − rel) × max(0, 1 − critReceiveMod)`. `mult = 1 + 5(1 − rel)/hits`. The part-damage chance `CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR / rel` replaces the multiplier.
10. **External.** Cumulative join cap `max(20, ΣenemyWarshipHP × 0.05 × (1 + 0.2·floor(h/24)))`. External groups strike at every air hour, with mission efficiency as an input multiplier on attack.
11. **Defines to add:** NAVAL_COMBAT_AIR_* target BASE/SCALE/STRENGTH/LOW_AA, SHIP_SILHOUETTE_VALUE_PLANES_SUPPORT, CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER (0.5 in 1.19.3), CARRIER_PERCENTAGE_DEFEND, COMBAT_MULTIPLANE_CAP, BIGGEST_AGILITY/SPEED_FACTOR_DIFF, COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION, COMBAT_BETTER_SPEED_DAMAGE_INCREASE, TOP_SPEED_DAMAGE_BONUS_FACTOR, AIR_WING_MAX_STATS_SPEED. NAVAL_STRIKE_CARRIER_MULTIPLIER is 12 in 1.19.3.

## Open questions / UNRESOLVED

- `combat[0x120]` (air every hour): I found no setter. `region[0x60]` = region id (medium confidence).
- Name of `airfx+0xe8` (equipment field +0x260) in the night targeting term. The vanilla effect is 0.
- The top-speed term inside `0xf7ad60` (constant ÷1e7 path); stat indices 0x2f/0x30 (defence/attack).
- `0xf78160` (assumed fuel); `airfx+0xc0` external efficiency source; caller and 5th argument of `0x16205c0`.
- Port-strike branch (`combat[0x10c]`, PORT_STRIKE_DAMAGE_FACTOR: `clamp(strikers × avgAttack × factor, 0, 100)` to the port) is not decoded beyond that.
