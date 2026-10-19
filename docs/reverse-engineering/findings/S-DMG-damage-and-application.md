# Findings: damage of a hit and how it is applied (HOI4 1.19.3)

Area: S-DMG, S-DMG-PIERCE (damage side), S-DMG-RANDOM, S-DMG-APPLY, S-POS-DMG (damage side), S-CRIT (multiplier side), S-CRIT-PART, S-XP (manpower and experience loss).

## Conventions and helpers

- Fixed point: int64 with scale 100000. `a*b` is computed as `imul` then a magic-number divide by 1e5 (`0x29f16b11c6d1e109`, `sar 0xe`, add sign bit). That is **truncation toward zero**. Division `a/b` is `a*100000 idiv b`, also truncation. If the divisor is 0 or the value would overflow, the result is the raw constant `0xffffffff` (about 42949.67).
- Fixed to int (`0x24ed730`): adds ±0.5 and then truncates, so it **rounds half away from zero**.
- `0x24ef6f0(out, x)` stores `-x`. The code uses `-1.0` (raw -100000) as a "no value" sentinel.
- RNG `0x2233db0` (`random_fixed`, clausewitz `random.cpp`): an integer hash of the global seed and a counter, masked to 31 bits, then `% 100000`. It returns a fixed-point **U in {0, 0.00001, …, 0.99999}**, uniform on [0,1). Every call takes a (file, line) tag. The damage path uses line tags 0x587 (hit), 0x593 (damage randomness), 0x59f (crit) in naval_fire_exchange_member.cpp, and 0x2f4 (part chance) and 0x78f (part pick) in ship.cpp.
- I named modifier indices from the modifier registration function `0x55f7f0`, which calls `0x5574d0(token, index, …)` after loading the `MODIFIER_*` string (table in `scratch_dmg_modidx.json`). The ones used here: 0x1b7 NAVAL_DAMAGE_FACTOR, 0x1b8 NAVAL_DEFENSE_FACTOR, 0x1ba NAVAL_CRITICAL_SCORE_CHANCE_FACTOR, 0x1bb NAVAL_CRITICAL_EFFECT_FACTOR, 0x18c NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR, 0x1d3 NAVAL_CRITICAL_SCORE_CHANCE_FACTOR_AGAINST_A_COUNTRY, 0x1c2 NAVAL_TORPEDO_ENEMY_CRITICAL_CHANCE_FACTOR, 0x1c3 NAVAL_TORPEDO_DAMAGE_REDUCTION_FACTOR, 0x1a0 NAVAL_CASUALTY_ON_HIT_FACTOR, 0x1a1 NAVAL_CASUALTY_ON_SINK_FACTOR.
- Ship fields (ship.cpp object): +0x6f8 HP, +0x308 max HP (always used as `max(maxHP, 0.001)`), +0x700 org, +0x708 experience, +0x710 experience cap, +0x808 manpower (int32), +0x80c max manpower (int32), +0x328 reliability (inferred, see S-CRIT), +0x568 `critical_part_damage_chance_mult` (inferred), +0x550 sub-unit critical parts, +0x918 list of damaged parts (part, count), +0x260 / +0x268 ship-level torpedo damage reduction / torpedo enemy crit chance (inferred from use).

## Pipeline overview

`0x1970a90` (the same function continues at 0x1970b48; I call it the *shot profile*) builds a struct per (shooter, target, weapon type). It is called once per call of the hit loop `0x19740c0`. Struct fields: +0x00 damage, +0x08 crit chance, +0x10 crit multiplier, +0x18 hit chance, +0x20 piercing, +0x28 target armor, +0x30 damage-reduction factor (or -1), +0x38 cooldown.
Weapon type (arg r8d): **0 = heavy, 1 = torpedo, 2 = light** (light becomes depth charge against a submarine). Evidence: type 2 uses MODIFIER_NAVAL_LIGHT_GUN_HIT_CHANCE_FACTOR (0x1c0), type 0 uses ..._HEAVY_... (0x1c1), and type 1 uses the torpedo crit defines.

`0x19740c0` then loops `while charge[weapon] >= cooldown { charge -= cooldown; fire one shot }`, where the charge lives at `member+0xf0+8*weapon`. Every shot in the loop uses the same profile. The function returns 1 once the target sinks.

---

## S-DMG (+ S-DMG-PIERCE damage side): base damage of a hit

**What the game does.**

Shot profile (`0x1970b48`):
- Heavy (type 0): `dmg = HeavyAttack(shooter)` (`0xc3a580` → `0xc34a50` type 1). If the target is a submarine, `dmg = 0` (0x1970fc4). Otherwise `dmg = trunc(dmg × PierceFactor(heavyPiercing, targetArmor))`.
- Light (type 2): `dmg = LightAttack(shooter)` (`0xc3a5d0`). If the target is a submarine, `dmg = trunc(DepthChargeAttack × DEPTH_CHARGES_DAMAGE_MULT)` (0x1970c79). **In both cases** the damage is then multiplied by `PierceFactor(lightPiercing, targetArmor)` (the code falls through from 0x1970cbb into the 0x196fd60 call). So depth charges are **not** armor-exempt. They use the shooter's light piercing against the submarine's armor.
- Torpedo (type 1): `dmg = TorpedoAttack(shooter)` (`0xc3ac00`). No piercing factor (0x196fd60 is never called), and piercing is set to the sentinel -1.
- `PierceFactor(p, a)` (`0x196fd60`): `ratio = (a == 0) ? 100 : trunc(p/a)`. It returns `NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[i]` for the first `i` with `ratio >= NAVY_PIERCING_THRESHOLDS[i]`. If no threshold matches, it returns `VALUES[0] + 1`, which cannot happen with vanilla's last threshold of 0.
- Damage-reduction factor (+0x30), 0x1971338..0x1971416:
  - torpedo: `1 − (target.mod[NAVAL_TORPEDO_DAMAGE_REDUCTION_FACTOR] + targetShip[+0x260])`, **not clamped**;
  - gun or depth charge, when `armor >= piercing`: `clamp(1 + (1 − p/a) × COMBAT_ARMOR_PIERCING_DAMAGE_REDUCTION, 0, 1)`. Vanilla sets the define to 0 ("deprecated"), so the factor is exactly 1;
  - otherwise: the sentinel -1, meaning "no factor".
  - Quirk: a gun whose piercing is exactly the sentinel value takes the torpedo branch. This never happens in practice.
- The attack getter `0xc34a50` returns `baseAttack × (1 + Σ additive factors)`, clamped ≥ 0. The sum includes navy_{capital_ship,screen,submarine}_attack_factor (plus the _against_a_country versions), NAVAL_NIGHT_ATTACK × night factor, **NAVAL_DAMAGE_FACTOR (0x1b7)** (0xc34e3b), the admiral and pride-of-the-fleet terms (0x1d1), and an out-of-fuel penalty of `OUT_OF_FUEL_ATTACK_FACTOR` (torpedo: `OUT_OF_FUEL_TORPEDO_FACTOR`) × (1 − fuel ratio). I did not decode this getter fully. It belongs to the stats area.

Hit loop (`0x19740c0`), computed once before the shot loop:
- `posPenalty = DAMAGE_PENALTY_ON_MINIMUM_POSITIONING − trunc(DAMAGE_PENALTY_ON_MINIMUM_POSITIONING × positioning)`, where positioning is the shooter's combatant `+0x158` (`0x161ed30`).
- `dmgMult = 1 − posPenalty + shooter.mod[NAVAL_DAMAGE_FACTOR] − target.mod[NAVAL_DEFENSE_FACTOR]` (0x19741c3..0x19742a2, through the member stat getter `0x1972570`, ids 0x1b7 and 0x1b8). The terms are **additive**.
- `base = max(0, trunc(dmg × dmgMult))`.

Per shot, after a successful hit:
1. `d = trunc(base × randFactor)` (S-DMG-RANDOM). If `d <= 0`, skip the shot.
2. Crit roll (S-CRIT).
3. If the +0x30 factor is not -1: `d = trunc(d × factor)`. This is the torpedo damage reduction.
4. Split into HP and org, then the crit multiplier (S-DMG-APPLY).

So the order is: `attack(with additive factors) → ×pierce → ×(1 − posPen + dmgF − defF) → ×random → ×torpReduction → ×STR / ×(1−hpRatio)×ORG → ×crit`. Every step truncates to 1e-5.

**Evidence.** 0x1970c79, 0x1970cbb..0x1970d38, 0x1970f90..0x197104d, 0x196fd60..0x196fe73, 0x1971338..0x1971416, 0x19741a8..0x1974304, 0x1974419..0x197444b, 0x161ed30.

**Verdict.**
- Piercing tiers: MATCHES for guns.
- Depth charges: **DIFFERS**. They are not armor-exempt: `DC × 0.7 × PierceFactor(lightPiercing, subArmor)`. They also take a tier crit factor (see S-CRIT).
- Torpedoes ignore armor: MATCHES. Torpedo reduction is not clamped at 0: minor difference.
- Positioning: **DIFFERS in combination**. The penalty is additive with naval_damage_factor and the target's naval_defense_factor inside one `(1 − pen + dmg − def)` multiplier, clamped ≥ 0. It is not a separate multiplicative factor. With no modifiers it is identical to the spec.
- NEW: shooter NAVAL_DAMAGE_FACTOR and target NAVAL_DEFENSE_FACTOR enter here. NAVAL_DAMAGE_FACTOR also appears inside the attack getter `0xc34a50`. Both calls go to the ship modifier sum `0xc38760`, so the modifier looks **applied twice** (once in the attack value, once in the hit multiplier). Confidence medium-low: I did not confirm that the two `0xc38760` contexts return the same value.
- Experience damage factor (spec xpFactor): **UNRESOLVED**. I found no experience term in `0x1970b48` or `0x19740c0`. If it exists, it is inside the attack getter or the modifier sum (`0xc38760` → `0xd6fa50` task-force modifiers). The S-XP [LEGACY] level table cannot be confirmed from this path.

**Confidence.** High for the order, truncation and positioning combination. High for the depth-charge piercing factor. Medium for the double NAVAL_DAMAGE_FACTOR.

## S-DMG-RANDOM

**What the game does.** `randFactor = 1 + COMBAT_DAMAGE_RANDOMNESS × (U − 0.05)`, where U is `random_fixed` in [0, 0.99999] with a 1e-5 step:
```
01974390  sub  rcx, 0x1388           ; U - 0.05
01974397  imul rcx, [COMBAT_DAMAGE_RANDOMNESS]
...       add  rdx, 0x186a0          ; + 1.0
```
With R = 0.5 the range is **[0.975, 1.474995]**, mean 1.225. The game rolls it once per hit, after the hit roll and before the crit roll. It multiplies the HP and org damage alike, because it is applied before the split.
`0xd1b800` does **not** read COMBAT_DAMAGE_RANDOMNESS: it reads 0x3334d70, the neighbouring global (the who.py `(+8)` false positive). `0x19740c0` is the only reader.

**Verdict.** MATCHES `BattleRules.DamageRandomness = WikiDecoded`. `DefineComment` is wrong for 1.19.3 and can be dropped or kept only as a variant. The engine draws the random factor before the crit roll in `Calculate`; the RNG order is listed below.
**Confidence.** High.

## S-DMG-APPLY: HP and organization

**What the game does** (0x197444e..0x19744fb, then `0xc3e0e0`):
- `hpRatio = trunc(HP / max(maxHP, 0.001))` of the target **before this shot** (`0xc3ac60`). It is recomputed every shot, so earlier shots in the same loop count.
- `strDmg = trunc(d × COMBAT_DAMAGE_TO_STR_FACTOR)`
- `orgDmg = trunc(trunc((1 − hpRatio) × d) × COMBAT_DAMAGE_TO_ORG_FACTOR)` (the ORG define is read at 0x1974453 with `mov rax, [..]`, which who.py missed)
- The crit or part step then modifies both (S-CRIT, S-CRIT-PART).
- Apply (`0xc3e0e0(ship, strDmg, orgDmg, flag=0)`): experience and manpower loss first (S-XP), then `HP = clamp(HP − strDmg, 0, maxHP)`, then `org = clamp(org − orgDmg, 0, maxOrg)` (maxOrg from `0xc37e70`).
- Bookkeeping: `0x1620fa0(..., min(strDmg, hpBefore), 6)` records damage-dealt stats. `0x15c6250` adds the actual HP lost (`hpBefore − hpAfter`) to a per-attacker damage record, used for kill or assist credit.
- Sinking: `0xc3e680` returns sunk when `HP <= 0`, unless the ship has the +0x738 link. Then the damage record gets `+0x48 = 1`, and `0x1975480` hands out kill or assist credit (it reads MIN_TRACTED_ASSIST_DAMAGE_RATIO). The hit loop returns 1, so the shooter stops firing at the dead target. If the target survives and lost HP, `0x196fa40` records the damage.
- Special case: if ship +0x738 holds a valid handle (role unresolved), `0xc3e0e0` instead forwards `strDmg/maxHP` to that object and sets `HP = maxHP × object ratio`, with org set to 0. Part damage is also skipped (`0xc3b130`). A target that is not a ship (member handle +0x10, probably a convoy) uses `0x1ad81e0`, `0x1ad8610` and `0x1ad8680` instead. I did not decode those.

The wiki example works out: a 100 HP / 50 org ship hit for 50 loses 30 HP each time, and org 0, then 15, then 30.

**Verdict.** MATCHES the spec formula, including the org scaling by HP already lost. Details the engine should copy: org damage uses `d` before the crit split and is then multiplied by the crit multiplier too; manpower and experience use the unclamped `strDmg` (see S-XP); every step truncates.
**Confidence.** High.

## S-POS-DMG (damage side)

`penalty = DAMAGE_PENALTY_ON_MINIMUM_POSITIONING × (1 − positioning)` (`0x161ed30`; the positioning value is combatant +0x158, computed in `0x161b960`). It is subtracted inside `1 − penalty + naval_damage_factor − target naval_defense_factor` (see S-DMG). **DIFFERS** from the spec's separate multiplicative factor only when the modifiers are non-zero. Confidence high.

## S-CRIT (multiplier and chance as seen in the damage path)

Shot profile:
- Guns and depth charges: `rel = min(target[+0x328], 1)`.
  - `critChance = trunc(trunc(COMBAT_BASE_CRITICAL_CHANCE × (1 − rel)) × NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES[tier])`, where `tier` comes from `0x196fd60` with the same piercing and armor. For depth charges that means light piercing against submarine armor.
  - `critMult = 1 + (1 − rel) × COMBAT_CRITICAL_DAMAGE_MULT` (0x1970db2..0x1970dd2).
- Torpedo: `critChance = COMBAT_TORPEDO_CRITICAL_CHANCE × max(0, 1 + target.mod[NAVAL_TORPEDO_ENEMY_CRITICAL_CHANCE_FACTOR] + targetShip[+0x268])` (0x1970f23..0x1970f67, 0x1971309..0x1971327). `critMult = COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT` flat, with no reliability term.

Hit loop: `critChance ×= 1 + shooter.mod[NAVAL_CRITICAL_SCORE_CHANCE_FACTOR] + shooterCountry.mod[NAVAL_CRITICAL_SCORE_CHANCE_FACTOR_AGAINST_A_COUNTRY](target country) + target.mod[NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR]` (0x19741e2..0x19742ca). The roll is a crit when `critChance > 0 and U <= critChance` (0x197440a, `cmovle`). It is rolled only on hits with `d > 0`, after the damage-randomness roll.

On a crit with no part damaged: `strDmg ×= critMult; orgDmg ×= critMult` (0x197462f..0x197466d).

**Verdict.**
- Gun crit multiplier: **DIFFERS**. It is `1 + 5 × (1 − rel)`, not `1 + 4 × (1 − rel)`. At reliability 0.8 that gives 2.0 rather than 1.8.
- Depth-charge tier factor: **DIFFERS**. It is the real tier factor (light piercing against sub armor), not 1.
- Crit-chance modifiers: NEW. They are additive in one `(1 + …)` term.
- Torpedo crit: MATCHES, with the extra ship-level term.
- The +0x328 field is identified as reliability from how it is used: clamped to ≤ 1, used as `(1 − x)`, and the part chance divides by it.

**Confidence.** High for the formulas. Medium that +0x328 is reliability.

## S-CRIT-PART: critical part damage

**What the game does.**
Chance (0x1974511..0x19745f1), evaluated on each crit:
- If the target has no ship object, or has the +0x738 link, there is no part damage and the normal crit multiplier applies.
- `p = trunc(CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT × ship[+0x568])`, where +0x568 is very likely the sub-unit's `critical_part_damage_chance_mult` (vanilla 1 for all hulls).
- If `rel > 0`: `p = p / rel`. If `rel <= 0`, or `p >= 1`, a part is damaged without a roll. Otherwise a part is damaged when `U < p` (roll tagged ship.cpp line 0x2f4).
- A counter at stats +0x124 counts crits.

Part damage `0xc34500(ship, &strDmg, &orgDmg)`:
- Candidates: the ship's sub-unit `critical_parts` (ship +0x550), plus the `critical_parts` of every module in every design slot (slot list +0x60 → module → +0xb8 → +0x240). `0xc31030` merges each candidate into a list of 16-byte entries `{part*, int maxInstances}`:

```
merge(list, part):                                  // 0xc31030
    for entry in list:                              // 0xc31052 loop
        if entry.part == part:                      // found (0xc31125)
            if entry.max > -1:
                entry.max += part.damage_instance_per_added_module   // +0x174
                if part.max_damage_instance > -1:                    // +0x178
                    entry.max = min(entry.max, part.max_damage_instance)
            return
    list.append({part, part.base_damage_instance})  // +0x170 (0xc31064)
```

  So a part listed by several modules is one candidate (one weight in the pick), not one per module. [CAL] Only the merged list fits the logged picks: with one entry per module the heavy-gun share of a 4-battery SHBB would be several times larger.
- Slot order [CAL]: the slot list +0x60 is walked in the order of the slot **names'** first appearance in `common/units/equipment/*.txt` (files in load order; the names are interned when the files are read), not in the hull's own slot order. For TFB over vanilla: `fixed_ship_anti_air_slot`, `fixed_ship_radar_slot`, `fixed_ship_engine_slot`, `fixed_ship_secondaries_slot`, `front_1`, `mid_1`, `mid_2`, `rear_1`, `mid_3`, `rear_2`, `fixed_ship_armor_slot`, `front_2`, `fixed_ship_battery_slot`, `fixed_ship_fire_control_system_slot`, `fixed_ship_torpedo_slot` (`_custom_slot` left out). All 26 part picks of 18 logged TFB battles fit this order; the hull order gave two wrong picks (an SHBB's heavy guns at 5709, a light cruiser's secondaries at 5657). The walk itself was not traced.
- Part defaults (constructor `0x1019290`, from `0xac3130`): +0x148 str_damage_multiplier 1, +0x150 org_damage_multiplier 1, +0x158 str_damage 0, +0x160 org_damage 0, +0x168 chance 1 (all fx5), +0x170 base_damage_instance 1, +0x174 damage_instance_per_added_module 0, +0x178 max_damage_instance −1.
- A candidate is dropped if it is already damaged `count >= maxInstances` times (the damaged list is ship +0x918; -1 means unlimited).
- Weighted pick: `r = trunc(U × Σ chance)` (roll tagged ship.cpp 0x78f). Walk the candidates in order with `r −= chance` and pick the first that takes `r` below 0 (falls back to the last candidate). `chance` is the part's weight field, +0x168.
- Damage: `strDmg = trunc(strDmg × str_damage_multiplier) + str_damage`, and `orgDmg = trunc(orgDmg × org_damage_multiplier) + org_damage` (part fields +0x148/+0x158 and +0x150/+0x160). This **replaces** the crit multiplier: the caller skips the crit multiplier when `0xc34500` returns true (0x197461f).
- The part is added to (or its count incremented in) ship +0x918. `0xc3d090(ship, 1)` then recomputes the ship stats, and the task-force refresh is called.
- If there are no candidates, the function returns false and the normal crit multiplier applies.
- Part effects come from data (`common/units/critical_parts/00_critical_parts.txt`): `stat_penalties` are factors on the ship's stats, `stat × max(0, 1 + count × penalty)` (0xc3d340); `modifier` adds to the ship's modifiers. [CAL] In the logged battles the penalties show on every stat checked: the attacks (heavy ×0.5, secondaries' light ×0.75) and a short-circuited radar's surface detection (−7: 0; the enemy's positioning rose by that share of the detection difference) and hit chance factors (×0.975). An earlier reading that only the attacks take them came from part picks made in the wrong slot order. In the modifier sum `0xc38760` (0xc38b21..0xc38be2), each damaged part contributes `modifierValue × count × (1 + NAVAL_CRITICAL_EFFECT_FACTOR)`. That is the only place NAVAL_CRITICAL_EFFECT_FACTOR is used here: it scales part effects, not crit damage.
- Vanilla parts. Hulls (DD, CL, CA, BB, BC) have `rudder_jammed, destroyed_ammo_storage, broken_propeller, on_fire`. Carriers have `damaged_flight_deck` instead of `rudder_jammed`. Submarines add `ballast_tanks_inoperable, leaking_pressure_hull, battery_leak`. Gun, torpedo, radar and similar modules add `damaged_light_guns`, `damaged_heavy_guns`, `damaged_secondaries`, `damaged_torpedoes`, `jammed_torpedo`, `shortcircuited_radar_N`, `fire_direction_breakdown`, `coolant_loss`, `broken_diesel`, `leaking_*` and others. Examples: `destroyed_ammo_storage` (chance 0.1, ×10 str and org, −70% hg/lg/torpedo attack); `on_fire` (org ×2 +5, navy_org_factor −0.5, naval_morale_factor −0.8); `broken_propeller` (chance 0.5, ×0.5, speed −90%); `rudder_jammed` (chance 0.25, ×0.25, retreat chance −90%, speed −50%).

**Verdict.** **MATCHES** (since 2026-10-19; `CriticalPartRules`, `ShipDesign.CriticalPartCandidates`, `Ship.DamagePart`): the chance with `critical_part_damage_chance_mult` and the skipped roll, the merged candidates in the game's slot order, the weighted pick, the replaced crit multiplier, the stat penalties on the attacks and the part modifiers × (1 + NAVAL_CRITICAL_EFFECT_FACTOR). The replayed `jukki_usa_vs_bc_japan` battle matches all 10,257 shots ([LOG](LOG-ingame-logging.md)).

**Confidence.** High for control flow, the multiplier replacement, the defaults and the merge. Medium for the field-to-key mapping (+0x148 to +0x168 matched to the file keys by role and defaults, then confirmed by the logged damage). The slot order is calibrated, not traced.

## S-XP: manpower and experience loss on damage (`0xc3e0e0`, flag = 0)

All of this uses the HP **before** the HP subtraction.
- Experience: `keep = clamp(HP − trunc(strDmg × NNavy.EXPERIENCE_LOSS_FACTOR), 0, maxHP)`, then `XP = min(trunc(XP × trunc(keep / HP)), XPcap)` (`ratio = 1` if HP <= 0) (0xc3e22d..0xc3e2c5). With the vanilla factor of 1, XP falls by the fraction of **current HP** this hit takes off. It does not depend on manpower.
- Manpower, applied only if `strDmg > 0` and `maxMP > 0`:
  - `f = trunc(trunc(strDmg / maxHP) × (1 + mod[NAVAL_CASUALTY_ON_HIT_FACTOR]))`. If `1 + mod <= 0`, there is no loss.
  - `floor = round(maxMP × MIN_MANPOWER_RATIO_TO_DROP)`.
  - If `MP > floor`: `loss = round(maxMP × f × MANPOWER_LOSS_RATIO_ON_STR_LOSS)`, then `MP −= min(loss, MP − floor)` (0xc3e2d4..0xc3e40f).
  - Rounding is half away from zero.
  - `strDmg` here is the post-crit, **unclamped** value, which can exceed the remaining HP.
- On sinking (taskforce code, `0xd77ff0` at 0xd782b4..0xd783b0, not combat-time): `ratio = clamp(MANPOWER_LOSS_RATIO_ON_SUNK × (1 + mod[NAVAL_CASUALTY_ON_SINK_FACTOR]), 0, 1)`. `returned = round(MP × (1 − ratio))` goes back to the country's manpower pool. The rest is lost.
- who.py missed the reads of MANPOWER_LOSS_RATIO_ON_STR_LOSS (0xc3e3d3) and MIN_MANPOWER_RATIO_TO_DROP (0xc3e390). Both are in `0xc3e0e0`.

**Verdict.**
- Manpower: **DIFFERS** in detail. The game uses unclamped `strDmg / maxHP` (not the actual HP lost), applies the `(1 + casualty_on_hit)` modifier and rounds half away from zero (not banker's rounding). The floor logic matches.
- Experience: **DIFFERS**. The engine's `XP −= XP × lostCrewShare × ELF` should be `XP × max(0, HP − ELF × strDmg) / HP`.

**Confidence.** High.

## RNG call order per shot (for scripted tests)
1. hit roll (0x587): hit if `hitChance > U` (hit chance is stored ×100; see the hit-chance findings);
2. damage randomness (0x593);
3. crit (0x59f), only if `critChance > 0` and `d > 0`;
4. part chance (ship.cpp 0x2f4), only on a crit when `p < 1` and `rel > 0`;
5. part pick (ship.cpp 0x78f), only when a part is damaged and candidates exist.

## Recommended engine changes
1. `DamageRules.Calculate`: restructure to the game order. `base = attack × pierce`, then `× max(0, 1 − P×(1−pos) + shooter.navalDamageFactor − target.navalDefenseFactor)`, then `× (1 + R×(U−0.05))`, then `× torpedoFactor` (torpedo only, `1 − reduction`, no clamp). Merge `PositioningRules.DamageFactor` into this additive term. Make `WikiDecoded` the default and only confirmed randomness model.
2. Depth charges: apply `PierceFactor(lightPiercing, subArmor)` and use the matching crit tier factor. Remove "depth charges ignore armor".
3. Split, then crit: `str = d × STR`, `org = d × (1 − hpBefore/maxHp) × ORG`, and on a crit multiply **both** by `critMult`. Guns: `critMult = 1 + COMBAT_CRITICAL_DAMAGE_MULT × (1 − rel)`. Torpedoes: flat ×2.
4. Crit chance: multiply by `1 + naval_critical_score_chance_factor (+ against-country) + target naval_critical_receive_chance_factor`. Roll after the damage randomness. Crit when `U <= chance`.
5. Critical parts: `p = CHANCE × criticalPartDamageChanceMult / rel` (no roll if `p >= 1` or `rel <= 0`). Pick a part by `chance` weight from the hull's and modules' `critical_parts`, respecting instance caps. Replace the crit multiplier with `×str_damage_multiplier + str_damage` and `×org_damage_multiplier + org_damage`. Keep the part on the ship and apply its `stat_penalties` and `modifier` × count × (1 + naval_critical_effect_factor) for the rest of the battle. Load the parts from `common/units/critical_parts/*.txt` and the `critical_parts` lists in unit and module files (milestone 3/4).
6. `ExperienceRules.ApplyManpowerLoss`: take the pre-clamp `strDmg` and the HP before the hit. `loss = roundAway(maxMP × strDmg/maxHP × (1 + casualtyOnHit) × 0.5)`, capped at `MP − roundAway(maxMP × 0.1)`, skipped if `MP <= floor`. Experience: `XP *= clamp(HP − ELF×strDmg, 0, maxHP) / HP`.
7. Optional fidelity: truncate to 1e-5 after each multiply (a `Fixed` helper), and draw U as `rng % 100000 / 1e5`.
8. Spec: update S-DMG, S-DMG-RANDOM (resolved), S-POS-DMG, S-CRIT (multiplier and modifiers), S-CRIT-PART and S-XP as above. Mark the experience damage factor and the double NAVAL_DAMAGE_FACTOR as open.

## Unresolved
- Where (or whether) the experience-level damage bonus applies. It is not in the hit path; check the attack getter `0xc34a50` and the modifier sum `0xc38760`/`0xd6fa50`.
- Whether NAVAL_DAMAGE_FACTOR is really counted twice (attack getter and hit multiplier).
- What ship +0x738 is (some linked object that changes damage application) and how damage to targets that are not ships works (`0x1ad8610`).
- How `0xc31030` aggregates part instance counts.
