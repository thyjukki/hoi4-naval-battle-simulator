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

## Status

The first pass over hoi4.exe 1.19.3 decoded every surface and air spec item (checked against 1.19.4: [Patch
1.19.4](#patch-1194)); see [findings/README.md](findings/README.md). Check [index.md](index.md) (functions, fields, modifier ids, define read sites) before decoding anything. The tooling is in `tools/re/`. Open questions that remain, ordered by impact:

1. **Aggressiveness check** (S-RETREAT): the strength score `0x1989c60` that makes outmatched task forces retreat.
2. **Darkness** (S-NIGHT): how the province darkness value is computed from the time of day.
3. **Critical parts** (S-CRIT-PART): how `0xc31030` aggregates part instance caps; needs part data loaded from `common/units/critical_parts`.
4. **Light-slot submarine weight** (S-TARGET-DC): which shooter stat (ship+0x228) gates targeting submarines.
5. **Air**: what sets the "air every hour" flag, the external efficiency source, and the port-strike branch (A-*).
6. Positioning and retreat modifiers by name (admiral and doctrine effects).

## Patch 1.19.4

Steam updated the game to 1.19.4.0 on 2026-10-27 (hoi4.exe SHA-256
`B6650C85002374218E60598BD04AAA26CBAD996FEDCE77126FCF92FC6BD6D888`). Checked on 2026-10-28:

- **Addresses.** Every RVA moved. `tools/re/remap.py` matched 106,077 of 138,286 1.19.3 functions (88,777 with identical
  code once rip-relative and call targets are masked). The logger's 30 functions and probes, its forced-roll call sites
  and the tick (unmatched by code, found as the caller of both the hourly and the daily update) were mapped and checked
  by hand; they are the `RVA_1_19_4` table in `tools/ingame/battle_logger.js`. The carrier cooldown store keeps its value
  in `edi` (was `edx`).
- **Unchanged:** the modifier ids (`modids.py`, 648 ids); the source lines of every combat roll (`randlines.py`), except
  the ship recovery roll (taskforce.cpp 0xb31 → 0xb2d) and the `random.h` helper (0x96 → 0x95); the struct offsets the
  combat code and the logger read (ship, fire-exchange member and group, naval combatant, combat, task force, leader,
  AirFX, wing). Moved: equipment type fields from +0x3c4 by +0x60, some country fields (+0x290 → +0x2d8),
  `CBuildingProductionLine` (the new repair queue). 1.19.4 passes an empty string as the source file to asserts and
  random draws.
- **Scripts:** no combat define, static modifier, weather setting, hull, module, critical part, naval doctrine or naval
  MIO changed (`common/` diffed; every importer re-run over 1.19.4 + TFB gave identical data). Vanilla's Proximity Fuze
  tech went from `navy_anti_air_attack_factor` 0.25 to 0.2; TFB replaces that file.
- **In game** (TFB dev checkout, `output/ingame/v1194/`): constant-roll battles CL3 (U 0.05), mixed fleets (U 0.02) and
  MIO + admirals (U 0.05) log byte for byte what 1.19.3 logged; five random seeds of the mixed fleets (18–22, 53,000
  shots) give the same shots, hours and every `random_fixed` roll as their 1.19.3 logs; the engine replays them as
  before. The carrier battles (`air_cv_mirror`, `air_cv_bombers_vs_fighters`) match every shot and strike.
- **One rule changed:** RemovePlanes picks each lost plane at random (A-MIXED-REMOVE,
  [A-MIXED](findings/A-MIXED-plane-variants-in-a-wing.md#1194-a-random-plane-per-loss-removeplanes-0xfe7a10)); the land
  plane battles diverged from hour 26 until the engine got the rule (`BattleRules.PlaneLosses`) and the logger forced
  the new draws.

The [RE] findings keep their 1.19.3 addresses; map one into 1.19.4 with `python tools/re/remap.py <1.19.3 exe> <1.19.4
exe> 0x...` (a copy of the 1.19.3 exe is needed; Steam's betas list has `1.19.3.0`).

## Turning a finding into code

1. Copy [finding-template.md](finding-template.md) to `findings/<SPEC-ID>-<short-name>.md` and fill it in.
2. Update the spec item: replace the old rule if needed, set the tag to [RE], and link the finding.
3. Change the code. If a `BattleRules` variant is now proven wrong, delete it, and add a regression test with the values from the finding.
4. If the finding reveals a new hard-coded constant, add it to `HardcodedValues` tagged [RE].
