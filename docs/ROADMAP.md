# Roadmap

The aim is a simulator that reproduces HOI4 naval combat as closely as possible, verified against the game's own files, its executable, and recorded battles ([mechanics/README.md](mechanics/README.md)).

## Status (2026-10-19)

**Matches hoi4.exe 1.19.3** (decoded, engine rewritten to it): ship stats (S-STATS), hour loop and firing order, activation and cooldowns, targeting and screening, hit chance, damage, crits, retreat, submarines, positioning, experience and manpower (including the daily refill in combat), and air combat. Built-in defines and static modifiers are vanilla 1.19.3; a mod folder (`--defines`) overrides them.

**GUI** (2026-10-18): a local web app (`src/NavySimulator.Web`, [gui.md](gui.md)) edits scenarios, fleets and designs, steps battles hour by hour with rewind and per-ship combat logs, and runs batches. The engine's hour loop is now `BattleSession`, steppable, with a per-hour timeline and structured events.

**Calibration** (2026-10-19): [`tools/ingame/`](../tools/ingame/README.md) replays a saved battle in the game for any number of dice seeds, unattended (about a minute each), and logs every combat hour and every shot from inside the game ([LOG finding](reverse-engineering/findings/LOG-ingame-logging.md)). First case, the TFB CL3 battle (`data/battle-scenario-la-vs-torp-cl3.json`, save `light_attack_vs_torp_cruiser_battle_start.hoi4`), 12 game seeds: 336 h, BUL 7075 HP left (sd 161), ROM 2802 (sd 72), 1.2 ROM ships sunk. The shot logs found two engine errors, now fixed: night was applied with weather off, and `naval_damage_factor` counted once instead of twice. The last difference is TFB's `smoke_generator` technology (`naval_retreat_speed = 0.2`), which the engine cannot express yet; standing it in with `BASE_ESCAPE_SPEED = 0.26`, 200 simulator seeds give 338 h, 7056, 2801 and 0.9 sunk.

**Gaps, by impact:**
1. Combat-time modifier layer: research and country modifiers (first `naval_retreat_speed`, which the CL3 case needs), doctrines, admiral skills/traits, spirits, `naval_*_factor` attack/armor/speed (formulas decoded in S-STATS-COMBAT). Only the experience terms are modelled. `data/spirits` is ignored with loader warnings until then.
2. Weather (hit chance, carrier traffic, naval strike targeting). `rules.weatherEnabled = false` matches the game with `disable_weather`, including no night (S-WEATHER, S-NIGHT).
3. Darkness per sea region; with weather on it is [ASSUMED] full dark 20:00 to 06:00.
4. Critical part damage and lasting penalties (S-CRIT-PART, S-STATS-PARTS).
5. Late-joining ships, the aggressiveness check (strength retreat), task-force retreat; air: kamikaze, port strikes, the top-speed interception term, plane experience, loss split across wings.
6. Nine unsettled cross-finding disagreements ([findings README](reverse-engineering/findings/README.md)); none changes current results.
7. Calibration setups are built by hand in the game: a generator from simulator scenarios to saves needs the simulator's hull and module IDs mapped to the game's IDs and slot names (milestone 4).

**Next steps (handoff 2026-10-19):**
- Research modifiers, the start of gap 1:
  - `ResearchDto` only carries ship stat modifiers (`statModifiers`, `statAverages`, `statMultipliers`). Add country modifiers such as `naval_retreat_speed` and apply them where the game sums them: escape speed `0x196fe80` reads modifier 0x5b through the member modifier getter `0x1972570`, whose generic gather `0xc38760` includes the country's modifiers, so `RetreatRules.EscapeSpeedPerDay` (S-RETREAT-SPEED). A new DTO field also needs the GUI editor (AGENTS.md).
  - Add TFB's `smoke_generator` (`common/technologies/MTG_naval.txt`) to `data/researches` and to both sides of the CL3 scenario.
  - Check: TFB defines without the override should give about 338 h, BUL 7056, ROM 2801, 0.9 sunk over 200 seeds, and escapes 31 to 35 hours after the retreat decision. The 12 game runs are in `output/ingame/cl3_batch/` (local, not committed): `python tools/ingame/summarize.py output/ingame/cl3_batch`.
- Record more cases with the runner: a mirror match, a capital battle, and a carrier battle with weather on (weather off also removes night). Set each up once in the game and save as text.
- Write the CL3 observations as the first `data/calibration/` case (calibration.md format).
- Open from the logs: armor's `naval_defense_factor` is decoded and now applied but not confirmed (TFB's experience blocks have no defense term); light hits landed on ROM ships at 68 % HP on average in the game against 83 % in the simulator, measured before the escape-speed difference was found, so recheck it after the research modifiers.

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

Recording side done: `tools/ingame/` (2026-10-19). A runner that reads `data/calibration/*.json`, sweeps `BattleRules` variants and defines profiles, and ranks them against the recorded battles ([calibration.md](calibration.md)). Results feed back into the spec as [CAL] tags.

## Ongoing: reverse-engineering

Findings land in `docs/reverse-engineering/findings/` and turn [WIKI]/[ASSUMED] spec items into [RE] ones, in the priority order in [reverse-engineering/README.md](reverse-engineering/README.md).
