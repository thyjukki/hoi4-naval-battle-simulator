# NavySimulator

Simulates Hearts of Iron IV naval battles, aiming to reproduce the game's combat as exactly as possible. The rules are checked against the game's own define files, reverse-engineering of the executable, and recorded in-game battles.

70% vibing 20% headsmashing and 10% actuall coding....

## Status

- **Surface combat**: checked against the HOI4 1.19.3 executable by static analysis. Every rule in [docs/mechanics/surface.md](docs/mechanics/surface.md) is tagged with its source; the evidence is in [docs/reverse-engineering/findings/](docs/reverse-engineering/findings/README.md).
- **Air combat**: decoded from the executable and simulated ([docs/mechanics/air.md](docs/mechanics/air.md)): sortie cadence and efficiency, stances, stacking, land-based planes, interception, AA, hits, damage and crits. Weather, kamikaze and port strikes are not modelled.
- **GUI**: a local web app to edit scenarios, fleets and designs, step battles hour by hour with per-ship combat logs, and run batches ([docs/gui.md](docs/gui.md)).
- Plan: [docs/ROADMAP.md](docs/ROADMAP.md). All docs: [docs/README.md](docs/README.md).

## Build, run, test

```powershell
dotnet build NavySimulator.sln
dotnet run --project src/NavySimulator.Cli -- --iterations 20 --seed 1
dotnet test NavySimulator.sln -p:UseAppHost=false
```

## GUI

```powershell
dotnet run --project src/NavySimulator.Web
```

Opens `http://localhost:5180`: set up the scenario (sides, fleets, researches, rules, defines), edit fleets and ship designs with live stats, step a battle +1 h / +10 h / +N h or run it to the end, rewind to any hour, expand ship types into ships and read each ship's combat log, and run batches with outcome statistics. It edits the JSON in `data/`. Details: [docs/gui.md](docs/gui.md).

## CLI

CLI options (`--help`):

| Option | Meaning |
|---|---|
| `--data <dir>` | Data directory (default: the copy next to the executable, else `./data`) |
| `--scenario <file>` | Scenario file in the data directory (default `battle-scenario.json`) |
| `--defines <path>` | Extra defines: a `.lua` file, or a HOI4 / mod root folder (uses its `common/defines/*.lua`). Relative paths are tried against the working directory, then the data directory. Repeatable. |
| `--iterations <n>`, `--seed <n>` | Number of runs; run *i* uses seed + *i* − 1 |
| `--out <dir>` | Output root (default `output`) |

To match your game exactly, load your install's defines:

```powershell
dotnet run --project src/NavySimulator.Cli -- --defines "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV"
```

### Importing hulls, modules and ship types

`data/hulls/`, `data/modules/`, `data/ship-types/` and `data/critical-parts/` are read from the game, with mods applied over it as the launcher loads them:

```powershell
dotnet run --project src/NavySimulator.Cli -- import-ship-data --game "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV" --mod "<mod folder>"
```

It replaces those three folders and keeps designs; the loader and the GUI's Designs page report designs that break the game's designer rules (S-DESIGN in [docs/mechanics/surface.md](docs/mechanics/surface.md)). The GUI does the same from Settings → Import, with the mods chosen under Simulate as.

### Importing doctrines and spirits

`data/doctrines/` (the naval and air doctrine folders: grand doctrines, tracks, subdoctrines with their rewards) and `data/spirits/` (navy and air spirits with the doctrines they need) are read the same way:

```powershell
dotnet run --project src/NavySimulator.Cli -- import-doctrines --game "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV" --mod "<mod folder>"
```

A scenario side then picks its doctrines per folder, with each track's mastery (S-DOCTRINE in [docs/mechanics/surface.md](docs/mechanics/surface.md)), and its spirits by ID.

### Importing MIOs

`data/mios/` (the MIOs that can be assigned to ships, with their trait trees, one file per game file), `data/mio-policies/` and `data/equipment-groups/` are read the same way:

```powershell
dotnet run --project src/NavySimulator.Cli -- import-mios --game "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV" --mod "<mod folder>"
```

A design names its MIO (`mioID`); a scenario side can name its country (`country`, which limits the MIOs it can have), assign another MIO or none to any of its designs (`designMios`), and unlocks each MIO's traits and attaches a policy (`mios`, by MIO ID), as the country does in the game (S-MIO in [docs/mechanics/surface.md](docs/mechanics/surface.md)). The import also writes `data/countries/` (the game's country tags with their names).

### Importing admirals

`data/admirals/` (every character with a navy leader role, with the country that recruits it), `data/leader-traits/` (the traits an admiral can have) and `data/leader-skills/` (what each level of the navy skills gives) are read the same way:

```powershell
dotnet run --project src/NavySimulator.Cli -- import-leaders --game "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV" --mod "<mod folder>"
```

A scenario side's `admiral` holds the admiral's name, skill levels (`skill`, `attackSkill`, `defenseSkill`, `maneuveringSkill`, `coordinationSkill`) and traits: picked from `data/admirals/` and edited, or made up (S-ADMIRAL in [docs/mechanics/surface.md](docs/mechanics/surface.md)). Its `prideOfTheFleet` names a capital-ship or carrier design of its fleet; the first ship of that design is the country's pride of the fleet (S-POTF).

### Importing planes

`data/planes/` holds the game's fixed plane equipment (no plane designer: TFB's carrier fighters, carrier naval bombers, naval bombers, CAS, ...), with stats, equipment types, allowed missions and whether a plane is carrier capable:

```powershell
dotnet run --project src/NavySimulator.Cli -- import-planes --game "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV" --mod "<mod folder>"
```

A fleet's `carrierAirwings` gives each carrier its wings (per design, one loadout per ship); a scenario side's `landBasedAirwings` lists its land-based planes (one entry per air base, with a mission efficiency) and `planeStockpile` the planes it has in stock (A-WINGS, A-EXTERNAL, A-REINFORCE in [docs/mechanics/air.md](docs/mechanics/air.md)).

### Testing a scenario in the game

`list-ships` prints a scenario's ships as JSON (design, hull, modules by slot, ship type, HP, organization, manpower, experience, stats); `tools/ingame/make_save.py` uses it to write the scenario into a battle save, and `tools/ingame/run_battles.py` replays that save in the game and logs it ([tools/ingame/README.md](tools/ingame/README.md)):

```powershell
dotnet run --project src/NavySimulator.Cli -- list-ships --scenario jukki_usa_vs_bc_japan.json --defines "<mod folder>"
```

`replay` fights a logged game battle hour by hour with the game's own rolls, each hour from the game's state, and reports every shot and ship value that differs (S-REPLAY); `--shots <file>` also writes every shot, game and engine side by side, and `--states <file>` every end-of-hour ship value that differs, as CSV:

```powershell
dotnet run --project src/NavySimulator.Cli -- replay --log output/ingame/<save>_<timestamp>/s1 --scenario jukki_usa_vs_bc_japan_no_doctrines.json --defines "<mod folder>"
```

## Output

`output/<scenario>_<timestamp>/`:
- `summary.txt`: outcome shares and averages over all runs.
- `run-NNN/summary.txt`: per-run result with per-weapon hit rates and per-design losses.
- `run-NNN/hourly.log`: per-hour lines, positioning and screening, damage, retreats.
- `run-NNN/shots.csv`: every shot with its hit chance, profile factor, modifiers, damage, crit and piercing tier.
- `run-NNN/ships.csv`: final state of every ship.

## Data

Everything lives in `data/`:

| Folder / file | Content |
|---|---|
| `battle-scenario.json` | The battle: fleets per side, researches, doctrines, spirits, `defines` sources, `rules` variants |
| `hulls/`, `modules/`, `ship-types/`, `critical-parts/` | Hulls with their module slots, modules, ship types (naval sub-units) and the critical parts crits can damage, imported from the game with its IDs and names (below) |
| `mios/`, `ship-designs/` | MIOs; designs: a hull and one module per slot (`"modules": { "<slot>": "<module>" }`) |
| `doctrines/`, `spirits/` | Doctrine folders and navy/air spirits, imported from the game (above) |
| `researches/`, `planes/`, `force-compositions/` | Modifiers, planes, fleets |
| `defines/tfb-legacy.lua` | The previous simulator's TFB-mod values as an optional defines profile |
| `calibration/` | Baselines: game observations of a setup (per seed: length, outcome, losses and HP left per design, HP curves) and the simulator's numbers they were last compared with ([docs/calibration.md](docs/calibration.md)) |
| `schemas/` | JSON schemas for editor autocompletion |

Scenario additions:
- `"defines": ["defines/tfb-legacy.lua"]` applies define overrides.
- `"rules": { "nightEnabled": false, "weatherEnabled": false, "startHourOfDay": 6 }` sets the remaining options (`weatherEnabled: false` is the game with the console command `disable_weather`) ([docs/mechanics/README.md](docs/mechanics/README.md)).
- Per side, `"aggressiveness": "DoNotEngage" | "EngageAtLowRisk" | "EngageAtMediumRisk" | "EngageAtHighRisk" | "AlwaysEngage"` (default `EngageAtMediumRisk`): the task force's aggressiveness (engagement rule) in the game, which decides retreat thresholds and which lines submarines attack.
- Per side, `"repairPriority": "NeverRepair" | "Low" | "Medium" | "High"` (default `High`): the task force's repair priority, recorded with the setup. Combat does not use it.
- Per side, `"doctrines": { "naval": { "grandDoctrine": "new_fleet_in_being", "tracks": { "capital_ships": { "subdoctrine": "line_of_battle", "mastery": 300 } } } }` and `"spiritIDs": ["decisive_battle_spirit"]`: the doctrines and spirits, with each track's mastery (rewards unlock at 100, 200, … by default). Air doctrines (`"air"`) are recorded but not simulated yet.

## Layout

```
src/NavySimulator.Engine     simulation: defines, ships, battle rules, results (no I/O)
src/NavySimulator.GameData   JSON loading and saving, ship data and doctrine import from the game, report files, Paradox script parser, defines .lua parser
src/NavySimulator.Cli        command line entrypoint
src/NavySimulator.Web        GUI: local Blazor web app (docs/gui.md)
tests/                       xUnit tests for Engine, GameData and the GUI services
data/                        scenario and content JSON, defines profiles
docs/                        mechanics spec, roadmap, reverse-engineering and calibration guides
tools/                       Python helpers: module import, data splitting, re/ (hoi4.exe static analysis)
```
