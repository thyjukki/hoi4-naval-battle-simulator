# AGENTS Guide for NavySimulator

## Project Goal
- Simulate Hearts of Iron 4 (HOI4) naval battles as close to the real game as possible, down to decoded formulas.
- Sources of truth, in order: reverse-engineering findings (`docs/reverse-engineering/findings/`), calibration against recorded in-game battles (`docs/calibration.md`), the game's own files (defines, unit/equipment scripts). The wiki and community testing are secondary.
- The spec lives in `docs/mechanics/`. Every rule has an ID (`S-*` surface, `A-*` air) and provenance tags ([DEFINE], [RE], [CAL], [WIKI], [PDX], [COMMUNITY], [LEGACY], [ASSUMED]). Code cites the ID where it implements the rule.
- Fleets consist of ships; ships have a design = hull + one module per hull slot + optional MIO (the design names the MIO; the side unlocks its traits and policy, S-MIO); the ship type (light cruiser, battleship, ...) is not a hull but follows from hull and modules (S-DESIGN), and gives the role and organization; researches (technologies and equipment bonuses) and spirits add stat modifiers, combined as decoded in S-STATS (`docs/mechanics/surface.md`).
- A battle has a place and time: a sea region (its weather periods and naval terrain), a battle province (its darkness) and a start date; the weather is fixed or simulated from the region (S-REGION, S-NIGHT, S-WEATHER, S-WEATHER-SIM, S-TERRAIN).
- Battles are between 2 sides, each with a fleet, researches, doctrines (per folder: grand doctrine, a subdoctrine per track and the track's mastery; S-DOCTRINE), spirits, the country's national state (1936 history, completed focuses, hand-edited national spirits and variables; S-NATIONAL), an admiral (skills and traits, picked from the country's admirals or made up; S-ADMIRAL), a pride of the fleet (one capital ship or carrier of the fleet; S-POTF) and the task force's aggressiveness (engagement rule: retreat threshold and which lines submarines attack) and repair priority (recorded, not used in combat). Use the game's terms: aggressiveness (Do Not Engage, Engage At Low/Medium/High Risk, Always Engage) and repair priority (Never repair, Low, Medium, High; not used in combat).

## Possible stats
- Speed, Range, Organization, HP, Reliability
- Light Attack / Piercing, Heavy Attack / Piercing, Torpedo Attack, Depth Charges
- Armor, Anti-Air
- Surface / Sub Visibility, Surface / Sub Detection
- Hit chance factors (light, heavy, torpedo, depth charge), torpedo damage reduction, torpedo enemy crit chance

## Project Snapshot
- Solution `NavySimulator.sln`:
  - `src/NavySimulator.Engine`: simulation only, no I/O. Namespace `NavySimulator.Engine`.
  - `src/NavySimulator.GameData`: JSON setup loading (`Loading/`), importing hulls, modules, ship types, doctrines and spirits from game/mod folders (`Import/`), saving edits back to JSON (`Saving/CatalogWriter`), report files (`Reports/ReportWriter`), Paradox script parser (`Paradox/`), defines `.lua` parser and binder (`Defines/`). Namespace `NavySimulator.GameData`.
  - `src/NavySimulator.Cli`: entrypoint (`Program.cs`) and options. Executable name `NavySimulator`.
  - `src/NavySimulator.Web`: the GUI, a local Blazor Server app (`Services/` for state and logic, `Components/` for pages). Spec in `docs/gui.md`.
  - `tests/NavySimulator.Engine.Tests`, `tests/NavySimulator.GameData.Tests`, `tests/NavySimulator.Web.Tests`: xUnit.
- Target runtime `.NET 10`, implicit usings, nullable enabled, no NuGet packages outside tests.

## Architecture
- `GameDefines` (`Engine/Defines/`) holds `NNavy`, `NAir`, `NMilitary` define classes whose property names are the exact game keys, defaulting to vanilla 1.19.4 (same values as 1.19.3), plus `StaticModifiers` (blocks of `00_static_modifiers.txt`) and `HardcodedValues` for decoded constants that are in no script file. It is an instance per scenario: never add static mutable state.
- `DefinesLoader` (GameData) applies `.lua` files or game/mod folders over the vanilla defaults; `DefinesBinder` binds by property name.
- `BattleRules` (`Engine/Battles/BattleRules.cs`) holds the few options left after reverse-engineering (night, start hour, tooling switches). If a mechanic is uncertain, add a variant here instead of guessing silently, and document it in the spec.
- Location (`Engine/Environment/`: `BattleLocation`, `SeaRegion`, `NavalTerrain`, `RegionWeather`, `WeatherPattern`, `GameDate`): each hour the session builds a `BattleEnvironment` (darkness, the region's weather and its modifier holder, the naval terrain) that the combat rules take where they read location modifiers (`DarknessRules`, `WeatherRules`). `GameData/Loading/LocationLoader` builds it from the scenario and the imported regions.
- `BattleSession` runs the hour loop as hoi4.exe does, one `StepHour` at a time: positioning/screening for both sides, then the attacker's turn, then the defender's (per ship: submarine reveal, state machine, retreat decision, firing), experience, end check. `BattleEngine.Simulate` runs a session to its end. Mechanics changes go in `BattleSession` and the rule classes; stepping in any chunk sizes must give the same battle as `Simulate` (tested).
- Rules live in `Engine/Battles/Mechanics/` as static classes, one concern each: `PositioningRules`, `ScreeningRules`, `WeaponCadenceRules`, `HitChanceRules`, `TargetingRules`, `DamageRules`, `RetreatRules`, `SubmarineRules`, `ExperienceRules`, and `MemberModifierRules` (the game's member modifier getter, through which every combat rule reads modifiers).
- All randomness goes through `IRandomSource` (seeded `SeededRandomSource`; scripted in tests). Combat rules call `NextDouble(RollKind)` so `ReplayRandomSource` can feed a logged game battle's own rolls; `NavySimulator replay` (`Engine/Battles/Replay/`, `GameData/Replay/`) compares the engine with a battle logged by `tools/ingame` shot by shot (S-REPLAY). Use it first when a calibration case disagrees.
- Results: `BattleResult` with per-shot `ShotRecord`s, `ShipResult`s, `SideResult`s, structured `BattleEvent`s, a per-hour `Timeline` (`HourSummary`), the hourly log and notes about ignored inputs. A session can also keep every ship's state per hour (`recordShipHistory`, used by the GUI to rewind).
- Setup: `SetupLoader.ReadCatalog` reads a data directory into a `ContentCatalog` (DTOs plus each item's source file); `SetupLoader.BuildScenario` validates and builds a `BattleScenario` from a catalog and a scenario DTO (in-memory edits included); `ContentLibrary` makes engine objects for previews. `LoadScenarioFromDirectory` is both steps in one.
- Designs: `Hull` (slots, count limits), `StatModule` (category, forbidden categories, added equipment types), `ShipType` (a naval sub-unit) in `Engine/Ships/`; `DesignRules` checks a design and picks its ship type (S-DESIGN). Rule breaks are loader warnings, not errors, and a design's `allowAnyModule` silences them.
- Air combat (`Engine/Battles/Air/`: `AirPhase`, `AirRules`, `RegionAirWarRules`) runs after each side's surface fire in air hours, after the region's air war that opens each air hour (A-REGION: plane losses and disruption between the battle's air wings); aces (A-ACE) come from `data/aces/`; spec `docs/mechanics/air.md`.
- MIOs (`Engine/Mios/`: `Mio`, `MioSetup`, `MioRules`): a design names its MIO, a side can assign another one per design (`designMios`, `BattleParticipant.MioOf`) and sets unlocked traits and policy per MIO; `BattleParticipant` passes the side's MIO bonus into the design stats (S-MIO, S-STATS-MIO). A side's `country` limits its MIOs (S-MIO-COUNTRY, GameData `MioCountries`).
- Admirals (`Engine/Leaders/`: `Admiral`, `AdmiralRules`, `LeaderTrait`, `LeaderSkills`): a side's admiral builds the game's two leader containers from skill levels and traits; `BattleParticipant` puts container A and the traits' sub-unit modifiers on every ship (`Ship.AdmiralModifiers`, read by `MemberModifierRules.ShipModifier`), and the session reads the admiral-only modifiers (fleet-size penalty, enemy retreat chance) from the enemy's admiral (S-ADMIRAL).
- Doctrines and spirits (`Engine/Doctrines/`: `DoctrineRules`, `CountryEffects`; `Engine/Ships/Spirit.cs`): a side's active effects (grand doctrine, subdoctrines, rewards its mastery unlocks, milestones of complete tracks, spirits) become country modifiers, technology-like sub-unit bonuses and design equipment bonuses in `BattleParticipant` (S-DOCTRINE-EFFECT). Only the naval folder is simulated; air doctrines are recorded.
- Reverse-engineering: findings in `docs/reverse-engineering/findings/` (decoded in hoi4.exe 1.19.3, with SHA-256; checked in 1.19.4), tooling in `tools/re/`. After a game patch, map the addresses with `tools/re/remap.py` and re-verify before trusting the [RE] tags (`docs/reverse-engineering/README.md#patch-1194` shows how 1.19.4 was done).
- National focuses and spirits (`Engine/Nations/`: `Nation`, `NationalSetup`, `NationalRules`, `DynamicModifier`): a side's `national` setup for its `country` resolves the history's and completed focuses' effect statements (ideas, dynamic modifiers, variables) plus hand edits into active national spirits and dynamic modifiers, whose effects join `BattleParticipant.ActiveEffects` like a doctrine's (S-NATIONAL). Absent: none, so existing scenarios and baselines are unchanged.
- Override sets (`GameData/Overrides/`, `data/overrides/`, spec `docs/overrides.md`): modding prototypes that change defines, static modifier keys and imported content by ID and JSON path, applied by `SetupLoader.BuildScenario(..., overrides)` after every define source and to a copy of the catalog (`ContentCatalog.Copy`). The GUI has one active set (`Workspace.ActiveOverrides`; `Workspace.Content` is the catalog with it, `Workspace.Catalog` stays as on disk) and the Batch page compares the same seeds with and without it; the CLI takes `--overrides`. A new imported collection needs an entry in `OverrideCollections`.

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
- Calibration baselines live in `data/calibration/` (`docs/calibration.md#baselines`). After a combat rule change, replay the roll-logged seeds they list (`NavySimulator replay`; every shot should still match) and compare 200 simulator seeds with each case; `tools/ingame/make_calibration.py` rewrites a case with a new simulator reference.
- Hull, module, ship type and critical part data: never hand-edit `data/hulls/`, `data/modules/`, `data/ship-types/` or `data/critical-parts/`; re-import them with `dotnet run --project src/NavySimulator.Cli -- import-ship-data --game "<HOI4 dir>" --mod "<mod dir>"` (the user's data is TFB's: the local dev checkout over vanilla). IDs and names are the game's. The same holds for `data/doctrines/` and `data/spirits/` with `import-doctrines`, for `data/mios/`, `data/mio-policies/`, `data/equipment-groups/` and `data/countries/` with `import-mios`, for `data/admirals/`, `data/leader-traits/` and `data/leader-skills/` with `import-leaders`, for `data/sea-regions/` and `data/naval-terrains/` with `import-regions`, for `data/planes/` and `data/aces/` with `import-planes` (the game's fixed plane equipment, no plane designer, and `common/aces`), and for `data/nations/` with `import-national` (default USA and JAP; `--countries` for others).
- Reverse-engineering: check `docs/reverse-engineering/index.md` before decoding anything in hoi4.exe; add every newly decoded function and field to it, with pseudocode in a findings file.
- Keep changes small and compile-check with `dotnet build` after structural edits.

## Known Integration Boundaries
- No external dependencies beyond the .NET SDK (xUnit in tests).
- Runtime input: `data/` JSON (`hulls/`, `modules/`, `ship-types/`, `critical-parts/`, `mios/`, `mio-policies/`, `equipment-groups/`, `countries/`, `admirals/`, `leader-traits/`, `leader-skills/`, `ship-designs/`, `researches/`, `doctrines/`, `spirits/`, `planes/`, `sea-regions/`, `naval-terrains/`, `nations/`, `force-compositions/`, `battle-scenario.json`) and optional defines (`data/defines/*.lua`, or a HOI4/mod folder, which also gives the static modifiers and `common/weather.txt`), and override sets (`data/overrides/`).
- The GUI uses only the ASP.NET Core shared framework (no NuGet packages); its per-user settings live in the local application data folder, outside the repository.
- Battle mechanics reference: https://hoi4.paradoxwikis.com/Naval_battle (snapshot in `docs/reference/`), treated as a secondary source.
