# JSON Schemas for Rider

Schemas are generated from the current data layout in `NavySimulator/Data/`.

## Suggested Rider mappings

Map each schema to these patterns:

- `battle-scenario.schema.json` -> `NavySimulator/Data/battle-scenario.json`
- `force-compositions.schema.json` -> `NavySimulator/Data/force-compositions.json`
- `hulls.schema.json` -> `NavySimulator/Data/hulls/*.json`
- `modules.schema.json` -> `NavySimulator/Data/modules/*.json`
- `mios.schema.json` -> `NavySimulator/Data/mios/*.json`
- `ship-designs.schema.json` -> `NavySimulator/Data/ship-designs/*.json`
- `researches.schema.json` -> `NavySimulator/Data/researches/*.json`
- `spirits.schema.json` -> `NavySimulator/Data/spirits/*.json`

## Notes

- `common.schema.json` holds shared definitions (roles + stat blocks).
- Stat blocks allow additional numeric properties to support imported/experimental keys present in current JSONs.
- `mios`, `researches`, and `spirits` currently accept both `statMultiplier` and `statMultipliers` because current files use both styles.

