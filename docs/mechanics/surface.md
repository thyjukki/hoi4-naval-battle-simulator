# Surface combat spec

Status: implemented (`src/NavySimulator.Engine/Battles/`) and checked against **hoi4.exe 1.19.3** by static analysis. Evidence for every [RE] item is in [`docs/reverse-engineering/findings/`](../reverse-engineering/findings/README.md). Tags are explained in [README.md](README.md). Numbers in parentheses are vanilla 1.19.3 values; the engine reads all of them from `GameDefines`, so loading other defines (or a mod) changes them.

"Tick" is the game's combat hour counter. It is 1 during battle hour 0, 2 during hour 1, and so on.

## S-HOUR: hour structure [RE]

Each hour:
1. Positioning (S-POS), screening (S-SCREEN) and submarine detection (S-SUB) for both sides, from the same pre-fire state. They are not recomputed after losses during the hour.
2. **Attacker's turn**, then **defender's turn** (S-ORDER). In a turn, every ship of the side, in fleet order:
   1. steps its submarine reveal timer (S-SUB);
   2. **Waiting**: activates if its line is active (S-ACTIVATE). Ships become *fighting*; convoys go straight to *retreating*. A ship does not fire in the hour it activates;
   3. **Fighting**: escape progress is reset; from tick `COMBAT_MIN_DURATION` (8) it may decide to retreat (S-RETREAT). A ship that starts retreating does not fire this hour. Otherwise it fires (S-COOLDOWN, S-TARGET);
   4. **Retreating**: fires, then advances its escape (S-RETREAT-SPEED).
   
   After its ships, the side's air phase runs (A-*, not implemented). If the enemy has only hidden submarines left (nothing the side can target) and no air acted, all of the side's ships start retreating.
3. Experience gain (S-XP), end check (S-END).

**Code.** `BattleEngine.cs`. Fire-exchange groups: with `COMBAT_MAX_GROUPS` (1) each side forms one group paired with the other side's, so groups have no effect in vanilla and are not modelled.

## S-ORDER: firing order [RE]

The attacker's whole side fires before the defender's, every hour. Damage applies immediately per shot, so a defender ship sunk by the attacker never fires that hour. Ships are processed in fleet (list) order, not shuffled. This gives the attacker a real first-strike edge; `BattleEngineTests.Simulate_MirrorMatchFavoursTheAttacker` measures it.

The previous `Interleaved` and `Simultaneous` variants are removed.

## S-POS: positioning [RE]

`positioning = clamp(BASE_POSITIONING - sizePenalty + detectionBonus, 0, 1)`, summed and clamped once.
- **S-POS-SIZE**: every ship in the battle counts 1, all lines and retreating ships included. If `own >= MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY` (4): `sizePenalty = clamp((own/enemy - 1) × HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR (0.25), 0, MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO (0.75))`.
- **S-POS-DETECT**: `bonus = (ΣownSurfaceDetection - ΣenemySurfaceDetection) / 100 × RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR (0.01)`, at most `MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION` (0.25), applied only if positive. Ten ships at 40 against ten at 20 gives only +0.02.
- **S-POS-DMG**: `DAMAGE_PENALTY_ON_MINIMUM_POSITIONING (0.5) × (1 - positioning)` is subtracted inside the additive damage multiplier (S-DMG).
- **S-POS-SCREEN**: see S-SCREEN.

**Not modelled** (no inputs for them yet): country/admiral positioning modifiers; the defender's initial-phase modifier during `COMBAT_INITIAL_DURATION`; the carrier-ratio penalty (needs air; formula in the air finding); the penalty for ships joining a running battle (`POSITIONING_PENALTY_FOR_SHIPS_JOINED_COMBAT_AFTER_IT_STARTS` with hourly decay); the convoy-escort dominance bonus. Carriers' detection should include `carrier_surface_detection` scaled by how full the deck is (needs air).

## S-SCREEN: screening efficiency [RE]

- `factor = max(0.01, 1 + screening_efficiency - SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING (0.5) × (1 - positioning))`.
- `screening = clamp(screens / ceil(3 × (capitals + carriers) + 0.5 × convoys) × factor, 0, 1)`; 1 if nothing needs screening.
- `carrierScreening = clamp(capitals / ceil(1 × carriers + 0.25 × convoys) × factor, 0, 1)`; 1 if nothing needs it.
- The required counts are **rounded up** (one capital plus one convoy needs 4 screens). Ships in every in-battle state count; submarines do not.
- Effects come from the static modifiers `screening_bonus` (`naval_hit_chance 0.4`, `naval_retreat_speed 0.2`, `convoy_retreat_speed 0.2`) scaled by screening, for battle-line and carrier-line ships, and `capital_screening_bonus` (`naval_retreat_speed 0.2`, `sortie_efficiency 0.1`) scaled by `1 - (1 - screening)(1 - carrierScreening)` for carrier-line ships. They are loaded from `common/modifiers` (`GameDefines.StaticModifiers`).

**Code.** `ScreeningRules.cs`. **Not modelled:** the `screening_efficiency` modifier and the `screening_without_screens` fallback.

## S-ACTIVATE: when lines start firing [RE]

Thresholds come from `CARRIER_ONLY_COMBAT_ACTIVATE_TIME` (0), `CAPITAL_ONLY_COMBAT_ACTIVATE_TIME` (6) and `ALL_SHIPS_ACTIVATE_TIME` (8), and depend on what is in the battle. Composition is pooled over **both sides**, counting only visible ships still in the battle; carrier-like = carriers, capital-like = capitals and submarines.
- With carriers present: carriers 0, capitals and submarines 6, screens and convoys 8.
- No carriers: capitals and submarines 0, screens and convoys `8 + 0 - 6` = 2.
- No carriers and no capital-like ships: everything 0.

A waiting ship activates at the first tick **strictly above** its threshold (always once the tick passes `ALL_SHIPS_ACTIVATE_TIME`) and fires from the next hour. So in a battle with carriers, carriers fire from hour 1, capitals from hour 7 and screens from hour 9; in a screens-only battle, from hour 1.

**Code.** `WeaponCadenceRules.ActivationThreshold`, `IsActivated`. Support ships (counted with carriers in the game) do not exist in the data model yet.

## S-COOLDOWN: weapon cadence [RE]

Three slots per ship, processed in this order: **heavy, torpedo, light**. Depth charges share the light slot. `cooldown = max(BASE_GUN_COOLDOWNS[slot] ({1, 4, 1}), MIN_GUN_COOLDOWN)`; only torpedoes have a modifier (`naval_torpedo_cooldown_factor`, not modelled).

Each hour, for a slot with attack: `charge = min(charge + 1, ceil(cooldown))`. If `charge >= cooldown`, **one target** is picked for the slot, then the slot fires `while charge >= cooldown: charge -= cooldown` at that target, stopping if it sinks. Guns fire every hour; torpedoes on the fourth processed hour and every fourth after; fractional cooldowns carry over; a cooldown below 1 gives several shots in one hour. Charge is per ship and slot, starts at 0 and is not reset by retreating.

**Code.** `WeaponCadenceRules.Charge`, `TrySpendShot`; `BattleEngine.FireSlots`/`FireAt`.

## S-TARGET: target selection [RE]

Lines: 1 screens, 2 capitals, 3 carriers and convoys. Submarines are not on a line. Only visible ships in the battle (waiting, fighting or retreating) are candidates; hidden submarines never are. Reach decisions use the enemy's visible line counts **taken at the start of the side's turn**, so if the only reachable line is wiped out during the turn, later shots find no target and are not fired.

- **S-TARGET-LIGHT**: lines up to the closest non-empty one (`screens ? 1 : capitals ? 2 : 3`). Any shooter, carriers included.
- **S-TARGET-HEAVY**: up to line 2 when both screens and capitals are present, otherwise up to line 3.
- **S-TARGET-TORP**: **one roll** `r`: up to line 1 if `r <= screening`, else line 2 if `r <= carrierScreening`, else line 3. Screens are always candidates when present. There is no fallback: a torpedo with nothing in reach fires at nothing.
- **S-TARGET-SUB**: submarines have a nearest line from their task force engagement level. Do-not-engage and Low: line 3, or 2 if the carrier line is empty, or 1 if capitals are also gone. Medium: line 2, or 1 if capitals and the carrier line are empty. High and Death Incarnate: line 1. That combines with the torpedo roll, so a low-engagement submarine facing a fully screened convoy fires at nothing. Submarines also use their light slot under the same rule.
- **S-TARGET-DC**: the light slot may target revealed submarines as well as its surface lines; the submarine's weight needs a shooter stat above zero. That stat is not identified; sub detection is [ASSUMED]. A ship whose only weapon is depth charges never picks a target.
- **S-TARGET-WEIGHT** (`HardcodedValues`):
  - Ships: base capital 2, screen 3, submarine 4, other (carrier) 1; ×15 for heavy and torpedo against capitals and carriers; ×2 for the light slot against screens. Then `× (2 - hpRatio)`.
  - Convoys: base 4, 40 from submarines; ×15 for heavy and torpedo. Then `× (1 + 10 × (1 - hpRatio))`.
  - Then an exclusive state multiplier: fighting ×1.5, retreating ×0.5, waiting ×1. Active convoys are always retreating.
  - The resulting table is the wiki's (30/2, 3/6, 15/1, 60/4, 600/40, 4).
- **Pick**: `T = U × Σweights`; the heaviest candidate takes `[0, maxWeight]`, then the rest accumulate in list order. That is weight-proportional.
- **Retreating shooters** use exactly the same rules (the wiki's "highest weight among active targets" rule is not in the game).

**Code.** `TargetingRules.cs`. **Not modelled:** `naval_torpedo_screen_penetration_factor` (scales `1 - screening` for the roll).

## S-HIT: hit chance [RE]

```
hit = base × profileFactor × crewFactor × modifierSum, then at least COMBAT_MIN_HIT_CHANCE (0.005)
shot hits when U < hit   (no upper cap: hit >= 1 always hits)
```
- **S-HIT-BASE**: `COMBAT_BASE_HIT_CHANCE` (0.1); against a submarine (light slot, depth charges) `× DEPTH_CHARGES_HIT_CHANCE_MULT` (1.1).
- **S-HIT-SHIP**: `100 × visibility / (max(speed × HIT_PROFILE_SPEED_FACTOR (0.5), 0.1) + HIT_PROFILE_SPEED_BASE (20))`; submarines use sub visibility; convoys `CONVOY_HIT_PROFILE` (85). The wiki's 1936 light cruiser (visibility 15, 32 kn → 41.7) is a unit test. Not modelled: `naval_visibility_factor` and a per-ship hit-profile multiplier of unidentified source.
- **S-HIT-WEAPON**: `GUN_HIT_PROFILES[slot]` {heavy 70, torpedo 100, light 45}; depth charges `DEPTH_CHARGES_HIT_PROFILE` (100).
- `profileFactor = clamp((shipProfile / weaponProfile)², MIN_HIT_PROFILE_MULT (0), 1)`.
- `crewFactor = max(0, 1 + COMBAT_LOW_ORG_HIT_CHANCE_PENALTY (-0.5) × missingOrg + COMBAT_LOW_MANPOWER_HIT_CHANCE_PENALTY (-0.25) × missingManpower)`, both the shooter's, **added** into one factor.
- **S-HIT-MODS**: `modifierSum = 1 + naval_hit_chance + the shooter's hit chance factor for the slot` (depth charges use the light factor), one **unclamped sum**. `naval_hit_chance` here is:
  - `screening_bonus` × screening (0.4 at full screening) for battle-line and carrier-line shooters;
  - `night` × darkness (−0.25 at full dark), see S-NIGHT.

  Admiral skills and traits, doctrines and weather would add to the same sum; they are not modelled.
- `COMBAT_EVASION_TO_HIT_CHANCE` has no reader in 1.19.3 (dead define).

**Code.** `HitChanceRules.cs`.

## S-NIGHT: night [RE effect, ASSUMED darkness]

The static modifier `night` is applied to the battle province "multiplied by amount of darkness": `naval_hit_chance -0.25`, `naval_retreat_speed +0.1` (and `carrier_traffic -1.0` for air). The engine uses darkness 1 from 20:00 to 06:00 and 0 otherwise (`HardcodedValues.NightStartHour/NightEndHour`, `BattleRules.StartHourOfDay`). The game computes darkness per province from the day/night terminator; that is not decoded. `BattleRules.NightEnabled` turns night off.

## S-DMG: damage of a hit [RE]

Per slot and target the game computes, once before its shots:
- heavy: `heavyAttack × pierce(heavyPiercing, armor)`; nothing against submarines;
- light: `lightAttack × pierce(lightPiercing, armor)`; against a submarine: `depthChargeAttack × DEPTH_CHARGES_DAMAGE_MULT (0.7) × pierce(lightPiercing, subArmor)`. Depth charges **do** go through armor;
- torpedo: `torpedoAttack`, no armor; later `× (1 - torpedo damage reduction)`;
- `base = max(0, damage × max(0, 1 - positioningPenalty + shooter naval_damage_factor - target naval_defense_factor))`. The modifiers come from experience (S-XP); admirals and doctrines would add to them.

Then per hit: `d = base × (1 + COMBAT_DAMAGE_RANDOMNESS (0.5) × (U - 0.05))` (range ×0.975 to ×1.475); crit roll (S-CRIT); torpedo reduction; split into HP and organization (S-DMG-APPLY); critical multiplier on both.

- **S-DMG-PIERCE**: `ratio = piercing / armor` (armor 0 counts as ratio 100); the first `i` with `ratio >= NAVY_PIERCING_THRESHOLDS[i]` ({2, 1, 0.75, 0.5, 0.1, 0}) selects `NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[i]` ({1, 1, 0.7, 0.4, 0.3, 0.1}) and `..._CRITICAL_VALUES[i]` ({2, 1, 0.75, 0.5, 0.1, 0}). If nothing matches (possible only with modded tables) the value is `table[0] + 1`.
- **S-DMG-RANDOM**: `1 + R × (U - 0.05)` is confirmed (`DamageRandomnessModel.Game`). The define comment's "7–10" reading is wrong for 1.19.3.
- `COMBAT_ARMOR_PIERCING_DAMAGE_REDUCTION` (0, "deprecated") is still read; not modelled.

**Code.** `DamageRules.Profile`, `RollHit`. **Open:** `naval_damage_factor` also appears in the attack getter, so it may count twice when it comes from admirals or doctrines (not relevant while only experience feeds it).

## S-CRIT: critical hits [RE]

- Guns and depth charges: chance `COMBAT_BASE_CRITICAL_CHANCE (0.05) × (1 - reliability) × CRITICAL_VALUES[tier]`; multiplier `1 + COMBAT_CRITICAL_DAMAGE_MULT (5) × (1 - reliability)`. At reliability 0.8 that is ×2.0, not the ×1.8 the old spec had.
- Torpedoes: chance `COMBAT_TORPEDO_CRITICAL_CHANCE (0.1) × max(0, 1 + target torpedo-crit modifier)`; multiplier `COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT` (2), no reliability.
- Reliability is the target's, clamped to 1. A crit is rolled only when chance and damage are positive, with `U <= chance`, and it multiplies **both** HP and organization damage.
- All crit chances are also multiplied by `1 + naval_critical_score_chance_factor + naval_critical_receive_chance_factor` (not modelled).
- **S-CRIT-PART**: on a crit, a part is damaged with chance `CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT (0.1) × critical_part_damage_chance_mult / reliability` (always if that is ≥ 1 or reliability is 0). The part is a weighted pick from the hull's and modules' `critical_parts`; its damage multipliers **replace** the crit multiplier, and its stat penalties stay on the ship (scaled by `naval_critical_effect_factor`). Part data is not loaded yet, so the engine follows the game's "no part candidates" path: the roll is made and recorded, and the regular crit multiplier applies.

**Code.** `DamageRules.Profile`, `RollHit`, `RollPartDamage`.

## S-DMG-APPLY: HP, organization, manpower [RE]

- `strDamage = d × COMBAT_DAMAGE_TO_STR_FACTOR (0.6)`; `orgDamage = d × (1 - hpBeforeShot / maxHp) × COMBAT_DAMAGE_TO_ORG_FACTOR (1)`. The wiki's 100 HP / 50 org example (30 HP, then 0, 15, 30 org) is a unit test.
- Before HP is reduced, experience and manpower losses use the **unclamped** `strDamage` (S-XP). Then HP and organization are reduced and clamped at 0.
- A ship sinks at 0 HP; it stops being a target and does not fire for the rest of the hour.

**Code.** `DamageRules.Apply`, `ExperienceRules.ApplyDamageLosses`.

## S-RETREAT: retreat decision [RE]

Only fighting ships, from tick `COMBAT_MIN_DURATION` (8), before they fire:
- Threshold by the task force engagement level: Do-not-engage and Low `REPAIR_AND_RETURN_PRIO_LOW_COMBAT` (0.9); Medium `..._MEDIUM_COMBAT` (0.6); High and Death Incarnate `..._HIGH_COMBAT` (0.4). Capital ships and carriers multiply it by `CAPITAL_SHIP_COMBAT_RETREAT_MULT` (0.5).
- If `hp / maxHp < threshold`: roll `U <= COMBAT_RETREAT_DECISION_CHANCE` (0.2); success → retreating, no shots this hour. Organization is not used.

**Not modelled** (documented in the finding): the retreat-chance modifiers (`naval_retreat_chance`, the after-initial-combat one, the enemy's); the side-wide retreat order; the **aggression check**, which makes healthy ships of an outmatched task force retreat (it needs a strength score that is not decoded); and the convoy-raiding submarine rule (`SUBMARINE_ESCAPE_RATIOS`). `REPAIR_AND_RETURN_UNIT_DYING_STR` is not read by naval combat.

**Code.** `RetreatRules.TryStartRetreat`. `BattleRules.RetreatEnabled = false` disables retreat (tooling).

## S-RETREAT-SPEED: escaping [RE]

Daily speed, additive:
```
BASE_ESCAPE_SPEED (0.05) + min(tick × ESCAPE_SPEED_PER_COMBAT_DAY (0.015) / 24, MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION (0.5))
+ retreat speed modifiers (screening_bonus × screening for battle line and carrier line; capital_screening_bonus × combined
  carrier screening for the carrier line; night × darkness for surface warships)
+ speed × SPEED_TO_ESCAPE_SPEED (0.5) / 100
+ submarines: ESCAPE_SPEED_SUB_BASE (0.1), or ESCAPE_SPEED_HIDDEN_SUB (0.35) while hidden
```
Each hour a retreating ship adds `speed / 24` to its progress and escapes at 1. A 30-knot capital with full screening at tick 10 escapes in about 59 hours.

**Code.** `RetreatRules.EscapeSpeedPerDay`, `AdvanceRetreat`.

## S-SUB: submarines [RE]

- Submarines start **hidden** (reveal timer 0) and can only be targeted while revealed. Hidden submarines are not counted for activation or line counts.
- At the start of each of its updates, a revealed submarine's timer counts down. If it is hidden and the enemy's detection `D > 0`, it is revealed for `SUBMARINE_HIDE_TIMEOUT` (8) hours when `U <= chance`. The same roll happens after it fires torpedoes, with the numerator × `SUBMARINE_REVEAL_TORPEDO_FIRING_DETECTION_MULTIPLIER` (1.1).
- `chance = min(1, D × SUBMARINE_REVEAL_DETECTION_MULTIPLIER (0.065) / max(SUBMARINE_BASE_STEALTH_VALUE (100) - V, 0.1))`, `V = subVisibility × (1 + SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING (1) × (1 - ownPositioning))`.
- `D` = mean sub detection over the enemy side's ships in battle that have any, × `NAVAL_COMBAT_SUB_DETECTION_FACTOR` (plus an air term, milestone 2).
- A side that faces only hidden submarines disengages (S-HOUR).

**Code.** `SubmarineRules.cs`. **Not modelled:** the sub-only-battle rule at hour 0 (`SUBMARINE_REVEALED_TIMEOUT`), visibility modifiers. The old `SUBMARINE_REVEAL_BASE_CHANCE`/`POW` defines are not read by 1.19.3.

## S-XP: experience [RE]

- **Levels**: `level = count(NMilitary.UNIT_EXP_LEVELS {0.1, 0.3, 0.75, 0.9} <= xp / NAVY_MAX_XP)`, i.e. 0–4.
- **Effects**: level `ARMY_EXP_BASE_LEVEL` (1) has none. Below it, the static modifier `ship_experience_malus_min` (`naval_damage_factor -0.1`, `naval_defense_factor -0.1`) × `(base - level) / base`. Above it, `ship_experience_bonus_max` (`+0.3`, `+0.2`) × `(level - base) / (4 - base)`. Damage and defense factors enter the additive damage multiplier (S-DMG). Experience has no other combat effect; carriers get `carrier_experience_*` sortie modifiers instead (air).
- **Gain**: each hour, a ship that fired gains `UNIT_EXPERIENCE_PER_COMBAT_HOUR (10) × UNIT_EXPERIENCE_SCALE (1) × (enemyGroupSize + 1) / (ownGroupSize + 1) × EXPERIENCE_FACTOR_NON_CARRIER_GAIN (0.04)`, × `EXPERIENCE_FACTOR_CONVOY_ATTACK` (0.04) if its last target was a convoy. Damage dealt does not matter. Group size counts every ship the side brought (sunk ships stay in the group's list; medium confidence). Carriers that did not fire gain from sortie efficiency × `EXPERIENCE_FACTOR_CARRIER_GAIN` (air). `XP_GAIN_FACTOR` is not used here.
- **Loss** on each hit, before HP drops: `xp × clamp(hp - EXPERIENCE_LOSS_FACTOR (1) × strDamage, 0, maxHp) / hp`. Manpower loss: `round(maxManpower × strDamage / maxHp × MANPOWER_LOSS_RATIO_ON_STR_LOSS (0.5))`, never below `round(maxManpower × MIN_MANPOWER_RATIO_TO_DROP (0.1))`, and nothing once at that floor. Rounding is half away from zero.

**Code.** `ExperienceRules.cs`.

## S-MANPOWER: reinforcement during battle [RE]

Ships do not regain manpower in combat (`DAILY_MANPOWER_GAIN_RATIO` is "for ships not in combat"); the old option is removed.

## S-END: end of battle [RE]

The battle ends when either side has no ship left in it (waiting, fighting or retreating with HP above 0). Retreating ships keep the battle going until they escape. There is no maximum duration in the game; `BattleRules.MaxHours` is a safety stop (`Timeout`). `ContinueAfterRetreat` (tooling) puts escaped ships back in.

## Changes from the first version of this spec

| Item | Milestone 1 (wiki-based) | Now (hoi4.exe 1.19.3) |
|---|---|---|
| S-ORDER | interleaved random order | attacker side first, fleet order |
| S-HOUR | retreat phase after firing; retreating ships do not fire | decision before firing; retreating ships fire |
| S-ACTIVATE | 0/6/8 regardless of composition | composition-dependent, pooled, strict `>` |
| S-COOLDOWN | next-fire hour | per-slot charge; one target per slot per hour; multi-shot |
| S-TARGET | live line membership, two torpedo rolls, carrier light-gun exclusion, retreating-shooter rule | turn snapshot, one roll, none of those rules |
| S-TARGET-WEIGHT | retreat ×0.5 and active ×1.5 could stack | exclusive states; convoy damage ×(1+10·missing) |
| S-HIT-MODS | multiplicative, −25% night, org and manpower separate | one additive sum; org and manpower added |
| S-HIT | capped at 100% | no cap; minimum 0.005 applied last |
| S-CRIT | ×(1 + 4·(1−rel)); depth charges ignored armor | ×(1 + 5·(1−rel)); depth charges use piercing tiers |
| S-DMG | separate positioning multiplier; ×(level damage) | additive (1 − penalty + damage − defense) |
| S-RETREAT | repair priority 0.6/0.3/0.1 | engagement level 0.9/0.6/0.4, capitals ×0.5 |
| S-RETREAT-SPEED | multiplicative screening bonus; subs replace base | additive; subs add to base; hidden subs faster |
| S-SUB | always revealed | hidden/revealed with reveal rolls |
| S-XP | 7 legacy levels, linear damage bonus, gain ∝ enemy/own | 5 levels, static modifiers, gain ∝ (enemy+1)/(own+1), HP-based loss |
| Screening | `ceil` missing | requirements rounded up, factor floor 0.01 |
