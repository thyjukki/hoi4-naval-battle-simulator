# Roadmap

The aim is a simulator that reproduces HOI4 naval combat as closely as possible, verified against the game's own files, its executable, and recorded battles ([mechanics/README.md](mechanics/README.md)).

## Status (2026-10-18)

**Matches hoi4.exe 1.19.3** (decoded, engine rewritten to it): ship stats (S-STATS), hour loop and firing order, activation and cooldowns, targeting and screening, hit chance, damage, crits, retreat, submarines, positioning, experience and manpower, and air combat. Built-in defines and static modifiers are vanilla 1.19.3; a mod folder (`--defines`) overrides them.

**GUI** (2026-10-18): a local web app (`src/NavySimulator.Web`, [gui.md](gui.md)) edits scenarios, fleets and designs, steps battles hour by hour with rewind and per-ship combat logs, and runs batches. The engine's hour loop is now `BattleSession`, steppable, with a per-hour timeline and structured events.

**Not yet compared against a recorded battle.** "Matches the code" is not "matches the results" until calibration cases exist.

**Gaps, by impact:**
1. Combat-time modifier layer: doctrines, admiral skills/traits, spirits, `naval_*_factor` attack/armor/speed (formulas decoded in S-STATS-COMBAT). `data/spirits` is ignored with loader warnings until then.
2. Weather (hit chance, carrier traffic, naval strike targeting). `rules.weatherEnabled = false` already matches the game with `disable_weather` (S-WEATHER).
3. Darkness per sea region; currently [ASSUMED] full dark 20:00 to 06:00.
4. Critical part damage and lasting penalties (S-CRIT-PART, S-STATS-PARTS).
5. Late-joining ships, the aggressiveness check (strength retreat), task-force retreat; air: kamikaze, port strikes, the top-speed interception term, plane experience, loss split across wings.
6. Nine unsettled cross-finding disagreements ([findings README](reverse-engineering/findings/README.md)); none changes current results.

**Next steps:**
- Record 3 to 5 TFB battles per [calibration.md](calibration.md):
  - a mirror match (the attacker should win about two thirds);
  - one carrier battle, noting the hours air damage appears (strike timing and region phase);
  - a small battle with the hourly log (hit rates, damage).
- Build the combat-modifier layer (gap 1) in parallel.
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

## GUI (done)

Local Blazor Server app in `src/NavySimulator.Web` ([gui.md](gui.md)): scenario editor with live validation, fleet and design editors writing back to `data/` (design stats with researches and comparisons), Battle page with +1/+10/+N hours, run to end, rewind to any hour, charts, damage dealt split by weapon, ship type and design, fleet trees by ship type, per-ship combat logs and filterable shot/event/air logs, Batch page with outcome distributions and replay by seed, Library, and Settings with a "Simulate as" switch between vanilla and any HOI4 install, local (launcher) mod or workshop mod found on the computer. Engine and loader changes behind it: `BattleSession`, `BattleEvent`/`HourSummary` results, `ContentCatalog`/`BuildScenario`, `CatalogWriter`.

Possible next steps: check modules against hull slots (needs the slot data from milestone 4), edit hulls/modules/researches, A/B batches of two scenarios, a calibration view that overlays a recorded battle (milestone 5).

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
