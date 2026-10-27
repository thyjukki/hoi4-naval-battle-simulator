# Override sets

An override set tries a mod change without editing the game's or the mod's files: it changes defines, static modifier keys and imported content (hulls, modules, researches, doctrines, spirits, MIO traits and policies, admiral traits, ...) for the battles that use it. The data in `data/` and the define sources (Simulate as) stay as they are.

## Where they apply

The set is applied when a scenario is built, after everything else:

1. Built-in vanilla defines, the scenario's define sources, the GUI's (Simulate as) or the CLI's `--defines`, as in [mechanics/defines.md](mechanics/defines.md#loading-order).
2. The set's `defines` and `staticModifiers`.
3. The set's `content` changes, on a copy of the content read from `data/`.

- **GUI**: one active set at a time (Overrides page, or none). Every battle, batch and stat preview uses it, unsaved changes included; the top bar shows it. The Batch page can run the same seeds with and without it and lists what changes ([gui.md](gui.md#batch)). Editors (designs, fleets, scenarios) still write the files as they are; the set is never written into them.
- **CLI**: `NavySimulator --overrides <set>`, a set file or a name in `<data>/overrides/`.
- The define chain in reports and on the Battle and Batch pages ends with `override set '<name>'`.

## File

One set per file in `data/overrides/` (schema `data/schemas/override-set.schema.json`):

```json
{
  "$schema": "../schemas/override-set.schema.json",
  "overrideSet": {
    "id": "cl-buff",
    "name": "CL buff",
    "description": "Light guns hit harder, screens a little less useful.",
    "defines": {
      "NNavy.COMBAT_BASE_HIT_CHANCE": 0.12,
      "NNavy.BASE_GUN_COOLDOWNS": [3, 5, 2]
    },
    "staticModifiers": {
      "screening_bonus": { "naval_hit_chance": 0.3, "convoy_retreat_speed": null }
    },
    "content": [
      { "collection": "modules", "id": "ship_light_battery_3", "path": "statModifiers.lightAttack", "value": 4 },
      { "collection": "doctrines", "id": "naval", "path": "subdoctrines[line_of_battle].rewards[flagships].mastery", "value": 150 },
      { "collection": "spirits", "id": "trade_interdiction_academy_spirit", "path": "effects.countryModifiers.naval_hit_chance", "value": 0.05 },
      { "collection": "leader-traits", "id": "spotter", "path": "modifiers.spotting_chance", "value": 0.2 }
    ]
  }
}
```

- `defines`: `Category.KEY` as the game names it (`NNavy`, `NAir`, `NMilitary`, `NDoctrines`, `NGame`, `NBuildings`, `NGraphics`), with a number, boolean, string or list of numbers. Only defines the simulator reads can be set; others are warned about.
- `staticModifiers`: by block, then modifier. A number sets the key in the block the define sources gave (other keys of the block stay); `null` removes it. The blocks naval combat reads are listed in [mechanics/defines.md](mechanics/defines.md); a key in another block is applied but warned about, since combat never reads it.
- `content`: `collection` is the data folder of the item (`hulls`, `modules`, `ship-types`, `critical-parts`, `researches`, `doctrines`, `spirits`, `mios`, `mio-policies`, `leader-traits`, `leader-skills`, `planes`, `aces`, `naval-terrains`), `id` the item's ID (for `doctrines` the folder, `naval` or `air`), `path` the field inside the item as in its JSON file: property names separated by dots, and `[id]` after a list picking the entry with that ID (`traits[x]` of an MIO, `subdoctrines[x].rewards[y]` of a doctrine folder). A path into a dictionary may name a new key (a modifier the item did not have); `null` removes a key. Designs, fleets and scenarios are not overridden: they are edited directly. Admirals are copied into each scenario side, so change their traits through `leader-traits`.

Changes apply in order. A change that cannot apply is skipped with a warning (in the CLI output, the scenario check and the Overrides page): an unknown collection, item, list entry or define, a field the item type does not have, a value of the wrong type (text for a number), or a change of an item's ID.

## Code

| Piece | Where | Role |
|---|---|---|
| `OverrideSetDto`, `ContentOverrideDto` | `GameData/Contracts/` | The file's content. |
| `OverrideApplier` | `GameData/Overrides/` | Applies a set to a catalog copy (`ContentCatalog.Copy`) and to `GameDefines`; `SetupLoader.BuildScenario(..., overrides)` calls it. |
| `ContentPath`, `ContentFields`, `OverrideCollections`, `OverrideValues` | `GameData/Overrides/` | Paths inside an item's JSON, the fields of an item the editor lists, the collections a set can change, value parsing and comparison. |
| `OverrideSetStore` | `GameData/Overrides/` | Reads, writes and resolves set files. |
| `OverrideSets`, `OverrideInfo`, `BatchComparison` | `Web/Services/` | The GUI's sets in memory, the editor's rows, the batch comparison. |
| `Workspace.Content` | `Web/Services/` | The catalog with the active set, which previews and the scenario editor read; `Workspace.Catalog` stays as on disk. |
