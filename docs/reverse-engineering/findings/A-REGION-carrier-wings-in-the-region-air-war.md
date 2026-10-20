# A-REGION: carrier wings in the region air war during a naval battle

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-CADENCE, A-WINGS, A-DMG (the efficiency `e_i`), A-LOSSES, A-EXTERNAL (`docs/mechanics/air.md`);
  S-REPLAY (roll order)
- **Confidence:** read from code only (static analysis), checked against what tools/ingame logged in TFB carrier battles
  (planes lost between naval strikes; per-hit damage ×0.55–0.6 for a bomber-only side facing 180 carrier fighters, ×1.0
  in a fighter mirror). Per section below.

Method: `tools/re/` (`hoi4re.py`, `ctx.py`, `modmap.py`, `who.py`) plus throw-away disassembly and field-scan scripts
that label define globals from `defmap.json`. Modifier names from the registration table used by
[A-ACE](A-ACE-aces-and-wing-experience-in-naval-strikes.md). Source files: `strategicair.cpp` (0xc40000..0xc66ce1),
`airmission.cpp` (0xf75a00..0xf87550), `airwing.cpp`, `naval\naval_fire_exchange_air.cpp`. Conventions as in
[A-air-combat](A-air-combat.md): RVAs, fixed point ×1e5, every product and quotient truncated toward zero, `0x24ed730`
rounds half away from zero, `U` = `random_fixed` (0x2233db0) in {0, 0.00001, …, 0.99999}. `mission` = the air mission
object at `wing+0x90` (fields in [A-MISSION-SAVE](A-MISSION-SAVE-air-mission-save-format.md)). Defines are given as
vanilla / TFB.

## Summary

- Carrier wings of a task force in a naval combat take part in the **region air war** of the combat's strategic region,
  in that region's **air hours** only (the A-CADENCE clock, `hour % 8 == regionId % 8` with the 4 h define). The air war
  runs in the hourly strategic-air update **before** the naval combat hour, so the naval strike of the same hour sees
  the planes left after it and the disruption it caused.
- Each country in turn is the **attacker**: its air-superiority / interception carrier wings (and every mission it flies
  in the region) attack **every mission of the enemies that touches the region**, including the enemy's carrier naval
  bombers and fighters. Only the defender takes losses in a pairing; the attacker loses planes in the enemy's turn.
- Losses: `x = scale × dmg × 0.01 / defence`, random rounding, capped per mission; `scale` is
  `COMBAT_DAMAGE_SCALE_CARRIER` (5 / 5) when both sides are carrier wings in combat. Planes are removed at the end of
  the air update and are also added to the naval combat's air-object loss count.
- A non-fighter defender mission flying in that region also gets **disruption** (`DISRUPTION_FACTOR_CARRIER` 6 / 2
  between carrier wings), reduced by its own side's escorts (air-superiority wings, `ESCORT_*`). The result is the
  mission's `+0xa8`, which 0xf78160 turns into the naval strike efficiency `e_i` of A-DMG. **Only missions on the
  region mission list (`strategicAir+0x98`) get `+0xa8`**; a carrier wing that is only on the scrambled-carrier list
  (`+0x140`) keeps `e_i = 1`. Carrier wings that have a strategic region (the game sets it, see A-REGION-LISTS) are on
  both lists.

## A-REGION-ORDER: the hourly strategic-air update 0xc57d30

Called from the gamestate hourly update at 0x1dfda5, before the combat manager (0x1e0103). Every step below except 4 runs
only for strategic regions in their air hour.

```
ctx = { countries: active strategic-air objects (air manager +0x30),
        flag[r] = r has provinces (region+0x24) and 0xe24530(region) (air hour),   // 0x1962420
        regions: the flagged region ids }                                         // 0x1961fe0 / 0x1962300
1. job 0x2959f80 (body 0xc46030), per country: reset its region records, friendly / enemy lists 0xc64ee0
   (strategicAir+0x110 = self + allies, +0x128 = enemies; medium), scrambled carrier lists 0xc66480 (A-REGION-LISTS),
   then 0xf62a50 for every pool (the wing hourly update 0xf62750).
2. 0xc58660: executing_mission refresh (0xc49e80, A-MISSION-SAVE), and job 0x2959f00 (0xc46ea0 → 0xc4a2f0):
   for every mission on +0x98[r] and +0x140[r]: +0xa8 = +0xb0 = 0 (disruption reset); executing_mission == 4
   (interception) is re-chosen; region air superiority records (Σ effective_air_superiority +0xe8).
3. per country: not-ready wings 0xf62af0; empty pools removed.
4. 0xc5efc0 (every hour, all air bases): one random_int(strategicair.cpp, 0x1be8) seeds a local hash generator for the
   accident rolls 0xc60290 (A-REGION-ACCIDENT).
5. per country (ctx.countries order): 0xc5f4c0 = the air war with this country as attacker (A-REGION-WAR).
6. job 0x2959fa0 (0xc46ac0): escort value 0xf77360 for every mission on +0x98[r] / +0x140[r] (A-REGION-DISRUPT).
7. job 0x2959fc0 (0xc47130 → 0xc4c570): disruption after escorts → mission+0xa8 (A-REGION-DISRUPT).
8. per country, per flagged region of its active list, every mission on +0x98[r] then +0x140[r] with
   executing_mission ≠ 0: 0xf87550 (apply the hour's losses; A-REGION-LOSSES).
9. naval combats in flagged regions: both sides' external join counter +0x3e8 = 0 (A-WINGS-EXT).
10. per country, per flagged region: land-list missions with executing & 0x1f7fa: 0xf82110 (mission execution,
    external joins; uses +0xa8).
```

So in an air hour: reset → air war → escorts → disruption → losses applied → (combat manager) naval strike with
`e_i` from this hour's disruption. The disruption is not carried to the next air hour (reset in step 2) and no strike
happens in between.

**Confidence:** high for the order and the air-hour gate (the flag array is filled only by 0xe24530 and every job reads
it); medium for the meaning of `+0x110` / `+0x128` (built by 0xc53270, not decoded).

**Disagrees** with [A-MISSION-SAVE](A-MISSION-SAVE-air-mission-save-format.md#a-mission-save-eff-effectiveness-is-recomputed):
0xc46030 is the body of the **hourly** job 0x2959f80 (task built at 0xc57f93 in 0xc57d30), so the wing update
0xf62750 (and the region change penalty decay 0xf80490 in it) runs every hour, not only daily.

## A-REGION-LISTS: which carrier wings are in the region

Two per-region lists per country (`strategicAir`):

- `+0x98[r]` (`_ActiveMissions`): missions whose strategic region is `r`, registered by SetRegion 0xf859d0 → 0xc5daa0.
- `+0x140[r]`: **scrambled carrier missions**, rebuilt every hour by **0xc66480**:
```
clear all +0x140[r]
for base in strategicAir.bases(+0x10):
    if base is not a carrier (base+0x68 != 0): continue
    tf = ship(base+0x60)+0x728;  if !InCombat(tf) (0xc00000): continue
    r = region of the task force's location (tf+0x1f0 → +0xc8 → +0x60);  if !flag[r]: continue
    for wing in the country's pools at the base:
        m = wing.mission
        if m.region(+0x30) and m.type(+0x10) != 0: continue      // player mission elsewhere: stays on its own list
        +0x140[r].push(m); add r to the active region list +0xc8
```
Carrier CanExecute 0xf79c10 (from 0xc49e80 for this list) needs: available missions ≠ 0, wing not busy (`+0x79`,
ready timer `+0x64 >= +0x60`, no transfer target), planes > 0, no training bit in `type`, `period` vs night coverage
(DAY_NIGHT_COVERAGE_FACTOR), AGGRESSION_THRESHOLD (0xf80930); then
```
effectiveness(+0xd8)          = 0xf78500(mission, isCarrier = 1, 0, 0)          // partly decoded in A-MISSION-SAVE
effective_planes_count(+0xe0) = trunc(round(planes × effectiveness) × CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[tf+0x4ec])
                                                                                  // 0xf78360; TFB ratio 1.0 for every stance
```
`executing_mission` = ChooseCarrier 0xf7acc0 (fighters 0x1, naval bombers 0x10). If `effective_planes_count` is 0
(vanilla stance index 0, ratio 0) the wing does not execute and takes no part.

**Carrier wings with a region.** The game's own carrier-battle save had `strategic_region=30`, `region_change_penalty=0.9`
on carrier bombers with `type=0`: SetRegion was called by the game. A caller that does it: 0xc647b0 (from the task force
code 0xd70032): for each carrier of the task force, every wing whose mission region equals the old region (null for a
fresh mission) gets SetRegion(new region). Such a wing is on `+0x98[r]` **and** (with `type=0`) on `+0x140[r]`.
`0xf7ee40(mission)` = the mission's region, or for a carrier wing without one the region of the carrier's location.

**Confidence:** high for 0xc66480 and the carrier CanExecute; medium for the region sync 0xc647b0 being what gives the
in-game carrier wings their region (not traced from a task-force move).

## A-REGION-WAR: pairing, 0xc5f4c0 → 0xf81a20

```
0xc5f4c0(self, ctx):
for r in self.activeRegions(+0xc8, sorted) with flag[r] and (+0x98[r] or +0x140[r] non-empty):
    defenders = []                                           // entries {mission, int n(+8), int left(+0xc), byte cv(+0x10)}
    for other in ctx.countries, other != self, hostile(self, other) (0xc591c0: 0x700570 at war, + a fallback list):
        for m in other.allMissions(+0xb0): if !(m.type & 0x30000) and m.executing != 0 and r in m.regionList(+0x58):
            cv = base is a carrier and its task force is in combat;  skip submarine carriers (0xc3b220)
            defenders += {m, cv}
        for m in other.+0x140[r]: if m.executing != 0: defenders += {m, cv = 1}
    if defenders empty: continue
    attackers = []
    for m in self.+0x98[r]: ex = m.executing
        if ex & 0x20000: SAM 0xf63580(...); continue;  if ex == 0 or ex & 0x10000: continue
        attackers += {m, cv as above}                        // any mission type: bombers on this list attack too
    for m in self.+0x140[r]: if m.executing & 5: attackers += {m, cv = 1}   // scrambled: only AS / interception
    if attackers empty: continue
    stable-sort attackers by S18 × S28 descending (0xc43580 / 0xc43b80; S from 0xf5e890 without province)
    rnd = random_int(strategicair.cpp, 0x12c9) & 0x7fffffff
    prov = province of r: all-sea region (region+0xa0 == 1.0): provinces[rnd % n]; else the first from rnd % n on that is
           hostile to the first defending country (0x700570), else provinces[rnd % n]
    0xbb3800(...)                                            // combat record for the map (no effect)
    0xf81a20(region, prov, self tag, attackers, defenders)
```
The `+0x58` list of a mission (built in SetRegion by 0xf7a760) holds `{region id, distance in region}` entries; a
mission is a defender in every region on it, and in a region other than its target only with the distance weight below.
A carrier wing on both lists is **two defender entries** (and, if a fighter, two attacker entries) for one mission;
read from the code, not tested (medium-low).

```
0xf81a20(region, prov, tag, A, D):
for a in A: a.n = a.left = a.mission.effective_planes_count (+0xe0, assert > 0);  totA += a.n
det  = air detection of `tag` in the region (0xf7a460 + 0xf77730; DETECT_CHANCE_*; not decoded)
totD = 0xf80540(D, tag, det, 1.0)                            // defender weights, below
both lists: 0xf64a30(wing)                                   // marks the wing (no combat effect found)
xp   = min(1, totD × COMBAT_MULTIPLANE_CAP / totA) × AIR_WING_XP_AIR_VS_AIR_COMBAT_GAIN;  attackers: 0xf5dfa0(wing, xp)
0xf81ce0(tag, D)                                             // war support / statistics per defending country
for a in A (sorted order): 0xf7be70(a, D, region, prov, det)
```

**Defender weights 0xf80540** (`n = mission.effective_planes_count`):
```
anyStrike = some defender has executing & 0x1f7fa (not AS / interception / training)
k(d) = d.cv ? clamp(1 + T1, 0, 1) : clamp(det + T1, 0, 1)    // 0xf75820; T1 = targeted modifier slot 1 of the
                                                             // attacker's country vs the defender's owner (0 by default)
executing & 0x1f7fa:  w = CAS 2.0 | port strike 100.0 | strategic 1 + owner modifier 4 | else 1.0
                      d.n = d.left = max(1, round(trunc(n × k) × (1 + stat37) × w))
executing AS, anyStrike: d.n = d.left = n
otherwise:            d.n = d.left = max(1, round(trunc(n × k) × (1 + stat37)))
stat37 = wing+0x548 = raw equipment stat index 0x37 (name not mapped; 0 for most planes ?)
```
Between two carrier task forces in combat `cv = 1`, so `k = 1` and naval bombers count with their full
`effective_planes_count`.

**Confidence:** high for the lists, filters, sort and the defender weights; medium for `hostile` and `+0x58`;
detection value UNRESOLVED.

## A-REGION-COMBAT: one attacker against the defenders, 0xf7be70 → 0xf862a0

**Wing stats 0xf5e890(wing, out, prov, missionBits)** (resolves the A-WINGS-EXT open item):
```
raw: atk = stat 0x30, def = stat 0x2f, agi = stat 0x31 (0xf60970)
mA = mD = mG = 1 + Σ air_attack/defence/agility_factor (0x6f/0x70/0x71) of country, wing holder, ace   // 0xf5d3b0
if prov > 0 and the mission has a region:
    M = mission efficiency 0xf7ed40 by missionBits (AS air_superiority_efficiency 0xb, interception 0xd, CAS 0xf,
        naval 0x10; A-ACE); missionBits == 8: mD += strategic_bomber_defence_factor (0x76)
    + region weather 0x6f/0x70/0x71 × weatherF (0xf5c9d0); + province night (> DAY_NIGHT_COVERAGE_FACTOR) × nightF (0xf5bc80)
    + air_home_defence_factor (0x74) if the province's region is the base's region (carrier: the fleet's), else
      air_power_projection_factor (0x75), to mA and mD only
out+0x00 attack  = (M + mA) × atk;   out+0x18 = attack  / AIR_WING_MAX_STATS_ATTACK  (100 / 500)
out+0x08 defence = (M + mD) × def;   out+0x20 = defence / AIR_WING_MAX_STATS_DEFENCE (100 / 300)
out+0x10 agility = (M + mG) × agi;   out+0x28 = agility / AIR_WING_MAX_STATS_AGILITY (100 / 400)
speed term 0xf60860 = speed(0xf60760, A-ACE) / AIR_WING_MAX_STATS_SPEED (800 / 1100)
```

**0xf7be70(a, D, region, prov, det)** (`S` = attacker stats with `prov`, `a.mission.executing`):
```
if S.attack <= 0 or a.mission.effective_planes_count == 0: return
for d in D: wgt(d) = 1, or, if d's region (0xf7ee40) is set and differs from the attacker's:
            wgt(d) = min(1, distInRegion(d.+0x58 entry of this region) / INTERCEPTION_DISTANCE_SCALE (50))
                     × INTERCEPTION_DAMAGE_SCALE (0.3)
totLeft = Σ d.left;   sameStrike = Σ d.left over d with executing & 0x1f7fa and region(d) == this region
if totLeft == 0: return
eng  = min(totLeft × COMBAT_MULTIPLANE_CAP (3 / 3), a.n)
P    = a.n × (1 + spd×DISRUPTION_SPEED_FACTOR) × (1 + S28×DISRUPTION_AGILITY_FACTOR)
           × (1 + S18×DISRUPTION_ATTACK_FACTOR) × (1 + det×DISRUPTION_DETECTION_FACTOR)    // (1 / 1.5), (0 / 1), (0 / 1), (1 / 1)
pot  = eng × S.attack
for d in D with d.left > 0:
    share = d.left / totLeft
    cv2   = d.cv and a.cv
    0xf862a0(d.mission, d, max(0.001, share × pot × wgt(d)), prov, max(0.001, share × eng), a.wing, S, a non-AS, cv2)
    if d.executing & 0x1f7fa and region(d) == region:                                   // disruption
        dis = trunc(max(0, (1 + T13) × (cv2 ? DISRUPTION_FACTOR_CARRIER : DISRUPTION_FACTOR) × P))   // (6 / 2), (4 / 4)
        d.mission.+0xb0 += trunc(trunc(d.left / sameStrike × dis) × wgt(d));  d.mission.+0xc8 = attacker tag
        (T13 = targeted modifier slot 0xd of the attacker's country vs the defender's owner, 0xf75690)
ace earn: U(airmission.cpp 0x82b) <= ACE_EARN_CHANCE_BASE → U(0x833), U(0x834) <= eng × ACE_EARN_CHANCE_PLANES_MULT
          × (1 + air_ace_generation_chance_factor); both → ace created (random_int 0x83f)   (A-ACE)
```
`d.left` is the capacity **after** the earlier attackers (see below), read when the share is taken.

**Losses of the defender, 0xf862a0(m, d, pot, prov, engShare, aWing, S, aStrike, cv2):**
```
D  = 0xf5e890(m.wing, prov, m.executing);  vA = speed(aWing), vD = speed(m.wing)   (0xf60760, assert > 0)
dmg = pot × COMBAT_DAMAGE_STATS_MULTILPIER                                           // 0.2 / 0.35
red = S28 < D28 ? (min(D28 / S28, BIGGEST_AGILITY_FACTOR_DIFF) − 1) × trunc(dmg × COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION) : 0
                                                                                     // 4 / 5, 0.45
inc = vA > vD > 0 ? (min((vA/MAXS) / (vD/MAXS), BIGGEST_SPEED_FACTOR_DIFF) − 1) × trunc(dmg × COMBAT_BETTER_SPEED_DAMAGE_INCREASE)
                    + trunc(trunc(vA / 100) × TOP_SPEED_DAMAGE_BONUS_FACTOR) × dmg : 0   // 0xf7ad60; 3.5 / 3, 0.65, 0.025 / 0.04
                                                                                     // MAXS = AIR_WING_MAX_STATS_SPEED; vA, vD raw (fixed)
dmg += inc − red
cap = min(d.left, 0xf78360(m, base, 0xf78500(m, isCarrier)) − m.+0x98)             // this hour's budget minus losses so far
d.left = max(0, d.left − round(engShare / COMBAT_MULTIPLANE_CAP))
if D.defence != 0:
    x = max(0.001, trunc(trunc(scale × dmg) × 0.01) / D.defence)                    // scale = cv2 ? COMBAT_DAMAGE_SCALE_CARRIER (5 / 5)
    if x < cap: losses = floor(x) + (U(airmission.cpp 0xacf) < frac(x) ? 1 : 0)    //       : COMBAT_DAMAGE_SCALE (1 / 0.1)
    else: losses = cap                                                              // no roll
else: losses = cap
m.+0x98 += losses
killer: ace = aceOf(aWing)
    ace:    if m.+0x9c or m.+0xa0 set: U(0xade); keep the old one if U >= 0.5;   then m.+0x9c = ace, m.+0xa4 = attacker tag
    no ace: if neither set and m.+0xa4 > 0: U(0xae5), keep if U >= 0.5;          then m.+0x9c = none, m.+0xa4 = attacker tag
statistics 0xc5cb60 (no plane removal); losses > 0 and cv2: 0xf5ab00(aWing, …) (kill tally); region tally 0xc5c780
```
`0.01` is `imul 0x3e8`. `D.defence` is the absolute defence (`out+0x08`), `S28` / `D28` the normalised agilities.

**Confidence:** high for the structure, the defines, the rounding and the cap; medium for `wgt` (distance) and the
top-speed term (`vA` raw ÷ 100, as in A-air-combat); the targeted modifier slots 1 / 0xd are unnamed.

## A-REGION-DISRUPT: from disruption to the naval strike efficiency `e_i`

**Escorts 0xf77360 (step 6):** a mission whose executing has 0x1 (air superiority):
```
m.+0xb8 = m.effective_planes_count × (1 + spd × ESCORT_SPEED_FACTOR) × (1 + S28 × ESCORT_AGILITY_FACTOR)
          × (1 + S18 × ESCORT_ATTACK_FACTOR) × (1 + det × DISRUPTION_DETECTION_FACTOR) × ESCORT_FACTOR
          × (1 + air_escort_efficiency (0xc) of its country)        // (1/1), (1/2), (1/2), (1/1), (2/5); S without province
otherwise m.+0xb8 = 0
```
**After escorts, 0xc4c570 (step 7)**, per country, per flagged region `r` of its active list:
```
taken = Σ +0xb0, red = Σ +0xb8   over the missions on +0x98[r] and +0x140[r] of every strategic air in +0x110
                                 (self and allies; asserts TotalDisruptionTaken / TotalDisruptionReduction >= 0)
if taken > 0:  f = 1 − min(1, red / taken)
               for m in self.+0x98[r] with executing & 0x1f7fa and not a missile (0xf63140): m.+0xa8 = trunc(f × m.+0xb0)
```
Missions only on `+0x140[r]` keep `+0xa8 = 0`.

**0xf78160(mission, out, prov, planes, missionBit)** (the A-WINGS-EXT formula with its stats resolved):
```
if +0xa8 == 0 or missionBit & 0x4400 or planes <= 0 or S20 <= 0: e = 1
den = planes × (1 + S20 × DISRUPTION_DEFENCE_DEFENCE_FACTOR) × (1 + S18 × DISRUPTION_DEFENCE_ATTACK_FACTOR)
               × (1 + spd × DISRUPTION_DEFENCE_SPEED_FACTOR)       // (0.5 / 0.9), (0.5 / 0.8), (1 / 1); S = 0xf5e890(wing, prov, missionBit)
e   = clamp(1 − trunc(sqrt(+0xa8 / den) × 0.316228), 0.001, 1)     // = 1 − sqrt(+0xa8 / (10 × den)); 0x24ed7d0 fixed sqrt
```
In the naval strike (0x1978e30 at 0x1979231) `planes = n_i` (the wing's share of the AirFX planes), `prov` the battle
province, `missionBit` the wing's executing. `avgAttack = Σ attack_i × n_i × e_i / Σ n_i`; for `e_i < 1`,
`round((1 − e_i) × n_i)` planes are reported as disrupted (0xc5c5c0, statistics).

This fits the in-game results: in a fighter mirror the defenders' own fighters escort (TFB ESCORT_FACTOR 5 against
DISRUPTION_FACTOR_CARRIER 2), `red >= taken`, `f = 0`, `e_i = 1` (damage per hit matched exactly); a bomber-only side has
`red = 0`, `f = 1`, and its bombers strike at `e_i < 1` (×0.55–0.6 observed).

**Confidence:** high for 0xf77360, 0xc4c570 and 0xf78160; medium that `+0x110` is self + allies (the in-game result
needs it); medium that in-game carrier wings are on `+0x98` (A-REGION-LISTS).

## A-REGION-LOSSES: when the planes go, and the naval combat's count

**0xf87550(mission)** (step 8, after the whole air war of the hour):
```
losses = m.+0x98
if the wing has a live ace and losses > 0: U(airmission.cpp 0xa50) <= ACE_DEATH_CHANCE_BASE + 0.1 × losses
        × ACE_DEATH_CHANCE_PLANES_MULT → death; killer ace if U(0xa53) <= ACE_DEATH_BY_OTHER_ACE_CHANCE (A-ACE)
if losses > 0:
    0xf63580(wing, losses, killer tag +0xa4)               // RemovePlanes: from the wing's equipment entries in order
    carrier base and its task force in a combat (0xd64530): 0x15c4500(combat, wing, losses)
        → side 0x161fe00: the AirFX holding the wing (externals side+0x100, then the groups' AirFX) → airfx+0x60 += losses
m.+0x98 = m.+0xa4 = 0; m.+0x9c = none
```
So the region-war losses of carrier wings **are counted in the naval combat's air-object loss tally** (`airfx+0x60`;
its readers are not decoded), and the AirFX plane count (`+0x5c`, recounted at each air step, 0x1976ca0) is lower at
the strike. The cap `budget − m.+0x98` limits one mission's losses per air hour to its `effective_planes_count`.

**Confidence:** high.

## A-REGION-ACCIDENT: the other hourly plane loss

Every hour (not only air hours) 0xc5efc0 draws one `random_int(strategicair.cpp, 0x1be8)` (if `gamestate+0xa35 == 0`)
and seeds a local hash generator (constants 0x4ad685b3 / 0x68e31da4 / 0x1b56c4e9 / 0x92d68ca2; no further game RNG).
Per air base, 0xc60290:
```
u = next() % 100000;  if u >= 10000: no accident this hour (90 %)
start at a random pool (next() % pools); for each wing:
    training (type & 0x800): only if not on a carrier whose task force is in combat, and 0xf80a40
    else: executing ≠ 0, not barrage / drop nuke / SAM, not a missile
    p = 0xf61d70(wing) × ACCIDENT_CHANCE_BALANCE_MULT (0.1 / 0.1);  if p > 0.1 × next()/1e5: 0xf5dd80(wing, next()) and stop
```
Carrier wings executing their scrambled mission can therefore lose planes to accidents in any hour of a naval battle,
while the CARRIER_MULT path (0xc4daf0, ACCIDENT_CHANCE_BASE / _CARRIER_MULT) sets the per-wing chance. The chance 0xf61d70
and the planes killed by 0xf5dd80 are not decoded. **Confidence:** medium (outline only).

## A-REGION-OTHER: other effects on the naval battle

- No naval-combat code reads the region air superiority records (`strategicAir+0xe0[r]+0x78 / +0x80`) or the region
  war results: the only strategic-air calls from the naval modules are the carrier/base helpers listed in
  [A-air-combat](A-air-combat.md) and [A-WINGS](A-WINGS-carrier-wings-and-reinforcement.md) (crowding 0xc53fd0, pools
  0xc536b0, the join cap 0xc54980, 0xc5c5c0, 0xc5cb60, 0xc5d0f0). So no effect on positioning, detection, targeting or
  sortie efficiency was found (medium: searched the call graph, not every data read).
- The carrier sortie efficiency (A-SORTIE) is not used in the region war; the region war uses
  `effective_planes_count`.
- Through the planes lost (A-LOSSES count), the disruption `e_i` (A-DMG) and the aces killed or earned.

## Roll order for the engine (per hour, before the naval combat hour)

1. Every hour: `random_int(strategicair.cpp, 0x1be8)`; the accident draws that follow use the local generator.
2. Air hour, per attacking country (active-country order), per region (ascending id) that has attackers and defenders:
   `random_int(strategicair.cpp, 0x12c9)`; then per attacker (sorted), per defender with `left > 0`:
   `U(airmission.cpp 0xacf)` only when `defence ≠ 0` and `0.001 ≤ x < cap`; then `U(0xade)` or `U(0xae5)` per the
   killer rule; after the attacker's defenders: `U(0x82b)` [, `U(0x833)`, `U(0x834)` [, `random_int(0x83f)`]].
3. Air hour, per country, per region, per mission (land list then carrier list) with losses and a live ace:
   `U(0xa50)` [, `U(0xa53)`].

## Defines and constants involved

| Name | Vanilla / TFB | Role |
|---|---|---|
| `NAir.HOURS_DELAY_AFTER_EACH_COMBAT` | 4 / 4 | region air hour (A-CADENCE clock) |
| `NNavy.CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO` | {0, .25, .5, .75, 1} / all 1 | carrier wings' effective planes (0xf78476) |
| `NAir.COMBAT_MULTIPLANE_CAP` | 3 / 3 | engaged planes, capacity use, XP ratio (0xf7c094, 0xf864e2, 0xf81bcb) |
| `NAir.COMBAT_DAMAGE_STATS_MULTILPIER` | 0.2 / 0.35 | damage (0xf863be) |
| `NAir.COMBAT_DAMAGE_SCALE` / `_CARRIER` | 1 / 0.1, 5 / 5 | losses; carrier when both sides are carrier wings in combat (0xf86563 / 0xf8656d) |
| `NAir.BIGGEST_AGILITY_FACTOR_DIFF`, `COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION` | 4 / 5, 0.45 / 0.45 | agility reduction (0xf8641a, 0xf8642e) |
| `NAir.BIGGEST_SPEED_FACTOR_DIFF`, `COMBAT_BETTER_SPEED_DAMAGE_INCREASE`, `TOP_SPEED_DAMAGE_BONUS_FACTOR` | 3.5 / 3, 0.65 / 0.65, 0.025 / 0.04 | speed bonus (0xf7ad60) |
| `NAir.AIR_WING_MAX_STATS_ATTACK / DEFENCE / AGILITY / SPEED` | 100/100/100/800, 500/300/400/1100 | normalised stats (0xf5ef01.., 0xf60873) |
| `NAir.DISRUPTION_FACTOR` / `_CARRIER` | 4 / 4, 6 / 2 | disruption (0xf7c500 / 0xf7c50c) |
| `NAir.DISRUPTION_SPEED / AGILITY / ATTACK / DETECTION_FACTOR` | 1/0/0/1, 1.5/1/1/1 | disruption power (0xf7c0d7..0xf7c159); DETECTION also escorts (0xf77450) |
| `NAir.ESCORT_FACTOR`, `ESCORT_SPEED / AGILITY / ATTACK_FACTOR` | 2, 1/1/1; 5, 1/2/2 | escort reduction (0xf7756a, 0xf77438..0xf77448) |
| `NAir.DISRUPTION_DEFENCE_DEFENCE / ATTACK / SPEED_FACTOR` | 0.5/0.5/1, 0.9/0.8/1 | `e_i` (0xf781f9, 0xf781f1, 0xf78278) |
| `NAir.INTERCEPTION_DISTANCE_SCALE`, `INTERCEPTION_DAMAGE_SCALE` | 50, 0.3 | defender passing through another region (0xf7c372, 0xf7c3c5) |
| `NAir.AIR_WING_XP_AIR_VS_AIR_COMBAT_GAIN` | 0.8 | attacker XP (0xf81c26) |
| `NAir.ACE_*` | see A-ACE | ace rolls (0xf7c674.., 0xf875a0..) |
| `NAir.ACCIDENT_CHANCE_BALANCE_MULT` | 0.1 / 0.1 | accident (0xc605a9) |
| hard-coded 0.01, 0.316228, 2.0 / 100.0, 0.5, 10 % | | loss divisor (`imul 0x3e8`), `e_i`, CAS / port strike weights, killer rolls, accident gate (`u < 10000`) |

## How it was verified

Static reading. Cross-checks: the call chain 0xc57d30 → 0xc5f4c0 → 0xf81a20 → 0xf7be70 → 0xf862a0 is the only path to
COMBAT_DAMAGE_SCALE_CARRIER and DISRUPTION_FACTOR_CARRIER; the job vtables 0x2959f80 / f00 / fa0 / fc0 were matched to
their constructors in 0xc57d30 / 0xc58660; the disruption fields follow `+0xb0` (only writer 0xf7be70) → `+0xa8` (only
writer 0xc4c570, reset 0xc4a2f0) → 0xf78160 (only reader with 0xf846c6, paradrop). The in-game observations (planes lost
before the first strike two hours into the battle, i.e. at the first air hour; ×1.0 per-hit damage in the mirror and
×0.55–0.6 against 180 fighters) match the reading. Suggested in-game checks below.

**What to log (tools/ingame, Frida):**
- 0xf81a20 entry: `rcx` region (`+0x58` id), `edx` province, `r9` attackers and `[rsp+0x28]` defenders: lists of 24-byte
  entries `{mission, int n, int left, byte cv}`; per mission `wing = [m+0x28]`, `m+0x14` executing, `m+0xe0`, `m+0x30`.
  This shows whether carrier wings appear twice (both lists).
- 0xf7be70 entry: `[rsp+0x28]` = detection `det` (fixed).
- 0xf862a0 entry `rcx` mission, `r8` pot, `[rsp+0x28]` engShare, `[rsp+0x48]` cv2; at exit `m+0x98`, `rdx+0xc`.
- After step 7 (or at 0x1979231): `m+0xa8 / +0xb0 / +0xb8` of each carrier wing; the return of 0xf78160 (`[rbp−0x59]`
  in 0x1978e30).
- 0xf87550 entry: `m+0x98`; 0xf63580 calls (`rcx` wing, `edx` planes) from 0xf87675 (air war) and 0xf5dded (accident).
- Rolls: random_int lines 0x1be8 / 0x12c9 (strategicair.cpp); random_fixed lines 0xacf, 0xade, 0xae5, 0x82b, 0x833,
  0x834, 0xa50, 0xa53 (airmission.cpp).

## Differences from the current spec

- New mechanic: the region air war in air hours (A-CADENCE clock), before the naval hour, between the two sides' carrier
  wings (and any other missions over the region). It removes planes from carrier wings outside the naval strike.
- A-DMG `e_i` is not "1 unless out of fuel": it is the disruption efficiency of the same air hour, for wings on the
  region mission list, after escorts. Without enemy air-superiority wings, or with enough own escorts, it is 1.
- A-LOSSES: region-war losses are added to the battle's AirFX loss count.
- Accidents remove carrier planes in any hour (outline only).

## UNRESOLVED

- The detection value `det` (0xf77730; DETECT_CHANCE_*, modifiers 8 / 9, NO_SEARCH_MISSION_DETECT_FACTOR, a debug lock
  table at 0x332f5f0).
- `strategicAir+0x110` / `+0x128` membership (0xc53270); `hostile` 0xc591c0 beyond 0x700570.
- Mission `+0x58` region list (0xf7a760) contents for a carrier wing; whether a carrier wing with a region is really a
  double defender / attacker entry.
- What triggers the region sync 0xc647b0 for a task force that never moves (the in-game carrier wings had a region).
- `stat37` (wing+0x548) and targeted modifier slots 1 and 0xd (names).
- The isCarrier branch of the mission effectiveness 0xf78500.
- Accident chance 0xf61d70 and amount 0xf5dd80; the readers of `airfx+0x60`.
- Jobs 0x2959ee0 / 0x2959f20 / 0x2959f40 in 0xc58660 (bodies 0xc466d0, 0xc47420, 0xc476b0) were not read.
