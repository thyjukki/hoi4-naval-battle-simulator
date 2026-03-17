# NavySimulator

NavySimulator currently focuses on **scenario setup**:
- load hulls, modules, MIO bonuses, ship designs, fleets, and battle participants from JSON
- build an in-memory `BattleScenario`
- print setup summary in `Program.cs`

Current simulation scope is intentionally small:
- ships resolve `LightAttack`, `HeavyAttack`, and `TorpedoAttack` each hour with cooldowns
- targeting is deterministic and weapon-specific (light closest line, heavy first two lines, torpedo with screening checks)
- battle lines are built from explicit hull `role` values (`Screen`, `Capital`, `Carrier`, `Submarine`, `Convoy`)
- hit chance uses base chance + weapon/ship hit profiles with a minimum hit floor
- armor/piercing, critical hits, and weather/terrain/commander/doctrine modifiers are not implemented yet

Simulation runs in hourly rounds until one side has no ships left or `MaxHours` is reached.

## Data Files
Setup is split into multiple files under `NavySimulator/Data/`:
- `hulls.json`
- `modules.json`
- `mios.json`
- `ship-designs.json`
- `force-compositions.json` (actual ships + fleet composition)
- `battle-scenario.json`

`force-compositions.json` uses explicit ship instances, then fleets reference those ship IDs.
`hulls.json` now includes `role` (`Screen`, `Capital`, `Carrier`, `Submarine`, `Convoy`) used by battle-line grouping and screening calculations.

## Run
```powershell
dotnet build "NavySimulator.sln"
dotnet run --project "NavySimulator/NavySimulator.csproj"
```

## Key Types
- Loader: `NavySimulator/Setup/Loading/SetupLoader.cs`
- Validation exception: `NavySimulator/Setup/Validation/SetupValidationException.cs`
- Scenario: `NavySimulator/Domain/Battles/BattleScenario.cs`
- Simulator: `NavySimulator/Domain/Battles/BattleSimulator.cs`
- Participants: `NavySimulator/Domain/Battles/BattleParticipant.cs`
- Fleet: `NavySimulator/Domain/Fleets/Fleet.cs`

