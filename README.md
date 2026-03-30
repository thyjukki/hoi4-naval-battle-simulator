# NavySimulator

NavySimulator simulates HOI4 naval battles from JSON-defined content (hulls, modules, mios, designs, fleets, researches, spirits, and planes).

70% 20% headsmashing and 10% actuall coding....

## Current Scope
- Setup is loaded into an in-memory `BattleScenario` by `SetupLoader`.
- Battles run in hourly ticks with separate air and surface phases (`BattleSimulator`, `NavalAirCombatSimulator`, `NavalSurfaceCombatSimulator`).
- Air combat supports carrier/external bomber sortie timing, target selection, ship AA preemptive kills, fleet AA damage reduction, and carrier wing plane-loss tracking. Still not working...

## Scenario setup
- Scenario is defined in `NavySimulator/Data/battle-scenario.json` with references to other data files.


## Output
- Full hourly logs and summaries are written under:
  - `output/<SCENARIO_ID>_<yyyyMMdd_HHmmss>/`
- For multi-iteration scenarios, run-specific files use `-RUN<n>` suffix and an aggregate `summary-averages.txt` is generated.
- Ship reports are split by side:
  - `output/.../attacker/`
  - `output/.../defender/`

## Data Layout
Data is loaded from `NavySimulator/Data/` folders (with legacy single-file fallback still supported by `SetupLoader`):
- `hulls/*.json`
- `modules/*.json`
- `mios/*.json`
- `ship-designs/*.json`
- `researches/*.json`
- `spirits/*.json`
- `planes/*.json`
- `force-compositions/*.json`
- `battle-scenario.json`

There is also schema files for each data type under `NavySimulator/Data/Schemas/'. If using a JSON editor with schema validation, you can point to these for autocompletion and error checking.

## Build, Run, Test
```powershell
dotnet build "NavySimulator.sln"
dotnet run --project "NavySimulator/NavySimulator.csproj"
dotnet test "NavySimulator.sln" -p:UseAppHost=false
```

## TODO
- Clean up project structure
- Add command line args for scenario selection, iteration count, and toggles
- Fix air combat logic
- Critical hits
- Experience loss and gain
- Weather effects
