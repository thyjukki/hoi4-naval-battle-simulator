# GUI

`src/NavySimulator.Web` is a local web app (Blazor Server, .NET 10, no NuGet packages) for setting up scenarios, editing fleets and ship designs, and running battles hour by hour or in batches. It edits the same JSON files in `data/` that the CLI reads, and runs the same engine.

## Start

```powershell
dotnet run --project src/NavySimulator.Web
```

It opens `http://localhost:5180` in your browser. It listens on localhost only. Stop it with Ctrl+C.

| Option | Meaning |
|---|---|
| `--data <dir>` | Data directory to edit and simulate. Default: the nearest `data` folder above the working directory or the executable, so running from the repository edits `data/`. |
| `--defines <path>` | Extra define source for this session only (repeatable), like the CLI's `--defines`. To simulate a mod every time, pick it under Settings → Simulate as. |
| `--port <n>` | Port (default 5180). |
| `--no-browser` | Do not open the browser. |
| `--settings <file>` | GUI settings file. Default: `NavySimulator/gui-settings.json` in the local application data folder (`%LOCALAPPDATA%` on Windows). |

The GUI keeps one shared state for every browser tab: reloading the page or switching pages keeps the scenario being edited, the battle and the last batch.

## Pages

### Battle

- **Start battle** builds the scenario being edited (with the seed in the toolbar) and stops before hour 0. **Start and run to end** does both.
- **+1 h**, **+10 h**, **+N h** (any number) and **Run to end** advance the battle. Run to end stops after 5,000 hours without an end and says so; press it again to continue. **Stop** interrupts a running simulation.
- The clock shows the hours fought, the next hour of day and the outcome once the battle is over.
- **Viewing** (slider, ‹ ›, or a click on a chart or an hour row) rewinds the whole page to the end of an earlier hour: ship states, side totals and every log only show what had happened by then. **Back to live** returns to the latest hour. Stepping always returns to live.
- **Side cards**: ships in battle, sunk, escaped and retreating, fleet HP, positioning and screening of the shown hour, damage dealt, shots and hits, and **Damage dealt by** weapon (light guns, heavy guns, torpedoes, depth charges, air), ship type (screens, capitals, carriers, submarines, land-based planes) or design: HP damage, share of the side's HP damage, organization damage, and hits/shots with hit rate (air strikes counted apart). Both cards show the same split, up to the shown hour.
- **Charts**: fleet HP and organization (% of start), ships in battle and sunk, HP damage per hour, positioning and screening. Night hours are shaded.
- **Fleet trees**: each side's ships grouped by design (ship type), with HP and organization bars, shots/hits, damage dealt and taken and kills. Click a type to list its ships; click a ship to open its panel.
- **Ship panel**: HP, organization, experience, totals, the ship's stats after researches, an HP/organization chart and the ship's own combat log: shots it fired and took (hit chance, hit or miss, crit, piercing tier, damage, HP and organization damage, killing blow), air strikes, and its activation, retreat, escape or sinking. Filter it to fired, received or events. Click another ship's name to jump to it.
- **Combat log** tabs, all up to the shown hour:
  - **Hours**: one row per hour, newest first: lines per side, positioning, screening, shots and hits, HP damage each way, ships in battle, events.
  - **Events**: activations, retreat decisions, escapes, disengagements, sinkings (with the killer) and re-engagements, filterable by kind and ship.
  - **Shots**: every shot with hit chance, profile factor and modifier, result, damage, piercing tier, HP and organization damage. Filter by hours, side, weapon, ship name, hits, crits or killing blows; the bar sums the filtered shots.
  - **Air**: air strikes (planes flying, intercepted, shot down by AA, hits, AA reduction, damage).
  - **Raw log**: the CLI's `hourly.log` lines.
  - **Notes**: inputs the engine ignores, and skipped weapon actions.
- **Downloads**: `shots.csv`, `ships.csv` and `hourly.log` of the battle so far. **Save report** writes the CLI's report folder (`summary.txt`, `hourly.log`, `shots.csv`, `ships.csv`, `air.csv`) to `output/`.
- A banner says when the scenario or content changed after the battle started; **Restart with the changes** starts again with the same seed.

### Scenario

- Open any scenario file in the data directory root (files with a `battleScenario` block), start a **New** one, **Save**, **Save as** (file name in the data directory root) or **Revert**. A dot in the top bar marks unsaved changes. Opening (or reverting) a scenario first reloads the content files (designs, fleets and the rest) if any changed on disk since the GUI read them, so a scenario written outside the GUI finds the designs and fleets written with it.
- Every setting of `battle-scenario.json` is editable: ID, terrain, weather, max hours, iterations, seed (empty for a random one), retreat and re-engage switches, the `rules` block (damage randomness, start hour, air region phase, night, weather), the scenario's define sources, and per side: fleet, aggressiveness and repair priority (with the game's names; repair priority is recorded but not used in combat), ship experience level, commander, doctrine, researches, spirits, land-based naval strike planes (count, plane, mission efficiency), carrier stance and carrier night traffic.
- The scenario is validated as you type, with the same checks the CLI runs; warnings about ignored content are listed.
- **Swap sides** exchanges attacker and defender (the attacker fires first each hour, S-ORDER). **Mirror attacker → defender** copies the attacker's whole setup to the defender, for mirror matches.

### Fleets

Force compositions in `data/force-compositions/`: ID, file, ship designs with counts and per-design experience level, and carrier air wings (plane, fighter or bomber, wings, planes per wing). The table shows each design's headline stats and fleet totals. **Use in the scenario as attacker/defender** puts the fleet into the edited scenario. Renaming a fleet also renames it in the edited scenario.

### Designs

Ship designs in `data/ship-designs/`: a sortable, filterable table of every design with headline stats and the number of fleets using it, and an editor for ID, file, hull, MIO and modules (add, remove, duplicate, change; each module shows its effect). The stats panel shows the design's stats (S-STATS-DESIGN), optionally with the attacker's or defender's researches applied (S-STATS-SHIP), compared with the saved version or any other design. Renaming a design renames it in every fleet that uses it. A design used by a fleet cannot be deleted. Module slots are not checked against the hull.

### Batch

Runs the edited scenario many times on all cores; run *i* uses seed base + *i*, as in the CLI. It shows the outcome shares, distributions (mean, median, min, 10th and 90th percentile, max) of hours, ships sunk and escaped and HP lost, mean fleet HP over time with a 10–90 % band, per-design averages (sunk rate, damage per ship, hit rate) and every run. **Inspect** replays a run's seed on the Battle page with the batch's scenario, with all shots and ship logs. `runs.csv` downloads the runs; **Save summary** writes the CLI's aggregate `summary.txt` and `runs.csv` to `output/`.

### Library

Read-only tables of hulls, modules (with how many designs use them), MIOs, researches, spirits and planes, as written in the JSON.

### Settings

- **Simulate as**: pick whose defines and static modifiers every battle uses: **Vanilla HOI4** (the built-in 1.19.3 values) or one of the folders found on this computer: the HOI4 install, the mods the launcher knows (the `*.mod` files and folders in `Documents/Paradox Interactive/Hearts of Iron IV/mod`, so local checkouts are included) and Steam workshop mods. Only folders with `common/defines` or `common/modifiers` are listed. The choice is saved in the GUI settings file and shown in the top bar (**Simulating …**); **Rescan** searches again. A mod is applied over the built-in vanilla values, as the game loads it; ship, module and research data still come from `data/`.
- **Define sources** (advanced): the same setting as a list, for layering several sources or adding a `.lua` file or any folder by path. Choosing under Simulate as replaces the list.
- **Effective defines**: the source chain for the edited scenario, how many values were applied, and every applied value.
- **Reload data from disk** after editing JSON outside the GUI (opening a scenario also does it when files changed); content problems (read errors, ignored keys) are listed.

## What the GUI writes

- Designs, fleets and scenarios go to their JSON files in the data directory. Only the edited entry is replaced (or appended); other entries keep their content, the file keeps its line endings, and it is re-indented with two spaces. New files get the matching `$schema`.
- Reports go to `output/` next to the data directory.
- Settings go to the GUI settings file (outside the repository).

## Architecture

| Piece | Where | Role |
|---|---|---|
| `BattleSession` | `Engine/Battles/` | The hour loop, steppable: `StepHour`, `Step(n)`, `RunToEnd`, `BuildResult` at any time. Keeps a per-hour `Timeline`, structured `Events` and, when asked, every ship's state at the end of every hour. `BattleEngine.Simulate` runs a session to its end, so the CLI and the GUI fight the same battle for the same seed. |
| `ContentCatalog`, `ContentLibrary`, `SetupLoader.BuildScenario` | `GameData/Loading/` | The data directory's content as DTOs with their source files; engine objects for previews; scenario building from in-memory edits. |
| `CatalogWriter` | `GameData/Saving/` | Writes designs, fleets and scenarios back to their files. |
| `ReportWriter` | `GameData/Reports/` | The report files shared by the CLI and the GUI. |
| `Workspace` | `Web/Services/` | Singleton GUI state: catalog, edited scenario, saving, stat previews, settings, the active ruleset (Simulate as). |
| `Hoi4Locator` | `Web/Services/` | Finds the HOI4 install, Steam workshop mods and the launcher's mods (`Documents/Paradox Interactive/Hearts of Iron IV/mod`) for Simulate as. |
| `BattleRunner` | `Web/Services/` | The Battle page's session, stepped on a background thread under a lock; `View(hour)` copies it into an immutable `BattleView` for rendering. |
| `BattleViewBuilder`, `DamageBreakdownBuilder`, `ShipLog`, `ShotFilter` | `Web/Services/` | Turn a session into what the Battle page shows at an hour: ship rows, damage splits, a ship's log, filtered shots. |
| `BatchRunner` | `Web/Services/` | Parallel batch runs, one scenario per worker. |
| Pages and components | `Web/Components/` | Razor components; `app.css` holds the whole style (dark by default, light when the system asks). |

The services are covered by `tests/NavySimulator.Web.Tests` (stepping equals the engine, rewound views, ship logs, filters, damage splits, editing and saving, batch determinism, mod discovery and switching).

## Limits

- Module slots, hull module categories and design rules are not checked; any module fits any hull.
- Hulls, modules, MIOs, researches, spirits and planes are read-only in the GUI; edit their JSON and use **Reload data from disk**.
- Unsaved editor changes live in the server process; they are lost when it stops.
- Rewinding keeps one state per ship per hour in memory, which is why Run to end pauses every 5,000 hours.
