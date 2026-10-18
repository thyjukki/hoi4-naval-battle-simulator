# Defines: sources and versions

The engine reads every numeric combat constant from a `GameDefines` instance (`src/NavySimulator.Engine/Defines/`). Property names are the exact game keys (`NNavy.COMBAT_BASE_HIT_CHANCE`), so the loader binds them from any defines file by name.

## Loading order

1. Built-in defaults: vanilla 1.17 (below).
2. The scenario's `"defines"` list, in order. Each entry is a `.lua` file, or a game/mod root directory whose `common/defines/*.lua` files are applied in file-name order, as the game does. Relative paths resolve against the data directory.
3. Each `--defines <path>` on the command line, in order.

A later value overrides an earlier one. The CLI prints how many values were applied. Type mismatches (for example a string where a number is expected) become warnings, and the default is kept.

To match your game exactly, point at your install: `--defines "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV"`, followed by each mod's folder in load order.

## Where the built-in values come from

Paradox does not publish `00_defines.lua` separately. The built-in values were taken from complete copies of the file shipped inside public mods, and a value counts as vanilla only when independent copies for the same game version agree:

| Version | Primary copy (pinned) | Cross-checked against |
|---|---|---|
| 1.16.9 | [sams7891/hoi4_latvia_mod@917a183](https://raw.githubusercontent.com/sams7891/hoi4_latvia_mod/917a183ea3f9832fc1b8020551eaf8b7bcb21bb9/common/defines/00_defines.lua) | 6 more mod copies for 1.16.x; all agree on every NNavy/NAir/NMilitary key |
| 1.17.x | [TheEmperor-1/WorldWarI@9ac92ee](https://raw.githubusercontent.com/TheEmperor-1/WorldWarI/9ac92ee1c6e7ed19ea43ca4ace93470d2c1708db/common/defines/00_defines.lua) | VVUH/VVUH and Musvage/The-Second-Sengoku; all agree on every NNavy/NAir key |

`NGame.SAVE_VERSION` confirms the versions (25 in 1.16.9, 30 in the 1.17 copies). The 1.17 data comes from fewer copies, so it is somewhat less certain. Keys removed in 1.17 keep their 1.16.9 value and say so in their doc comment (for example `CARRIER_STACK_PENALTY`).

Only combat-relevant keys are properties (about 190 of roughly 600 NNavy/NAir keys). To use another key, add a property with the exact game name and its vanilla default to the matching class; the binder picks it up automatically.

## Known differences between versions

1.16.9 → 1.17, combat-relevant:
- `MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION` 0.0 → 0.25; `MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY` 0 → 4.
- `CARRIER_STACK_PENALTY`/`_EFFECT` removed, replaced by `NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY`, `NAVAL_COMBAT_PLANE_STACKING_PENALTY_EFFECT` and `SHIP_SILHOUETTE_VALUE_PLANES_*` (A-STACK).
- `NAVAL_COMBAT_AIR_*_TARGET_SCORE` replaced by `*_TARGET_BASE` and `*_TARGET_SCALE` (A-TARGET).
- New: carrier stances (`CARRIER_OFFENSIVE_STANCE_*`, `SELECTED_SORTIE_INITIAL_TIME`), `CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER`, `CARRIER_PERCENTAGE_DEFEND`.

Later than 1.17 (not verified): `NAVAL_STRIKE_CARRIER_MULTIPLIER` is reported as 12 at 1.18.3 (10 in the 1.17 copies), and the 1.18.1 notes mention carrier planes about 20% stronger. Load your own install's defines.

## Values that did not match vanilla

Many constants in the previous `Hoi4Defines.cs` differed from vanilla. Commit history says they were matched to the TFB mod's define file, and the research matched several of them to the changelog of Steam Workshop item 2559317737, the same mod `tools/import_hoi4_ship_modules.py` reads modules from. They are preserved in [`data/defines/tfb-legacy.lua`](../../data/defines/tfb-legacy.lua); add `"defines": ["defines/tfb-legacy.lua"]` to a scenario to use them. Prefer loading the mod's own `common/defines` when it is available, since the legacy file was never re-checked against it.

| Define | Previous code | Vanilla 1.17 |
|---|---|---|
| `BASE_GUN_COOLDOWNS` {heavy, torpedo, light} | {3, 5, 2} | {1, 4, 1} |
| `GUN_HIT_PROFILES` {heavy, torpedo, light} | {95, 130, 60} | {80, 100, 45} |
| `HIT_PROFILE_SPEED_FACTOR` / `_BASE` | 0.85 / 5 | 0.5 / 20 |
| `COMBAT_MIN_HIT_CHANCE` | 0.02 | 0.05 |
| `COMBAT_DAMAGE_RANDOMNESS` | 0.2 | 0.5 |
| `COMBAT_DAMAGE_TO_STR_FACTOR` | 0.5 | 0.6 |
| `COMBAT_TORPEDO_CRITICAL_CHANCE` / `_DAMAGE_MULT` | 0.25 / 3.75 | 0.1 / 2 |
| `NAVY_PIERCING_THRESHOLDS` | 9 tiers | 6 tiers |
| `ALL_SHIPS_ACTIVATE_TIME` / `CAPITAL_ONLY_COMBAT_ACTIVATE_TIME` | 12 / 8 | 8 / 6 |
| `ANTI_AIR_MULT_ON_INCOMING_AIR_DAMAGE` / `_POW_` | 0.03 / 0.58 | 0.18 / 0.225 |
| `ANTI_AIR_TARGETING` | 0.4 | 0.9 |
| `NAVAL_STRIKE_CARRIER_MULTIPLIER` | 1.75 | 10 |
| `BASE_CARRIER_SORTIE_EFFICIENCY` | 0.2 | 0.5 |

Three previous values were marked as testing overrides and are commented out in the profile: `MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY = 99`, the 40% retreat threshold, and night hours 24/0.

## Values that are not defines

`HardcodedValues` holds constants the model needs that are not in any defines file: surface target weights, the +40% screening hit bonus, retreat speed bonuses, experience levels, night. Each one carries a [WIKI], [LEGACY] or [ASSUMED] tag. They are the main targets for [reverse-engineering](../reverse-engineering/README.md), and some (experience levels) probably live in other game script files.
