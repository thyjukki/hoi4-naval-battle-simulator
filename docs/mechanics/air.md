# Naval air combat spec

Status: **design for milestone 2; not implemented.** The previous air simulator was removed in milestone 1 (see "Why the old model was dropped" below). The engine currently reports carrier wings and land-based planes as ignored.

Tags are explained in [README.md](README.md). Sources are collected in the research notes summarised here. The wiki's naval battle page mixes values from 1.7–1.11 with newer ones, so [WIKI] claims about air rank lower than define comments ([PDX]).

## Overview of one air phase

For each side, at a sortie hour (A-CADENCE):
1. Work out how many planes fly: carrier planes (A-SORTIE, A-STANCE, A-STACK) and land-based naval strike planes (A-EXTERNAL).
2. Air-to-air: enemy fighters (carrier CAP, fighters in the airzone) engage the bombers (A-INTERCEPT). Disrupted or destroyed bombers do not strike.
3. Each surviving bomber wing picks a target (A-TARGET).
4. The target's AA may shoot down part of the wing before it strikes (A-AA-PREEMPT).
5. Hits and damage (A-HIT, A-DMG), reduced by fleet AA (A-AA-REDUCE), with crits (A-CRIT).

Planes do not use hit profiles, armor or piercing. [WIKI]

## A-CADENCE: when planes strike

**Candidates.**
- Carriers every `2 × CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT` (6 h): the define comment says delays are "generally used as double because of roundtrip". [PDX] [WIKI]
- Carriers every `2 × HOURS_DELAY_AFTER_EACH_COMBAT` (8 h), aligned to the global air clock "close to high noon". The wiki marks the 6 h statement obsolete, and a mod author says the carrier define "doesn't work". [WIKI] [COMMUNITY]
- Land-based planes every 8 h at 04:00 / 12:00 / 20:00. [WIKI]
- Carriers may act from hour 0 (S-ACTIVATE).

**Plan.** `BattleRules.CarrierSortiePeriodHours` and `SortieClockAligned` (time-of-day phase vs. battle-start phase). **First [RE] target for air**, since cadence sets the volume of all air damage. Calibration signal: the hours at which carrier damage appears in the battle log.

## A-SORTIE: how many carrier planes can fly

**Rule (best estimate).** `sortieEfficiency = BASE_CARRIER_SORTIE_EFFICIENCY (0.5) + 0.10 × carrierScreening + doctrine/trait/spirit/veterancy modifiers`. [DEFINE] [WIKI]
`flyable = planes × clamp(sortieEfficiency × (1 - CAPACITY_PENALTY (2) × overcrowdFraction), 0, 1)`, with capacity = deck size × `CARRIER_SIZE_STAT_INCREMENT` (10). This matches the wiki's 100-planes-on-an-80-deck example: above-100% sortie efficiency only offsets overcrowding. [WIKI]
Then `× clamp(1 + carrier traffic modifiers, 0, 1)`: night −100%, storm −80%. [WIKI]

**Open.** The +10% carrier screening bonus has no define name. The previous simulator multiplied by carrier organization, which no source supports. Carrier veterancy affects sortie efficiency rather than damage; the exact values belong to the experience-levels script (S-XP).

## A-STANCE: carrier stances (1.17+)

**Rule.** The offensive share is `CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[index]`, default index `CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX` (2, i.e. 50%); the defensive share is the rest. The selected stance overrides the default for the first `SELECTED_SORTIE_INITIAL_TIME` (24) hours. [PDX define comments, patch 1.17]

**Open.** [ASSUMED] the offensive share strikes ships and the defensive share flies CAP/interception (A-INTERCEPT). Community reports call stances buggy. Input: per-participant stance index.

## A-STACK: plane cap per battle (replaces the old 4-carrier rule)

**Rule.** Planes without penalty `= NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY (80) + SHIP_SILHOUETTE_VALUE_PLANES_CARRIER (16) × enemyCarriers + _CAPITAL (10) × enemyCapitals + _SCREEN (5) × enemyScreens / (1 + SCREEN_CAP_REDUCTION_FACTOR (0.02) × enemyCapitals) + _CONVOY (4) × convoys + _SUBMARINE (7) × subs`. Each plane above the cap reduces takeoffs by `NAVAL_COMBAT_PLANE_STACKING_PENALTY_EFFECT` (0.5%), "subject to diminishing returns". [PDX define comments]

**Open.** The diminishing-returns curve: candidates `1 / (1 + 0.005 × excess)` and `0.995^excess` (`BattleRules` variant). Whether land-based planes count. `CARRIER_STACK_PENALTY`/`_EFFECT` were removed in 1.17. The TFB profile still sets them, but the engine will not read them.

## A-EXTERNAL: land-based planes joining

**Rule.** Eligible = planes on Naval Strike in the airzone × mission efficiency. Join cap per sortie `= max(NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP (20), sumEnemyCurrentHp × NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO (0.05) × (1 + NAVAL_COMBAT_EXTERNAL_PLANES_JOIN_RATIO_PER_DAY (0.2) × combatDays))`. [DEFINE] [WIKI]

**Open.** Whole vs fractional days. Mission efficiency is an input (range, weather, airbase overcrowding, fuel), not simulated. `NAVAL_STRIKE_CARRIER_MULTIPLIER` (A-DMG) is why land planes look about 10× weaker than carrier planes per plane.

## A-INTERCEPT: air-to-air

**Rule (outline).** Normal air-combat math, scaled by `COMBAT_DAMAGE_SCALE_CARRIER` (5) between carrier planes, with disruption factor `DISRUPTION_FACTOR_CARRIER` (6) when both sides are carrier wings: `damage = attackers × air_attack × COMBAT_DAMAGE_STATS_MULTILPIER (0.2) × agility mitigation (COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION 0.45) × speed factor (COMBAT_BETTER_SPEED_DAMAGE_INCREASE 0.65) × top-speed bonus`. [DEFINE] [WIKI Air_combat]
Since 1.17, carrier fighters intercept land-based naval strikes: "the number of carrier fighters determines interception strength; bomber agility, defense and speed determine survival chances". [PDX] `CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER` (0.35) and `CARRIER_PERCENTAGE_DEFEND` (0.35, "percentage of planes able to defend a carrier from air attacks") are new in 1.17. [DEFINE]

**Open.** The largest gap in the air model; no published formula. Needs [RE] of the air-combat routine called from naval combat. Milestone 2 will ship this as an explicit [ASSUMED] model behind a rule switch, or leave fighters out and report them as ignored.

## A-TARGET: target selection

**Candidates.**
- Wiki (old defines): weight = `maxHp × typeScore × (1 + NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE (5) × missingHpFraction) × lowAa`, with type scores 10/50/200 for sub/capital/carrier and `lowAa = 5 × (5 - AA)` when AA < 5. [WIKI]
- Current define names: `typeScore = NAVAL_COMBAT_AIR_<TYPE>_TARGET_BASE + NAVAL_COMBAT_AIR_<TYPE>_TARGET_SCALE × (1 - screeningEfficiency)`, per the comments "based on screening efficiency" and "max value when zero screening efficiency". Base 10 (convoy 1); scale 10/10/50/200/1 for sub/screen/capital/carrier/convoy. [PDX define comments]; the combination is [ASSUMED].
- Low-AA factor "scales between 0 → NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE" (5). [PDX]

Hidden submarines are excluded. [WIKI]

**Open.** Which screening efficiency applies to which group; whether max HP still multiplies; the low-AA curve (the wiki form is not monotonic near AA 5). `BattleRules.AirTargetingModel` with both candidates.

## A-AA-PREEMPT: preemptive shoot-down

**Rule.** Only the targeted ship fires. Chance `= ANTI_AIR_TARGETTING_TO_CHANCE (0.2) × max(ANTI_AIR_TARGETING (0.9) - AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY (0.02) × wingAverageAgility, 0.01)`, so at most 18%. On success it destroys `shipAA × ANTI_AIR_ATTACK_TO_AMOUNT (0.01)` of the planes in the attacking wing, randomly rounded. [DEFINE] [WIKI]

**Open.** Positioning reduces AA by up to `AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING` (0.7); where exactly is unknown. What a "wing" is for land-based planes. The previous simulator assumed 10 planes for every land-based wing.

## A-AA-REDUCE: fleet AA damage reduction

**Rule.** `reduction = min(ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE (0.18) × (targetAA + SHIP_TO_FLEET_ANTI_AIR_RATIO (0.25) × sideTotalAA)^ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE (0.225), MAX_ANTI_AIR_REDUCTION_EFFECT_ON_INCOMING_AIR_DAMAGE (0.75))`; damage `× (1 - reduction)`. The side total includes the target. Convoys count 0 as the target but add to the total. [PDX define comment] [WIKI]
Worked values: target 10 / side 100 → 40%; target 30 / side 300 → 51%; the cap needs a pool of about 566.

**Open.** "Side" means every ship on that side in the battle (wiki) rather than only the target's task force; to be confirmed.

## A-HIT: how many planes hit

**Rule.** `hits = planesAfterAA × clamp(navalTargeting / 10 × NAVAL_STRIKE_TARGETTING_TO_AMOUNT (0.3), 0, 1)`. Target speed, visibility and armor do not matter. [DEFINE] [WIKI]

**Open.** Expected value or a per-plane roll (`BattleRules` variant).

## A-DMG: strike damage

**Rule.** `base = navalAttack × hits`, `× NAVAL_STRIKE_CARRIER_MULTIPLIER` (10 in 1.17 copies, 12 reported for 1.18.3) if the planes' carrier is in this battle, `× (1 - AA reduction)`. HP `-= base × NAVAL_STRIKE_DAMAGE_TO_STR` (1); org `-= base × NAVAL_STRIKE_DAMAGE_TO_ORG` (1.5). [DEFINE] [PDX define comments]
`DamageRules.Apply` already takes the HP/org factors as parameters, so strikes reuse it.

**Open.** Whether the full-HP org immunity (S-DMG-APPLY) and damage randomness also apply to strikes. Whether naval attack is per plane (the designer stat) or per wing. The previous simulator's extra `× 0.1` has no source and is dropped.

## A-CRIT: crits from air

**Rule.** As torpedoes: chance `COMBAT_TORPEDO_CRITICAL_CHANCE` (0.1), multiplier `COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT` (2); part damage chance `CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR / reliability`. [WIKI]

## A-LOSSES: plane losses and wing state

Planes are lost to A-INTERCEPT and A-AA-PREEMPT (and kamikaze self-losses: `AIR_NAVAL_KAMIKAZE_LOSSES_MULT`). Carrier wings keep their state for the rest of the battle; damaged wings fly with fewer planes. Plane experience changes per `AIR_WING_XP_*` (milestone 3).

## Why the old model was dropped

| # | Old behavior | Problem |
|---|---|---|
| 1 | AA reduction `1 - pool^POW × MULT`, clamped to [0, 1] | Ignored the 0.75 cap; with the TFB values (0.03 / 0.58) a large AA pool did very little |
| 2 | Damage `hits × navalAttack × 0.1 × carrierMult` | Unsourced `× 0.1`; HP/org strike factors unused (the ship's 0.5/1.0 gun factors applied instead) |
| 3 | Land-based wings fell back to *carrier* plane stats | With no carriers on that side, land planes had 0 naval attack and did nothing |
| 4 | Preemptive AA always killed out of 10 planes | Wrong for land-based groups |
| 5 | Sortie efficiency used non-vanilla define names, an "any screen gives the full bonus" rule, and × carrier organization | No source |
| 6 | Night disabled by testing values | Night is the main limiter of carrier operations |
| 7 | Fighters, interception, stacking cap, stances, air crits | Not modelled |
| 8 | Sortie every 8 h from battle start | Candidates: 6 h, or 8 h aligned to the clock |

## Milestone 2 implementation outline

1. `Battles/Air/`: `AirPhase` (orchestration per S-HOUR step 2), `CarrierWingState`, rules classes per item above (`SortieRules`, `AirTargetingRules`, `AntiAirRules`, `NavalStrikeRules`), each citing its `A-*` ID.
2. Inputs: per-participant carrier stance, land-based wing groups (plane design, count, mission efficiency) instead of a bare plane count.
3. `BattleRules` variants: cadence period/phase, targeting model, stacking curve, hit rounding.
4. Results: per-sortie records (planes flown, intercepted, shot down, hits, damage) as `ShotRecord` does for guns.
5. Tests: unit tests for each formula's worked values above, plus an engine test that land planes do damage without carriers (old bug 3).
