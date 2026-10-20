# A-ACE: air aces and wing experience in naval strikes

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-LOSSES (the per-wing call after a strike), A-HIT, A-DMG, A-AA-PREEMPT, A-INTERCEPT (plane
  stat getters), S-REPLAY (roll order) (`docs/mechanics/air.md`)
- **Confidence:** read from code only (static analysis; no in-game test). Per section below.

Method: `tools/re/` (`hoi4re.py`, `ctx.py`, `who.py`) plus throw-away disassembly scripts that label define globals from
`defmap.json`. Modifier ids → names from the registration function 0x55f7f0 (loc keys `MODIFIER_*`; script keys checked
against `documentation/modifiers_documentation.md`). Save token ids from the static token registration 0x831c0.
Source files: `airmission.cpp` (0xf80f60), `aces.cpp` (0x6176c0..0x61be8b), `airwing.cpp` (stat getters),
`naval\naval_fire_exchange_air.cpp` (0x197b890). Conventions as in [A-air-combat](A-air-combat.md): RVAs, fixed point
×1e5, every product truncated toward zero; a `random_fixed` U is an int in 0..99999 (shown as 0..0.99999).

## Objects

- **Ace** (`CAce`, 0x178 bytes, ctor 0x6176c0, vtable 0x27de2a0: vt+0x10 save writer 0x61bd30, vt+0x20 read member
  0x61afb0). `+0x18` type mask of its template, `+0x30` its modifier container (a copy of the template's `effect`),
  `+0xe0` country, `+0xe8` the wing it flies with (handle), `+0xf0 / +0x110 / +0x130` name / surname / callsign strings,
  `+0x150` is_female, `+0x154` portrait, `+0x158` **alive** (ctor sets 1), `+0x15c` kill type, `+0x160` killer (ace
  handle), `+0x168` killer country, `+0x16c` handled, `+0x170` the template (`common/aces` entry; `+0x28` type mask,
  `+0x30` `chance`, `+0x38` / `+0x48` its `effect`).
- **Wing** `+0x2e8` = ace handle (0xf5aab0 resolves it; null if none). Attach: 0xf5b320 (detaches an old ace with
  0x61bc70(old, 0), then 0x61bc70(new, wing) sets `ace+0xe8`). `wing+0x238` = experience modifier holder, `wing+0x218` =
  experience.
- **Country** `+0x12d8 / +0x12e4` = its aces (list / count).
- **Air mission** (`wing+0x90`): `+0x9c` the enemy ace that last hit this wing in air-to-air combat (set by the generic
  air combat 0xf862a0, cleared by 0xf87550), `+0xa4` a country tag for the killer (same writers), `+0x30` the mission's
  strategic region (see [A-MISSION-SAVE](A-MISSION-SAVE-air-mission-save-format.md); carrier wings in the game's own
  carrier-battle save have one).

## A-ACE-ROLLS: what 0xf80f60 does after each naval strike

**Caller** (0x197b890, 0x197c830..0x197c882): after the strike of one AirFX (one wing pool, [A-WINGS](A-WINGS-carrier-wings-and-reinforcement.md)),
for **every wing in that AirFX's strike list** (`airfx+0x28`, count `+0x34`; all wings whose available missions allow
naval strike / kamikaze / port strike, in pool order; not limited to the first nWings or to wings executing a strike
mission), if the handle resolves:
```
0xf80f60(wing.mission, losses, damage)
losses = intercepted + aaKills            // the AirFX total ([rsp+0x44]: 0x197b380 result + 0x197c8b0 result)
damage = trunc(target HP before − HP after)   // 0x197ac80's return: rbx = HP lost, / 1e5 truncated; 0 if no hits
```
Both arguments are the AirFX's totals; every strike wing of the AirFX gets the same values. Reached only when the strike
passed target selection: the early returns of 0x197b890 (no strike wings, carrier member dead, the averages ≤ 0, no
planes fly, no target; 0x197b8d3..0x197ba9e) skip it. If no striker survives the AA (0x197bf32) it is still reached,
with damage = 0. The port-strike branch reaches it too.

**0xf80f60(mission, losses, damage)** (0xf80f60..0xf81494; `wing = mission+0x28`):
```
ace = wing.ace (0xf5aab0)
if ace and ace.alive (+0x158):                                   // ---- the wing has a live ace
    if losses <= 0: return
    pDeath = ACE_DEATH_CHANCE_BASE + trunc(trunc(losses × ACE_DEATH_CHANCE_PLANES_MULT) × 0.1)   // imul 0x2710
    U1 = random_fixed(line 0xb52)
    if U1 > pDeath: return                                       // equal counts as a death
    killer = null
    if mission.+0x9c resolves (0x5386f0):                        // an enemy ace hit this wing in air-to-air combat
        U2 = random_fixed(line 0xb54)
        if U2 <= ACE_DEATH_BY_OTHER_ACE_CHANCE: killer = mission.+0x9c
    0x61bba0(ace, kill type 1, killer, &mission.+0xa4)           // alive = 0, kill type, killer, killer country
    return
else:                                                            // ---- no ace, or a dead one
    if damage <= 0: return
    if losses >= damage: return
    country = owner(wing)                                        // wing+0x9c4 tag if set (exile), else the pool's country
    pEarn = trunc((ACE_EARN_CHANCE_BASE + trunc(trunc(damage × ACE_EARN_CHANCE_PLANES_MULT) × 0.1))
                  × (1 + country.air_ace_generation_chance_factor))       // modifier 0x7, country+0x5b8
    U3 = random_fixed(line 0xb68)
    if U3 > pEarn: return                                        // equal counts as earned
    seed = hash32(wing id handle wing+0x18) + (date.hours − 43 800 000) + gamestate+0x720
    ace = 0x618860(aceDb, seed, country, &country.aces, wing)    // create; see A-ACE-CREATE. No game RNG draw inside.
    if ace == null: return
    s = random_int(line 0xb72) & 0x7fffffff                      // seed of the script scope only
    fire on_action on_ace_promoted (token global 0x333d618) for country, FROM = ace, scope seed s (0xa79ba0)
```

- `losses` and `damage` are integers. `trunc(n × M)` with an integer n is exact; `× 0.1` truncates at 5 decimals.
  Vanilla: pDeath = 0.005 + 0.0001 × losses; pEarn = (0.01 + 0.0005 × damage) × (1 + f). TFB (ACE_EARN_CHANCE_PLANES_MULT
  0.002): pEarn = (0.01 + 0.0002 × damage) × (1 + f), e.g. 100 HP of damage → 3 %.
- The earn condition is "the strike did damage and the AirFX lost fewer planes than the integer HP damage". Despite the
  define comments, nothing counts kills: the "planes" argument is the HP damage.
- A wing with a live ace never rolls 0xb68 (one ace per wing); it rolls 0xb52 instead, and only when the AirFX lost
  planes. A dead ace stays attached (no detach found outside load and wing disband), so its wing rolls 0xb68 again.
- `hash32(h)` = `x = (h.word0 << 32) + h.word1; m = x × 0x49631a45 (64-bit); (m >> 32) ^ (uint32)m` (0x221ef50).
  `gamestate+0x720` is an int written from `random_int(gamestate.cpp, 0x980)` (0x1e1120, caller 0xdd6a30), in
  `gamestate.daily` (0x1d58a7) and at init (0x1d1b5d); its meaning is UNRESOLVED. `date.hours − 43 800 000` is the
  hour count used by the air clock ([A-CADENCE](A-air-combat.md#a-cadence-when-planes-strike)).
- The `0xb72` integer is only the random seed of the `on_ace_promoted` scope. Vanilla's on_action picks a news event
  with it; TFB's only adds war support (+0.02) and a SIA variable. No combat effect.
- 0x618860 calls `LightRandomLog` (0x2234110, aces.cpp line 0x1ea), which logs a value but draws nothing.

**Roll order per strike.** AirFX after AirFX (group order, as in [A-CADENCE](A-air-combat.md#a-cadence-when-planes-strike)):
that AirFX's strike rolls (interception 0x197b686, AA lines 0x212 / 0x21c, hits 0x45d, crit, …), then for each of its
strike wings in list order: `[0xb52 [0xb54]]` if the wing has a live ace, else `[0xb68 [0xb72]]`. Rolls are skipped
exactly as in the pseudocode. In a normal naval battle `mission+0x9c` is empty (only land air-to-air combat sets it), so
0xb54 does not occur.

**Evidence.** 0xf80f9f..0xf810a1 (ace branch), 0xf810a6..0xf81178 (earn chance and roll), 0xf8117e..0xf813c0 (seed and
create), 0xf813c6..0xf8146f (int roll, on_action); caller 0x197c830..0x197c882; arguments 0x197be8c, 0x197bf1f..0x197bf27,
0x197c01e, 0x197c82e; damage return 0x197b099..0x197b14a, 0x197b359..0x197b374. on_action token: 0xf85f0a writes
0x333d618 next to the `on_ace_promoted` string (0xf85ecf).

**Confidence.** High for the formulas, the conditions and the roll order. High that the arguments are the AirFX totals
(interception + AA losses, integer HP damage). Medium for the name of `gamestate+0x720`.

## A-ACE-CREATE: which ace (0x618860)

0x618860 builds the ace with its own deterministic hash generator (the Clausewitz light random, same mixing as 0x534fe0),
seeded with `seed`; it does not draw from the game RNG, so a logged game battle shows no roll for it.
```
A = mixA(seed); B = mixB(seed)                       // 0x618890..0x6188e8
draw(): x = A − B × 0x4ad685b3; B += 1; return mixC(x) & 0x7fffffff     // e.g. 0x618bf4..0x618c3e
mixA(s): a = s×0x7eb8da44; r = (a>>8)^a; r += 0x10d8365e; a = (r<<8)^r; c = a×0x6d22f31b; return (c>>8)^c
mixB(s): a = s×0x205ad647; r = (a>>8)^a; r −= 0x4b0c3ceb; a = (r<<8)^r; c = a×0x387a9f3a; return (c>>8)^c
mixC(x): r = (x>>8)^x; r += 0x68e31da4; a = (r<<8)^r; c = a×0x1b56c4e9; r = (c>>8)^c; c = r×0x92d68ca2; return (c>>8)^c
                                                     // uint32 arithmetic, logical shifts
if aceDb has no templates: return null
match  = templates (aceDb list +0x28, file order) with template.type & wing sub-unit type ≠ 0
         // sub-unit type = [[wing+0x38]+0x20]+0x5a8, the pool's common/units/air.txt `type` (A-WINGS-TYPES)
total  = Σ match.chance
if match empty or total <= 0: return null
t      = trunc((draw() mod 100000) / 1e5 × total)
pick   = first template in match with cumulative chance ≥ t (else the last)
names  = further draws (historical ace names of the country first, else first/last name lists; no names → null)
ace    = new CAce(country, …); attach to the wing if it has no ace handle (0xf5b320); set names;
         ace.+0x18 = pick.type, ace.+0x30 = copy of pick.effect, ace.+0x170 = pick; portrait = draw() & 0xff
country.aces += ace
```
So P(template k) = chance_k / Σ chance over the templates whose `type` overlaps the wing's sub-unit type. TFB
`cv_nav_bomber` (type naval_bomber): support_good / unique / genius = 0.9 / 0.4 / 0.05 → 66.7 % / 29.6 % / 3.7 %, effects
`naval_strike_targetting_factor` 0.05 / 0.10 / 0.15 (plus `ground_attack_factor`, `air_agility_factor`).
TFB `cv_CAS` (type `cas strategic_bomber`) also matches support_* (and bomber_* via strategic_bomber). TFB `cv_fighter`
(type fighter) matches fighter_*.

**Confidence.** High for the weighted pick and the match on the sub-unit type. Medium for the name-list details (not
needed by the simulator).

## A-ACE-STATS: how an ace and wing experience enter the naval strike and interception stats

**The ace's value of a modifier** (0x61a930(ace, out, id, −1)):
```
aceVal(id) = trunc(trunc(ace.effect[id] × sizeF) × (1 + aceCountry.ace_effectiveness_factor))   // modifier 0x25a, ace+0xe0
sizeF      = (wing planes p = [ace.wing]+0x7c) > 0 ? min(ACE_WING_SIZE / p, ACE_WING_SIZE_MAX_BONUS) : 1     // 0x6182d0
```
`p` is the ace's wing's **current** plane count. With ACE_WING_SIZE 100 and MAX 2 (vanilla and TFB), every wing of 50
planes or fewer (all carrier wings) doubles the ace effect: support_good gives +0.10 naval targeting, unique +0.20,
genius +0.30 (× (1 + ace_effectiveness_factor)). A wing above 100 planes gets less than the template value. A dead ace
(`+0x158 == 0`) gives nothing; every getter checks the flag.

**Naval strike getters** (per wing `w`, called by 0x1978e30 with `province` = the battle's province id
(`[combat+0x38]+0xa4`) and `flags` = the wing's `executing_mission`):
```
naval targeting 0xf61280:  base = trunc(stat35 × 0.1);  S = 1 + aceVal(0x17) + owner.naval_strike_targetting_factor(0x17) + R(0x17)
naval attack    0xf60f10:  base = trunc(stat34 × 0.1) (× AIR_NAVAL_KAMIKAZE_DAMAGE_MULT on kamikaze);
                           S = 1 + aceVal(0x16) + owner.naval_strike_attack_factor(0x16) + M + R(0x16)
naval agility   0xf60bc0:  base = trunc(stat31 × AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY);
                           S = 1 + aceVal(0x18) + owner.naval_strike_agility_factor(0x18) + M + R(0x18)
result = trunc(S × base)

M and R only if province > 0 and the mission has a strategic region (mission+0x30):
M     = owner mission efficiency by the mission's `type` (wing+0xa0), first match:      // 0xf7ed40
        &0x4 air_intercept_efficiency (0xd), &0x1 air_superiority_efficiency (0xb), &0x2 air_cas_efficiency (0xf),
        &0x110 air_nav_efficiency (0x10), else 0
R(id) = trunc(regionWeather(region id).Get(id) × weatherF(w))                           // 0xf19fd0 holder +0x48
      + (provinceNight(province) > DAY_NIGHT_COVERAGE_FACTOR ?                         // 0xf19660: wm+0x370[prov]+0x240
           trunc(provinceNightHolder(province).Get(id) × nightF(w, flags)) : 0)        // 0xf197d0: +0x270
weatherF(w) = clamp(1 + owner.air_weather_penalty + w.xp(+0x238).air_weather_penalty + aceVal(0x106), 0, 2)   // 0xf5c9d0
nightF(w)   = clamp(1 + owner.air_night_penalty + w.xp.air_night_penalty + rawStat(0x4c, flags) + aceVal(0x105)
                    + (region and type & 0x8 ? owner.air_strat_bomber_night_penalty : 0)
                    − (base is a ship whose task force has a mission (+0x374 ≠ 0) ? owner.air_carrier_night_penalty_reduction_factor : 0),
                    0, 2)                                                                  // 0xf5bc80
```
`owner` = the wing's country (`wing+0x9c4` tag, else the pool's). `stat` = the raw equipment stat 0xf60970 (`wing+0x390`
array, or the per-mission table `wing+0x968`); the stat cache is filled by 0xf661e0 from the equipment and never reads
the ace handle. The country terms are the country's own container (`+0x5b8`); no admiral, no ship.

**Only the ace's own wing gets its bonus.** The getters are per wing; 0x1978e30 then averages
`Σ value_i × n_i / Σ n_i` over the strike wings (A-DMG), so an ace on one of k equal wings raises the AirFX average by
1/k of its bonus. The carrier factors `naval_carrier_air_*_factor` multiply the average afterwards (A-DMG), unchanged.

**Which ace effects matter in naval combat.** Only `naval_strike_targetting_factor` (0x17), `naval_strike_attack_factor`
(0x16), `naval_strike_agility_factor` (0x18), and `air_maximum_speed_factor` (0x2, interception speed). Plus
`air_weather_penalty` / `air_night_penalty` inside the R term. **`air_agility_factor` (0x71), `air_attack_factor` (0x6f),
`air_defence_factor` (0x70) and `ground_attack_factor` have no effect**: the naval agility getter reads 0x18, not 0x71,
and interception uses raw stats. So TFB support aces affect only naval targeting; TFB fighter aces only the speed term
of interception.

**Interception (0x197b380, chunk 0x197b3b0..0x197b890).** Air attack (0x30), defence (0x2f) and agility (0x31) are raw
0xf60970 stats: no ace, no country modifier, no experience. Speed is 0xf60760:
```
speed = trunc(rawSpeed(wing+0x580, stat 0x3e) × (1 + aceVal(0x2) + (ace template has a nonzero 0x2 ? owner.air_ace_bonuses_factor(0x72) : 0)))
```
(`air_ace_bonuses_factor` is added as a flat term, once per ace modifier the template has; the same pattern is in the
generic air-combat stats 0xf5d3b0 / range 0xf5e5c0). The per-wing speeds then enter the unweighted wing means of A-INTERCEPT.

**Wing experience.** `wing+0x238` is rebuilt by 0xf65f20 from the wing's experience: the static blocks at +0xc8 / +0xd0 of
the static modifier struct (most likely `air_wing_experience_bonus_max` / `air_wing_experience_malus_min`), scaled by
the experience (and `air_untrained_pilots_penalty_factor` 0x73). In naval strikes this holder is read **only** for
`air_weather_penalty` and `air_night_penalty` inside weatherF / nightF. TFB's `air_wing_experience_bonus_max` has
`naval_strike_attack_factor = 0.15`, but 0xf60f10 never reads `wing+0x238`, so it has **no effect** in naval strikes
(it would act in 0xf5e890, the generic air-combat getter, which the naval module does not call). Experience levels
(0xf5c250, AIR_WING_XP_LEVELS) are read only by the UI.

**No experience gain in a naval strike.** 0xf80f60 touches no experience. The wing XP gain 0xf5dfa0
(FIELD_EXPERIENCE_FACTOR, AIR_WING_XP_MAX, air_mission_xp_factor) is called only from 0xf81a20 (air vs air), 0xf82110
(ProcessGroundMission) and 0xf83ed0, all reached from strategic-air updates, and 0xf64af0 only from 0xf82ca0 (land-based
wings joining a combat, [A-WINGS-EXT](A-WINGS-carrier-wings-and-reinforcement.md#a-wings-ext-land-based-planes-joining-a-naval-battle)).
Carrier wings gain nothing from the strike itself.

**Evidence.** 0xf612aa..0xf6156a (targeting), 0xf60bea..0xf60ec9 (agility), 0xf60f3a..0xf6125b (attack), 0x61a930,
0x6182d0, 0xf5c9d0, 0xf5bc80, 0xf7ed40, 0xf60760, 0x1979245 / 0x1979313 / 0x1979350 (call sites with
`r8d = [rsp+0x40]` = province from 0x1978e94 and `r9d = wing+0xa4`), 0xf65f20 (holder +0x238), 0xf661e0 (stat cache).

**Confidence.** High for the getter formulas, the ace scaling and that the bonus is per wing. High that the ace's
`air_agility_factor` / `air_attack_factor` do not reach naval combat. Medium for the names of the static blocks behind
`wing+0x238` and for the content of the region and province-night holders (whether any vanilla/TFB weather or night
modifier carries `naval_strike_*`; if none does, R = 0).

## A-ACE-DEATH: can an ace die in naval combat

Yes, only through 0xf80f60: after a strike in which its AirFX lost planes (`losses > 0`), with chance
`ACE_DEATH_CHANCE_BASE + 0.1 × losses × ACE_DEATH_CHANCE_PLANES_MULT` (line 0xb52), killer = none unless an enemy ace
is recorded on the mission (line 0xb54, not in naval). Death sets `alive = 0` at once, so the bonus is gone from the next
getter call (the next strike). The other ace-death sites are outside naval combat: 0xf87550 (pending air-to-air losses,
lines 0xa50 / 0xa53, strategic air 0xc57d30) and the accident chunk 0xf5df48 (`roll ≤ ACE_DEATH_CHANCE_BASE`, kill
type 2; from the strategic-air accident code 0xc60334). Whether an accident check runs for carrier wings during a battle
is not decoded.

**Confidence.** High.

## A-ACE-SAVE: aces in a save

- The ace is written by its country (CCountry::Write 0x7191b0, 0x719ab0..0x719ad5): one `ace = { … }` block (token 0x31e2)
  per entry of `country+0x12d8`. Writer 0x61bd30 keys, in order: the persistent id (0x2220340), `modifier` (template name,
  token 0x2965, only if a template is set), `name` (0x1b), `surname` (0x31e3), `callsign` (0x31e4, if set), `is_female`
  (0x2d68, if true), `portrait` (0x31e5), `alive = no` (0x30a3, only when dead), `handled` (0x4bd2, if set), `kill_type`
  (0x4b7c, if ≠ 0), `killer_name` (0x34f2, if set; a `{ id type }` handle), `killer_country` (0x34f3, if set).
- The wing (writer 0xf68b37, at 0xf68d60..0xf68d9a) writes `ace = { id type }` (token 0x31e2, handle 0x2220180) only when
  its ace handle (`wing+0x2e8`) resolves.
- On load, the country reader 0x705ce0 → 0xc61f80 re-attaches aces to wings (0x61bc70).
- So **a generated save starts with no aces** as long as neither the country blocks nor the `air_wings` entries contain
  `ace=` blocks. A save copied from a real game can carry aces: strip both, or keep them on purpose.

**Confidence.** High for the keys and their tokens (names from the token map). Medium for the load re-attach path.

## Summary: what the simulator must do

1. State: per strike wing an optional ace `{template, alive}`; per side `air_ace_generation_chance_factor`,
   `ace_effectiveness_factor`, `air_ace_bonuses_factor` (country modifiers; TFB sources are air doctrines and leader
   traits); `common/aces` templates (`type`, `chance`, `effect`).
2. After each AirFX strike (also when no striker survived; not after an early return), for each strike wing of that
   AirFX in list order:
   - live ace: if `losses > 0`: `U(0xb52) ≤ 0.005 + 0.0001·losses` (defines) → ace dies (no 0xb54 in naval).
   - otherwise: if `damage > 0 and losses < damage`: `U(0xb68) ≤ (BASE + 0.1·damage·MULT)·(1 + gen factor)` → new ace;
     template drawn by `chance` among templates matching the sub-unit type (not from the game RNG); if an ace was made,
     consume one integer roll `0xb72` (no effect).
   - `losses` = the AirFX's intercepted + AA-killed planes this strike; `damage` = integer part of the HP the target lost.
3. Stats: naval targeting / attack / agility per wing get `+ aceVal(id)` inside `(1 + Σ)`, `aceVal = effect ×
   min(100 / wing planes, 2) × (1 + ace_effectiveness_factor)`; interception speed `× (1 + aceVal(speed) + bonuses)`.
   Nothing else (no air_agility, no air_attack, no experience bonus).
4. Replay: the ace's template is not in the roll log (deterministic hash of wing id, hour and `gamestate+0x720`). Log it
   from the game (e.g. read `ace+0x170` or save `modifier=`) or take it as an input per seed.

## Defines and constants involved

| Name | Vanilla | TFB | Where |
|---|---|---|---|
| NAir.ACE_EARN_CHANCE_BASE | 0.01 | 0.01 | 0xf81117 |
| NAir.ACE_EARN_CHANCE_PLANES_MULT | 0.005 | 0.002 | 0xf810cf (× damage, then × 0.1) |
| NAir.ACE_DEATH_CHANCE_BASE | 0.005 | 0.005 | 0xf81008; also 0xf875f3, 0xf5df61 (not naval) |
| NAir.ACE_DEATH_CHANCE_PLANES_MULT | 0.001 | 0.001 | 0xf80fbd (× losses, then × 0.1); also 0xf875a0 |
| NAir.ACE_DEATH_BY_OTHER_ACE_CHANCE | 1.0 | 1.0 | 0xf8105a; also 0xf8762b |
| NAir.ACE_WING_SIZE | 100 | 100 | 0x61832b (and 0x61a1a5, not decoded) |
| NAir.ACE_WING_SIZE_MAX_BONUS | 2 | 2 | 0x618315 |
| NAir.DAY_NIGHT_COVERAGE_FACTOR | 0.5 | ? | 0xf61503, 0xf60e62, 0xf611de |
| hard-coded 0.1 | | | `imul 0x2710` in both chance formulas (0xf80fea, 0xf810fc) |
| hard-coded clamp [0, 2] | | | weatherF / nightF (0x30d40) |

Modifier ids (0x55f7f0): 0x2 `air_maximum_speed_factor`, 0x7 `air_ace_generation_chance_factor`, 0xb / 0xd / 0xf / 0x10
`air_superiority_efficiency` / `air_intercept_efficiency` / `air_cas_efficiency` / `air_nav_efficiency`, 0x16 / 0x17 /
0x18 naval strike attack / targetting / agility factor, 0x6f / 0x70 / 0x71 `air_attack_factor` / `air_defence_factor` /
`air_agility_factor`, 0x72 `air_ace_bonuses_factor`, 0x73 `air_untrained_pilots_penalty_factor`, 0x104 / 0x105 / 0x106
`air_strat_bomber_night_penalty` / `air_night_penalty` / `air_weather_penalty`, 0x10d
`air_carrier_night_penalty_reduction_factor`, 0x25a `ace_effectiveness_factor`.

## How it was verified

Static reading only. The call site, arguments and loop of 0x197b890 were followed back to the interception, AA and strike
results. The ace fields were matched with the ace save writer's keys. The roll lines (0xb52, 0xb54, 0xb68, 0xb72) and
their conditions match the in-game log pattern reported by the caller: one 0xb68 per strike wing per strike, 0xb72 only
right after some 0xb68, 0xb52 rare (a wing with a live ace whose AirFX lost planes; such a wing stops rolling 0xb68).
That pattern also identifies the ace's wing in a log.

## Unresolved

- `gamestate+0x720` (third seed term of the ace template pick): what the value is and when it changes.
- Whether a dead ace is ever detached from its wing during play; if not, a new ace earned by that wing is created and
  registered but not attached (0x6199b6 skips 0xf5b320 when the wing still has a handle), so it gives no bonus.
- The content of the region weather and province night holders (`R` term): whether any vanilla/TFB weather or night
  effect has `naval_strike_*`; and `rawStat(0x4c)` in nightF.
- Whether the strategic-air accident check (0xc60334 → 0xf5df48, which can kill an ace) runs for carrier wings while
  their carrier is in a naval battle.
- The static-modifier block names behind struct +0xc8 / +0xd0 (assumed `air_wing_experience_bonus_max` / `_malus_min`).
- 0x61a1a5 (a second ACE_WING_SIZE reader in 0x619b60) is not decoded (UI or tooltip, probably).
