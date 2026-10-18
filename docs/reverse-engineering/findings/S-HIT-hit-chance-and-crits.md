# Findings: per-shot hit chance, critical hits, piercing tier (HOI4 1.19.3)

Area: S-HIT, S-HIT-BASE, S-HIT-SHIP, S-HIT-WEAPON, S-HIT-MODS, S-CRIT (+ S-CRIT-PART touch points), S-DMG-PIERCE.

## Function map (RVAs)

| RVA | Role |
|---|---|
| 0x1970a90 (+ chunks 0x1970b48, 0x197142d) | `BuildShot`: fills an 8-slot shot struct (damage, crit chance, crit mult, hit chance, piercing, target armor, armor-reduction factor, cooldown) for one weapon type. Only caller: 0x19740c0 (call at 0x1974155). |
| 0x19740c0 | Fire loop: per shot rolls hit, damage randomness, crit, part; applies crit mods, damage/defense factors. |
| 0x1973e20 / 0x1973521 | Callers: per weapon type (loop `type = 0..2`), pass shooter ship stats into 0x19740c0. |
| 0xc37000 (body 0xc37006) | Ship hit profile. 0x1ad7f00 = convoy hit profile (returns `CONVOY_HIT_PROFILE`; this read is missing from uses.json). |
| 0xc37180 | UI only (`STAT_NAVY_HIT_PROFILE` tooltip), same profile² clamp. Not combat. |
| 0x196fd60 | Piercing tier lookup `(armor, piercing, valueTable)`. |
| 0x17e4f70 | UI only (`NAVY_PIERCING_TOOLTIP_INFO`). Not combat. |
| 0x1972570 | Generic "member modifier" getter `(member, out, modifierId)`: modifier sum + ECombatBox screening bonus (it is not a screening function as such). |
| 0x19723b0 | ECombatBox of a member: 1 screen, 2 battle line, 3 carrier group (carriers and convoys), 4/5 submarine (revealed/hidden). |
| 0x2233db0 | Clausewitz random: hash of a global counter, `raw = (31-bit value) mod 100000`, so the result is fixed point **U ∈ {0, 0.00001, …, 0.99999}**. |

Weapon type enum (r8d / `[rbp+0x40]`): **0 = heavy, 1 = torpedo, 2 = light** (light turns into depth charges when the target is a submarine). Evidence: type 0 uses heavy attack/piercing getters 0xc3a580/0xc3a570 and modifier 0x1c1 (heavy gun hit chance), type 1 uses COMBAT_TORPEDO_*, type 2 uses 0xc3a5d0/0xc3a5c0 and modifier 0x1c0 (light gun). `GUN_HIT_PROFILES[type]` (0x197118a) gives { heavy 70, torpedo 100, light 45 } in vanilla 1.19.

Modifier IDs were recovered from the modifier-definition registration in 0x55f7f0 (`call 0x5574d0` with `edx = id`, loc key `MODIFIER_*`): 0x28 NAVAL_HIT_CHANCE, 0x1c7 NAVAL_HIT_CHANCE_AGAINST_A_COUNTRY, 0x1c1 / 0x1c0 / 0x1bf NAVAL_HEAVY_GUN / LIGHT_GUN / TORPEDO _HIT_CHANCE_FACTOR, 0x1c2 NAVAL_TORPEDO_ENEMY_CRITICAL_CHANCE_FACTOR, 0x1c3 NAVAL_TORPEDO_DAMAGE_REDUCTION_FACTOR, 0x1ba NAVAL_CRITICAL_SCORE_CHANCE_FACTOR, 0x1d3 NAVAL_CRITICAL_SCORE_CHANCE_FACTOR_AGAINST_A_COUNTRY, 0x18c NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR, 0x1b7 NAVAL_DAMAGE_FACTOR, 0x1b8 NAVAL_DEFENSE_FACTOR, 0x19f NAVAL_VISIBILITY_FACTOR, 0x29 NAVAL_SPEED_FACTOR. (Script names are the lower-case keys, e.g. `naval_hit_chance`.)

Fixed point: every `a*b` below is `trunc((a_raw*b_raw)/100000)` (magic 0x29f16b11c6d1e109, `sar 14`, plus sign bit = truncation toward zero). Every `a/b` is `trunc(a_raw*100000 / b_raw)` (idiv); a zero divisor or overflow gives the sentinel raw `0xFFFFFFFF` (= 42949.67).

---

## S-HIT: full hit chance formula

**What the game does** (0x1970b48..0x1971304; roll in 0x19740c0):

```
// hit chance is kept in PERCENT units: base*100 and min*100 (imul ..., 0x989680 = 100.0)
hc  = COMBAT_BASE_HIT_CHANCE * 100
if target is submarine (light slot = depth charges): hc *= DEPTH_CHARGES_HIT_CHANCE_MULT
r   = targetProfile / weaponProfile           // weaponProfile = GUN_HIT_PROFILES[type], or DEPTH_CHARGES_HIT_PROFILE vs subs
p   = r * r                                   // SQUARED (imul rcx, rcx at 0x19711d1)
p   = (p < MIN_HIT_PROFILE_MULT) ? MIN_HIT_PROFILE_MULT : min(p, 1.0)
hc  = hc * p
om  = 1 + COMBAT_LOW_ORG_HIT_CHANCE_PENALTY * (1 - shooterOrgRatio)
        + COMBAT_LOW_MANPOWER_HIT_CHANCE_PENALTY * (1 - shooterManpowerRatio)   // manpower term only for ships
hc  = hc * max(0, om)
hc  = hc * M                                  // M = additive modifier sum, see S-HIT-MODS (NOT clamped)
hc  = max(hc, COMBAT_MIN_HIT_CHANCE * 100)    // no upper clamp
HIT  iff  100 * U < hc                        // U from 0x2233db0, i.e. U < hc/100 (strict)
```

- `MIN_HIT_PROFILE_MULT` is the lower clamp of the squared profile ratio (vanilla 0.0, so no effect). The upper clamp is 1.0.
- `COMBAT_MIN_HIT_CHANCE` is applied **last**, after every multiplier. Vanilla value is **0.005** (0.5%).
- **No cap at 100%.** A summed modifier can push `hc` above 1, which is then a guaranteed hit.
- `M` is not clamped. A negative `M` gives a negative `hc`, which becomes the minimum.

**Evidence.** 0x1970dda / 0x19710eb / 0x1970ed0 (`imul rcx, [BASE_HIT], 0x989680`), 0x1970dff (DC mult), 0x1971185-0x1971209 (ratio, square, clamp), 0x1971212-0x1971293 (org/manpower), 0x197129c-0x19712c2 (`max(0,om)`, then `*rsi`), 0x19712de-0x1971304 (min). Roll: 0x1974343 `call 0x2233db0`; 0x1974348 `imul rcx, [rax], 0x64`; `cmp [rbp-0x38], rcx; jle miss`.
Shooter org ratio = 0xc39530 (`[ship+0x700] / maxOrg`), manpower ratio = 0xc37ae0 (`[+0x808]/[+0x80c]`, clamped to [0,1], 1 if max is 0). Both are the **shooter's**.

**Verdict.** DIFFERS:
1. Org and manpower penalties are **added** into one factor (`1 - 0.5·missingOrg - 0.25·missingMp`, floored at 0). They are not two separate multipliers.
2. All other modifiers are one **additive** sum `M` (S-HIT-MODS), not a product.
3. There is no clamp to 1.
The squared profile ratio, its clamp, and the min applied last MATCH the spec's shape.
**Confidence.** High.

## S-HIT-BASE

`COMBAT_BASE_HIT_CHANCE` (0.1). Depth charges (light slot vs a submarine target) multiply by `DEPTH_CHARGES_HIT_CHANCE_MULT` (1.1). MATCHES. Note the percent scaling (×100) used internally. It is harmless, but it means hit chance has 1e-7 resolution in absolute terms. **Confidence.** High.

## S-HIT-SHIP: target hit profile (0xc37006)

```
s    = speed(target) * HIT_PROFILE_SPEED_FACTOR          // speed = 0xc3a740 (current speed incl. naval_speed_factor etc.)
vis  = target is sub (bit7 of [ship+0x628]) ? subVisibility[+0x1d0] : surfaceVisibility[+0x1c8]
vis  = max(0, vis * (1 + naval_visibility_factor(target)))     // modifier 0x19f on the target
v    = vis * HIT_PROFILE_MULT * shipHitProfileMult[+0x678]       // per-ship multiplier, see define comment "Ship Hit Profile Mult"
prof = v / (max(s, 0.1) + HIT_PROFILE_SPEED_BASE)                // 0x2710 = 0.1 floor on the speed term
```
Convoys: `CONVOY_HIT_PROFILE` (0x1ad7f00 reads data 0x3334310).

**Verdict.** MATCHES the spec's formula, with three NEW details:
- the `naval_visibility_factor` modifier is applied to visibility;
- a per-ship `[ship+0x678]` hit-profile multiplier (source stat not identified, presumably 1.0 by default);
- the speed term has a 0.1 floor.

The sub/surface visibility choice comes from a ship flag. I believe this flag is "is submarine" (medium).
**Confidence.** High for the formula, medium for what `+0x678` is.

## S-HIT-WEAPON

`GUN_HIT_PROFILES[type]` with type 0 heavy = **70** (1.19 vanilla; the spec text says 80, while the engine default is already 70), 1 torpedo = 100, 2 light = 45. Vs submarines: `DEPTH_CHARGES_HIT_PROFILE` (100). Evidence: 0x197118a-0x19711a7 (`cmovne` picks the DC profile when the target is a sub). **Verdict.** MATCHES the order. Fix the spec value 80 → 70. **Confidence.** High.

## S-HIT-MODS

`M` is built as one sum (0x1970b6c-0x1970bea and per-type tail 0x1970e4d / 0x1970f1e / 0x1971139):

```
M = 1
  + naval_hit_chance(shooter member)                 // mod 0x28 via 0x1972570
      // 0x1972570 adds, for shooter box 2 (battle line) or 3 (carrier group):
      //   sideScreening[side+0x148] * screening_bonus.naval_hit_chance   (static modifier, vanilla 0.4)
      //   box 3 also: (1-(1-scr)(1-capScr[side+0x150])) * capital_screening_bonus.<id>  (no hit-chance entry in vanilla)
  + naval_hit_chance_against_a_country(shooter country vs target country)   // mod 0x1c7, ships only
  + shipStat_weaponHitFactor(shooter)                // light [+0x248], torpedo [+0x258], heavy [+0x250]  (equipment/design stat)
  + naval_<light|torpedo|heavy>_gun_hit_chance_factor(shooter member)  // mod 0x1c0 / 0x1bf / 0x1c1
```
- Depth charges use the **light-gun** factors (`[+0x248]`, mod 0x1c0).
- Screening: the 0.4 is **not hard-coded**. It is the `naval_hit_chance = 0.4` entry of static modifier `screening_bonus` (common/modifiers/00_static_modifiers.txt). The struct slot `+0x100` is bound to `screening_bonus` in 0x60ed2a, and `+0x108` to `capital_screening_bonus`. It applies to boxes 2 and 3 (battle line and carrier group, the latter including convoys), **once** per shot (only modifier id 0x28 has a `screening_bonus` entry), and **additively** inside `M`.
- Night and weather: no dedicated code in the shot function. They are static modifiers that add `naval_hit_chance`: `night` (−0.25 × darkness, "multiplied by amount of darkness"), `weather_rain_light` −0.05, `weather_rain_heavy` −0.2, `weather_snow` −0.05, `weather_blizzard` −0.1. They reach `M` through the generic modifier gather (0xc38760, which also takes the battle location `[side+0x130]`) as part of mod 0x28. Admiral skills/traits, doctrines and spirits enter the same way (`naval_hit_chance` and the per-weapon factors). In vanilla the admiral skill levels have `naval_hit_chance` commented out.
- `COMBAT_EVASION_TO_HIT_CHANCE`: no real naval combat reader (the only hit is a +8 false positive). It is dead.

**Verdict.** DIFFERS:
1. Additive, not multiplicative.
2. Screening is `+0.4·screening` inside the sum.
3. Night is `−0.25 × darkness` added into the same sum, not a ×0.75 step from 20:00 to 06:00.
4. `naval_hit_chance` and the per-weapon factors are separate terms of one sum.

**Confidence.** High for the structure and screening. Medium for night/weather (I traced the generic modifier path but not the darkness value computation).

---

## S-DMG-PIERCE: piercing tier (0x196fd60)

```
ratio = (armor == 0) ? 100.0 : piercing / armor      // truncating fixed division
for i in 0 .. count-1:                                // count = size of NAVY_PIERCING_THRESHOLDS (data 0x33399a4, asserted equal to table size)
    if ratio >= NAVY_PIERCING_THRESHOLDS[i]: return table[i]
return table[0] + 1.0                                 // fallback, unreachable in vanilla (last threshold 0)
```
- It is called twice with the **same index rule**: `table = NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES` (damage factor) and `NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES` (crit factor).
- Armor 0 hits index 0, which gives damage 1.0 and crit factor **2.0**.
- Armor is the target's (0xc3a520). Piercing is the shooter's light (0xc3a5c0) or heavy (0xc3a570) piercing.
- Torpedoes skip the lookup (piercing set to the sentinel −1.0).
- **Depth charges DO use it**: the depth-charge damage (`dcAttack × DEPTH_CHARGES_DAMAGE_MULT`, 0x1970c79) goes through the light-branch lookup with the shooter's light piercing vs the sub's armor. The DC crit chance also uses the crit-values tier.

Extra factor (0x197132c-0x1971416), slot `+0x30`:
- If `piercing != −1` and `armor >= piercing`: `f = clamp(1 + (1 − piercing/armor) × COMBAT_ARMOR_PIERCING_DAMAGE_REDUCTION, 0, 1)`. Vanilla define is 0 ("deprecated"), so f = 1. The code is still live for mods.
- For torpedoes: `f = 1 − (naval_torpedo_damage_reduction_factor(target, mod 0x1c3) + targetShip[+0x260])`.
- Otherwise there is no factor (sentinel −1).

The damage after randomness is multiplied by `f` (0x1974419-0x197444b).

**Verdict.**
- Tier selection: MATCHES (first `i` with `ratio >= threshold`, the same `i` for both tables).
- DIFFERS: depth charges do not ignore armor; they use light piercing vs sub armor.
- NEW: armor 0 is ratio 100, which gives crit factor 2.
- NEW: the torpedo damage reduction is additive between the modifier and the ship stat.
- NEW: the (vanilla-inert) `COMBAT_ARMOR_PIERCING_DAMAGE_REDUCTION` term.

**Confidence.** High.

---

## S-CRIT: critical hits

Built in 0x1970b48, finalised and rolled in 0x19740c0.

Guns (heavy, light, depth charges):
```
rel        = min(1, targetReliability)                 // a3b460: [ship+0x328]; convoys 0x1ad8180
critChance = COMBAT_BASE_CRITICAL_CHANCE * (1 - rel)
critChance = critChance * NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES[tier]
critMult   = 1 + (1 - rel) * COMBAT_CRITICAL_DAMAGE_MULT          // NOT (MULT - 1)
```
Torpedoes:
```
critChance = max(0, COMBAT_TORPEDO_CRITICAL_CHANCE
                 * (1 + naval_torpedo_enemy_critical_chance_factor(target member, mod 0x1c2) + targetShip[+0x268]))
critMult   = COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT                  // no reliability scaling
```
All weapons, in 0x19740c0 (0x19741e2-0x19742de):
```
critChance *= 1 + naval_critical_score_chance_factor(shooter, 0x1ba)
                + naval_critical_score_chance_factor_against_a_country(shooter vs target, 0x1d3)
                + naval_critical_receive_chance_factor(target, 0x18c)
```
Roll order per shot:
1. hit (`100·U < hc`);
2. damage randomness `1 + (U − 0.05) × COMBAT_DAMAGE_RANDOMNESS` (0x197438a), clamped at ≥0;
3. **only if critChance > 0**: `crit iff U <= critChance` (inclusive, 0x197440a `cmovle`);
4. on a crit vs a ship (not convoy, and `0xc3b130(ship)` false): part chance `pc = CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT × targetShip[+0x568]`. Then `pc / rel` if `rel > 0`: if that is ≥ 1, the part is damaged; else damaged iff `U < pc/rel`. If `rel <= 0`, the part is damaged directly. Then `0xc34500` applies the part damage and gets the STR/ORG damage by pointer.
   - If `0xc34500` returns true (a part was damaged), **the regular crit multiplier is NOT applied**.
   - Otherwise (no part, convoy target, part roll failed): STR damage and ORG damage are both multiplied by `critMult` (0x197462f-0x197466d).

- `COMBAT_ARMOR_PIERCING_CRITICAL_BONUS` has no surface reader. Its only reader is the air strike at 0x197ac80 (air area). The surface "piercing crit bonus" is purely the CRITICAL_VALUES tier (2.0 at ratio ≥ 2 or armor 0).
- Reliability is the **target's** (confirms the spec assumption).

**Verdict.** DIFFERS:
1. Gun crit multiplier is `1 + 5·(1−rel)` (spec/engine `1 + 4·(1−rel)`); at rel 0.8 that is 2.0, not 1.8.
2. Depth-charge crits use the real tier factor, not 1.
3. The extra additive crit-chance modifiers (score / against-country / receive) apply to all weapons, torpedoes included.
4. Crit uses `U <= p` (negligible).
5. S-CRIT-PART: the part chance has the extra target factor `[+0x568]` and no 0.01 reliability floor. A successful part hit replaces the generic crit multiplier.

Torpedo chance and multiplier otherwise MATCH.
**Confidence.** High for the formulas. Medium for the meaning of `[+0x568]` and `0xc3b130`, which are UNRESOLVED (probably a ship stat or modifier scaling part-damage chance, and an "is convoy-like / no parts" test).

---

## Related (outside my area, seen in passing)

Damage scale in 0x19740c0: `damage × max(0, 1 − X + naval_damage_factor(shooter, 0x1b7) − naval_defense_factor(target, 0x1b8))`, where `X` comes from `0x161ed30(side, side+0x158)`, probably the positioning penalty (S-POS-DMG). Org loss uses `(1 − targetHP/maxHP)` (0xc3ac60) as in the spec.

## Recommended engine changes

1. `HitChanceRules.Modifier`: return `max(0, 1 + orgPen·(1−orgRatio) + mpPen·(1−mpRatio)) × (1 + naval_hit_chance_sum + 0.4·screening [battle line & carrier group, from static modifier screening_bonus] + night/weather naval_hit_chance + weaponHitFactor(design) + weaponHitFactor(modifiers))`. The second factor is a sum (all additive) and must not be clamped. Depth charges use the light-gun factor.
2. `HitChanceRules.Calculate`: `final = max(base × profileFactor × orgMpFactor × M, COMBAT_MIN_HIT_CHANCE)` with no upper clamp. Roll `hit = U < final` (a chance ≥ 1 always hits).
3. Night: replace the 20:00–06:00 ×0.75 step with `+(-0.25 × darkness)` added into `M` (darkness model still to be decided; keep a `BattleRules` variant). Weather: add the static-modifier values to `M`.
4. `ShipHitProfile`: apply `naval_visibility_factor` to visibility, floor the speed term at 0.1 before adding `HIT_PROFILE_SPEED_BASE`, and keep a per-ship hit-profile multiplier (default 1).
5. `DamageRules.RollCritical` (guns and depth charges): multiplier `1 + COMBAT_CRITICAL_DAMAGE_MULT × (1 − rel)`. Depth charges use `NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES[tier]` with tier = light piercing vs sub armor. Multiply every crit chance (torpedo too) by `(1 + crit_score_chance + crit_receive_chance [+ against-country])`. Torpedo chance factor is `1 + modifier + ship stat` (additive). Roll `crit = chance > 0 && U <= chance`.
6. Depth-charge damage: `dcAttack × DEPTH_CHARGES_DAMAGE_MULT × NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[tier(lightPiercing / subArmor)]`.
7. Piercing tier: an armor of 0 gives index 0 (ratio 100). Fallback when nothing matches is `table[0] + 1` (mod-only edge).
8. S-CRIT-PART: part chance `CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT × partFactor / rel` (≥1 means always; rel ≤ 0 means always). When a part is hit, do not also apply the crit multiplier.
9. Spec text: `COMBAT_MIN_HIT_CHANCE` is 0.005 (not 0.05), and heavy `GUN_HIT_PROFILES` is 70 (not 80). Remove the `COMBAT_EVASION_TO_HIT_CHANCE` "open" item (no reader). Mark `COMBAT_ARMOR_PIERCING_CRITICAL_BONUS` as air-only.
10. RNG granularity (optional, for exactness): the game's U is an integer 0..99999 / 1e5, and every product is truncated toward zero at 5 decimals.
