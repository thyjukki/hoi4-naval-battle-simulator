# JSON Schemas for Rider

Schemas are generated from the current data layout in `data/`.

## Suggested Rider mappings

Map each schema to these patterns:

- `battle-scenario.schema.json` -> `data/battle-scenario.json`
- `force-compositions.schema.json` -> `data/force-compositions/*.json`
- `hulls.schema.json` -> `data/hulls/*.json`
- `modules.schema.json` -> `data/modules/*.json`
- `ship-types.schema.json` -> `data/ship-types/*.json`
- `critical-parts.schema.json` -> `data/critical-parts/*.json`
- `calibration.schema.json` -> `data/calibration/*.json`
- `mios.schema.json` -> `data/mios/*.json`
- `mio-policies.schema.json` -> `data/mio-policies/*.json`
- `equipment-groups.schema.json` -> `data/equipment-groups/*.json`
- `countries.schema.json` -> `data/countries/*.json`
- `admirals.schema.json` -> `data/admirals/*.json`
- `leader-traits.schema.json` -> `data/leader-traits/*.json`
- `leader-skills.schema.json` -> `data/leader-skills/*.json`
- `ship-designs.schema.json` -> `data/ship-designs/*.json`
- `researches.schema.json` -> `data/researches/*.json`
- `spirits.schema.json` -> `data/spirits/*.json`
- `doctrines.schema.json` -> `data/doctrines/*.json`
- `planes.schema.json` -> `data/planes/*.json`
- `sea-regions.schema.json` -> `data/sea-regions/*.json`
- `naval-terrains.schema.json` -> `data/naval-terrains/*.json`
- `nations.schema.json` -> `data/nations/*.json`
- `override-set.schema.json` -> `data/overrides/*.json`

## Notes

- `common.schema.json` holds shared definitions (roles + stat blocks).
- `hulls/`, `modules/`, `ship-types/` and `critical-parts/` are written by `NavySimulator import-ship-data`, `doctrines/` and `spirits/` by `NavySimulator import-doctrines`, `mios/`, `mio-policies/`, `equipment-groups/` and `countries/` by `NavySimulator import-mios`, `admirals/`, `leader-traits/` and `leader-skills/` by `NavySimulator import-leaders`, with the game's IDs; designs, researches and scenarios reference those IDs.
- Stat blocks allow additional numeric properties to support imported/experimental keys present in current JSONs.
- `researches` currently accept both `statMultiplier` and `statMultipliers` because current files use both styles. MIO bonuses use the game's stat keys (`armor_value`, `lg_attack`, ...).

