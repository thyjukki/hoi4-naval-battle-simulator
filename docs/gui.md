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
- **Side cards**: ships in battle, sunk, escaped and retreating, fleet HP, fleet cost (as on the Scenario page) and IC lost: the cost of the ships sunk, with its share of the fleet cost, and apart the other ships' HP damage valued at their cost (the game repairs it in port), positioning and screening of the shown hour, damage dealt, shots and hits, and **Damage dealt by** weapon (light guns, heavy guns, torpedoes, depth charges, air), ship type (screens, capitals, carriers, submarines, land-based planes) or design: HP damage, share of the side's HP damage, organization damage, and hits/shots with hit rate (air strikes counted apart). Both cards show the same split, up to the shown hour.
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
- Every setting of `battle-scenario.json` is editable: ID, terrain, weather, max hours, iterations, seed (empty for a random one), retreat and re-engage switches, the `rules` block (damage randomness, start hour, air region phase, night, weather, naval dominance: the side with more dominance from its ships at the start regains organization faster, S-DOMINANCE; carrier wings fly naval strikes, A-WINGS, as logged in game), the scenario's define sources, and per side: fleet, country, aggressiveness and repair priority (with the game's names; repair priority is recorded but not used in combat), ship experience level, pride of the fleet, admiral, doctrines, MIOs, researches, spirits, land-based planes (one row per air base: plane, mission (naval strike, which joins the battle, or air superiority, which fights only in the region's air war), planes, mission efficiency (scales the planes offered each air hour and those fighting in the region's air war), disruption factor (multiplies their attack); joining in the listed order; the plane picker groups naval strike planes first), the plane stockpile (planes in stock by type, which refill the land-based wings at each day change; carrier wings get none during a battle; the add picker lists the side's own planes first), carrier stance, carrier night traffic, whether the carrier wings fly a mission over the region (A-REGION: then all of them fight in the region's air war and can be disrupted; in the logged test battles the combat's attacker's did) and the carrier mission efficiency (the share of carrier planes in the region's air war). The research list shows each research's effects, country modifiers included.
- **Country** (S-MIO-COUNTRY): the tag the side plays, countries with their own naval MIOs first; it limits the MIO pickers to the MIOs that country can have, and the admiral picker to its admirals. "(any)" offers every MIO and admiral.
- **Pride of the Fleet** (S-POTF): **None** or one of the side's fleet's designs the game allows (capital ships and carriers); the first ship of that design is the country's pride of the fleet. The hint says what it gives with the side's admiral (attack and defense for the whole side from a Media Personality admiral). Changing the fleet drops a pride the new fleet does not have; a design that is not an eligible ship of the fleet is marked (the scenario check reports it).
- **Admiral**, **Doctrines**, **MIOs**, **Researches** and **Spirits** are foldable sections; folded, each shows a summary (the admiral's name, skill line and trait count; grand doctrines; country, designs with an MIO and traits unlocked; the selected researches or spirits, or their count). They start folded and keep their state while you stay on the page.
- **Admiral** (S-ADMIRAL): **None**, **Custom admiral** (skills 1, no traits) or one of the imported admirals of the side's country (all countries' when the country is empty), listed with "skill · attack/defense/maneuvering/coordination". Picking one copies its name, skills and traits into the scenario; edit them freely. The hint says whether the copy still matches the game's (**Reset to the game's** takes it again). Under each skill is what its level gives (the traits' skill adds included). **Traits** is a filterable checklist grouped as Basic, Assignable (trait tree), Terrain, Personality and Status, each with its effects; keys combat does not read from an admiral are marked "no effect in battle" or "not simulated". Below: what every ship of the fleet gets (container A), the traits' sub-unit modifiers (dimmed when the fleet has no ship of that sub-unit), the leader efficiency when a status trait changes it, traits the game would not allow (⚠; the scenario check warns, it is simulated anyway) and the parts that are not simulated. **Copy from** takes the other side's admiral. Experience and traits gained during the battle are not simulated.
- **Doctrines** (S-DOCTRINE): one block per imported doctrine folder (Naval Doctrine, Air Doctrine). Pick the grand doctrine; its effects are shown under it, and a row per track lets you slot a subdoctrine of that track and set the track's mastery, either as a number or with the rewards picker ("3 / 5 (300)" sets the mastery that unlocks three rewards). Under each slotted subdoctrine its effects and rewards are listed, unlocked ones ticked; a complete track shows **milestone** (hover for the grand doctrine's milestone effects). Changing the grand doctrine keeps the tracks the new one also has. **Copy from** takes the other side's doctrines and spirits. Air doctrines are recorded and checked for spirits, but their effects are not simulated yet.
- **MIOs** (S-MIO): first a row per design of the side's fleet with its MIO for this side: "Designer's" keeps the design's own MIO, **None** removes it, or pick any MIO that covers the design and the side's country can have (grouped by country; a current choice that does not fit is marked). Then one block per MIO the side's designs are assigned to (and any other MIO the side has a setup for), with the designs using it. The trait tree is laid out on the game's grid; click a trait to unlock or lock it. Unlocked traits are ticked and outlined, traits the side could unlock now are dashed, the rest dimmed; a trait unlocked without its parents, or together with a trait it excludes both ways, is outlined red and counted as "not allowed" (the scenario check warns; it is simulated anyway). Traits without a position in the script are listed under the grid. Each cell shows the trait's battle bonus (hit chance factors added, other stats in %); hover for the parents, exclusions, equipment limits, conditions and production bonuses. The initial trait is listed above the tree and always applies. **Policy** offers the policies the MIO can take, with their bonus; **Reset** clears the side's setup of that MIO. **Copy from** takes the other side's MIO assignments and setups. The traits are the side's, as in the game: every design of the MIO gets them.
- **Spirits** (S-SPIRIT) are grouped by slot (Spirit of the Navy, Spirit of Naval Command, ...); each shows its name, effects and, when the side's doctrines do not meet its `has_doctrine` conditions, what it needs (⚠). Such a spirit can still be selected; the scenario check warns, as it does for two spirits in one slot.
- **Doctrine and spirit effects** (under the spirits) lists every active source (grand doctrine, subdoctrines, unlocked rewards, milestones, spirits) with its effects, and the parts combat does not simulate (country modifiers the engine does not read yet, stats such as supply consumption, scripted effects, the air folder).
- The scenario is validated as you type, with the same checks the CLI runs; warnings about ignored content are listed.
- **Swap sides** exchanges attacker and defender (the attacker fires first each hour, S-ORDER). **Mirror attacker → defender** copies the attacker's whole setup to the defender, for mirror matches.
- **Fleet cost** (next to them, and in each side's header) compares what the two fleets cost to build, in IC, with each side's share and the attacker : defender ratio: the sum of the ships' production cost after the side's equipment bonuses (researches, doctrines, MIO traits; S-STATS). Carrier air wings, land-based planes and MIO production bonuses are not counted. It follows the last valid build of the scenario. The Battle and Batch pages show the IC lost.

### Fleets

Force compositions in `data/force-compositions/`: ID, file, ship designs with counts and per-design experience level, and carrier air wings per carrier: each carrier of a carrier design (#1, #2, ...) has its own wings (plane from the carrier-capable planes, its naval role (strike, fighter by its sub-unit, or both) shown, wings, planes per wing) and shows its wing and plane count against its deck capacity (carrier size × `CARRIER_SIZE_STAT_INCREMENT`, red when over). **Use for every carrier** copies one carrier's wings to every carrier of the fleet. Each plane sub-unit on a carrier is its own air object in battle (A-WINGS). The table shows each design's headline stats and fleet totals. **Use in the scenario as attacker/defender** puts the fleet into the edited scenario. Renaming a fleet also renames it in the edited scenario.

### Designs

Ship designs in `data/ship-designs/`: a sortable, filterable table of every design (hull and ship type with the game's names, role, headline stats, the number of fleets using it; ⚠ marks a design that breaks a designer rule) and an editor laid out like the game's designer (S-DESIGN):
- **Hull**: the game's hulls by archetype (Cruiser, Heavy Ship, ...), with their year. Switching hulls keeps the modules in the slots the new hull also has and that still take them, and says which were removed.
- **Ship type**: picked from the hull and modules as the game does, e.g. the armor makes a TFB cruiser a light or heavy cruiser (S-DESIGN-TYPE); it gives the role and organization. "fallback" means no ship type accepts the design (the game would not build it).
- **Modules**: one row per slot, fixed slots first, then custom slots; `*` marks a required slot. Each slot offers only the modules its categories allow, grouped by category, with the game's short and long names, and shows the module's effect and the categories it forbids. Broken rules are shown at the slot: a category the slot does not take, a module another module forbids, an empty required slot, and above the slots, a module count limit or a missing ship type. **Hull defaults** fits the game's default variant of the hull; **New design** starts from it.
- **MIO**: the MIOs that cover the hull (S-MIO-MATCH), grouped by country, with archetypes and debug-mode MIOs last; the hint shows the initial trait. A scenario side can assign another MIO to the design, and sets the unlocked traits and policy, in the scenario editor; the design's stats here have the initial trait only (the side preview has the side's MIO and traits).
- **Allow any module** overrides the restrictions for the design: every slot offers every module, and broken rules are only noted ("Overridden"). It is saved with the design (`allowAnyModule`), and the loader then does not warn about it.

The stats panel shows the design's stats (S-STATS-DESIGN), optionally with the attacker's or defender's researches applied (S-STATS-SHIP), compared with the saved version or any other design. Saving writes the modules in the hull's slot order. Renaming a design renames it in every fleet that uses it. A design used by a fleet cannot be deleted.

### Batch

Runs the edited scenario many times on all cores; run *i* uses seed base + *i*, as in the CLI. It shows the outcome shares, distributions (mean, median, min, 10th and 90th percentile, max) of hours, ships sunk and escaped, HP lost and IC lost (production cost of the ships sunk; the header shows each fleet's cost), mean fleet HP over time with a 10–90 % band, per-design averages (sunk rate, damage per ship, hit rate) and every run. **Inspect** replays a run's seed on the Battle page with the batch's scenario, with all shots and ship logs. `runs.csv` downloads the runs (with each side's IC lost); **Save summary** writes the CLI's aggregate `summary.txt` and `runs.csv` to `output/`.

### Library

Read-only tables of hulls (name, archetype, year, fixed + custom slots with their categories on hover, manpower, naval dominance factor, base stats with range), modules (category, names, effect, the categories they forbid and equipment types they add, how many designs use them), ship types (role, hulls, the modules one of which they need, organization, priority), MIOs (name, countries, the equipment they cover, initial trait, trait count with the traits and their bonuses on hover, file) and MIO policies, researches, doctrines (grand doctrines with their milestones, subdoctrines with their rewards and the mastery each needs), spirits (slot, name, the doctrines they need, effects) and planes (ID, name, naval role, carrier capable, year, stats), as written in the JSON. A research's effect lists its stat blocks and then its country modifiers (S-STATS-COUNTRY) with the game's names, e.g. "Fleet speed while retreating +20%"; country modifiers the engine does not read yet are marked "not simulated" (they also appear in the content warnings).

### Settings

- **Simulate as**: pick whose defines and static modifiers every battle uses: **Vanilla HOI4** (the built-in 1.19.3 values) or one of the folders found on this computer: the HOI4 install, the mods the launcher knows (the `*.mod` files and folders in `Documents/Paradox Interactive/Hearts of Iron IV/mod`, so local checkouts are included) and Steam workshop mods. Only folders with `common/defines` or `common/modifiers` are listed. The choice is saved in the GUI settings file and shown in the top bar (**Simulating …**); **Rescan** searches again. A mod is applied over the built-in vanilla values, as the game loads it; ship, module and research data still come from `data/`.
- **Define sources** (advanced): the same setting as a list, for layering several sources or adding a `.lua` file or any folder by path. Choosing under Simulate as replaces the list.
- **Effective defines**: the source chain for the edited scenario, how many values were applied, and every applied value.
- **Import hulls, modules and ship types** reads them from the HOI4 install and the mods chosen under Simulate as (as `NavySimulator import-ship-data` does) and replaces the files in `hulls/`, `modules/` and `ship-types/`; designs are kept and the Designs page shows the rules they now break.
- **Import MIOs** reads the naval MIOs with their trait trees, the MIO policies and the equipment groups from the same sources (as `NavySimulator import-mios` does) and replaces the files in `mios/`, `mio-policies/` and `equipment-groups/`; designs keep their MIO and scenarios their traits.
- **Import admirals** reads every character with a navy leader role, the admiral traits and the navy skill levels from the same sources (as `NavySimulator import-leaders` does) and replaces the files in `admirals/`, `leader-traits/` and `leader-skills/`; scenarios keep their admirals (each side holds a copy).
- **Import doctrines and spirits** reads the naval and air doctrines and the navy and air spirits from the same sources (as `NavySimulator import-doctrines` does) and replaces the files in `doctrines/` and `spirits/`; scenarios keep their selections and the scenario check reports IDs that no longer exist.
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
| `ShipDataImporter`, `ShipDataWriter`, `GameFileSystem`, `LocalisationTable` | `GameData/Import/` | Read hulls, modules, ship types and their names from the game with mods layered over it, and write them to the data directory. |
| `DoctrineImporter`, `DoctrineWriter` | `GameData/Import/` | The same for doctrine folders and navy/air spirits. |
| `MioImporter`, `MioWriter` | `GameData/Import/` | The same for naval MIOs (include resolved), MIO policies and equipment groups. |
| `LeaderImporter`, `LeaderWriter` | `GameData/Import/` | The same for admirals (with their recruiting country), admiral traits and navy skill levels. |
| `DoctrineRules` | `Engine/Doctrines/` | Which rewards a track's mastery unlocks, the active effects of a setup, `has_doctrine` (S-DOCTRINE). |
| `MioRules` | `Engine/Mios/` | An MIO's bonus for a design from a side's setup, the tree's rules, which designs and policies an MIO takes (S-MIO). |
| `Admiral`, `AdmiralRules` | `Engine/Leaders/` | An admiral's skill levels and modifier containers, and the traits the game would not allow (S-ADMIRAL). |
| `AdmiralInfo` | `Web/Services/` | Admiral labels, trait groups and summaries (what combat reads from an admiral). |
| `MioInfo` | `Web/Services/` | Summaries of MIO traits and policies, the trait states of a side's tree, editing a side's MIO setup. |
| `DoctrineInfo` | `Web/Services/` | Summaries of doctrine and spirit effects, reward thresholds, the doctrines a spirit still needs. |
| `DesignRules` | `Engine/Ships/` | The designer rules: slots, forbidden categories, count limits and the ship type (S-DESIGN). |
| `DesignSlots` | `Web/Services/` | The Designs page's slot editor: slot rows with their issues, the modules a slot offers, switching hulls, hull defaults, readable names. |
| `ReportWriter` | `GameData/Reports/` | The report files shared by the CLI and the GUI. |
| `Workspace` | `Web/Services/` | Singleton GUI state: catalog, edited scenario, saving, stat previews, settings, the active ruleset (Simulate as). |
| `Hoi4Locator` | `Web/Services/` | Finds the HOI4 install, Steam workshop mods and the launcher's mods (`Documents/Paradox Interactive/Hearts of Iron IV/mod`) for Simulate as. |
| `BattleRunner` | `Web/Services/` | The Battle page's session, stepped on a background thread under a lock; `View(hour)` copies it into an immutable `BattleView` for rendering. |
| `BattleViewBuilder`, `DamageBreakdownBuilder`, `ShipLog`, `ShotFilter` | `Web/Services/` | Turn a session into what the Battle page shows at an hour: ship rows, damage splits, a ship's log, filtered shots. |
| `BatchRunner` | `Web/Services/` | Parallel batch runs, one scenario per worker. |
| Pages and components | `Web/Components/` | Razor components; `app.css` holds the whole style (dark by default, light when the system asks). |

The services are covered by `tests/NavySimulator.Web.Tests` (stepping equals the engine, rewound views, ship logs, filters, damage splits, editing and saving, batch determinism, mod discovery and switching).

## Limits

- Design rules follow the scripts (S-DESIGN), not decoded code: the ship type pick in particular is inferred.
- Hulls, modules, ship types, MIOs, researches, doctrines, spirits, imported admirals and planes are read-only in the GUI. Re-import hulls, modules, ship types, MIOs, doctrines, spirits, admirals and planes from the game (Settings); edit the other JSON and use **Reload data from disk**.
- Reward thresholds in the doctrine editor use the vanilla `NDoctrines.DEFAULT_REWARD_MASTERY`; battles use the scenario's defines.
- Unsaved editor changes live in the server process; they are lost when it stops.
- Rewinding keeps one state per ship per hour in memory, which is why Run to end pauses every 5,000 hours.
