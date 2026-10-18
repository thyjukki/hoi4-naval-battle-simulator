# AGENTS Guide for NavySimulator

## Project Goal
- Simulate Hearts of Iron 4 (HOI4) naval battles as close to the real game as possible, down to decoded formulas.
- Sources of truth, in order: reverse-engineering findings (`docs/reverse-engineering/findings/`), calibration against recorded in-game battles (`docs/calibration.md`), the game's own files (defines, unit/equipment scripts). The wiki and community testing are secondary.
- The spec lives in `docs/mechanics/`. Every rule has an ID (`S-*` surface, `A-*` air) and provenance tags ([DEFINE], [RE], [CAL], [WIKI], [PDX], [COMMUNITY], [LEGACY], [ASSUMED]). Code cites the ID where it implements the rule.
- Fleets consist of ships; ships have a design = hull + modules + optional MIO; researches and spirits add stat modifiers.
- Battles are between 2 sides, each with a fleet, researches, spirits, repair (retreat) priority and submarine engagement risk.

## Possible stats
- Speed, Organization, HP, Reliability
- Light Attack / Piercing, Heavy Attack / Piercing, Torpedo Attack, Depth Charges
- Armor, Anti-Air
- Surface / Sub Visibility, Surface / Sub Detection
- Hit chance factors (light, heavy, torpedo, depth charge), torpedo damage reduction, torpedo enemy crit chance

## Project Snapshot
- Solution `NavySimulator.sln`:
  - `src/NavySimulator.Engine`: simulation only, no I/O. Namespace `NavySimulator.Engine`.
  - `src/NavySimulator.GameData`: JSON setup loading, Paradox script parser (`Paradox/`), defines `.lua` parser and binder (`Defines/`). Namespace `NavySimulator.GameData`.
  - `src/NavySimulator.Cli`: entrypoint (`Program.cs`), options, report writing. Executable name `NavySimulator`.
  - `tests/NavySimulator.Engine.Tests`, `tests/NavySimulator.GameData.Tests`: xUnit.
- Target runtime `.NET 10`, implicit usings, nullable enabled, no NuGet packages outside tests.

## Architecture
- `GameDefines` (`Engine/Defines/`) holds `NNavy`, `NAir`, `NMilitary` define classes whose property names are the exact game keys, defaulting to vanilla 1.19.3, plus `StaticModifiers` (blocks of `00_static_modifiers.txt`) and `HardcodedValues` for decoded constants that are in no script file. It is an instance per scenario: never add static mutable state.
- `DefinesLoader` (GameData) applies `.lua` files or game/mod folders over the vanilla defaults; `DefinesBinder` binds by property name.
- `BattleRules` (`Engine/Battles/BattleRules.cs`) holds the few options left after reverse-engineering (night, start hour, tooling switches). If a mechanic is uncertain, add a variant here instead of guessing silently, and document it in the spec.
- `BattleEngine` runs the hour loop as hoi4.exe does: positioning/screening for both sides, then the attacker's turn, then the defender's (per ship: submarine reveal, state machine, retreat decision, firing), experience, end check.
- Rules live in `Engine/Battles/Mechanics/` as static classes, one concern each: `PositioningRules`, `ScreeningRules`, `WeaponCadenceRules`, `HitChanceRules`, `TargetingRules`, `DamageRules`, `RetreatRules`, `SubmarineRules`, `ExperienceRules`.
- All randomness goes through `IRandomSource` (seeded `SeededRandomSource`; scripted in tests).
- Results: `BattleResult` with per-shot `ShotRecord`s, `ShipResult`s, `SideResult`s, hourly log and notes about ignored inputs.
- Air combat (`Engine/Battles/Air/`: `AirPhase`, `AirRules`) runs after each side's surface fire in air hours; spec `docs/mechanics/air.md`.
- Reverse-engineering: findings in `docs/reverse-engineering/findings/` (hoi4.exe 1.19.3, with SHA-256), tooling in `tools/re/`. Re-run it after game patches before trusting the [RE] tags.

## Code Patterns to Follow Here
- Use `var` wherever possible.
- One top-level type per file. Engine types in `NavySimulator.Engine.*`, loading code in `NavySimulator.GameData.*`.
- Every formula reads its constants from `GameDefines`; no literals in rule code except where the spec says so (then put them in `HardcodedValues` with a provenance tag).
- Comment each rule with its spec ID and provenance tag, e.g. `// S-HIT-SHIP [DEFINE + WIKI]`.
- Match the existing style of simple POCO/domain types with constructor assignment; keep composition explicit in constructors.
- Preserve nullable-aware signatures.

## Build, Run, and Validate
- `dotnet build NavySimulator.sln`
- `dotnet run --project src/NavySimulator.Cli -- --help`
- `dotnet test NavySimulator.sln -p:UseAppHost=false`
- The CLI writes reports to `output/<scenario>_<timestamp>/` (summary, hourly log, shots.csv, ships.csv per run).

## Agent Workflow Tips for This Repo
- Combat behavior: read the spec item in `docs/mechanics/` first, then the matching class in `Engine/Battles/Mechanics/`. Update both together.
- Setup/data issues: `src/NavySimulator.GameData/Loading/SetupLoader.cs` and DTOs in `Contracts/Dto/`.
- Define values: never hand-copy numbers into code. Add a property to the defines class with the exact game key and vanilla default; see `docs/mechanics/defines.md`.
- Add regression tests in `tests/NavySimulator.Engine.Tests/Mechanics/` (rule-level, use `ScriptedRandomSource` for exact rolls) or `Battles/` (engine-level).
- `tools/import_hoi4_ship_modules.py` refreshes `data/modules/00_imported_ship_modules.json` from HOI4 module scripts (to be replaced by `PdxParser`-based import in milestone 4).
- Reverse-engineering: check `docs/reverse-engineering/index.md` before decoding anything in hoi4.exe; add every newly decoded function and field to it, with pseudocode in a findings file.
- Keep changes small and compile-check with `dotnet build` after structural edits.

## Known Integration Boundaries
- No external dependencies beyond the .NET SDK (xUnit in tests).
- Runtime input: `data/` JSON (`hulls/`, `modules/`, `mios/`, `ship-designs/`, `researches/`, `spirits/`, `planes/`, `force-compositions/`, `battle-scenario.json`) and optional defines (`data/defines/*.lua`, or a HOI4/mod folder).
- Battle mechanics reference: https://hoi4.paradoxwikis.com/Naval_battle (snapshot in `docs/reference/`), treated as a secondary source.
