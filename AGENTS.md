# AGENTS Guide for NavySimulator

## Project Goal
- Simulate Hearts of Iron 4 (HOI4) naval battles as close to the real game as possible, down to decoded formulas.
- Sources of truth, in order: reverse-engineering findings (`docs/reverse-engineering/findings/`), calibration against recorded in-game battles (`docs/calibration.md`), the game's own files (defines, unit/equipment scripts). The wiki and community testing are secondary.
- The spec lives in `docs/mechanics/`. Every rule has an ID (`S-*` surface, `A-*` air) and provenance tags ([DEFINE], [RE], [CAL], [WIKI], [PDX], [COMMUNITY], [LEGACY], [ASSUMED]). Code cites the ID where it implements the rule.
- Fleets consist of ships; ships have a design = hull + one module per hull slot + optional MIO; the ship type (light cruiser, battleship, ...) is not a hull but follows from hull and modules (S-DESIGN), and gives the role and organization; researches (technologies and equipment bonuses) and spirits add stat modifiers, combined as decoded in S-STATS (`docs/mechanics/surface.md`).
- Battles are between 2 sides, each with a fleet, researches, spirits and the task force's aggressiveness (engagement rule: retreat threshold and which lines submarines attack) and repair priority (recorded, not used in combat). Use the game's terms: aggressiveness (Do Not Engage, Engage At Low/Medium/High Risk, Always Engage) and repair priority (Never repair, Low, Medium, High; not used in combat).

## Possible stats
- Speed, Organization, HP, Reliability
- Light Attack / Piercing, Heavy Attack / Piercing, Torpedo Attack, Depth Charges
- Armor, Anti-Air
- Surface / Sub Visibility, Surface / Sub Detection
- Hit chance factors (light, heavy, torpedo, depth charge), torpedo damage reduction, torpedo enemy crit chance

## Project Snapshot
- Solution `NavySimulator.sln`:
  - `src/NavySimulator.Engine`: simulation only, no I/O. Namespace `NavySimulator.Engine`.
  - `src/NavySimulator.GameData`: JSON setup loading (`Loading/`), importing hulls, modules and ship types from game/mod folders (`Import/`), saving edits back to JSON (`Saving/CatalogWriter`), report files (`Reports/ReportWriter`), Paradox script parser (`Paradox/`), defines `.lua` parser and binder (`Defines/`). Namespace `NavySimulator.GameData`.
  - `src/NavySimulator.Cli`: entrypoint (`Program.cs`) and options. Executable name `NavySimulator`.
  - `src/NavySimulator.Web`: the GUI, a local Blazor Server app (`Services/` for state and logic, `Components/` for pages). Spec in `docs/gui.md`.
  - `tests/NavySimulator.Engine.Tests`, `tests/NavySimulator.GameData.Tests`, `tests/NavySimulator.Web.Tests`: xUnit.
- Target runtime `.NET 10`, implicit usings, nullable enabled, no NuGet packages outside tests.

## Architecture
- `GameDefines` (`Engine/Defines/`) holds `NNavy`, `NAir`, `NMilitary` define classes whose property names are the exact game keys, defaulting to vanilla 1.19.3, plus `StaticModifiers` (blocks of `00_static_modifiers.txt`) and `HardcodedValues` for decoded constants that are in no script file. It is an instance per scenario: never add static mutable state.
- `DefinesLoader` (GameData) applies `.lua` files or game/mod folders over the vanilla defaults; `DefinesBinder` binds by property name.
- `BattleRules` (`Engine/Battles/BattleRules.cs`) holds the few options left after reverse-engineering (night, start hour, tooling switches). If a mechanic is uncertain, add a variant here instead of guessing silently, and document it in the spec.
- `BattleSession` runs the hour loop as hoi4.exe does, one `StepHour` at a time: positioning/screening for both sides, then the attacker's turn, then the defender's (per ship: submarine reveal, state machine, retreat decision, firing), experience, end check. `BattleEngine.Simulate` runs a session to its end. Mechanics changes go in `BattleSession` and the rule classes; stepping in any chunk sizes must give the same battle as `Simulate` (tested).
- Rules live in `Engine/Battles/Mechanics/` as static classes, one concern each: `PositioningRules`, `ScreeningRules`, `WeaponCadenceRules`, `HitChanceRules`, `TargetingRules`, `DamageRules`, `RetreatRules`, `SubmarineRules`, `ExperienceRules`, and `MemberModifierRules` (the game's member modifier getter, through which every combat rule reads modifiers).
- All randomness goes through `IRandomSource` (seeded `SeededRandomSource`; scripted in tests). Combat rules call `NextDouble(RollKind)` so `ReplayRandomSource` can feed a logged game battle's own rolls; `NavySimulator replay` (`Engine/Battles/Replay/`, `GameData/Replay/`) compares the engine with a battle logged by `tools/ingame` shot by shot (S-REPLAY). Use it first when a calibration case disagrees.
- Results: `BattleResult` with per-shot `ShotRecord`s, `ShipResult`s, `SideResult`s, structured `BattleEvent`s, a per-hour `Timeline` (`HourSummary`), the hourly log and notes about ignored inputs. A session can also keep every ship's state per hour (`recordShipHistory`, used by the GUI to rewind).
- Setup: `SetupLoader.ReadCatalog` reads a data directory into a `ContentCatalog` (DTOs plus each item's source file); `SetupLoader.BuildScenario` validates and builds a `BattleScenario` from a catalog and a scenario DTO (in-memory edits included); `ContentLibrary` makes engine objects for previews. `LoadScenarioFromDirectory` is both steps in one.
- Designs: `Hull` (slots, count limits), `StatModule` (category, forbidden categories, added equipment types), `ShipType` (a naval sub-unit) in `Engine/Ships/`; `DesignRules` checks a design and picks its ship type (S-DESIGN). Rule breaks are loader warnings, not errors, and a design's `allowAnyModule` silences them.
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
- `dotnet run --project src/NavySimulator.Web -- --data <copy of data> --no-browser --settings <scratch file>`: GUI on http://localhost:5180 (use a data copy and a scratch settings file when testing, the GUI writes JSON and settings)
- `dotnet test NavySimulator.sln -p:UseAppHost=false`
- The CLI writes reports to `output/<scenario>_<timestamp>/` (summary, hourly log, shots.csv, ships.csv per run).

## Agent Workflow Tips for This Repo
- Combat behavior: read the spec item in `docs/mechanics/` first, then the matching class in `Engine/Battles/Mechanics/`. Update both together.
- Setup/data issues: `src/NavySimulator.GameData/Loading/SetupLoader.cs` and DTOs in `Contracts/Dto/`. A new DTO field also needs the GUI editor (`src/NavySimulator.Web/Components/`) and is written back by `CatalogWriter` automatically.
- GUI: keep logic in `src/NavySimulator.Web/Services/` (plain classes, tested in `tests/NavySimulator.Web.Tests`) and Razor components thin. Read battle state only through `BattleRunner.View`, which copies it under the stepping lock. Document user-visible GUI changes in `docs/gui.md`.
- Define values: never hand-copy numbers into code. Add a property to the defines class with the exact game key and vanilla default; see `docs/mechanics/defines.md`.
- Add regression tests in `tests/NavySimulator.Engine.Tests/Mechanics/` (rule-level, use `ScriptedRandomSource` for exact rolls) or `Battles/` (engine-level).
- Hull, module, ship type and critical part data: never hand-edit `data/hulls/`, `data/modules/`, `data/ship-types/` or `data/critical-parts/`; re-import them with `dotnet run --project src/NavySimulator.Cli -- import-ship-data --game "<HOI4 dir>" --mod "<mod dir>"` (the user's data is TFB's: the local dev checkout over vanilla). IDs and names are the game's.
- Reverse-engineering: check `docs/reverse-engineering/index.md` before decoding anything in hoi4.exe; add every newly decoded function and field to it, with pseudocode in a findings file.
- Keep changes small and compile-check with `dotnet build` after structural edits.

## Known Integration Boundaries
- No external dependencies beyond the .NET SDK (xUnit in tests).
- Runtime input: `data/` JSON (`hulls/`, `modules/`, `ship-types/`, `mios/`, `ship-designs/`, `researches/`, `spirits/`, `planes/`, `force-compositions/`, `battle-scenario.json`) and optional defines (`data/defines/*.lua`, or a HOI4/mod folder).
- The GUI uses only the ASP.NET Core shared framework (no NuGet packages); its per-user settings live in the local application data folder, outside the repository.
- Battle mechanics reference: https://hoi4.paradoxwikis.com/Naval_battle (snapshot in `docs/reference/`), treated as a secondary source.
