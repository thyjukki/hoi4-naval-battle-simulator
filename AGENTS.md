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
- Battle participants have researches that provide bonuses to certain stats
- Battle participants have spirits that provide bonuses to certain stats
- Battle has a weather condition that provides bonuses or penalties to certain stats
- The app loads fleet compositions, ship designs, modules, mios, researches, spirits, and planes from JSON files
- The app simulates battles between fleets and writes hourly/summary reports to output files.

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
- Surface combat resolves per ship and applies damage immediately, so later shots cannot target ships sunk earlier in the same hour
- Weapons use universal cooldown cadence by weapon type (all ships fire same weapon type on the same eligible hours)
- Carriers planes have a cooldown for flying
- Round is repeated until one fleet is destroyed, or set number of hours have passed
- Screening efficiency is determined by the ratio of screening group to battle line
- Carrier screening efficiency is determined by the ratio of carrier group to battle line
- Scenario supports `continueAfterRetreat` and `dontRetreat` toggles

## Battle mechanics
- Battle mechanics are explained in hoi4 wiki https://hoi4.paradoxwikis.com/Naval_battle
- 
## Project Snapshot
- Solution: `NavySimulator.sln` with executable `NavySimulator/NavySimulator.csproj` and tests `NavySimulator.Tests/NavySimulator.Tests.csproj`.
- Target runtime: `.NET 10` (`<TargetFramework>net10.0</TargetFramework>`).
- Language features enabled: implicit usings and nullable reference types.
- Current app entrypoint is top-level statements in `NavySimulator/Program.cs`.
- `Program.cs` loads scenario data from `NavySimulator/Data/`, runs `BattleSimulator`, prints short summaries, and writes logs/reports to `output/<scenario>_<runtime>/`.

## Architecture (Current State)
- Domain code is organized under `NavySimulator/Domain/` (`Battles`, `Fleets`, `Ships`, `Stats`) in the `NavySimulator.Domain` namespace.
- Setup/loading code is organized under `NavySimulator/Setup/` (`Contracts/Dto`, `Loading`, `Validation`) in the `NavySimulator.Setup` namespace.
- `Ship` (`NavySimulator/Domain/Ships/Ship.cs`) owns a `ShipDesign` instance.
- `ShipDesign` (`NavySimulator/Domain/Ships/ShipDesign.cs`) owns a `Hull`, `List<IModule>`, and optional `MioBonus`.
- `IModule` (`NavySimulator/Domain/Ships/IModule.cs`) defines the module contract via `string ID { get; }` and `ShipStats StatModifiers { get; }`.
- Setup data flow is constructor-injected object graph built by `SetupLoader`: `Hull/StatModule/MioBonus` + `Research/Spirit/Plane` -> `ShipDesign` -> `Ship` -> `Fleet` -> `BattleParticipant` -> `BattleScenario`.
- `BattleSimulator` orchestrates hourly simulation with explicit air and surface phase helpers.
- `NavalAirCombatSimulator` handles air sortie snapshots, strike targeting, preemptive AA, and strike damage.
- `NavalSurfaceCombatSimulator` handles weapon actions, activation delays, cooldown cadence, targeting, hit chance, and immediate damage application.
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
- Run tests:
  - `dotnet test NavySimulator.sln -p:UseAppHost=false`
- Verified behavior as of 2026-03-31: app prints setup + run summaries to console and writes detailed logs/reports under `output/`.

## Agent Workflow Tips for This Repo
- For setup/data issues, start in `NavySimulator/Setup/Loading/SetupLoader.cs` and matching DTOs in `NavySimulator/Setup/Contracts/Dto/`.
- For combat behavior issues, start in `NavySimulator/Domain/Battles/BattleSimulator.cs` and related domain models.
- Use `scritps/import_hoi4_ship_modules.py` to refresh `NavySimulator/Data/modules/00_imported_ship_modules.json` from HOI4 source modules (`.../common/units/equipment/modules/00_ship_modules.txt`); the script prints source modifiers it intentionally skips.
- Use `scritps/split_data_files.py` when migrating legacy monolithic `hulls.json`/`modules.json`/`mios.json`/`ship-designs.json` files into folder-based `Data/<type>/` files.
- Data loading prefers folder-based inputs; keep new content in `Data/<type>/*.json`.
- When introducing new behavior, demonstrate wiring in `Program.cs` so it is runnable immediately.
- Keep changes small and compile-check with `dotnet build` after structural edits.
- Add regression tests in `NavySimulator.Tests/Domain/Battles/` for battle logic changes.

## Known Integration Boundaries
- External dependencies: none beyond the .NET SDK; no NuGet package references currently.
- Runtime setup depends on JSON files under `NavySimulator/Data/` (`hulls/*.json`, `modules/*.json`, `mios/*.json`, `ship-designs/*.json`, `researches/*.json`, `spirits/*.json`, `planes/*.json`, `force-compositions/*.json`, `battle-scenario.json`).
- Cross-component communication is in-memory object references only after setup is loaded.
- Output artifacts include console executable (`NavySimulator/bin/<Configuration>/net10.0/`) and run reports under `output/`.

