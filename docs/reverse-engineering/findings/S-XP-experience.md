# Findings: ship experience (S-XP) and in-combat manpower (S-MANPOWER), HOI4 1.19.3

All addresses are RVAs in hoi4.exe 1.19.3. "fx15" = Clausewitz `CFixedPoint` (int64, scale 2^15 = 32768).
"fx5" = `CFixedPoint64` (int64, scale 100000, how most defines are stored). Conversions: `0x24ed3f0`
(fx5 -> fx15), `0x24ed580` (fx15 -> fx5). Rounding to int: `0x24ed730` (round half away from zero).

Toolkit note: `hoi4re.code_xrefs` gives false negatives. Its `_verify` disassembles from `p-12`,
which can start decoding in the middle of an instruction. Because of that, `who.py` reported "0 uses" for
`EXPERIENCE_FACTOR_NON_CARRIER_GAIN`, `DAILY_MANPOWER_GAIN_RATIO`, `MANPOWER_LOSS_RATIO_ON_STR_LOSS` and
`MIN_MANPOWER_RATIO_TO_DROP`, but all four have real readers. `scratch_xp_xrefs.py` (decodes from the
start of the function) and `scratch_xp_calls.py` (scans E8/E9 rel32 calls) in this folder are reliable
replacements.

Define storage, a new fact: defines whose reader is `0x2072200` (27 of them, including
`EXPERIENCE_FACTOR_CONVOY_ATTACK`, `EXPERIENCE_FACTOR_NON_CARRIER_GAIN`, `EXPERIENCE_FACTOR_CARRIER_GAIN`,
`NNavy.FIELD_EXPERIENCE_SCALE`) are stored as **fx15, truncated**: `(int64)(double)(f32 * 32768.0f)`
(`0x2072319` mulss by 32768.0, then cvttsd2si). So 0.04 is stored as 1310 (= 0.0399780) and 0.08 as 2621
(= 0.0799866). Every product in the XP code is `imul` followed by `sar 15`, which is floor division by 2^15.

---

## S-XP-LEVELS: experience levels of ships

**What the game does.**
- XP is stored on the ship at `ship+0x708`. The cap is at `ship+0x710`, set to `NNavy.NAVY_MAX_XP` (100)
  in the ship constructor (`0xc2ffe8`).
- `level(ship)` (`0xc36390` -> `0xbf9b90`): `ratio = max(xp,0) * 100000 / maxXP` (fx5, truncating idiv);
  `level = count(i : NMilitary.UNIT_EXP_LEVELS[i] <= ratio)`.
- Ships use the **army** array `NMilitary.UNIT_EXP_LEVELS = {0.1, 0.3, 0.75, 0.9}`. That gives
  **5 levels (0..4)**: L0 xp < 10, L1 10–30, L2 30–75, L3 75–90, L4 >= 90 (with NAVY_MAX_XP = 100).
  There is no navy-specific threshold array.

**Evidence.** `0xc2ffe8` (NAVY_MAX_XP -> ship+0x710), `0xc36390`, `0xbf9b90` (loop over the
`UNIT_EXP_LEVELS` vector at `data:3339500`, element count at `data:333950c`), `0xc339d0` / `0xbf9d90`
(progress within the level, UI only).

**Verdict.** DIFFERS. The spec/engine use 7 [LEGACY] thresholds `[0.1,0.2,0.3,0.4,0.55,0.7,0.9]`. The
game uses 4 thresholds and 5 levels, from `NMilitary.UNIT_EXP_LEVELS`. **Confidence: high.**

## S-XP-EFFECT: what each level does in combat

**What the game does** (`0xc3ce50`, recomputes the ship's experience modifiers whenever the dirty flag
`ship+0x800` is set):
```
base     = NMilitary.ARMY_EXP_BASE_LEVEL      (int, vanilla 1)
maxLevel = len(NMilitary.UNIT_EXP_LEVELS)     (4)
clear ship experience modifier list (ship+0x750)
if level < base:  s = (base - level) / base               -> add ship_experience_malus_min x s
                  if carrier: add carrier_experience_malus_min x s
if level > base:  s = (level - base) / (maxLevel - base)  -> add ship_experience_bonus_max x s
                  if carrier: add carrier_experience_bonus_max x s
(level == base: no modifier)
```
`s` is fx5 with truncating division (1/3 = 0.33333). The modifier is added scaled by `s` (`0x557aa0`,
multiplier `s * 100000 / 100000`). "carrier" is `0xc3af20` (ship flag bits 22 or 37, the same predicate
used for carrier XP gain).

The modifier blocks are game script, `common/modifiers/00_static_modifiers.txt`, loaded by name at
`0x60ec2b..` into static-modifier slots +0xd8/+0xe0/+0xe8/+0xf0:
```
ship_experience_bonus_max    = { naval_damage_factor = 0.3   naval_defense_factor = 0.2 }
ship_experience_malus_min    = { naval_damage_factor = -0.1  naval_defense_factor = -0.1 }
carrier_experience_bonus_max = { fighter_sortie_efficiency = 0.2 }
carrier_experience_malus_min = { fighter_sortie_efficiency = -0.1 }
```
Resulting vanilla table:

| Level | XP (of 100) | naval_damage_factor | naval_defense_factor | carrier fighter_sortie_efficiency |
|---|---|---|---|---|
| 0 | < 10 | -0.10 | -0.10 | -0.10 |
| 1 | 10–30 | 0 | 0 | 0 |
| 2 | 30–75 | +0.10 (0.3 x 0.33333) | +0.0667 | +0.0667 |
| 3 | 75–90 | +0.20 (0.3 x 0.66666) | +0.1333 | +0.1333 |
| 4 | >= 90 | +0.30 | +0.20 | +0.20 |

- No other naval-combat code reads ship XP or level. The readers of `c36390` are tooltips and script
  triggers (`0x1aa8f60..0x1ab5598`, `0x2014abf`); none of the naval module ranges reads `+0x708`. So
  experience acts **only** through these three modifiers: no direct hit-chance term, no direct damage term.
- When it updates: `c3ce50` runs at the end of `c3bde0`, the per-ship step of the task-force update,
  whenever the dirty flag is set. The flag is set by XP gain (`c31840`) and by XP loss (`c3e0e0`). So the
  level effect changes at that update tick, not the moment the threshold is crossed.

**Evidence.** `0xc3ce50`, `0xc3ce75` (ARMY_EXP_BASE_LEVEL), `0xc3ce8e` (vector size), `0x24ef6f0` (negate),
`0x557aa0`, `0x60ec2b`/`0x60ec5e` (static modifier names).

**Verdict.** DIFFERS. The spec has a damage factor of -10% .. +15% in 7 equal steps [LEGACY]. The game:
-10% / 0 / +10% / +20% / +30% `naval_damage_factor`, plus `naval_defense_factor` -10% .. +20%, plus the
carrier sortie modifier. Not traced here (it belongs to the damage area): how `naval_damage_factor` and
`naval_defense_factor` enter the per-shot damage, i.e. whether they are additive with other sources of the
same modifier, and whether defense divides or multiplies damage taken. **Confidence: high** for levels,
scaling and values. The combat use of naval_defense_factor is UNRESOLVED here.

## S-XP-GAIN: experience gain in combat

**Call chain.** The task-force update `0xd705f0` (CTaskForce vtable slot 18, `0x2963000`) loops over the
task force's ships (`tf+0x348`, count `tf+0x354`) and calls `0xc3bde0(ship, tf, &sum)`. That function:
- runs only if the task force is in a combat (`0xc00000`: `tf+0x1b4 > 0`), then
- calls `gain = 0xc338a0(ship, tf)`, then
- calls `0xc31840(ship, gain, mode=0 /*combat*/)`, which applies the modifiers and adds the XP.

Cadence: the same function also does task-force movement (`!_CurrentPath.IsEmpty()`), and the
national-XP part has a per-day cap (`FIELD_EXPERIENCE_MAX_PER_DAY`, `0x12a6740`, accumulator at +0x30).
Both point to an **hourly** tick, matching the define name. I did not find the vtable dispatcher, so the
cadence is medium confidence.

**Formula** (`0xc338a0` + `0x15c3320`, all fx15, truncating):
```
combat = first combat of the task force (tf+0x1a8 [0])
base   = UNIT_EXPERIENCE_PER_COMBAT_HOUR (10) * UNIT_EXPERIENCE_SCALE (1)          // NNavy, fx5 -> fx15
member = the ship's fire-exchange member: searched in all groups of combat side +0x98, then side +0xa0
if no member: factor = 0
elif member.lastShot.present:                       // member+0x138..0x148, see below
    factor = (lastShot.targetGroupSize + 1) / (ownGroupSize + 1)
    if lastShot.targetIsConvoy: factor *= EXPERIENCE_FACTOR_CONVOY_ATTACK (0.04)
    factor *= EXPERIENCE_FACTOR_NON_CARRIER_GAIN (0.04)
elif isCarrier(ship):                               // 0xc3af20
    e = mean over this carrier's air entries in its group (group+0x58, count +0x64,
        matched by entry owner (0x1976ae0) == ship+0x730) of carrierEfficiency(entry)   // 0x19786c0
        (0 if it has no entries)
    factor = e * EXPERIENCE_FACTOR_CARRIER_GAIN (0.08)
else: factor = 0
gain = base * factor
```
- `lastShot` (`member+0x138`, an optional, `+0x148` = present): it is cleared at the start of the member's
  hourly fire step (`0x1973470`: `[+0x148] = 0`). It is written on **every shot** in `0x1973eea`
  (just before `ApplyDamage 0x19740c0`):
  - `+0x140` = target member is a convoy (`target+0x10` reference resolves);
  - `+0x144` = size of the **target's** group at the time of the shot (`[target.group+0x14]`).

  So a ship gains non-carrier XP in an hour only if it fired at least one shot in that combat hour. The
  enemy count is taken from the last shot of the hour, and the own count is read when XP is computed.
- Group size `[group+0x14]` is the member count of the fire-exchange group. `NNavy.COMBAT_MAX_GROUPS = 1`:
  `0x161e6d0` creates a new group only while the side has fewer than that many, and otherwise merges. So
  in vanilla each side has exactly one group, and the ratio is
  **(enemy ships in combat + 1) / (own ships in combat + 1)**. Members include every ship the side brought
  in (screens, capitals, carriers, subs, and convoys on a convoy side). I did not verify whether sunk or
  escaped ships are removed from the group before the count is read.
- A carrier that fired a gun this hour takes the **non-carrier** branch. A carrier that did not fire gets
  the carrier branch, which depends only on its air wings' sortie efficiency in the combat
  (`CARRIER_EFFICIENCY` tooltip function `0x19786c0`, defined in A-SORTIE), not on damage.
- Damage dealt plays **no** part in ship XP.

Vanilla numbers: `gain/hour = 10 * 0.039978 * floorfx15((Nt+1)/(No+1)) = 0.39978 * ratio`. Examples:
10 vs 10 ships: 0.39978 XP/h, about 25 combat hours from 0 to level 1. 5 ships vs 20: 1.6 XP/h.
Convoy raiding (target is a convoy): `0.39978 * 0.039978 * ratio = 0.016 * ratio`.

**Modifiers and adding** (`0xc31840` -> `0xc3b240`, mode 0 = combat):
```
flat   = sum of modifier id 0x35 (country modifiers at country+0x5b8 + the ship's captain/leader at ship+0x8f8,
         + a global block via 0x55b160 in one branch)
factor = sum of modifier id 0x34 (same sources) + country modifier [ship+0x66c]
         (the ship type's "experience_gain_<type>_combat_factor", "Unit experience gain factor in combat")
gain'  = (gain + flat) * (1 + factor)
xp     = min(xp + gain', NAVY_MAX_XP); dirty = 1
```
The ids 0x34 / 0x35 are most likely `experience_gain_navy_unit_factor` / `experience_gain_navy_unit`
(the factor is the one added to 1.0). I could not confirm the id-to-name mapping (low-medium confidence).
`pride_of_the_fleet` gives `experience_gain_navy_unit_factor = 0.25`.

Side effects (not ship combat, listed for completeness): the captain gets `gain * LEADER_EXPERIENCE_SCALE`
(`0xc3be74` -> `0xc0f200`). National navy XP gets `gain * NNavy.FIELD_EXPERIENCE_SCALE` (0.075) times the
equipment-share ratio (`0xc3bee3` -> `0x12a6740`, capped by `FIELD_EXPERIENCE_MAX_PER_DAY` = 50). Naval HQ
gets `NAVAL_HEADQUARTERS_EXPERIENCE_SCALE`. `NNavy.XP_GAIN_FACTOR` is read only at `0xc1133e`, in unrelated
code, and is **not** part of ship combat XP.

**Verdict.** DIFFERS from the spec rule "each ship that fired gains PER_HOUR x SCALE x NON_CARRIER x
enemy/own". The differences:
- (a) the ratio is `(enemy+1)/(own+1)`, not `enemy/own`, and the enemy count is the target's group at the
  last shot;
- (b) a convoy target adds a further x0.04;
- (c) carriers that did not fire gain `mean sortie efficiency x 0.08 x base`;
- (d) the experience-gain modifiers apply;
- (e) fx15 truncation (0.04 becomes 0.039978).

The "fired this hour" condition MATCHES. **Confidence:** high for the formula and the terms; medium for the
hourly cadence and for the order relative to the fire phase (if the task-force update runs before the
combat's hourly step, the record used is from the previous hour's firing).

## S-XP-LOSS: experience loss when damaged

**What the game does** (`0xc3e0e0`, called from ApplyDamage at `0x19746d4` with flag = 0):
```
hpBefore = ship+0x6f8;  maxHp = max(ship+0x308, 0.001)
xp *= clamp(hpBefore - hpDamage * NNavy.EXPERIENCE_LOSS_FACTOR (1.0), 0, maxHp) / hpBefore   // 1 if hpBefore <= 0
```
So experience falls by the **fraction of current HP lost**. It does not depend on manpower.

Manpower loss in the same function:
```
frac  = hpDamage / maxHp * (1 + shipModifier[0x1a0])          // c38760, modifier name not resolved
floor = round(maxManpower * MIN_MANPOWER_RATIO_TO_DROP (0.1))
if curManpower > floor:
    loss = round(maxManpower * frac * MANPOWER_LOSS_RATIO_ON_STR_LOSS (0.5))
    curManpower -= min(loss, curManpower - floor)
```
Then `HP -= hpDamage` (clamped to [0, maxHp]) and `org -= orgDamage` (clamped).

**Verdict.** DIFFERS. The spec reduces XP by `lostManpowerShare * EXPERIENCE_LOSS_FACTOR`. The game uses the
HP fraction lost: `xp *= 1 - ELF * dmg / hpBefore`. The manpower formula MATCHES in shape: loss relative to
**max** manpower and max HP, rounded half away from zero, never below the floor. There is an extra
`(1 + modifier 0x1a0)` factor, probably a navy casualty modifier, UNRESOLVED. **Confidence: high**
(modifier name low).

## S-MANPOWER: manpower recovery during battle

**Corrected 2026-10-19.** The first pass read `gamestate+0xa39` as a setup/loading flag and concluded that ships
never regain manpower in combat. Both were wrong: the flag is `HasGameStarted()`, and the daily task-force update
refills ships whether or not they are fighting. The recorded CL3 battle (calibration.md) showed it first: the save
written in the battle's last hour had 74 of 75 and 71 of 74 ships at full manpower after heavy HP losses.

**What the game does.**
- **The refill.** The only gameplay reader of `NNavy.DAILY_MANPOWER_GAIN_RATIO` is the refill branch of the ship
  manpower update `0xc3e4e0(ship, refill)` (`0xc3e5be`). It does the following:
  - recomputes max manpower (`ship+0x80c`) from the equipment;
  - if `refill`: `deficit = max - cur`, then `gain = min(deficit, ceil(max * DAILY_MANPOWER_GAIN_RATIO))`. The
    product is taken in fx5 (`max * 100000 * ratio / 100000`), and rounded up when it has a fractional part
    (0xc3e5b4..0xc3e62c).
  - The gain is drawn from the national pool (`0xcfe7e0`, which returns the shortfall); a surplus goes back to
    the pool (`0xcfe1b0`).
- **Who refills.** `refill = true` is reached only through `0xd7a7e0(taskforce, force)`:
  `if (HasGameStarted() || force) for each ship: 0xc3e4e0(ship, true)`.
- **The flag.** `HasGameStarted()` is `gamestate+0xa39`, as the assert at 0x1de23e..0x1de295 names it:
  `!CCurrentGameState::GetInstance()->HasGameStarted() || _AllPlaythroughData.Contains( Tag )`, guarded by
  `cmp byte [gamestate+0xa39], 0`.
  - The setter `0x1edff0(gamestate, value)` writes its second argument: `mov [rcx+0xa39], dl`.
  - It is set to 1 only at "on_startup" in `ingameidler.cpp` (`0xdd9b20`, `mov dl, 1` at 0xdd9c48), just before
    the `on_startup` on_action fires.
  - It is cleared (`xor edx, edx`) by "Resetting game" `0x1a5630`, "LOADING_SAVEGAME" `0xdda5d0`, the main
    menu / exit to frontend `0xdd3a50`, `0xb3d9b0` and `0xdcb3f0`, and by gamestate init `0x1e0610`
    (0x1e063a).
  - So it is 1 for the whole of a running game.
- **When.** `CTaskForce` vtable `0x2962f70`, slot 19 (`+0x98`) = `0xd69090`, the task force's daily update. Its
  first instruction calls `0xd7a7e0(tf, false)`. The in-combat test `0xc00000` comes only after it and gates
  other work. The daily chain:
  - the gamestate tick `0x1dd370` advances the date by one hour (0x1dd4ac..0x1dd67a);
  - it then runs the hourly update `0x1df400`, which calls the combat manager's hourly update `0xbb8ae0`
    (0x1e0103);
  - then, only if the day changed (`cmp [rsp+0x58], r13d` at 0x1de3f5), it runs `gamestate.daily` `0x1d4810`
    (0x1de479);
  - `gamestate.daily` runs `country.daily` `0x6e76a0` for every country (0x1d59eb);
  - `country.daily` contains `fleets.daily`, which calls `0xd54840` for each fleet (country `+0x278`, count
    `+0x284`);
  - `0xd54840` calls vtable `+0x98` on each task force of the fleet (fleet `+0xb8`, count `+0xc4`).
  - The same function's `army.daily` and `railway_guns.daily` loops call slot 19 on armies and railway guns.
    `NRailwayGun.DAILY_MANPOWER_GAIN_RATIO`'s comment says "daily".
- **No other path.** Other writers of ship manpower `+0x808`: the constructor (`0xc30060`, -1), the damage loss
  `0xc3e40f`, `CNavalDeployment::CreateUnit` (`0xd0fcd7`, a new ship's crew), and the sinking code `0xd783b0`.
  None of them refill.

So every day, after the combat hour of the 00:00 tick, every ship of every country regains
`min(deficit, ceil(max * DAILY_MANPOWER_GAIN_RATIO))`, in battle or not. With 1200-crew ships and the vanilla
0.05, that is 60 a day.

**Verdict.** DIFFERED: the engine never refilled. It now applies the daily gain after each battle hour at 00:00
(`ExperienceRules.DailyManpowerGain`). Calibration agrees: over 200 seeds of the CL3 battle, 74.5 of 75 and 70.1
of 73.3 ships end at full manpower, against 74/75 and 71/74 in the save. **Confidence:** high on the chain, the
flag and the formula. The day boundary (the hour whose clock reads 00:00) follows from the day comparison in the
tick, not from a decoded date format.

---

## Recommended engine changes

1. **Levels**: replace `HardcodedValues.ShipExperienceLevels` with the defines `NMilitary.UNIT_EXP_LEVELS`
   `{0.1, 0.3, 0.75, 0.9}` and `NMilitary.ARMY_EXP_BASE_LEVEL` (1) (add both to the defines classes).
   `level = count(t <= xp/NAVY_MAX_XP)`, with the ratio truncated to 1e-5.
2. **Level effects**: replace `DamageFactor` with modifiers. With `s = (level-base)/(maxLevel-base)` for
   level > base, or `-(base-level)/base` for level < base, using the malus block:
   - `naval_damage_factor += 0.3*s` (bonus) or `-0.1*|s|` (malus);
   - `naval_defense_factor += 0.2*s` or `-0.1*|s|`;
   - carriers: `fighter_sortie_efficiency += 0.2*s` or `-0.1*|s|`.

   Read the four blocks from `common/modifiers/00_static_modifiers.txt` (milestone 4), with these vanilla
   values as defaults. Route them through the same modifier channel as research and spirit
   `naval_damage_factor` / `naval_defense_factor`, so the damage area decides how they stack. Recompute
   the level effect once per hour after the XP update, not per shot.
3. **Gain** (hourly, per ship, after the fire phase):
   `base = UNIT_EXPERIENCE_PER_COMBAT_HOUR * UNIT_EXPERIENCE_SCALE`.
   - If the ship fired this hour:
     `factor = (enemyShipsInBattle + 1) / (ownShipsInBattle + 1) * NON_CARRIER_GAIN`, times
     `CONVOY_ATTACK` if its last target this hour was a convoy.
   - Else if it is a carrier: `factor = mean sortie efficiency of its wings * CARRIER_GAIN` (0 without
     wings).
   - Else 0.

   Then `xp = min(NAVY_MAX_XP, xp + (base*factor + flatMod) * (1 + gainFactorMods))`. Take the enemy
   count at the ship's last shot of the hour. Optionally emulate fx15: store the three factors as
   `floor(v*32768)/32768`.
4. **Loss**: `xp *= max(0, hpBefore - hpDamage*NNavy.EXPERIENCE_LOSS_FACTOR) / hpBefore`, independent of
   manpower. Keep the manpower loss as it is: relative to max manpower, rounded, floor at
   `MIN_MANPOWER_RATIO_TO_DROP`. Note the unresolved `(1 + casualty modifier)` term.
5. **S-MANPOWER** (corrected): refill every ship afloat once a day, in combat too, after the combat hour at
   00:00: `min(deficit, ceil(max * DAILY_MANPOWER_GAIN_RATIO))`.
6. Spec: update S-XP (levels, effects, gain, loss) and S-MANPOWER with these IDs and addresses.

## Open / UNRESOLVED
- How `naval_damage_factor` and `naval_defense_factor` are applied per shot (damage area).
- The mapping of modifier ids 0x34/0x35 and 0x1a0 to names.
- Whether sunk or escaped ships are removed from the fire-exchange group before `[group+0x14]` is read.
- Exact hourly order of the task-force update (`0xd705f0`) relative to the combat hour, i.e. whether the
  XP uses this hour's shots or the previous hour's.
