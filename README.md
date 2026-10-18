# NavySimulator

Simulates Hearts of Iron IV naval battles, aiming to reproduce the game's combat as exactly as possible. The rules are checked against the game's own define files, reverse-engineering of the executable, and recorded in-game battles.

70% vibing 20% headsmashing and 10% actuall coding....

## Status

- **Surface combat**: rewritten in milestone 1. Every rule is in [docs/mechanics/surface.md](docs/mechanics/surface.md) with its source, and uncertain mechanics can be switched between candidate behaviors.
- **Air combat**: not simulated yet. The design for milestone 2 is in [docs/mechanics/air.md](docs/mechanics/air.md). Carrier wings and land-based planes in a scenario are ignored, and the run prints a note saying so.
- Plan: [docs/ROADMAP.md](docs/ROADMAP.md). All docs: [docs/README.md](docs/README.md).

## Build, run, test

```powershell
dotnet build NavySimulator.sln
dotnet run --project src/NavySimulator.Cli -- --iterations 20 --seed 1
dotnet test NavySimulator.sln -p:UseAppHost=false
```

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
| `hulls/`, `modules/`, `mios/`, `ship-designs/` | Ship definitions |
| `researches/`, `spirits/`, `planes/`, `force-compositions/` | Modifiers, planes, fleets |
| `defines/tfb-legacy.lua` | The previous simulator's TFB-mod values as an optional defines profile |
| `schemas/` | JSON schemas for editor autocompletion |

Scenario additions:
- `"defines": ["defines/tfb-legacy.lua"]` applies define overrides.
- `"rules": { "firingOrder": "Simultaneous" }` selects mechanic variants ([docs/mechanics/README.md](docs/mechanics/README.md)).
- Per side, `"retreatPriority": "Low" | "Medium" | "High"` and `"submarineEngagementRisk"`.

## Layout

```
src/NavySimulator.Engine     simulation: defines, ships, battle rules, results (no I/O)
src/NavySimulator.GameData   JSON loading, Paradox script parser, defines .lua parser
src/NavySimulator.Cli        command line entrypoint and report writing
tests/                       xUnit tests for Engine and GameData
data/                        scenario and content JSON, defines profiles
docs/                        mechanics spec, roadmap, reverse-engineering and calibration guides
tools/                       Python helpers (module import from game files, data splitting)
```
