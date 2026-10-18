# Naval air combat spec

Status: **implemented** in `src/NavySimulator.Engine/Battles/Air/` (`AirRules`, `AirPhase`, `AirGroup`, `AirWing`). The rules come from the reverse-engineering findings in [A-air-combat.md](../reverse-engineering/findings/A-air-combat.md) (hoi4.exe 1.19.3, `naval_fire_exchange_air.cpp`); each rule below names the function it was decoded from. Tags are explained in [README.md](README.md).

Planes do not use hit profiles, armor or piercing. [RE]

## Overview of one air step

An air step runs for each side **after that side's surface fire**, in air hours only (A-CADENCE). Within the step, each air group in turn does the following:
1. Planes flying: `round(planes × clamp(sortie, 0, 1))` for a carrier group (A-SORTIE); all joined planes for a land-based group (A-EXTERNAL).
2. Pick one target (A-TARGET).
3. Enemy carrier fighters intercept (A-INTERCEPT).
4. The target's AA fires once before the strike (A-AA-PREEMPT).
5. Hits (A-HIT) and damage (A-DMG), reduced by fleet AA (A-AA-REDUCE), with a possible crit (A-CRIT).

Air groups: one per carrier with wings (`fleet.carrierAirwingsByShipDesign`), plus one land-based group per side when `externalNavalStrikePlanes > 0` and `externalPlaneID` names a plane type. A sunk or escaped carrier's group stops flying.

## A-CADENCE: when planes strike [RE]

`airHour ⇔ hourOfDay mod (2 × HOURS_DELAY_AFTER_EACH_COMBAT) == regionPhase` (`0xe24530`). With the vanilla 4 h delay, every air group in the battle acts at the three hours of each day where `hour % 8 == regionId % 8`. The phase follows the game clock and the strategic region, not the battle start. Hour 0 counts.
- `rules.airRegionPhase` (default 4, which gives the wiki's 04/12/20) and `rules.startHourOfDay` set the phase and the clock.
- `CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT` **has no effect**: the cooldown it sets is written but read only by a dead eligibility list. Land-based groups also strike at every air hour (their "struck once" flag is dead too).
- The air step does not check S-ACTIVATE.

## A-SORTIE: how many carrier planes fly [RE]

`0x1976db0` / `0x19786c0`:
```
ratio   = CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[stance]; defender uses 1 − ratio
base    = (hour < SELECTED_SORTIE_INITIAL_TIME ? ratio : BASE_CARRIER_SORTIE_EFFICIENCY)
        + carrier sortie_efficiency modifiers (spirits with sortieEffiency)
        + screeningEff × screening_bonus.sortie_efficiency
        + carrierScreening × capital_screening_bonus.sortie_efficiency     (vanilla 0.1)
        + fighter_sortie_efficiency from carrier experience, if the first wing is a fighter
crowd   = planes ≤ cap ? 1 : (r = (planes − cap)/cap) > 0.9 ? 0 : clamp(1 − CAPACITY_PENALTY × r, 0, 1)
          with cap = deck size × CARRIER_SIZE_STAT_INCREMENT
night   = clamp(1 + darkness × night.carrier_traffic, 0, 1)                 (vanilla −1: no sorties at night)
sortie  = max(0, base × crowd × night) × org / maxOrg
flying  = round(allPlanesOnTheCarrier × clamp(sortie, 0, 1))
```
- The clamp comes only at the end, so a sortie efficiency above 1 offsets low org.
- `planes` counts all wings on the carrier, fighters included.
- The stance does not split planes into offence and defence (A-STANCE).
- Not modelled: weather `carrier_traffic`, the country `carrier_night_traffic`, `carrier_capacity_penalty_reduction`, doctrine and leader modifiers.

## A-STANCE [RE]

The stance index comes from `carrierStanceIndex` per participant, with default `CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX`. It is used in two places:
1. As the sortie base for the first 24 h (A-SORTIE).
2. In interception: `defShare = 1 − ratio` of the defending side's stance.

## A-STACK: stacking factor [RE]

`0x161e9a0` / `0x1c6a540`:
```
cap = max(NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY,
          10·capitals + 16·carriers + 7·subs + 4·convoys + ceil(5·screens / (1 + SCREEN_CAP_REDUCTION_FACTOR × 10·capitals)))
F   = ownCarrierPlanes > cap ? 1 / (1 + NAVAL_COMBAT_PLANE_STACKING_PENALTY_EFFECT × excess) : 1
```
- The cap counts the enemy's visible ships that are still in the battle; hidden subs do not count. Land-based planes are not in `ownCarrierPlanes`.
- The game also counts support ships at 3 (`SHIP_SILHOUETTE_VALUE_PLANES_SUPPORT`). The simulator has no support role.
- F does **not** reduce the planes that fly. It shrinks two things: the strike wings used for the stat averages (`max(1, round(n × F))`), and the AA base (`ceil((flying − intercepted) × F)`).

## A-EXTERNAL: land-based planes [RE]

```
cap = max(NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP,
          floor(Σ enemy warship current HP × JOIN_RATIO × (1 + JOIN_RATIO_PER_DAY × floor(hour / 24))))
```
- The cap is **cumulative** over the battle; lost planes do not free room.
- Land-based planes skip the sortie step and get no carrier multiplier.
- Their attack is multiplied by `externalMissionEfficiency`, an input (range, weather, airbase and fuel are not simulated).
- Port strikes are not modelled.

## A-INTERCEPT: carrier fighters vs strike planes [RE]

`0x197b380`. The defenders are all fighter wings on the target side's carriers that are still in the battle; land-based fighters are not used.
```
D       = CARRIER_PERCENTAGE_DEFEND × Σ defender planes × defShare
engaged = min(D, Σ attacking strike-wing planes × COMBAT_MULTIPLANE_CAP)
dmg     = engaged × CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER × avg defender air attack
agiRed  = aAgi > dAgi ? (min(aAgi/dAgi, BIGGEST_AGILITY_FACTOR_DIFF) − 1) × dmg × COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION : 0
spdInc  = dSpd > aSpd ? (min(dSpd/aSpd, BIGGEST_SPEED_FACTOR_DIFF) − 1) × dmg × COMBAT_BETTER_SPEED_DAMAGE_INCREASE : 0
x       = max(0.001, (dmg + spdInc − agiRed) × 0.01 / avg attacker air defence)
losses  = floor(x) + (frac(x) > U ? 1 : 0)
```
- The averages are unweighted means per wing.
- Defenders take no losses, and there is no disruption.
- The top-speed term of the speed helper is not decoded and not modelled. [RE, partial]
- `COMBAT_DAMAGE_SCALE_CARRIER` and `DISRUPTION_FACTOR_CARRIER` are not read in naval combat.

## A-TARGET: target selection [RE]

`0x197a700`, a weighted random draw over the visible enemy ships that are in the battle:
```
t      = TYPE_BASE × S + TYPE_SCALE × (1 − S)          S = target side's screening efficiency
weight = maxHP × t × (1 + NAVAL_COMBAT_AIR_STRENGTH_TARGET_SCORE × (1 − HP/maxHP))
       + NAVAL_COMBAT_AIR_LOW_AA_TARGET_SCORE × clamp(5 − AA, 0, 5)
```
The low-AA term is additive and, at real weights, negligible. Each group picks one target per strike.

## A-AA-PREEMPT: preemptive shoot-down [RE]

`0x197c8b0`. Only the target fires.
```
chance = ANTI_AIR_TARGETTING_TO_CHANCE × max(1.5 × ANTI_AIR_TARGETING − 0.5 × agi, 0.01)
         agi = AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY × average agility of the selected strike wings
fires if U ≤ chance
kills  = randomly rounded ceil((flying − intercepted) × F) × clamp(targetAA × ANTI_AIR_ATTACK_TO_AMOUNT, 0, 1)
```
Convoys have no AA. Kamikaze is not modelled.

## A-AA-REDUCE: fleet AA damage reduction [RE]

`0x196fca0`:
```
fleetAA   = Σ warship AA on the target side × SHIP_TO_FLEET_ANTI_AIR_RATIO × side positioning
reduction = min(MAX_ANTI_AIR_REDUCTION_EFFECT_ON_INCOMING_AIR_DAMAGE,
                ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE × (fleetAA + targetAA)^ANTI_AIR_POW_ON_INCOMING_AIR_DAMAGE)
```
Convoys add nothing, either to the side total or as the target.

## A-HIT: how many planes hit [RE]

```
strikers = flying − intercepted − aaKills
hits     = round(strikers × clamp(avgTargeting × NAVAL_STRIKE_TARGETTING_TO_AMOUNT, 0, 1) × U)
```
- `avgTargeting` is `0.1 × designer naval targeting`, plane-weighted over the selected strike wings.
- The uniform U halves the expected hits.
- Strikers include fighters, because `flying` counts all carrier planes.
- Weather `naval_strike` is not modelled.

## A-DMG: strike damage [RE]

```
attack = 0.1 × plane-weighted designer naval attack of the selected strike wings
       × (NAVAL_STRIKE_CARRIER_MULTIPLIER for a carrier group | externalMissionEfficiency for land-based)
base   = hits × attack × (1 − reduction)
HP    -= base × NAVAL_STRIKE_DAMAGE_TO_STR;  org -= base × NAVAL_STRIKE_DAMAGE_TO_ORG
```
There is no damage randomness and no HP-scaled org damage. Sinking and retreat follow the surface rules (`DamageRules.Apply`).

## A-CRIT: crits from air [RE]

```
chance = max(0.001, COMBAT_BASE_CRITICAL_CHANCE × (1 + COMBAT_ARMOR_PIERCING_CRITICAL_BONUS) × (1 − min(rel, 1)))   (0 if rel ≥ 1)
mult   = 1 + COMBAT_CRITICAL_DAMAGE_MULT × (1 − rel) / hits
```
- Air crits use the gun crit defines, not the torpedo ones.
- In the game, a part hit (`CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR / rel`) replaces the multiplier. No part data is loaded, so the simulator always applies the multiplier and consumes the roll.
- `critical_receive_chance` is not modelled. In the game it acts as `(1 − v)`, so a negative value raises the chance.

## A-LOSSES [RE + ASSUMED]

- Planes are lost to interception and to preemptive AA. Air groups persist for the whole battle.
- [ASSUMED] Losses come off the wings in order. The game's split across wings is not decoded.
- Plane experience is not modelled.

## Experience from sorties [RE]

Carriers gain experience per sortie as part of S-XP (`ExperienceRules`).

## Not modelled

The weather terms (`carrier_traffic`, `naval_strike`), the country, doctrine and leader modifiers, kamikaze, port strikes, support-ship silhouettes, the top-speed interception term, plane experience, and how losses are split across wings.

## Inputs

| Field | Where | Meaning |
|---|---|---|
| `carrierAirwingsByShipDesign` | force composition | wings per carrier design (plane ID, type, count, planes per wing) |
| `externalNavalStrikePlanes`, `externalPlaneID`, `externalMissionEfficiency` | participant | land-based naval strike planes |
| `carrierStanceIndex` | participant | index into `CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO` |
| `rules.airRegionPhase`, `rules.startHourOfDay`, `rules.nightEnabled` | scenario | clock phase and night |

Each strike is reported as an `AirStrikeRecord`: hour, side, source, target, planes flying, intercepted, shot down, hits, AA reduction, crit, HP and org damage, and sunk.
