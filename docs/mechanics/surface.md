# Surface combat spec

Status: implemented in milestone 1 (`src/NavySimulator.Engine/Battles/`). Tags are explained in [README.md](README.md). Values below are vanilla 1.17 defaults; the engine always reads them from `GameDefines`, so loading other defines changes them.

## S-HOUR: hour structure

**Rule.** The battle runs in hours `0, 1, 2, …`. Each hour:
1. Rebuild battle lines from ships still in the battle (not sunk, not escaped) and compute positioning (S-POS) and screening (S-SCREEN) for both sides.
2. Air phase (A-*; milestone 2).
3. Surface phase: every ship fires each ready weapon (S-ACTIVATE, S-COOLDOWN), in the order set by S-ORDER.
4. Retreat phase: retreating ships move away (S-RETREAT-SPEED); damaged ships may start retreating (S-RETREAT).
5. Experience gain (S-XP), optional daily manpower recovery (S-MANPOWER), end check (S-END).

**Source.** [ASSUMED] ordering of the phases. **Code.** `BattleEngine.cs`.
**Open.** Whether retreat decisions happen before or after firing, and whether lines/screening are recomputed after mid-hour losses. Needs [RE].

## S-ORDER: firing order within an hour

**Rule.** Unknown. Three variants (`BattleRules.FiringOrder`):
- `Interleaved` (default): all ships of both sides in random order; damage applies immediately.
- `AttackerFirst`: the previous simulator's behavior. Gives the attacker a systematic edge, because defender ships sunk earlier in the hour never fire (see `BattleEngineTests.Simulate_MirrorMatchWinRateDependsOnFiringOrder`).
- `Simultaneous`: shots aim at the start-of-hour state; all damage applies at the end of the hour.

**Source.** [ASSUMED]. **Open.** Top priority for [RE]: find the per-hour loop in the naval combat code. Calibration signal: in an even mirror match, the attacker's win rate.

## S-POS: positioning

**Rule.** `positioning = clamp(BASE_POSITIONING - sizePenalty + detectionBonus, 0, 1)`.
- **S-POS-SIZE**: if `own >= MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY` (4) and `own > enemy`, then `sizePenalty = min(MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO (0.75), HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR (0.25) * (own/enemy - 1))`.
- **S-POS-DETECT**: `detectionBonus = clamp((ownBestSurfaceDetection - enemyBestSurfaceDetection) * RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR, 0, MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION (0.25))`.
- Effects: **S-POS-SCREEN** each ship's screening contribution is multiplied by `1 - SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING (0.5) * (1 - positioning)`; **S-POS-DMG** damage dealt is multiplied by `1 - DAMAGE_PENALTY_ON_MINIMUM_POSITIONING (0.5) * (1 - positioning)`.

**Source.** [DEFINE] for every constant. The define comments give the shape ("penalty for each +100% ship ratio"). [ASSUMED]: detection uses each side's best ship (sum and average are the other candidates), and the bonus can only cancel penalties because positioning is capped at 1. **Code.** `PositioningRules.cs`.
**Open.** Not implemented: carrier-ratio penalty (`HIGHER_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR`, `MAX_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR`, `CARRIER_PLANES_AMOUNT_FOR_POSITIONING`, belongs with air), penalty for ships joining late (`POSITIONING_PENALTY_FOR_SHIPS_JOINED_COMBAT_AFTER_IT_STARTS` and related), AA penalty (`AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING`, air), sub reveal (`SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING`, S-SUB). The wiki gives no positioning formula at all.

## S-SCREEN: screening efficiency

**Rule.**
- `screening = clamp(screens * contribution / (SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS (3) * (capitals + carriers) + SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS (0.5) * convoys), 0, 1)`; 1 if nothing needs screening.
- `carrierScreening = clamp(capitals * contribution / (CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS (1) * carriers + CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS (0.25) * convoys), 0, 1)`.
- `contribution` from S-POS-SCREEN.

**Source.** [DEFINE] [WIKI]. **Code.** `ScreeningRules.cs`. Effects are listed where they apply: S-HIT-MODS, S-TARGET-TORP, S-RETREAT-SPEED, A-SORTIE.

## S-ACTIVATE: when lines start firing

**Rule.** Carriers from hour `CARRIER_ONLY_COMBAT_ACTIVATE_TIME` (0), capitals from `CAPITAL_ONLY_COMBAT_ACTIVATE_TIME` (6), everything else from `ALL_SHIPS_ACTIVATE_TIME` (8). A ship is *actively fighting* once its line is active (used by S-TARGET).

**Source.** [DEFINE] [WIKI] (stated unconditionally). **Code.** `WeaponCadenceRules.ActivationHour`.
**Open.** `BattleRules.ActivationDependsOnComposition` reproduces the previous simulator, where the delays only applied if the battle contained carriers (for capitals) or carriers/capitals (for screens). That matches the define *names* ("capital only combat", "carrier only combat"), so it is a real candidate. [RE]/[CAL]: does a screens-only battle start shooting at hour 0 or hour 8?

## S-COOLDOWN: weapon cadence

**Rule.** `BASE_GUN_COOLDOWNS = { heavy 1, torpedo 4, light 1 }` hours, at least `MIN_GUN_COOLDOWN`. A weapon fires when its line activates, then every cooldown hours. Depth charges use the light cooldown.

**Source.** [DEFINE] [WIKI] (the wiki gives the index order). [ASSUMED]: depth-charge cooldown; fractional cooldowns (from modifiers such as the -25% Loading Drill trait, not modelled yet) still fire at most once per hour. **Code.** `WeaponCadenceRules`.
**Open.** `BattleRules.FirstShotWaitsForCooldown`: does a torpedo fire at activation, or one cooldown later? The previous simulator also delayed every first shot by `cooldown + 1` and used cooldowns 3/5/2 (TFB values, see [defines.md](defines.md)).

## S-TARGET: target selection

Each ship picks a target per weapon independently, every time that weapon fires. Group membership is checked live, so a group wiped out earlier in the hour no longer blocks fire at the next group.

- **S-TARGET-LIGHT**: closest non-empty group (screens, then capitals, then carrier group). Carrier and convoy shooters never target the enemy carrier group. [WIKI]
- **S-TARGET-HEAVY**: the first two non-empty groups. [WIKI]
- **S-TARGET-TORP**: screens are always reachable; capitals if a roll beats `1 - screening`; the carrier group (carriers and convoys) if a second roll beats `1 - carrierScreening`. If nothing is reachable, the closest group. [WIKI]
- **S-TARGET-SUB**: submarines fire torpedoes only. Screens only on High engagement risk; capitals on Medium or higher; the carrier group (convoys) always. The screening rolls of S-TARGET-TORP also apply. [WIKI] for the risk rules, [ASSUMED] for the rolls.
- **S-TARGET-DC**: depth charges target submarines only; every submarine counts as revealed (S-SUB). [ASSUMED]
- **S-TARGET-WEIGHT**: weighted random pick. Base weight by target group (light-gun weight in brackets): capital 30 (2), screen 3 (6), carrier 15 (1), convoy 60 (4), convoy from a submarine 600 (40), submarine 4. Then `× (1 + 1.0 * missingHpFraction)`, `× 0.5` if the target is retreating, `× 1.5` if the target is actively fighting. [WIKI] (the wiki says these are hard-coded in the executable; `HardcodedValues`).
- **S-TARGET-RETREATING**: a retreating shooter only considers actively fighting targets and takes the highest weight. [WIKI]

**Code.** `TargetingRules.cs`. **Open.** [RE] the weights (they are not in any defines file). The wiki's "+50% for actively fighting" could also mean "−33% for not yet active".

## S-HIT: hit chance

**Rule.** `hit = max(base * modifiers * max(MIN_HIT_PROFILE_MULT, min((shipProfile / weaponProfile)^2, 1)), COMBAT_MIN_HIT_CHANCE (0.05))`.
- **S-HIT-BASE**: `COMBAT_BASE_HIT_CHANCE` (0.1); depth charges `× DEPTH_CHARGES_HIT_CHANCE_MULT` (1.1). [DEFINE]
- **S-HIT-SHIP**: `HIT_PROFILE_MULT (100) * visibility / (HIT_PROFILE_SPEED_FACTOR (0.5) * speed + HIT_PROFILE_SPEED_BASE (20))`; subs use sub visibility; convoys a fixed `CONVOY_HIT_PROFILE` (85). [DEFINE] [WIKI] (decoded `CalcGunInfo()`). The wiki's light cruiser example (visibility 15, 32 kn, profile 41.7) is a unit test.
- **S-HIT-WEAPON**: `GUN_HIT_PROFILES = { heavy 80, torpedo 100, light 45 }`; depth charges `DEPTH_CHARGES_HIT_PROFILE` (100). [DEFINE]
- **S-HIT-MODS**, each multiplying the chance:
  - the shooter's hit chance factor for the weapon (`naval_*_hit_chance_factor` module/tech stats) [DEFINE stat names]
  - `1 + COMBAT_LOW_ORG_HIT_CHANCE_PENALTY (-0.5) * missingOrgFraction` [DEFINE] [WIKI]
  - `1 + COMBAT_LOW_MANPOWER_HIT_CHANCE_PENALTY (-0.25) * missingManpowerFraction` [DEFINE] [WIKI]
  - battle line, carrier group: `1 + 0.4 * screening` [WIKI] (no define found)
  - night: `1 - 0.25` between 20:00 and 06:00 [ASSUMED], `BattleRules.NightEnabled`, `StartHourOfDay`.

**Code.** `HitChanceRules.cs`. **Open.** Whether modifiers add up before multiplying (common in HOI4) or multiply ([ASSUMED] here). Night hours and penalty value. Weather, admiral skill and traits, doctrines (as battle modifiers rather than stats) are missing. `COMBAT_EVASION_TO_HIT_CHANCE` (0.007, torpedo ×10) still exists in the defines; its comment describes an evasion-based formula from before 1.6. Unused here; [RE] whether anything reads it.

## S-DMG: damage of a hit

**Rule.** `damage = attack × xpFactor (S-XP) × piercingFactor × critMultiplier (S-CRIT) × positioningFactor (S-POS-DMG) × torpedoReduction × depthChargeMult × randomFactor`.
- **S-DMG-PIERCE**: `ratio = piercing / armor`; the first index `i` with `ratio >= NAVY_PIERCING_THRESHOLDS[i]` (`{2, 1, 0.75, 0.5, 0.1, 0}`) selects `NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[i]` (`{1, 1, 0.7, 0.4, 0.3, 0.1}`). Torpedoes and depth charges ignore armor. [DEFINE] [WIKI]
- Torpedoes: `× (1 - target torpedo damage reduction)`. Depth charges: `× DEPTH_CHARGES_DAMAGE_MULT` (0.7). [DEFINE]
- **S-DMG-RANDOM**: `BattleRules.DamageRandomness` with `R = COMBAT_DAMAGE_RANDOMNESS` (0.5):
  - `WikiDecoded` (default): `1 + R × U(-0.05, 0.95)`, range −2.5% to +47.5% (wiki, decoded 1.7.1 `ProcessShootingAtTarget()`).
  - `DefineComment`: `1 - R × U(0, 1)`. The define's own comment says "if max damage is 10 and randomness is 30%, damage will be 7–10".
  - These differ by about 1.6× on average damage, so this needs [CAL] early.

**Code.** `DamageRules.Calculate`.

## S-DMG-APPLY: HP, organization, manpower

**Rule.** HP `-= damage × COMBAT_DAMAGE_TO_STR_FACTOR` (0.6). Org `-= damage × COMBAT_DAMAGE_TO_ORG_FACTOR (1) × (1 - hpBeforeHit / maxHp)`, so a ship at full HP loses no organization. Manpower and experience losses follow S-XP.

**Source.** [DEFINE] [WIKI] (the wiki's 100 HP / 50 org example is a unit test). **Code.** `DamageRules.Apply`.
**Open.** Whether 0 org has any effect besides the hit chance penalty (retreat? S-RETREAT).

## S-CRIT: critical hits

**Rule.**
- Torpedoes: chance `COMBAT_TORPEDO_CRITICAL_CHANCE (0.1) × (1 + target torpedo-crit modifier)`, multiplier `COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT` (2).
- Guns and depth charges: chance `COMBAT_BASE_CRITICAL_CHANCE (0.05) × NAVY_PIERCING_THRESHOLD_CRITICAL_VALUES[i] × (1 - targetReliability)`, multiplier `1 + (COMBAT_CRITICAL_DAMAGE_MULT (5) - 1) × (1 - targetReliability)`.
- **S-CRIT-PART**: on a crit, chance `CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT / reliability` to hit a part. Recorded in `ShotRecord.PartHit`, but part effects are **not modelled**: the shot uses the regular crit multiplier. The wiki's part table (magazine ×10 damage and −70% attack, fires, propeller, rudder, turrets, mounts, tubes, secondaries) needs module categories on designs first (milestone 3).

**Source.** [DEFINE] [WIKI]. [ASSUMED]: reliability is the target's; depth-charge crits use tier factor 1. **Code.** `DamageRules.RollCritical`.

## S-RETREAT: retreat decision

**Rule.** From hour `COMBAT_MIN_DURATION` (8), an active ship below the HP ratio for its task force's repair priority (`REPAIR_AND_RETURN_PRIO_{LOW,MEDIUM,HIGH}_COMBAT` = 0.6 / 0.3 / 0.1) starts retreating with chance `COMBAT_RETREAT_DECISION_CHANCE` (0.22) per hour. Retreating ships keep firing (S-TARGET-RETREATING) and can still be shot.

**Source.** [DEFINE] for the constants; [ASSUMED] for how they combine, including the priority mapping (in-combat low/medium/high run the opposite way to the out-of-combat 0.2 / 0.5 / 0.9). **Code.** `RetreatRules.TryStartRetreat`. `BattleRules.RetreatEnabled` turns it off.
**Open.** High priority for [RE]. Org-based retreat, `REPAIR_AND_RETURN_UNIT_DYING_STR` (0.2), whole-task-force retreat, the jammed-rudder part (−90% retreat chance).

## S-RETREAT-SPEED: escaping

**Rule.** Per hour `progress += speed / 24`, escaped at 1, where `speed = (BASE_ESCAPE_SPEED (0.045, subs ESCAPE_SPEED_SUB_BASE) + shipSpeed × SPEED_TO_ESCAPE_SPEED (0.95) / 100 + min(days × ESCAPE_SPEED_PER_COMBAT_DAY, MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION)) × (1 + 0.2 × screening [battle line, carrier group] + 0.2 × carrierScreening [carriers, convoys])`.

**Source.** [DEFINE] constants, [LEGACY] formula shape, [WIKI] screening bonuses. **Code.** `RetreatRules.EscapeSpeedPerDay`.
**Open.** The per-day reading of the escape speed makes a 30-knot ship take about 3 days to escape. That seems too slow; this needs [RE]/[CAL]. `ESCAPE_SPEED_HIDDEN_SUB` is not used yet. The night retreat bonus of the previous simulator had no source and was dropped.

## S-XP: experience

**Rule.**
- Levels: thresholds `[0.1, 0.2, 0.3, 0.4, 0.55, 0.7, 0.9] × NAVY_MAX_XP`, damage factor from −10% (level 0) to +15% (max level) in equal steps. [LEGACY]
- Gain: each ship that fired this hour gains `UNIT_EXPERIENCE_PER_COMBAT_HOUR × UNIT_EXPERIENCE_SCALE × EXPERIENCE_FACTOR_NON_CARRIER_GAIN × enemyShips / ownShips`. [DEFINE] constants, [LEGACY] formula.
- Loss: manpower lost `= round(hpLost / maxHp × maxManpower × MANPOWER_LOSS_RATIO_ON_STR_LOSS)`, never below `MIN_MANPOWER_RATIO_TO_DROP` of max; experience `-= xp × lostShare × EXPERIENCE_LOSS_FACTOR`. [DEFINE] constants, [LEGACY] formula.

**Code.** `ExperienceRules.cs`. **Open.** The level thresholds and damage bonuses most likely live in a game script file (unit experience levels), not in defines. Locate them and load them (milestone 4). Carrier experience affects sortie efficiency instead (A-SORTIE).

## S-MANPOWER: reinforcement during battle

**Rule.** If `BattleRules.DailyManpowerRecoveryInBattle` is on, every 24 hours ships regain `DAILY_MANPOWER_GAIN_RATIO` of max manpower. Off by default; the previous simulator had it on. [ASSUMED]

## S-SUB: submarines

**Rule (milestone 1 stand-in).** Submarines form their own group, fire torpedoes by S-TARGET-SUB, and can only be hit by depth charges. They are always considered revealed.

**Open (milestone 3).** Hide/reveal (`SUBMARINE_HIDE_TIMEOUT`, `SUBMARINE_REVEALED_TIMEOUT`, `SUBMARINE_REVEAL_BASE_CHANCE`, `SUBMARINE_REVEAL_POW`, `SUBMARINE_BASE_TORPEDO_REVEAL_CHANCE`, `NAVAL_COMBAT_SUB_DETECTION_FACTOR`), sub escape (`SUBMARINE_ESCAPE_RATIOS`), air sub detection (`NAVAL_COMBAT_AIR_SUB_DETECTION_*`).

## S-END: end of battle

**Rule.** The battle ends when a side has no ships left in it (all sunk or escaped). Outcomes: `AttackerVictory`, `DefenderVictory`, `BothSidesGone`, or `Timeout` at `BattleRules.MaxHours`. `ContinueAfterRetreat` (a tooling option) puts escaped ships back in when the other side still has ships afloat.

**Source.** [WIKI]. **Code.** `BattleEngine.IsBattleOver`.

## Changes from the previous simulator

| Item | Before | Now | Why |
|---|---|---|---|
| S-ORDER | attacker fired first every hour | random interleaving (option) | measurable first-mover bias |
| S-HIT-MODS | screening bonus applied up to three times (×2.6 heavy) | once, +40% | [WIKI] |
| S-TARGET | shots wasted when the closest group sank mid-hour; retreat weight applied twice | live groups; once | bug |
| S-TARGET | carriers' light guns could hit enemy carriers | excluded | [WIKI] |
| S-RETREAT | `Random.Shared` (not seeded), fixed 40% HP threshold | seeded, priority threshold | bug, [DEFINE] |
| S-END | annihilation handled as a retreat; endless re-engagement | separate outcome | bug |
| S-HIT-MODS | torpedo and depth-charge hit factors dropped by `ShipStats.Add/Scale` | kept | bug |
| Defines | static, mutable, mixed sources | per-scenario instance; vanilla or loaded | tests polluted each other; values did not match vanilla |
| S-COOLDOWN | first shot after `cooldown + 1` hours | at activation (option) | [WIKI] |
| Night | disabled by testing values (24/0) | 20:00–06:00, −25% [ASSUMED] | testing override |
