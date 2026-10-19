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

`data/hulls/`, `data/modules/` and `data/ship-types/` are read from the game, with mods applied over it as the launcher loads them:

```powershell
dotnet run --project src/NavySimulator.Cli -- import-ship-data --game "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV" --mod "<mod folder>"
```

It replaces those three folders and keeps designs; the loader and the GUI's Designs page report designs that break the game's designer rules (S-DESIGN in [docs/mechanics/surface.md](docs/mechanics/surface.md)). The GUI does the same from Settings → Import, with the mods chosen under Simulate as.

### Testing a scenario in the game

`list-ships` prints a scenario's ships as JSON (design, hull, modules by slot, ship type, HP, organization, manpower, experience, stats); `tools/ingame/make_save.py` uses it to write the scenario into a battle save, and `tools/ingame/run_battles.py` replays that save in the game and logs it ([tools/ingame/README.md](tools/ingame/README.md)):

```powershell
dotnet run --project src/NavySimulator.Cli -- list-ships --scenario jukki_usa_vs_bc_japan.json --defines "<mod folder>"
```

`replay` fights a logged game battle hour by hour with the game's own rolls, each hour from the game's state, and reports every shot and ship value that differs (S-REPLAY):

```powershell
dotnet run --project src/NavySimulator.Cli -- replay --log output/ingame/<save>_<timestamp>/s1 --scenario jukki_usa_vs_bc_japan.json --defines "<mod folder>"
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
| `battle-scenario.json` | The battle: fleets per side, researches, spirits, `defines` sources, `rules` variants |
| `hulls/`, `modules/`, `ship-types/` | Hulls with their module slots, modules, and ship types (naval sub-units), imported from the game with its IDs and names (below) |
| `mios/`, `ship-designs/` | MIOs; designs: a hull and one module per slot (`"modules": { "<slot>": "<module>" }`) |
| `researches/`, `spirits/`, `planes/`, `force-compositions/` | Modifiers, planes, fleets |
| `defines/tfb-legacy.lua` | The previous simulator's TFB-mod values as an optional defines profile |
| `schemas/` | JSON schemas for editor autocompletion |

Scenario additions:
- `"defines": ["defines/tfb-legacy.lua"]` applies define overrides.
- `"rules": { "nightEnabled": false, "weatherEnabled": false, "startHourOfDay": 6 }` sets the remaining options (`weatherEnabled: false` is the game with the console command `disable_weather`) ([docs/mechanics/README.md](docs/mechanics/README.md)).
- Per side, `"aggressiveness": "DoNotEngage" | "EngageAtLowRisk" | "EngageAtMediumRisk" | "EngageAtHighRisk" | "AlwaysEngage"` (default `EngageAtMediumRisk`): the task force's aggressiveness (engagement rule) in the game, which decides retreat thresholds and which lines submarines attack.
- Per side, `"repairPriority": "NeverRepair" | "Low" | "Medium" | "High"` (default `High`): the task force's repair priority, recorded with the setup. Combat does not use it.

## Layout

```
src/NavySimulator.Engine     simulation: defines, ships, battle rules, results (no I/O)
src/NavySimulator.GameData   JSON loading and saving, ship data import from the game, report files, Paradox script parser, defines .lua parser
src/NavySimulator.Cli        command line entrypoint
src/NavySimulator.Web        GUI: local Blazor web app (docs/gui.md)
tests/                       xUnit tests for Engine, GameData and the GUI services
data/                        scenario and content JSON, defines profiles
docs/                        mechanics spec, roadmap, reverse-engineering and calibration guides
tools/                       Python helpers: module import, data splitting, re/ (hoi4.exe static analysis)
```
