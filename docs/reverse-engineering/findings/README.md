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
| [A-air-combat.md](A-air-combat.md) | A-* (implemented in `Engine/Battles/Air/`) |

## Shared facts

- Defines are stored as int64 fixed point with scale 100000: `stored = round(float32(luaValue) × 100000)`. A few use binary fixed point (2^15) instead. Every multiply and divide truncates toward zero at that precision. The simulator uses doubles, which only matters for very small values.
- The random number generator returns `U = (hash % 100000) / 100000`, uniform over {0, 0.00001, …, 0.99999}.
- Modifier names were mapped from the modifier registration function (`MODIFIER_*` keys). Where a finding says "medium confidence" on a name, the behavior is decoded but the name is inferred.
- The tooling's cross-reference scan misses reads inside functions without unwind entries (small leaf functions) and, before a fix, some reads where it lost instruction alignment. "No reader found" claims were rechecked by hand where they mattered (e.g. `COMBAT_MIN_DURATION` is read by the leaf function `0x15c28e0`).

## Disagreements between findings, and how they were settled

- **Weapon slot order.** One finding listed slots as light/heavy/torpedo. The cooldown, hit and damage findings independently show {0 heavy, 1 torpedo, 2 light}, with depth charges in the light slot. That matches `BASE_GUN_COOLDOWNS` and the torpedo-only cooldown modifier on slot 1.
- **`COMBAT_MIN_DURATION`.** One finding called it unused. It is read by `0x15c28e0` (`hours >= COMBAT_MIN_DURATION`), which gates the retreat decision.
- **Night.** The hour-loop finding saw no direct darkness read in surface fire; the hit-chance finding traced `naval_hit_chance` through the generic modifier gather, which includes the battle province. The `night` static modifier is applied to provinces "multiplied by amount of darkness", which reconciles both: night affects hit chance (−0.25) and retreat speed (+0.1) through province modifiers. The darkness value itself is not decoded.

## Unsettled disagreements (to resolve on the next decoding pass)

Listed in [index.md](../index.md) as "**disagrees**". The engine follows the reading noted in brackets where it matters.
1. Member state bits 0x10 / 0x20: escaped / sunk (HOUR, ACT, TGT; set at 0x1975bea) vs sunk / left (POS). [escaped / sunk]
2. ECombatBox 4 / 5: hidden / revealed (ACT, TGT) vs revealed / hidden (HIT).
3. Hour counter order: Update then `hours++` in 0x15c3690 (HOUR) vs increment first (ACT); ties to the POS question whether hours is 0 or 1 in the first hour.
4. Side vt+0x70 (0x161f790): leader XP (HOUR) vs fire phase (POS, medium).
5. Positioning modifier 0x5a gate: `side+0xd8 == 0` (POS) vs side not all retreating (HOUR).
6. Visibility modifier in the sub reveal: 0x19f naval_visibility_factor (ACT) vs 0x1ba/0x1bc (POS).
7. combatant+0x130: commanding admiral (POS) vs battle location / context (HIT, AIR).
8. combat+0x10c / +0x10d: convoy or special combat (HOUR, ACT) vs port-strike flag (AIR) vs combatant flags (TGT).
9. 0x161ece0: combined `1-(1-s)(1-c)` (TGT) vs `side+0x150` (AIR, presumed). [combined]
10. Modifier 0x29: NAVAL_SPEED_FACTOR (HIT) vs the stat that gates submarine targets (TGT).
11. naval_night_attack: no read (HOUR) vs NAVAL_NIGHT_ATTACK × night factor in 0xc34a50 (DMG). Not modelled.
