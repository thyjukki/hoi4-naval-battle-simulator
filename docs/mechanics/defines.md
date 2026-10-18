# Defines: sources and versions

The engine reads every numeric combat constant from a `GameDefines` instance (`src/NavySimulator.Engine/Defines/`):
- `NNavy`, `NAir`, `NMilitary`: properties named exactly like the game keys (`NNavy.COMBAT_BASE_HIT_CHANCE`), bound by name from any defines file;
- `StaticModifiers`: the blocks of `common/modifiers/00_static_modifiers.txt` that naval combat applies directly (`screening_bonus`, `capital_screening_bonus`, `ship_experience_bonus_max`, `ship_experience_malus_min`, `carrier_experience_*`, `night`);
- `HardcodedValues`: constants that are in no script file, tagged [RE] (read from hoi4.exe) or [ASSUMED].

## Loading order

1. Built-in defaults: vanilla **1.19.3**, generated from the game's own `00_defines.lua` and `00_static_modifiers.txt`.
2. The scenario's `"defines"` list, in order.
3. Each `--defines <path>` on the command line, in order.

A source is a `.lua` file, a folder of `.lua` files, or a game/mod root. For a root, every `common/defines/*.lua` is applied in file-name order (as the game does), and the static modifier blocks above are taken from `common/modifiers/*.txt`; a block found later replaces the earlier one completely. Later sources override earlier ones, so list the game first and mods after it in load order:

```powershell
dotnet run --project src/NavySimulator.Cli -- `
  --defines "C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV" `
  --defines "C:\Program Files (x86)\Steam\steamapps\workshop\content\394360\2559317737"
```

The CLI prints how many values were applied. Type mismatches, unreadable statements and bad array shapes (S-COOLDOWN and S-DMG-PIERCE index into arrays) are reported; Lua control flow such as the loop in `00_graphics.lua` is skipped silently.

## How the game stores defines

hoi4.exe 1.19.3 converts most numeric defines to **int64 fixed point with scale 100000** (`round(float32(value) × 100000)`), a few to binary fixed point (2^15), and integer defines (activation times, `COMBAT_MIN_DURATION`) to int32. Combat arithmetic then truncates every product and quotient to 5 decimals. The simulator uses doubles; the difference only shows for very small values. Details: [findings/README.md](../reverse-engineering/findings/README.md).

## Version history of the built-in values

| Version | Source |
|---|---|
| 1.19.3 (current) | The installed game's `common/defines/00_defines.lua` and `common/modifiers/00_static_modifiers.txt` |
| 1.17 / 1.16.9 (milestone 1) | Mod copies of `00_defines.lua` cross-checked across several repositories |

1.17 → 1.19.3, combat-relevant changes:
- `COMBAT_MIN_HIT_CHANCE` 0.05 → **0.005**; heavy `GUN_HIT_PROFILES` 80 → **70**.
- `REPAIR_AND_RETURN_PRIO_{LOW,MEDIUM,HIGH}_COMBAT` 0.6/0.3/0.1 → **0.9/0.6/0.4**; new `CAPITAL_SHIP_COMBAT_RETREAT_MULT` (0.5).
- `COMBAT_RETREAT_DECISION_CHANCE` 0.22 → 0.2.
- Escape speeds: `BASE_ESCAPE_SPEED` 0.05, `SPEED_TO_ESCAPE_SPEED` 0.5, `ESCAPE_SPEED_PER_COMBAT_DAY` 0.015, `MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION` 0.5, `ESCAPE_SPEED_SUB_BASE` 0.1, `ESCAPE_SPEED_HIDDEN_SUB` 0.35.
- Submarine reveal reworked: `SUBMARINE_BASE_STEALTH_VALUE`, `SUBMARINE_REVEAL_DETECTION_MULTIPLIER`, `SUBMARINE_REVEAL_TORPEDO_FIRING_DETECTION_MULTIPLIER`; `SUBMARINE_HIDE_TIMEOUT` 20 → 8, `SUBMARINE_REVEALED_TIMEOUT` 16 → 12. The old reveal chance/pow defines are no longer read.
- Air: `NAVAL_STRIKE_CARRIER_MULTIPLIER` 10 → 12, `CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER` 0.35 → 0.5, `ANTI_AIR_ATTACK_TO_DAMAGE_REDUCTION_FACTOR` 1 → 2.5.
- Removed keys: `CARRIER_STACK_PENALTY*`, `NAVAL_COMBAT_AIR_*_TARGET_SCORE` (replaced by `*_TARGET_BASE`/`*_TARGET_SCALE`).

Defines that exist but are not read by naval combat in 1.19.3: `COMBAT_EVASION_TO_HIT_CHANCE`, `REPAIR_AND_RETURN_UNIT_DYING_STR`, `XP_GAIN_FACTOR` (for ship combat XP), `COMBAT_CHASE_RESIGNATION_HOURS` (stored, never used in the tick), `AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING` (tooltip only), `CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT` (written to a cooldown nothing uses).

## The TFB mod

The previous simulator's constants matched the TFB mod ("Their Finest Bruh", workshop item 2559317737), and the ship data in `data/` was entered from TFB. With the mod installed, load its folder after the game (see above). That applies `TFB_defines.lua` and TFB's own `00_static_modifiers.txt`.

`data/defines/tfb-legacy.lua` is a snapshot of an older TFB version (for example cooldowns `{3, 5, 2}`, where current TFB has `{3, 6, 2}`). It remains only for machines without the mod.

## Values that are not defines

`HardcodedValues` holds the decoded constants of the target weights (S-TARGET-WEIGHT), the detection divisor (S-POS-DETECT), the floors in screening, hit profile and submarine reveal, and the [ASSUMED] night hours. Spec items cite each one.
