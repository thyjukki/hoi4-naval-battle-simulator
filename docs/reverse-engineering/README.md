# Reverse-engineering workflow

Script files only tell us constant values. The *formulas*, the order of operations and the hard-coded constants live in the executable. This page describes how a finding from the binary becomes part of the simulator.

## Ground rules

- Work only on your own, legitimately obtained copy of the game, for interoperability and research.
- **Never commit** the executable, disassembly, decompiler output, or game script files in bulk. Commit *descriptions*: the formula in your own words or short pseudocode, the defines it reads, and how you verified it.
- Always record the exact game version and a hash of the executable you analyzed. Formulas change between patches.

## Practical approach

1. **Find defines by name.** The game registers every define under its string name (that is how `00_defines.lua` binds to it). In Ghidra or IDA, search the binary's strings for a define such as `COMBAT_BASE_HIT_CHANCE`, follow the cross-reference to where it is registered, and find the global it is stored in. Cross-references to that global are the places where the formula uses it. Starting from a rare define (`COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT`, `NAVAL_STRIKE_TARGETTING_TO_AMOUNT`) lands you directly in the right routine.
2. **Use source file strings.** Assertions and logs often embed source paths. The wiki cites code from `combatnaval.cpp` (functions such as `CalcGunInfo()` and `ProcessShootingAtTarget()`), so search for `combatnaval` to find the naval combat module.
3. **Follow the hour tick.** From a known per-shot function, walk up the call graph to the per-hour loop. That loop answers S-ORDER, S-HOUR and A-CADENCE in one place.
4. **Cross-check with defines.** Change one define in a test mod, observe the effect in game, and confirm it matches your reading.

## Priorities

Ordered by how much they move battle outcomes and how little we know (spec IDs in [../mechanics/](../mechanics/)):

1. **S-ORDER / S-HOUR**: the per-hour loop. Do both sides fire simultaneously, interleaved, or by side? When are lines and screening recomputed?
2. **S-DMG-RANDOM**: which randomness formula current versions use (the two candidates differ by about 1.6× in mean damage).
3. **A-CADENCE**: carrier and land-based sortie timing, and their clock alignment.
4. **S-RETREAT / S-RETREAT-SPEED**: the retreat decision and escape speed.
5. **A-TARGET, A-STACK, A-INTERCEPT**: the new 1.17 air formulas.
6. **S-ACTIVATE**: whether activation delays depend on battle composition.
7. **S-HIT-MODS**: whether hit modifiers add or multiply; the screening bonus constant; night and weather.
8. **S-TARGET-WEIGHT**: the hard-coded target weights.
9. **S-XP**: the experience levels and their effects (probably a script file, not the executable).

## Turning a finding into code

1. Copy [finding-template.md](finding-template.md) to `findings/<SPEC-ID>-<short-name>.md` and fill it in.
2. Update the spec item: replace the old rule if needed, set the tag to [RE], and link the finding.
3. Change the code. If a `BattleRules` variant is now proven wrong, delete it, and add a regression test with the values from the finding.
4. If the finding reveals a new hard-coded constant, add it to `HardcodedValues` tagged [RE].
