# Findings: hoi4.exe 1.19.3

All findings in this folder come from static analysis of one build:

| | |
|---|---|
| Game version | 1.19.3.0 "Operation Postern" (`hoi4_rev.txt` c01a3d507f10bd8c713e88234de69ca110618138) |
| Executable SHA-256 | `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5` |
| Method | Python + capstone over the PE image, starting from define-name strings and assert source paths (see `tools/re/`) |

Addresses are RVAs (image base 0x140000000). They are evidence for this build only; other patches move them.

Lookup index of every function, struct field, modifier id and define read site in these findings: [../index.md](../index.md).

| Finding | Spec items |
|---|---|
| [S-HOUR-order-and-end.md](S-HOUR-order-and-end.md) | S-HOUR, S-ORDER, S-END, fire-exchange groups, night |
| [S-ACTIVATE-activation-cooldown-retreat-subs.md](S-ACTIVATE-activation-cooldown-retreat-subs.md) | S-ACTIVATE, S-COOLDOWN, S-RETREAT, S-RETREAT-SPEED, S-SUB |
| [S-TARGET-targeting-and-screening.md](S-TARGET-targeting-and-screening.md) | S-TARGET-*, S-SCREEN, screening bonus |
| [S-HIT-hit-chance-and-crits.md](S-HIT-hit-chance-and-crits.md) | S-HIT-*, S-CRIT, S-DMG-PIERCE |
| [S-DMG-damage-and-application.md](S-DMG-damage-and-application.md) | S-DMG, S-DMG-RANDOM, S-DMG-APPLY, S-CRIT-PART, damage-side S-XP |
| [S-POS-positioning.md](S-POS-positioning.md) | S-POS-* |
| [S-XP-experience.md](S-XP-experience.md) | S-XP, S-MANPOWER |
| [S-STATS-design-stats.md](S-STATS-design-stats.md) | S-STATS-* (design, MIO, equipment bonuses, technologies, combat getters incl. the fully decoded attack/armor getter) |
| [S-STATS-COUNTRY-modifier-sources.md](S-STATS-COUNTRY-modifier-sources.md) | S-STATS-COUNTRY: modifier names by id (registration + token table), the modifier sums, the admiral's containers, which modifiers a country source can change |
| [A-air-combat.md](A-air-combat.md) | A-* (implemented in `Engine/Battles/Air/`) |
| [A-WINGS-carrier-wings-and-reinforcement.md](A-WINGS-carrier-wings-and-reinforcement.md) | A-WINGS, A-EXTERNAL, A-REINFORCE (air objects per wing pool, strike wings, land-based joins, daily reinforcement) |
| [A-MISSION-SAVE-air-mission-save-format.md](A-MISSION-SAVE-air-mission-save-format.md) | Save format of air missions over a strategic region (land-based wings in generated saves), executing mission choice |
| [A-ACE-aces-and-wing-experience-in-naval-strikes.md](A-ACE-aces-and-wing-experience-in-naval-strikes.md) | A-ACE (ace earn and death rolls after naval strikes, templates, the per-wing stat bonus) |
| [A-REGION-carrier-wings-in-the-region-air-war.md](A-REGION-carrier-wings-in-the-region-air-war.md) | A-REGION (the region's air war of the battle's wings: lists, pairing, losses, disruption, escorts, the strike efficiency 0xf78160) |
| [S-WEATHER-weather-toggle.md](S-WEATHER-weather-toggle.md) | S-WEATHER (weather console toggle, the weather a battle reads) |
| [LOG-ingame-logging.md](LOG-ingame-logging.md) | Objects and hooks behind `tools/ingame/` (logging battles from the running game); observed: replay by seed, hour counter, no night with weather off |
| [S-DOMINANCE-naval-dominance.md](S-DOMINANCE-naval-dominance.md) | S-DOMINANCE (ship dominance, a mission's dominance, region control for the organization regain; calibrated on saved region values and logged regain) |
| [S-DOCTRINE-doctrines.md](S-DOCTRINE-doctrines.md) | S-DOCTRINE-* (track mastery, rewards, milestones, bank, save layout, where doctrine effects go, `has_doctrine` and mastery triggers), S-SPIRIT-SLOTS (from the scripts) |
| [S-MIO-include-and-tree.md](S-MIO-include-and-tree.md) | S-MIO-INCLUDE, S-MIO-TREE (MIO reader, `include` / `trait` / `add_trait` / `override_trait` / `remove_trait` / `delete_included_values` resolution, trait merge, positions, parents, mutual exclusion, the unlock check) |
| [S-ADMIRAL-leader-combat.md](S-ADMIRAL-leader-combat.md) | S-ADMIRAL-* (leader efficiency, task-force cap and reassignment cooldown; the modifier definition flags and what the efficiency scaling does per flag; skill-level lookup and clamping; `sub_unit_modifiers` and the terrain/night adjusters; the admiral in combat speed, positioning, screening and the retreat roll; every other read of the commanding admiral; the damaged-part effect factor) |
| [S-MIO-equipment-matching.md](S-MIO-equipment-matching.md) | S-MIO-MATCH, S-MIO-POLICY, S-STATS-MIO (the equipment-type match of `limit_to_equipment_type`, `equipment_type`, policy and equipment bonus keys; a design's type set is its hull's; the bonus sum; the assignment check; the bonus is a snapshot per variant; checked against a TFB 1936 save) |
| [S-POTF-pride-of-the-fleet.md](S-POTF-pride-of-the-fleet.md) | S-POTF (pride of the fleet: every reader of the country's handle, the in-combat attack/defense term and its "same combat" test per member state, the PotF ship's static block in the modifier sum, the full experience-gain formula with its sources, the air crit sign, who can be assigned, losing it, the save keys), S-STATS-COMBAT, S-CRIT, A-CRIT, S-XP |

## Shared facts

- Defines are stored as int64 fixed point with scale 100000: `stored = round(float32(luaValue) × 100000)`. A few use binary fixed point (2^15) instead. Every multiply and divide truncates toward zero at that precision. The simulator uses doubles, which only matters for very small values.
- The random number generator returns `U = (hash % 100000) / 100000`, uniform over {0, 0.00001, …, 0.99999}.
- Modifier names were mapped from the modifier registration function (`MODIFIER_*` keys). Where a finding says "medium confidence" on a name, the behavior is decoded but the name is inferred.
- The tooling's cross-reference scan misses reads inside functions without unwind entries (small leaf functions) and, before a fix, some reads where it lost instruction alignment. "No reader found" claims were rechecked by hand where they mattered (e.g. `COMBAT_MIN_DURATION` is read by the leaf function `0x15c28e0`).

## Disagreements between findings, and how they were settled

- **Weapon slot order.** One finding listed slots as light/heavy/torpedo. The cooldown, hit and damage findings independently show {0 heavy, 1 torpedo, 2 light}, with depth charges in the light slot. That matches `BASE_GUN_COOLDOWNS` and the torpedo-only cooldown modifier on slot 1.
- **`COMBAT_MIN_DURATION`.** One finding called it unused. It is read by `0x15c28e0` (`hours >= COMBAT_MIN_DURATION`), which gates the retreat decision.
- **Night.** The hour-loop finding saw no direct darkness read in surface fire; the hit-chance finding traced `naval_hit_chance` through the generic modifier gather, which includes the battle province. The `night` static modifier is applied to provinces "multiplied by amount of darkness", which reconciles both: night affects hit chance (−0.25) and retreat speed (+0.1) through province modifiers. The darkness value itself is not decoded.
- **Equipment bonus lists.** S-STATS placed the idea equipment bonuses and the variant list at `country+0x160` and `country+0xa0`. DOC shows both are offsets in the equipment manager `[country+0xf68]` (callers 0xbafd23 → 0xe5f600 and 0x49ba22 → 0xe60110 load `[country+0xf68]` first; 0xe60110 reads `[r15+0xa0]`). The arithmetic in S-STATS is unaffected.
- **Leader-efficiency scaling 0x60fbd0.** STC read the definition flags as "bit 1 = scale, bit 2 = keep negatives"
  (medium). ADM: the flags are the fourth argument of the registration call 0x5574d0; bit 1 (positive is good) scales
  every nonzero entry, otherwise bit 2 (neutral) leaves the entry, otherwise (positive is bad) only negative entries are
  scaled ([S-ADMIRAL-leader-combat.md](S-ADMIRAL-leader-combat.md#2-the-scaling-0x60fbd0-and-the-modifier-flags)).
  STC's open item on CNavyLeader vt+0x88 / vt+0x90 and the cap 0xc18780 is settled there too (both size factors are 1
  for a navy leader).
- **Country MIO `+0x118` / `+0x134`.** S-MIO-INCLUDE called `+0x118` the template and `+0x134` "trait points?". The country MIO init 0xdb3c80 stores template + 0x88 at `+0x110` and template + 0x218 (the `allowed` / `available` / `visible` part) at `+0x118` (0xdb3df3..0xdb3e08), so `+0x118` reaches the template's triggers at `+0x58` / `+0xb0`, which is what the unlock check uses. `+0x134` is the trait points: UnlockTrait 0xdbc860 asserts `_TraitPoints > 0` and decrements it. No behaviour in S-MIO-INCLUDE changes ([S-MIO-equipment-matching.md](S-MIO-equipment-matching.md)).
- **Air objects, join cap, external efficiency (AIR vs WINGS).** AIR read one air object per carrier with "all wings on
  the carrier, fighters included" in `airfx+0x5c`, a cumulative join count `side+0x3e8`, and `airfx+0xc0` as mission
  efficiency. WINGS ([A-WINGS](A-WINGS-carrier-wings-and-reinforcement.md)) traced the constructor path: one air object
  per wing **pool** (one plane sub-unit at one base, 0x1c6a7c0 → 0x1976630 / 0x1979aa0), so `+0x5c` is that pool's
  planes (the deck crowding factor still sums the whole carrier); the hourly air update 0xc57d30 resets `side+0x3e8` to 0
  in every air hour before the joins (0xc584e4); and `+0xc0` comes from 0xf78160, the mission's disruption factor (the
  mission efficiency scales the planes offered). The fighter tests 0xf63100 / 0x1976f55 read the pool's sub-unit
  `type`, not the equipment flags. The engine follows WINGS.

## Unsettled disagreements (to resolve on the next decoding pass)

Listed in [index.md](../index.md) as "**disagrees**". The engine follows the reading noted in brackets where it matters.
1. ~~Member state bits 0x10 / 0x20~~ **Settled: 0x10 = escaped, 0x20 = sunk.** 0x10 is written at 0x19739bd right after the escape progress `member+0x130` reaches 1.0 (0x1973926). 0x20 is written at 0x1975bea in 0x1975480, which the shot function 0x19740c0 and the air strike 0x197ac80 call only after the HP test 0x1ad8680 (`hp <= 0`). POS is corrected.
2. ECombatBox 4 / 5: hidden / revealed (ACT, TGT) vs revealed / hidden (HIT).
3. Hour counter order: Update then `hours++` in 0x15c3690 (HOUR) vs increment first (ACT); ties to the POS question whether hours is 0 or 1 in the first hour.
4. Side vt+0x70 (0x161f790): leader XP (HOUR) vs fire phase (POS, medium).
5. Positioning modifier 0x5a gate: `side+0xd8 == 0` (POS) vs side not all retreating (HOUR).
6. ~~Visibility modifier in the sub reveal~~ **Settled: 0x19f** (script key `navy_visibility`), inside the sub visibility getter 0xc3aa20 (`sub_visibility × (1 + Mod(0x19f))`). 0x1ba (`naval_critical_score_chance_factor`) is read only in 0x19740c0; 0x1bc (`naval_torpedo_reveal_chance_factor`) only at 0x1973ff7, the torpedo-firing reveal. POS is corrected.
7. ~~combatant+0x130~~ **Settled: commanding admiral** (a CUnitLeader). 0xc38760 uses its fourth argument as a leader (holder `+0x408` → container `+0x418`, sub-unit adjusters 0xc19bf0) and falls back to the task force's fleet admiral 0xd64510 when it is 0; the shot profile (0x1970b12), the member getter (0x19725d7), air targeting and preemptive AA pass `[side+0x130]`. HIT and AIR are corrected by [S-STATS-COUNTRY](S-STATS-COUNTRY-modifier-sources.md).
8. combat+0x10c / +0x10d: convoy or special combat (HOUR, ACT) vs port-strike flag (AIR) vs combatant flags (TGT).
9. ~~0x161ece0~~ **Settled: combined.** It returns `1 − (1 − [side+0x148]) × (1 − [side+0x150])` in fixed point. 0x161e400 passes `side+0x148` and `side+0x150` as the two outputs of 0x161ccb0, which writes screening to the first and carrier screening to the second. AIR is corrected. The engine (`ScreeningRules.CombinedCarrierScreening`) already matched.
10. Modifier 0x29: NAVAL_SPEED_FACTOR (HIT) vs the stat that gates submarine targets (TGT).
11. ~~naval_night_attack~~ **Settled: read** (id 0x107) in the attack getter 0xc34a50 (0xc34de4), × the darkness of the shooter task force's province from 0xf19660 (0xc34dc7), attack kinds only. DMG was right; HOUR is corrected. Not modelled.
12. 0xf80f60 after a naval strike: wing experience (A-air-combat, A-WINGS: "losses and damage, i.e. experience") vs the ace earn and death rolls with no experience gain ([A-ACE](A-ACE-aces-and-wing-experience-in-naval-strikes.md)). The logged roll lines (0xb68 per wing, 0xb52 when planes were lost, 0xb72 after an earn) fit A-ACE; the engine follows it.
13. 0xc46030 / the wing update 0xf62750: daily (A-MISSION-SAVE, so the region change penalty decays slowly) vs the body of the hourly job 0x2959f80 ([A-REGION](A-REGION-carrier-wings-in-the-region-air-war.md)). Only matters for missions whose region changed; generated saves write `region_change_penalty=0`.
14. Carrier wings on the region mission list `+0x98`: medium-low in A-REGION (SetRegion by 0xc647b0 not traced). The logs settle it for the CL3 template: the combat attacker's carrier wings had disruption (only list missions get it) and the defender's never did; what decides it in general is open, so it is a scenario input (`carrierWingsOnRegionMission`).
