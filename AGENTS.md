# AGENTS Guide for NavySimulator

## Project Goal
- Tool to simulate naval battles in Hearts of Iron 4 (HOI4) by modeling ship designs and compositions.
- Fleets consist of ships
- Ships have Ship Design
- Ship Design consist of Hull and Modules and MIO (Military Industry Organization)
- Hull defines the ship's base stats and module slots
- Modules provide stats
- MIO Gives bonuses to stats (percentage increase to stats)
- Stats from hull and modules are combined to determine ship performance in battle
- Ships have Organization and HP, and take damage in battle
- Ships can suffer from critical hits that reduce their stats
- Battles are between 2 fleets
- Battles have a terrain (sea region) that provides bonuses or penalties to certain stats
- Battle participants have a commander that provides bonuses to certain stats
- Battle participants have a doctrine that provides bonuses to certain stats
- Battle participants have a technology level that provides bonuses to certain stats
- Battle participants have a nation modifiers that provides bonuses to certain stats
- Battle has a weather condition that provides bonuses or penalties to certain stats
- The app will load ship fleet compositions, ship designs and modules from JSON files
- The app will simulate battles between fleets and output results to the console.

## Possible stats
- Speed
- Organization
- HP
- Reliability
- Light Attack
- Light Piercing
- Heavy Attack
- Heavy Piercing
- Torpedo Attack
- Depth Charges
- Armor
- Anti-Air
- Surface Visibility
- Sub Visibility
- Suraface Detection
- Sub Detection

## Battle Simulation Scope
- Ships are split in 4 groups
  - Screening group
  - Battle line
  - Carrier group
  - Submarine group
- Battle is simulated in rounds of hours
- Each rounds all ships will fire their guns and torpedoes, and then take damage
- Weapons have cooldown, and carriers planes have a cooldown for flying
- Round is repeated until one fleet is destroyed, or set number of hours have passed
- Screening efficiency is determined by the ratio of screening group to battle line
- Carrier screening efficiency is determined by the ratio of carrier group to battle line

## Battle mechanics
- Battle mechanics are explained in hoi4 wiki https://hoi4.paradoxwikis.com/Naval_battle
- 
## Project Snapshot
- Solution: `NavySimulator.sln` with a single executable project: `NavySimulator/NavySimulator.csproj`.
- Target runtime: `.NET 10` (`<TargetFramework>net10.0</TargetFramework>`).
- Language features enabled: implicit usings and nullable reference types.
- Current app entrypoint is top-level statements in `NavySimulator/Program.cs`.
- `Program.cs` loads scenario data from `NavySimulator/Data/`, runs `BattleSimulator`, and prints hourly logs plus final outcome.

## Architecture (Current State)
- Domain code is organized under `NavySimulator/Domain/` (`Battles`, `Fleets`, `Ships`, `Stats`) in the `NavySimulator.Domain` namespace.
- Setup/loading code is organized under `NavySimulator/Setup/` (`Contracts/Dto`, `Loading`, `Validation`) in the `NavySimulator.Setup` namespace.
- `Ship` (`NavySimulator/Domain/Ships/Ship.cs`) owns a `ShipDesign` instance.
- `ShipDesign` (`NavySimulator/Domain/Ships/ShipDesign.cs`) owns a `Hull`, `List<IModule>`, and optional `MioBonus`.
- `IModule` (`NavySimulator/Domain/Ships/IModule.cs`) defines the module contract via `string ID { get; }` and `ShipStats StatModifiers { get; }`.
- Setup data flow is constructor-injected object graph built by `SetupLoader`: `Hull/StatModule/MioBonus` -> `ShipDesign` -> `Ship` -> `Fleet` -> `BattleParticipant` -> `BattleScenario`.
- `BattleSimulator` (`NavySimulator/Domain/Battles/BattleSimulator.cs`) runs hourly rounds with screening efficiency, deterministic targeting, cooldowns, and hit chance.
- No networking, database, DI container, or plugin loader are present.

## Code Patterns to Follow Here
- Use var for types when it can be used
- Keep domain types in `NavySimulator.Domain`, setup types in `NavySimulator.Setup`, and one top-level type per file.
- Match existing style of simple POCO/domain types with constructor assignment.
- Prefer extending the `IModule` contract with concrete module classes rather than adding ad-hoc dictionaries.
- Keep composition explicit in constructors (as in `Ship` and `ShipDesign`) instead of hidden factories.
- Preserve nullable-aware signatures when adding new fields/properties.

## Build, Run, and Validate
- Build from repo root:
  - `dotnet build NavySimulator.sln`
- Run app:
  - `dotnet run --project NavySimulator/NavySimulator.csproj`
- Verified behavior as of 2026-03-18: app prints scenario setup, then simulation hourly logs, then final battle result.
- There are currently no test projects in the solution; if you add tests, include them in `NavySimulator.sln`.

## Agent Workflow Tips for This Repo
- For setup/data issues, start in `NavySimulator/Setup/Loading/SetupLoader.cs` and matching DTOs in `NavySimulator/Setup/Contracts/Dto/`.
- For combat behavior issues, start in `NavySimulator/Domain/Battles/BattleSimulator.cs` and related domain models.
- When introducing new behavior, demonstrate wiring in `Program.cs` so it is runnable immediately.
- Keep changes small and compile-check with `dotnet build` after structural edits.
- If adding new projects (e.g., tests), update the solution file so Rider/CLI workflows stay aligned.

## Known Integration Boundaries
- External dependencies: none beyond the .NET SDK; no NuGet package references currently.
- Runtime setup depends on JSON files under `NavySimulator/Data/` (`hulls.json`, `modules.json`, `mios.json`, `ship-designs.json`, `force-compositions.json`, `battle-scenario.json`).
- Cross-component communication is in-memory object references only after setup is loaded.
- Output artifact is a console executable at `NavySimulator/bin/<Configuration>/net10.0/`.

