# Combat mechanics spec

The goal of this project is to reproduce HOI4 naval combat as exactly as possible. This spec is the contract between three sources of truth and the code:

1. **Game files**: `common/defines/*.lua` and the unit/equipment scripts, parsed directly (`NavySimulator.GameData`).
2. **Reverse-engineering** of the executable: formulas and orderings that are not visible in script files ([../reverse-engineering/README.md](../reverse-engineering/README.md)).
3. **In-game calibration**: recorded battles the simulator must match statistically ([../calibration.md](../calibration.md)).

Secondary sources (the wiki, dev diaries, forum testing) fill gaps until one of the three above confirms or replaces them.

## Spec items

Every rule has an ID: `S-*` for surface ([surface.md](surface.md)) and `A-*` for air ([air.md](air.md)). The code cites the ID at the place it implements the rule, so `grep -rn "S-HIT" src/` finds the implementation, and the spec links back to the file.

Each item records:
- **Rule**: the formula or behavior, with define names rather than numbers.
- **Source**: one or more provenance tags (below).
- **Code**: where it is implemented.
- **Open**: what is still unknown, and which `BattleRules` option (if any) switches between candidate behaviors.

## Provenance tags

| Tag | Meaning | Trust |
|---|---|---|
| **[DEFINE]** | The value is a named define, read from `00_defines.lua` (or a mod override). The *value* is verified; how the game *uses* it may still be inferred. | High for the value |
| **[RE]** | Confirmed by reverse-engineering the executable. Cite the finding file in `docs/reverse-engineering/findings/`. | Highest |
| **[CAL]** | Confirmed by calibration against recorded in-game battles. Cite the calibration case. | High |
| **[WIKI]** | Stated by the HOI4 wiki. Some wiki formulas quote decoded 1.7.1 code (`combatnaval.cpp`); the wiki also mixes values from different game versions. | Medium |
| **[PDX]** | Stated by Paradox: dev diary, patch notes, or the comment next to a define. | Medium |
| **[COMMUNITY]** | Forum or Steam testing, mod comments. | Low |
| **[LEGACY]** | Carried over from the previous version of this simulator; source unknown. | Low |
| **[ASSUMED]** | Chosen here to fill a gap. Must become [RE] or [CAL] eventually. | None |

The goal is to replace every [WIKI], [COMMUNITY], [LEGACY] and [ASSUMED] tag on a rule that matters for outcomes with [RE] or [CAL].

## Variants instead of guesses

When sources disagree, or none exist, the engine implements the candidates side by side and exposes them as `BattleRules` options (`src/NavySimulator.Engine/Battles/BattleRules.cs`), each pointing at its spec item. Calibration then picks the variant that matches the game, and the losing variants are deleted. Examples: `FiringOrder` (S-ORDER), `DamageRandomness` (S-DMG-RANDOM).

## Game version

Built-in defaults are vanilla **1.17** ([defines.md](defines.md)). The game is at 1.18/1.19 by now, and some values have changed since (for example `NAVAL_STRIKE_CARRIER_MULTIPLIER`). Exact results need your own install's defines: `--defines "<HOI4 install dir>"`, or a mod directory.
