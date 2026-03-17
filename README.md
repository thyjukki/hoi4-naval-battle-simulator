# NavySimulator

NavySimulator currently focuses on **scenario setup**:
- load hulls, modules, MIO bonuses, ship designs, fleets, and battle participants from JSON
- build an in-memory `BattleScenario`
- print setup summary in `Program.cs`

No battle simulation loop is executed yet.

## Data Files
Setup is split into multiple files under `NavySimulator/Data/`:
- `hulls.json`
- `modules.json`
- `mios.json`
- `ship-designs.json`
- `force-compositions.json` (actual ships + fleet composition)
- `battle-scenario.json`

`force-compositions.json` uses explicit ship instances, then fleets reference those ship IDs.

## Run
```powershell
dotnet build "NavySimulator.sln"
dotnet run --project "NavySimulator/NavySimulator.csproj"
```

## Key Types
- Loader: `NavySimulator/Setup/Loading/SetupLoader.cs`
- Validation exception: `NavySimulator/Setup/Validation/SetupValidationException.cs`
- Scenario: `NavySimulator/Domain/Battles/BattleScenario.cs`
- Participants: `NavySimulator/Domain/Battles/BattleParticipant.cs`
- Fleet: `NavySimulator/Domain/Fleets/Fleet.cs`

