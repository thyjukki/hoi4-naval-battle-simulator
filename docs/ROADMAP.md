# Roadmap

The aim is a simulator that reproduces HOI4 naval combat as closely as possible, verified against the game's own files, its executable, and recorded battles ([mechanics/README.md](mechanics/README.md)).

## Milestone 1: foundation and surface combat (done)

- Projects split into `Engine` (pure simulation), `GameData` (loading and parsing), `Cli`.
- Clausewitz script parser and `00_defines.lua` parser; defines bound by exact key name, with vanilla 1.17 defaults, loadable from any install or mod.
- Surface combat rewritten as small rule classes tagged with spec IDs; uncertain mechanics are `BattleRules` variants.
- Legacy bugs fixed (see the table at the end of [mechanics/surface.md](mechanics/surface.md)); the TFB-tuned values are kept as an optional defines profile.
- Spec: [surface](mechanics/surface.md), [air (design)](mechanics/air.md), [defines](mechanics/defines.md); [reverse-engineering](reverse-engineering/README.md) and [calibration](calibration.md) workflows.

## Verification against hoi4.exe 1.19.3 (done)

Static analysis of the executable decoded the hour loop, firing order, activation, cooldowns, targeting, hit chance, damage, crits, retreat, submarines, positioning, screening, experience and air ([reverse-engineering/findings/](reverse-engineering/findings/README.md)). The surface engine was rewritten to match; built-in defines and static modifiers are now 1.19.3.

## Milestone 2: air combat overhaul (done)

Implemented from the [RE] findings: [mechanics/air.md](mechanics/air.md): sortie cadence and efficiency, carrier stances, the dynamic plane cap, land-based planes joining, targeting, preemptive AA, fleet AA reduction, hits and damage, air crits, plane losses. Scenario inputs for land-based wing groups and carrier stance. Left open: weather terms, kamikaze, port strikes, the top-speed interception term, plane experience.

## Milestone 3: surface completeness

- Submarines: hide/reveal, escape (done), air sub detection (S-SUB).
- Critical part damage with lasting effects (S-CRIT-PART); needs module categories on designs.
- Weather, sea region and night as data-driven modifiers; admiral skills and traits; doctrines and spirits as battle modifiers (`data/spirits` uses keys such as positioning, hitChance and retreatDecisionChance that the stat model cannot express; the loader warns about them).
- Late-joining ships, task-force-level retreat.

## Milestone 4: import everything from game files

Replace the hand-written JSON with data parsed from the game and mods using `PdxParser`: hulls and modules (`common/units/equipment/`), ship types (`common/units/`), technologies, MIOs, doctrines, experience levels. Ship designs stay as user input but reference game IDs. This supersedes `tools/import_hoi4_ship_modules.py`.

## Milestone 5: calibration harness

A runner that reads `data/calibration/*.json`, sweeps `BattleRules` variants and defines profiles, and ranks them against the recorded battles ([calibration.md](calibration.md)). Results feed back into the spec as [CAL] tags.

## Ongoing: reverse-engineering

Findings land in `docs/reverse-engineering/findings/` and turn [WIKI]/[ASSUMED] spec items into [RE] ones, in the priority order in [reverse-engineering/README.md](reverse-engineering/README.md).
