# S-TERRAIN: naval terrain in a battle

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-TERRAIN (new), S-STATS-COMBAT, S-HIT-SHIP, S-POS, S-SUB, S-RETREAT-SPEED
- **Confidence:** read from code only. High for which terrain a battle gets, the per-sub-unit lookup and its fallback, the
  merge of the terrain's own block into every sub-unit block, and the list of combat readers. Medium for "no other
  combat reader" (a scan of the combat code for direct reads of `combat+0x48`, see Unresolved).

Builds on: the attack/armor getter 0xc34a50 ([S-STATS-COMBAT-GETTER](S-STATS-design-stats.md#s-stats-combat-getter-the-attack-and-armor-getter-0xc34a50-fully-decoded)),
combat speed 0xc3a740 ([S-ADMIRAL §6](S-ADMIRAL-leader-combat.md#6-combat-speed)), the ship modifier sum 0xc38760 and
`Mod()` 0x15c70b0 ([S-STATS-COUNTRY](S-STATS-COUNTRY-modifier-sources.md#the-modifier-sums)).

## Naval terrain

### Which terrain a battle gets

```
// strategicregiontemplate.cpp post-load 0x15a6a10 (template = strategic region template, the object at region+0x30)
template+0x110 = `naval_terrain` string from map/strategicregions (empty when the key is absent)
template+0x130 = TerrainDatabase(0x22fd10).Find(name)         // 0x3bd130
if name not empty and not terrain+0x10:                        // terrain+0x10 = `naval_terrain = yes`
    error "invalid naval terrain type for …"                   // 0x15a6b29

// NavalTerrain(template) 0x15a5560
t = template+0x130
if t and t+0x10: return t                                      // a valid naval terrain
return Find("ocean")                                           // 0x15a55d7: cached once (static 0x333d4e0)
```

A strategic region without `naval_terrain` (TFB: Black Sea, region 30) fights on the terrain `ocean`. In vanilla and TFB
`ocean` carries only `naval_mine_hit_chance` and `minimum_seazone_dominance`, so it changes nothing in battle.

Every reader goes `province+0xc8` (strategic region) `+0x30` (its template) → 0x15a5560. The naval combat stores it once:
`CNavalCombat::SetProvince` 0x15c4ba0 sets `combat+0x38 = province` and `combat+0x48 = NavalTerrain(province.region)`.
The ship getters (0xc34a50, 0xc38760, 0xc3a740, 0xc37cd0) instead look it up from the task force's province
`tf+0x1f0`, and `Mod()` from its location argument; in a battle all are the battle's province.

### The terrain object

| Offset | Content |
|---|---|
| +0x10 | `naval_terrain = yes` |
| +0xa8 | the terrain's own modifier holder (its top-level modifier keys: `navy_visibility`, `positioning`, `naval_mine_hit_chance`, …); container at +0xb8 |
| +0x168 | the terrain's own `units = { attack defence movement }` adjuster (+0x10 / +0x18 / +0x20) |
| +0x198 / +0x1a4 | per-sub-unit blocks, 0x110 bytes each / count; block +0x20 sub-unit index, +0x28 modifier holder (container +0x38), +0xe8 `units` adjuster |
| +0x1b0 / +0x1b8 | hash map sub-unit index → block index (hash 0x45d9f3b) |

Post-load 0x143eb40 resolves each block's name (`battleship`, `submarine`, … the ship type's sub-unit ID) in the sub-unit
database (0xac4e60; "sub unit not found for terrain " otherwise) and fills the map. A second post-load pass 0x143e980
(called through a vtable, entries at 0x29448f8 / 0x29c0ee0) **merges the terrain's own block into every sub-unit block**:

```
for block in terrain+0x198:
    block.units  += terrain.units                              // 0x1014800: attack, defence, movement added
    block.mods   += terrain.mods × 1.0                         // 0x557a40 → 0x557940 per entry (scale 100000)
```

Lookups by sub-unit index fall back to the terrain's own block:

```
SubUnitModifiers(terrain, idx) 0x143d340 = block(idx)+0x28 if the map has idx, else terrain+0xa8
SubUnitAdjuster(terrain, idx)  0x143e4a0 = block(idx)+0xe8 if the map has idx, else terrain+0x168
```

So every ship gets the terrain's top-level keys, plus its own sub-unit's block if the terrain has one, and never twice.

### What it changes in a battle

1. **Attack and armor** (0xc34a50, the `if province` branch after `naval_damage_factor`): `Σ += SubUnitAdjuster(terrain, ship+0x88).attack` for light, heavy,
   torpedo and submarine attack (kinds 0–3), `.defence` for armor (kind 4), inside `max(0, base × (1 + Σ))`. No admiral
   is needed, so the target's armor gets it too. TFB fjords: battleships, battlecruisers, heavy cruisers, carriers (and
   TFB's `sh_battleship`, `sh_carrier`) −0.2 attack and armor; deep ocean: destroyers −0.2, light cruisers −0.1.
2. **Combat speed** (0xc3a740, also 0xc37cd0): `v += SubUnitAdjuster(terrain, ship+0x88).movement` next to
   `naval_speed_factor`, `speed = base × max(0.1, 1 + v)`. This feeds the hit profile (S-HIT-SHIP) and, through the
   escape speed 0x196fe80 (which calls 0xc3a740), the retreat. TFB fjords: capitals −0.2; deep ocean: destroyers −0.2,
   light cruisers −0.1, submarines −0.25.
3. **Ship modifier sum** (0xc38760, 0xc388a4..0xc388e0): `+ SubUnitModifiers(terrain, ship+0x88).Get(id)` for every id.
   Of the terrain keys only `navy_visibility` (0x19f) has a ship reader in combat: the visibility getters 0xc3ab80
   (surface, hit profile) and 0xc3aa20 (submarine, reveal and hit profile), `visibility × (1 + Mod(0x19f))`. TFB fjords
   −0.2 for every ship; shallow sea submarines +1.0 (vanilla +0.33); deep ocean submarines −0.15.
   `navy_fuel_consumption_factor` (fjords, per sub-unit) is fuel, not simulated.
4. **Side modifiers** (`Mod()` 0x15c70b0, 0x15c739b..0x15c73c8): `+ terrain+0xb8 .Get(id)`, the terrain's top-level keys
   only (no sub-unit). Read for `positioning`, `screening_efficiency`, `screening_without_screens`,
   `naval_enemy_positioning_in_initial_attack` and convoy members. TFB fjords `positioning` −0.15, shallow sea −0.05.
5. **Admiral adjusters** (0xc34a50 via 0xc19c40): the commanding admiral's trait blocks named after the terrain
   (`water_fjords = { attack defence }` …) for attacks; already in S-ADMIRAL-ADJUST, they key on the same terrain object,
   so with `ocean` (no `naval_terrain`) no terrain trait applies.

The fallbacks 0xd6b1b0 / 0xd6b590 (attack / defence) and 0xd6eee0 (movement) are used only when `ship+0x88 < 0`. They
average 0x143e4a0's adjuster over the task force's ships (`tf+0x348`, count `tf+0x354`; 0 for an empty task force,
quotient in fx5). A ship always has a sub-unit index, so they do not matter for the simulator.

Not read in battle: `naval_mine_hit_chance` (mines), `minimum_seazone_dominance` (region control), `movement_cost`.
No reader of the naval terrain was found in the air code (carrier traffic, naval strike, A-REGION) or in the retreat
roll; the other callers of 0x15a5560 / 0x143d340 / 0x143e4a0 are UI (map modes 0xe05340, 0xe0d390, leader trait views
0x17e88a0, 0x178d300, 0x1bf27c0) and map graphics (0xf3a680).

## How it was verified

Static reading only. In-game check: a battle in a `water_fjords` region should show the capital ships' attack, armor and
speed at 80 % of a deep-ocean battle's, and the side's positioning 0.15 lower.

## Unresolved

- Readers of `combat+0x48` outside the ship getters: a scan of the combat code (0x15c0000–0x15d0000,
  0x161a000–0x1625000, 0x196c000–0x1982000) found no direct field reads of the terrain; the battle screen presumably shows
  it. Medium.
- 0xc37cd0 (a second speed getter with the same terrain term; callers 0xd6d030, 0xd6f0e0, 0xd76260): which speed it is.
