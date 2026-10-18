# JSON Schemas for Rider

Schemas are generated from the current data layout in `data/`.

## Suggested Rider mappings

Map each schema to these patterns:

- `battle-scenario.schema.json` -> `data/battle-scenario.json`
- `force-compositions.schema.json` -> `data/force-compositions.json`
- `hulls.schema.json` -> `data/hulls/*.json`
- `modules.schema.json` -> `data/modules/*.json`
- `mios.schema.json` -> `data/mios/*.json`
- `ship-designs.schema.json` -> `data/ship-designs/*.json`
- `researches.schema.json` -> `data/researches/*.json`
- `spirits.schema.json` -> `data/spirits/*.json`

## Notes

- `common.schema.json` holds shared definitions (roles + stat blocks).
- Stat blocks allow additional numeric properties to support imported/experimental keys present in current JSONs.
- `mios`, `researches`, and `spirits` currently accept both `statMultiplier` and `statMultipliers` because current files use both styles.

