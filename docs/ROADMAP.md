# Roadmap

The aim is a simulator that reproduces HOI4 naval combat as closely as possible, verified against the game's own files, its executable, and recorded battles ([mechanics/README.md](mechanics/README.md)).

## Status (2026-10-19)

**Matches hoi4.exe 1.19.3** (decoded, engine rewritten to it): ship stats (S-STATS), hour loop and firing order, activation and cooldowns, targeting and screening, hit chance, damage, crits, retreat, submarines, positioning, experience and manpower (including the daily refill in combat), and air combat. Built-in defines and static modifiers are vanilla 1.19.3; a mod folder (`--defines`) overrides them.

**GUI** (2026-10-18): a local web app (`src/NavySimulator.Web`, [gui.md](gui.md)) edits scenarios, fleets and designs, steps battles hour by hour with rewind and per-ship combat logs, and runs batches. The engine's hour loop is now `BattleSession`, steppable, with a per-hour timeline and structured events.

**Ship designer** (2026-10-19): hulls, modules and ship types are imported from the game and mods (`NavySimulator import-ship-data`, or Settings → Import in the GUI) with the game's IDs and names; `data/` holds TFB's (dev checkout over vanilla 1.19.3). Designs fit one module per hull slot, and the designer rules are checked: slot categories, required slots, forbidden categories, count limits, and the ship type from hull and modules (S-DESIGN), which now gives the role and organization. A design can override the restrictions (`allowAnyModule`). The migrated designs keep their stats exactly and three of the four scenarios their results for every seed. `torpedo_cruiser_torpattack` had light cruiser armor, which TFB's torpedo cruiser hull does not take; it now has Heavy Cruiser Armour IV and stays a heavy cruiser, so `battle-scenario-bk` changed (20 seeds: 531 h instead of 497, 9.0 defender ships sunk instead of 9.6).

**Calibration** (2026-10-19): [`tools/ingame/`](../tools/ingame/README.md) replays a saved battle in the game for any number of dice seeds, unattended (about a minute each), and logs every combat hour and every shot from inside the game ([LOG finding](reverse-engineering/findings/LOG-ingame-logging.md)). First case, the TFB CL3 battle (`data/battle-scenario-la-vs-torp-cl3.json`, save `light_attack_vs_torp_cruiser_battle_start.hoi4`), 12 game seeds: 336 h, BUL 7075 HP left (sd 161), ROM 2802 (sd 72), 1.2 ROM ships sunk. The shot logs found two engine errors, now fixed: night was applied with weather off, and `naval_damage_factor` counted once instead of twice. The last difference was TFB's `smoke_generator` technology (`naval_retreat_speed = 0.2`), a country modifier; researches now carry country modifiers (S-STATS-COUNTRY), and with TFB's defines 200 simulator seeds give 338 h, 7056, 2801 and 0.9 sunk, with escapes taking as long as in the game for every decision hour. Second case, a mixed fleet (`data/jukki_usa_vs_bc_japan.json`: BB, SHBB and CL against BC, SHBB and DD), generated into a save by `tools/ingame/make_save.py`, one game seed: start values and every attack and piercing value matched, but capitals hit 1.13–1.7 times as often in the game. TFB's `screening_bonus` adds heavy and light gun hit chance factors (1 and 0.3 × screening) that the engine ignored; now every key of the screening bonuses applies (`MemberModifierRules`) and all simulated hit chances are within 1 % of the logged ones. With the scenario's experience (20) and weather off, 12 game seeds against 200 simulator seeds: 188.6 h against 189.9, USA (BUL) 25.2 ships sunk and 474 HP left against 25.5 and 474, losses per design within 0.3 ships; Japan kept 8421 HP against 8129: the USA side dealt 8 % less, traced to dice luck in the game runs (USA hits 5.5 per run below their logged odds; with that luck the simulator predicts the game within 1 standard error). Targeting weights, positioning (TFB's 60-ship fleet-size rule), the damage multiplier, organization damage and retreat decisions were all confirmed shot by shot.

**Gaps, by impact:**
1. Combat-time modifier layer: country modifiers other than the retreat speeds and hit chance terms (researches carry `countryModifiers` and the engine reads `naval_retreat_speed`, `naval_retreat_speed_after_initial_combat`, `sub_retreat_speed`, `convoy_retreat_speed`, `naval_hit_chance` and the three `naval_*_hit_chance_factor`s; other keys get a loader warning), doctrines, admiral skills/traits, spirits, `naval_*_factor` attack/armor/speed (formulas decoded in S-STATS-COMBAT). Of the attack, armor and speed terms only the experience ones are modelled. `data/spirits` is ignored with loader warnings until then.
2. Weather (hit chance, carrier traffic, naval strike targeting). `rules.weatherEnabled = false` matches the game with `disable_weather`, including no night (S-WEATHER, S-NIGHT).
3. Darkness per sea region; with weather on it is [ASSUMED] full dark 20:00 to 06:00.
4. Critical part damage and lasting penalties (S-CRIT-PART, S-STATS-PARTS).
5. Late-joining ships, the aggressiveness check (strength retreat), task-force retreat; air: kamikaze, port strikes, the top-speed interception term, plane experience, loss split across wings.
6. Nine unsettled cross-finding disagreements ([findings README](reverse-engineering/findings/README.md)); none changes current results.
7. Calibration setups: `tools/ingame/make_save.py` writes a scenario into a template save (2026-10-19), but research IDs are mapped to game technologies by hand (`GAME_TECHS`) until technologies are imported, and spirits, doctrines, commanders and MIOs are not written.
8. The ship type pick (S-DESIGN-TYPE) is inferred from the scripts, not decoded; hull stats are assumed to inherit from the archetype only (S-DESIGN-HULL).

**Next steps (handoff 2026-10-19):**
- Research modifiers (done 2026-10-19): `ResearchDto.countryModifiers` by game key, summed per side (`BattleParticipant.CountryModifiers`) and read by `RetreatRules.EscapeSpeedPerDay` like the game's member modifier getter; `smoke_generator` is in `data/researches/misc.json` and on both CL3 sides. The 12 game runs are in `output/ingame/cl3_batch/` (local, not committed): `python tools/ingame/summarize.py output/ingame/cl3_batch`.
- Continue the modifier layer: the next country keys the combat rules read through the same getters (`naval_hit_chance` and the per-weapon hit chance factors are read since 2026-10-19 through `MemberModifierRules`; next `naval_damage_factor`/`naval_defense_factor` from doctrines and spirits, `naval_speed_factor`), then doctrines and spirits as sources of `countryModifiers` (replacing the [LEGACY] spirit stat blocks).
- Record more cases with the runner: a mirror match and a carrier battle with weather on (weather off also removes night). `make_save.py` builds them from scenarios.
- Write the CL3 observations as the first `data/calibration/` case (calibration.md format).
- Open from the logs: armor's `naval_defense_factor` is decoded and now applied but not confirmed (TFB's experience blocks have no defense term); the light-hit HP difference noted before the escape speed was fixed is gone: with `smoke_generator`, light hits land on ROM ships at 58.9 % HP on average (median 55.6 %) over 200 simulator seeds, against 58.9 % (median 55.5 %) in the 12 game runs.

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

Possible next steps: edit researches, A/B batches of two scenarios, a calibration view that overlays a recorded battle (milestone 5).

## Milestone 3: surface completeness

- Submarines: hide/reveal, escape (done), air sub detection (S-SUB).
- Critical part damage with lasting effects (S-CRIT-PART); needs module categories on designs.
- Weather, sea region and night as data-driven modifiers; admiral skills and traits; doctrines and spirits as battle modifiers (`data/spirits` uses keys such as positioning, hitChance and retreatDecisionChance that the stat model cannot express; the loader warns about them).
- Late-joining ships, task-force-level retreat.

## Milestone 4: import everything from game files

Replace the hand-written JSON with data parsed from the game and mods using `PdxParser`. Done (2026-10-19): hulls and modules (`common/units/equipment/`), ship types (`common/units/`) and their localised names, by `NavySimulator import-ship-data` (`GameData/Import/`); ship designs reference the game's IDs and slots. Left: technologies, MIOs, doctrines, experience levels, critical parts.

## Milestone 5: calibration harness

Recording side done: `tools/ingame/` (2026-10-19). A runner that reads `data/calibration/*.json`, sweeps `BattleRules` variants and defines profiles, and ranks them against the recorded battles ([calibration.md](calibration.md)). Results feed back into the spec as [CAL] tags.

## Ongoing: reverse-engineering

Findings land in `docs/reverse-engineering/findings/` and turn [WIKI]/[ASSUMED] spec items into [RE] ones, in the priority order in [reverse-engineering/README.md](reverse-engineering/README.md).
