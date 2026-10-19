# S-MIO-MATCH: which MIO bonuses reach a design

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5` (re-hashed from the analysed install)
- **Spec items affected:** S-MIO-MATCH, S-MIO-POLICY, S-STATS-MIO (and the equipment bonus key match of S-STATS-BONUS / S-DOCTRINE-EFFECTS)
- **Confidence:** read from code; the match rule, the variant's type set, the initial-trait limit and the assignment check are also confirmed against a TFB 1936 save (173 of 173 ship variants' saved MIO bonus and design team agree, see "How it was verified"). Refresh timing is read from code plus the game's own localisation.

Method: static analysis with `tools/re/` (`hoi4re.py`, RTTI names from the vtables' complete object locators, script token ids rebuilt
from the registration function 0x831c0), then a check of the rule against the plain-text saves in
`Documents/Paradox Interactive/Hearts of Iron IV/save games/` and the TFB script files. Background: [S-STATS-MIO](S-STATS-design-stats.md#s-stats-mio-mio-bonuses).

## Function map (RVAs)

| RVA | What |
|---|---|
| 0xbd3b80 | Refresh a variant's MIO bonus: `variant+0x498 = 0xdb7a10(mio, variant+0x3f0)`, `variant+0x4b0 = mio+0x148` (trait count), then RecalculateStats 0xbe0260 |
| 0xdb7800 (0xdb7a10 mode 1, 0xdb7a30 mode 2) | MIO bonus sum `(mio, out, equipmentType, mode)`: unlocked traits, initial trait, attached policy |
| 0xa4fe60 | Trait limit check `(trait, equipmentType)`: empty `limit_to_equipment_type` → true, else the list's Matches (vtable +0x48) |
| 0xa0ada0 | Equipment group "is empty": `group+0x5c == 0` (leaf, no unwind entry) |
| 0xa0a600 | `CAnonymousEquipmentGroup` vtable +0x48 Matches(type): token cache `+0x70` when `+0x88` is set (0xa0a430), else expand (0xa0b1b0) and 0xa0a550 |
| 0xa0a950 | `CEquipmentGroup` (named group) vtable +0x48 Matches(type): expand (0xa09290) and 0xa0a550 |
| 0xa0a430 | Type vs a token list: the type's, its archetype's or the archetype's family's name token is listed, or a listed token's type keyword bit is in the type's mask |
| 0xa0a550 | The same against a string list (0x4562a0 "contains", 0xf882f0 string → keyword bit) |
| 0xa0b1b0 | Expand a group's entries (`+0x50`, count `+0x5c`, 0x28 bytes each): an entry naming a group of the equipment group database is replaced by that group's expansion (recursive; with the flag the group's own name is kept too), other entries are appended once |
| 0xa09290 / 0xa08fe0 | Expansion without / with group names |
| 0xa09e80 | Build the token cache `+0x70` (sets `+0x88`) |
| 0xa0ae40 | Group cache rebuild on database reload (listener sub-object at group `+0x68`, vtable 0x27196c0 slot +0x10): `+0x90`/`+0xa8`, `+0x70`/`+0x88`, `+0xb0`/`+0xc8` |
| 0x4562a0 | String list contains (compares the cached hash at `+0x20`, then the characters; case-sensitive) |
| 0xc95730 | Equipment type's keyword mask: `(type+0x540, or the archetype's +0x540 when 0) \| type+0x548`, normalized by 0xf8b020 |
| 0xf8b020 | Mask normalization (only if any of 0x80004003c1 = convoy, capital_ship, submarine, screen_ship, floating_harbor, carrier, support_ship is set): capital_ship clears convoy, submarine and screen_ship; else submarine clears convoy and screen_ship; else screen_ship clears convoy |
| 0xf88330 / 0xf882f0 / 0xf8b070 | Keyword token → bit (table below) / string → token (0x22f060, 0x4c17 = `undefined`) → bit / parser variant (logs "incorrect type category") |
| 0xc949d0 | Equipment database post-load per type: `type+0x548 = OR of add_equipment_type (module+0x78) of the type's own default_modules` (looked up with 0x9f8460); also sums `+0x3cc` along the parent chain (not decoded) |
| 0xc99410 | Equipment type parser: `parent` (0x87) → `+0x4e0`; `archetype` (0x2f47) → `+0x4d8` (only an `is_archetype` type, then 0xc96610 copies its stats); `is_archetype` (0x2f48) → `+0x555`, `+0x4d8 = self`; `family` (0x2f8b) → `+0x4e8`; `type` (0xe1) → `+0x540 \|= bit`; `default_modules` (0x3b6f) → `+0x88` pairs `{slot token, module token}` ("Duplicate default module assignment") |
| 0xc922a0 / 0xc924b0 | Equipment type constructors: `+0x10 = 1`, `+0x4e0 = +0x4e8 = self` (`+0x4d8 = self` / 0x9f8570) |
| 0x9f86f0 / 0x9f8700 → 0x9e93e0 | Equipment lookup by name; a miss returns element 0 of the database (a reserved null entry: "IndexIterator->second > 0") |
| 0x9f8460 | Module lookup by token in the database's module list (`+0xe0`, count `+0xec`), miss → element 0 |
| 0x152f4a0 | Module parser: `add_equipment_type` (0x3b91) → `module+0x78` (0xf8ad40) |
| 0xbd3a50 / 0xbe1960 | The variant's own role mask `variant+0x408` (hull type, installed modules' `add_equipment_type` at module `+0x78`, removals at `+0x88`), then 0xbe2450. Not used by the MIO match (partly decoded) |
| 0xbe2450 | Validate a variant's design team: keeps it if 0xdb6fd0 passes, else clears the handle, the bonus list and `+0x4b0` and recalculates |
| 0xdb6fd0 → 0xdb6f40 | Can this MIO be on a variant of this equipment type: the MIO's `equipment_type` matches (0xa53260), or the attached policy has a bonus key that matches (0xa4bac0) |
| 0xa53260 | `templatePart+0x48` (= template `+0xd0`, the MIO's `equipment_type` group) Matches(type) |
| 0xa53660 | Trait by token in `templatePart+0x30` (= template `+0xb8`, 0x3d8 bytes each, count `+0x3c`) |
| 0xa536f0 / 0xa53850 | With `B = mio+0x118` (= template `+0x218`): the MIO's `available` trigger (`B+0x58`, inherited `B+0x358`) / `visible` trigger (`B+0xb0`, inherited `B+0x360`) |
| 0x13ae4b0 | Trigger evaluate `(trigger, scope, tooltip)`: an empty trigger (`+0x14 == 0`) is true |
| 0xa53be0 | MIO template parser: `equipment_type` (0x3f59) → template `+0xd0` (a `CAnonymousEquipmentGroup`, caches built at parse), `initial_trait` (0x3fe6) → `+0xb0`, `trait`/`add_trait` (0x2e8e/0x2e6b) → `+0xb8`, `allowed` +0x218, `available` +0x270, `visible` +0x2c8 |
| 0xa50000 | Trait parser: `limit_to_equipment_type` (0x2d12) → `trait+0x60` (a `CAnonymousEquipmentGroup`); `equipment_bonus` (0x3167, checked by 0x641610) and `production_bonus` (0x3f5a, 0x641660) both into `trait+0x130`; `available` +0x210, `visible` +0x268, `on_complete` +0x2c0 |
| 0xa519f0 / 0xa51690 | Trait constructor / constructor of its `+0x60` group (vtable 0x2719668, listener vtable 0x27196c0 at `+0x68`) |
| 0xa507b0 | Include merge of a trait (decoded in [S-MIO-INCLUDE](S-MIO-include-and-tree.md#trait-merge)): an empty limit list is taken whole from the included trait (0xa4d310), the equipment and the production bonus each per kind |
| 0xa4b260 | Policy bonus `(policy, out, mio, type)`: every entry of the policy's bonus map whose key matches (0xa4a7f0) is added with mode 3 (all stats) |
| 0xa4a7f0 | Policy key match `(mio, key, type)`: 0xc97e00 (the equipment bonus key match), or `key == "same_as_mio"` and 0xdb6e90 |
| 0xdb6e90 | `same_as_mio`: the MIO's `equipment_type` group Matches(the type looked up by its name) |
| 0xa4bac0 | Any key of the policy's bonus map matches the type (used by the assignment check) |
| 0xa4be60 / 0xa48e70 | Policy parser: `equipment_bonus` and `production_bonus` both read into one key → stat-list map |
| 0xe5d0d0 | Policy by id in the equipment manager `[country+0xf68]+0x148` (count `+0x154`); the MIO's attached policy id is `mio+0x188` |
| 0xc97e00 | Equipment bonus key match (re-read): name lookup → the type, its archetype or the archetype's family; else type keyword bit `(mask & bit) == bit`; else equipment group database (0x631f10 → 0x22f740) group's Matches |
| 0x22f740 / 0x631f10 | Equipment group database lookup by token (robin-hood table `+0x48`, 0x38-byte slots, item at `+0x10`) / the database instance (data 0x332eed0, `CEquipmentGroupDatabase`) |
| 0x63f3c0 | Add a stat list into another: for each `{token, int64}` entry passing 0x640d20(token, mode), `out[token] += value` (append when new). Plain int64 add, no clamp, no rounding |
| 0x640d20 | Mode filter: 1 → equipment stat token (0x6412d0), 2 → production stat token (0x641590), 3 → any |
| 0x13e81b0 | Token → EEquipmentStats (S-STATS); unknown tokens go to the sink index 0x4e; `naval_weather_penalty_factor` (0x3fcd) → 0x2a |
| 0x1e1150 | Create-variant command: sets the design team (0xbe1c40), then either the command's own bonus snapshot (0xbe1d00) or 0xbd3b80 |
| 0xbe1d00 / 0xbe1c40 | Set a variant's bonus snapshot and trait count from a list / set its design team handle |
| 0x1491ec0, 0x14925d0, 0x1492970, 0x1492d50 | Equipment designer (its working variant at `+0x1370`): changing the design team recomputes with 0xbd3b80; switching back to the original MIO restores its saved snapshot (`+0x2218`, count `+0x2238`) unless the refresh flag `+0x2208` is set |
| 0x1f72870 (chunk 0x1f7291f) | Designer opening a variant: keeps a copy of its bonus (`+0xa98`) and trait count (`+0xab0`), assigns the MIO, recomputes |
| 0x10bae50 | AI design: copies a variant, recomputes its MIO bonus (0xbd3b80) |
| 0xdbc860 | MIO UnlockTrait `(mio, trait)`: insert into `mio+0x140`, trait points `mio+0x134` − 1, `on_complete` (0xa4eb10), 0xdbbeb0 (task capacity), notify. Does not touch variants |
| 0x2fbd80 | `complete_mio_trait` effect execute (`NIndustrialOrganisation::CUnlockTraitEffect` vtable +0x68) → 0xdbc860 |
| 0x456890 | `has_mio_equipment_type` evaluate (`NIndustrialOrganisation::CHasEquipmentTypeTrigger` vtable +0xb0): the name is literally in the MIO's `equipment_type` list expanded with group names (`+0xb0` cache) |
| 0xbe3c80 | Variant writer: `design_team` (`+0x488`), `design_team_bonus` (`+0x490`), `number_of_design_team_traits` (`+0x4b0`, written when not 0) |
| 0xdb3c80 | MIO instance init: `+0x110 = template+0x88`, `+0x118 = template+0x218`, `+0x120` owner tag, `+0x140` trait set |

Vtables (RTTI): 0x2719610 `CEquipmentGroup` (+0x48 = 0xa0a950), 0x2719668 `CAnonymousEquipmentGroup` (+0x48 = 0xa0a600; second vtable
0x27196c0 at offset 0x68), 0x2719790 `CEquipmentGroupDatabase`, 0x2745dc0 `NIndustrialOrganisation::CUnlockTraitEffect`, 0x27aa858
`NIndustrialOrganisation::CHasEquipmentTypeTrigger`.

### Struct fields

| Object | Offset | Meaning |
|---|---|---|
| equipment type | +0x08 / +0x10 / +0x18 | name token / valid flag / name string |
| equipment type | +0x88 (count +0x94) | `default_modules` pairs `{slot token, module token}` |
| equipment type | +0x4d8 / +0x4e0 / +0x4e8 | archetype / `parent` / `family` (family defaults to the type itself) |
| equipment type | +0x540 | own `type` keyword mask (0 → the archetype's is used) |
| equipment type | +0x548 | OR of the default modules' `add_equipment_type` |
| equipment type | +0x555 | `is_archetype` |
| module | +0x78 | `add_equipment_type` mask |
| equipment group | +0x50 (count +0x5c) | entries (strings, 0x28 bytes, hash at +0x20) |
| equipment group | +0x68 | reload listener sub-object |
| equipment group | +0x70 (+0x88) / +0x90 (+0xa8) / +0xb0 (+0xc8) | caches: expanded tokens / expanded names / expanded names with group names (valid flag) |
| MIO trait (0x3d8) | +0x08 / +0x10 | token / owning template |
| MIO trait | +0x60 | `limit_to_equipment_type` group (empty: count at trait +0xbc is 0) |
| MIO trait | +0x130 | `equipment_bonus` and `production_bonus` entries in one list |
| MIO template | +0xb0 / +0xb8 (count +0xc4) / +0xd0 | initial trait / traits / `equipment_type` group |
| MIO template | +0x218 / +0x270 / +0x2c8 | `allowed` / `available` / `visible` |
| MIO instance | +0x110 / +0x118 / +0x120 | template + 0x88 / template + 0x218 / owner tag |
| MIO instance | +0x134 | trait points |
| MIO instance | +0x140 / +0x148 | unlocked trait set (head, size); the size is the "number of traits" stored on variants |
| MIO instance | +0x188 | attached policy id |
| policy (status) | +0x08 / +0x38 | id / template; its bonus map (head at template-pointer `+0x40`, node key string `+0x20`, stat list `+0x48`) |
| variant | +0x3f0 | equipment type: a concrete type, never an archetype (assert in 0xbd70e0) |
| variant | +0x408 | own role mask (modules included); not read by the MIO match |
| variant | +0x488 / +0x490 (+0x498 list, count +0x4a4) / +0x4b0 | design team / bonus snapshot / trait count at snapshot time |

### Type keyword bits (0xf88330, every token that maps to a bit)

convoy 0, armor 2, motorized 3, mechanized 4, infantry 5, **capital_ship 6, submarine 7, screen_ship 8**, floating_harbor 9,
fighter 10, interceptor 11, tactical_bomber 12, strategic_bomber 13, cas 14, naval_bomber 15, missile 16, suicide 17,
scout_plane 18, railway_gun 19, air_transport 20, maritime_patrol_plane 21, **carrier 22**, support 24, flame 25, amphibious 26,
anti_air 27, artillery 28, anti_tank 29, rocket 30, train 31, heavy_fighter 32, emplacement_gun_ammo 33, ballistic_missile 34,
nuclear_missile 35, sam_missile 36, missile_launcher 37, land_cruiser 38, support_ship 39.

Script tokens used here: `limit_to_equipment_type` 0x2d12, `equipment_type` 0x3f59, `has_mio_equipment_type` 0x3f94,
`add_equipment_type` 0x3b91, `archetype` 0x2f47, `is_archetype` 0x2f48, `parent` 0x87, `family` 0x2f8b, `default_modules` 0x3b6f,
`initial_trait` 0x3fe6, `trait` 0x2e8e, `equipment_bonus` 0x3167, `production_bonus` 0x3f5a, `complete_mio_trait` 0x3f4d,
`none` 0x165, `undefined` 0x4c17.

## What the game does

### The match (one resolver for trait limits, the MIO's `equipment_type` and policy keys)

`limit_to_equipment_type` and the MIO's `equipment_type` are the same object, an anonymous equipment group (the class that
`common/equipment_groups` entries also use). Its entries are expanded once (cached, rebuilt on reload), then matched against one
equipment type:

```
expand(list):                                         // 0xa0b1b0
    for name in list:
        if name is a group of common/equipment_groups: expand(that group's equipment_type)   // recursive
        else: add name (once)

matches(list, T):                                     // 0xa0a600 → 0xa0a430 / 0xa0a550
    A = T.archetype                                   // +0x4d8
    names = { T, A, A.family }                        // A.family is A itself unless the archetype sets `family`
    mask  = normalize((T.own_type_bits or A.own_type_bits) | T.default_module_type_bits)    // 0xc95730
    for e in expand(list):
        if e names one of `names`: return true        // exact, case-sensitive
        if keyword_bit(e) and keyword_bit(e) & mask: return true
    return false

normalize(m):                                         // 0xf8b020
    if m has capital_ship: drop convoy, submarine, screen_ship
    elif m has submarine:  drop convoy, screen_ship
    elif m has screen_ship: drop convoy
```

Token kinds:

- **Concrete equipment** (`ship_hull_heavy_2`): matches only that type. The `parent` chain is not followed, so
  `ship_hull_heavy_1` does not match a `ship_hull_heavy_2` design.
- **Archetype** (`ship_hull_heavy`, `ship_hull_cruiser`): matches every type of that archetype.
- **Type keyword** (`capital_ship`, `screen_ship`, `carrier`, `submarine`, `convoy`, `armor`, …, table above): matched against
  the type's mask. This is what the equipment group documentation calls a "category".
- **Equipment group** (`mio_cat_eq_all_battleship = { ship_hull_heavy ship_hull_super_heavy }`): replaced by its members,
  recursively; each member then matches by the rules above (a group may list keywords).
- Anything else (a sub-unit name such as `heavy_cruiser`, an `interface_category_*`, a module, a typo) matches nothing.

**Empty list.** An empty `limit_to_equipment_type` passes for every type (0xa4fe60 returns true before calling Matches). "Empty"
means no words were written: the entry list `+0x50` is the script list as read (0xa08390 → 0xa0c0c0 copies it unfiltered) and
0xa0ada0 counts it, so a limit that names only unknown equipment is not empty and matches nothing. The trait check does **not**
also test the MIO's `equipment_type`; that is done once, when the design team is assigned (below).

### The type the bonus is computed for: the hull, not the design

0xbd3b80 passes `variant+0x3f0`, the variant's equipment type, i.e. the concrete hull (`ship_hull_cruiser_2`). The keyword mask is
a static property of that hull, built when the equipment database loads:

- the hull's own `type`, or its archetype's when the hull has none (TFB/vanilla hulls set it on the archetype only:
  `ship_hull_heavy` capital_ship, `ship_hull_cruiser` screen_ship, `ship_hull_light` screen_ship, `ship_hull_carrier` carrier,
  `ship_hull_submarine` submarine);
- OR the `add_equipment_type` of the modules in the hull's own `default_modules` (0xc949d0), whatever the design actually
  installs;
- normalized (capital_ship wins over screen_ship).

The modules a design installs do **not** count. Their `add_equipment_type` goes into the variant's own role mask
(`variant+0x408`, 0xbd3a50), which picks the sub-unit, but the MIO code never reads it. The ship type (sub-unit) is not read
either.

Consequences for TFB (computed from its equipment files): a heavy cruiser built on `ship_hull_cruiser_N` (medium battery, heavy
cruiser armor, sub-unit `heavy_cruiser`) is `screen_ship` for MIO purposes, not `capital_ship`. `ship_hull_torpedo_cruiser`,
`ship_hull_cruiser_panzerschiff` and `ship_hull_cruiser_coastal_defense_ship` list `ship_medium_battery_1` /
`ship_armor_cruiser_heavy_1` in their default modules, so they are `capital_ship` (plus `anti_air` for two of them) and **not**
`screen_ship`, whatever is installed. `ship_hull_carrier_submarine` is carrier and submarine. All other TFB ship hulls have just
their archetype's keyword. So the `capital_ship` trait of `generic_battle_line_ship_organization` reaches its battleship hulls and
the three cruiser hulls above, not regular CA designs; its `mio_cat_eq_all_cruiser` traits reach every cruiser hull.

### The bonus sum (0xdb7800, mode 1 for a variant)

```
out = {}
if mio.visible and mio.available (triggers in MIO scope; empty = true):     // 0xa53850, 0xa536f0, 0x13ae4b0
    for key in mio.unlocked_traits:                    // set mio+0x140
        trait = template trait with that token         // 0xa53660
        if trait and limit_ok(trait, T): add(out, trait.bonus, mode)
if limit_ok(template.initial_trait, T): add(out, initial_trait.bonus, mode)  // the initial trait's own limit applies
policy = country's policy with id mio+0x188            // 0xe5d0d0
if policy:
    for (key, stats) in policy.bonus_map:             // equipment_bonus and production_bonus, one map
        if matches_key(key, T) or (key == "same_as_mio" and matches(mio.equipment_type, T)):
            add(out, stats, all)                       // mode 3; every matching key adds, no break
variant.mio_bonus = out; variant.trait_count = size(mio.unlocked_traits)

add(out, stats, mode): for (token, value) in stats with token in mode's category: out[token] += value
```

`matches_key` (0xc97e00, also used for idea, `add_equipment_bonus` and doctrine `equipment_bonus` keys) gives the same result as
the list match: an equipment name equal to the type, its archetype or the archetype's family; else a type keyword whose bit is in
the normalized mask; else an equipment group whose expansion matches. The policy's `same_as_mio` is the MIO's own
`equipment_type` list matched against the design. Traits, the initial trait and the policy all go into one list, so the result is
applied exactly as S-STATS-MIO says: light/heavy/torpedo hit chance factors to the add bucket, every other stat to the shared
multiplier. Production stats in the list have no equipment stat and fall into the sink index (no effect on the design).

### Which MIOs a design can have

0xdb6f40: the MIO's `equipment_type` matches the hull (same resolver), or the attached policy has a bonus key matching it.
0xbe2450 runs this check whenever a variant's role or equipment type is set (0xbe1960, 0xbd70e0, and a chunk of 0xbdf910; this
covers variant creation, history and load); if it fails, the design team is removed and the bonus cleared. Whether setting the
team in the designer (0xbe1c40) re-checks was not traced, nor the roster UI's filters and its use of the MIO's
`allowed`/`visible`/`available` triggers. `has_mio_equipment_type = X` (policy `allowed`) is a different, literal test: X must be a
name in the MIO's `equipment_type` after group expansion (group names kept); no archetype or keyword resolution.

### When the bonus is computed: a snapshot per variant

The bonus is computed only when a variant is created (the create-variant command, the designer, the AI designer, history) or its
design team is changed, and stored with the variant (`design_team_bonus`, `number_of_design_team_traits`). Unlocking a trait
(0xdbc860, also the `complete_mio_trait` effect) does not touch existing variants: its callees are the set insert, the trait's
`on_complete` effect, a modifier update (0xdbbc50), the task capacity update (0xdbbeb0) and a history entry (0xdb2810), and none
of the callers of 0xbd3b80 or 0xbe1d00 is on that path. The game's own text agrees: "Equipment that was made with an older
version of a Military Industrial Organization needs to be upgraded in order to have the latest Design Team traits applied", "This
will create a new variant", "Upgrading will add to new variant:" (INDUSTRIAL_ORG_TOOLTIP_UPGRADE_*). So a design has the traits unlocked when it (or the version it was upgraded to) was
made. The same holds for the policy part of the sum.

### Arithmetic

No clamp and no rounding in the sum: 0x63f3c0 adds the stored int64 fixed-point values (scale 1e5). The only rounding is when the
script decimals are parsed (not decoded here) and later in the variant formula (S-STATS-DESIGN).

## Defines and constants involved

| Name | Role |
|---|---|
| none | no define is read by the MIO match or sum |
| hard-coded mask 0x80004003c1 | the naval/role bits (convoy, capital_ship, submarine, screen_ship, floating_harbor, carrier, support_ship) that trigger normalization |
| hard-coded normalization order | capital_ship > submarine > screen_ship > convoy (0xf8b020) |
| mode 3 | the policy part is added for every stat token (0xa4b260 → 0x63f3c0) |

## How it was verified

- Static reading of the chain 0xbd3b80 → 0xdb7800 → 0xa4fe60 → vtable 0x2719668 +0x48 = 0xa0a600 → 0xa0a430/0xa0a550 → 0xc95730,
  and of the parsers (0xa50000, 0xa53be0, 0xc99410, 0x152f4a0, 0xa4be60). The keyword bit table was produced by walking 0xf88330's
  compare tree for every registered token.
- TFB 1936 saves (`autosave.hoi4` and `light_attack_vs_torp_cl3.hoi4`, same result): 173 ship variants have a design team. For
  all 173 the saved `design_team_bonus` equals the initial trait's `equipment_bonus` when the trait's limit matches the hull under the rule above, and
  nothing otherwise (no traits are unlocked at that date). Discriminating cases: `USA_newport_news_shipbuilding_organization`
  (initial trait limited to `carrier`, bonus inherited from `generic_task_force_ship_organization`) gives `max_strength = 0.05` to
  its four carrier variants and nothing to its destroyer; the Black Sea shipyard trait limited to
  `{ capital_ship screen_ship ship_hull_submarine }` reaches its submarines through the archetype name.
- Assignments: the history files give 175 ship variants a design team. Two ENG destroyers (`V/W Class`, `S Class`,
  `ship_hull_light_1`) assigned to `ENG_cammell_laird_organization` (battleship and cruiser groups) have no design team in the save: the assignment check
  runs and drops uncovered assignments. The Australian heavy cruisers `Canberra Class` and `County Class` (`ship_hull_cruiser_1`
  with `ship_medium_battery_2` and `ship_armor_cruiser_heavy_2`, sub-unit `heavy_cruiser`) keep `AST_morts_dock_organization`,
  whose `equipment_type` is only `{ screen_ship }`. If installed modules counted, their normalized mask would be capital_ship only
  and the assignment would have been dropped. This supports the "hull mask only" reading.
- Not verified in game: a `capital_ship`-limited trait on a CA design after unlocking it, and the snapshot timing (would need a
  save after unlocking a trait, or a design-screen tooltip).

## Differences from the current spec and code

- `ShipDesign.EquipmentBonusTypes` (used by `MioRules.Matches`, `Reaches`, `CanAssign` and the policy targets) contains the ship type
  (sub-unit) ID, the hull, the archetype, the archetype's `type` and **every installed module's** `add_equipment_type`, plus the
  groups holding any of them. The game uses: hull and archetype names (plus the archetype's `family`), and the keywords of the
  hull's own/archetype `type` plus the hull's **default** modules' `add_equipment_type`, normalized. To match the game:
  drop the installed modules and the ship type ID, add the hull's `default_modules` types, and apply the normalization (so a
  hull with capital_ship is not also screen_ship). Group membership should expand the group's members and match each by the same
  rule (a group may list keywords). Matching is case-sensitive in the game (no practical effect).
  In TFB this changes which traits reach CA designs (screen_ship traits yes, capital_ship traits no) and the torpedo cruiser,
  panzerschiff and coastal defense hulls (capital_ship traits yes, screen_ship traits no).
- The initial trait's limit (`MioRules.Reaches` on the initial trait), the policy's `same_as_mio` and the one-list sum already
  match the game. The importer's field-wise `include` merge of the initial trait (limit from the including MIO, bonus from the
  included one) matches the save.
- Not modelled: unlocked traits count only while the MIO's `visible` and `available` triggers hold (the initial trait and the policy
  always count); a policy key naming the hull also permits the assignment; invalid assignments lose the design team and its bonus.
- Spec (`docs/mechanics/surface.md`) has no S-MIO-MATCH / S-MIO-POLICY items yet; the code cites them. They should state the rule
  above, the snapshot timing (a design has the traits unlocked when it was made; the simulator's side setup is that snapshot) and
  the [RE] tag with a link here.
- Side results for other findings: the S-DOCTRINE "archetype's `+0x4e8` link (unidentified)" is the `family` field, and the "third
  lookup 0x22f740 + virtual check" is the equipment group database, so group names work as idea, named and doctrine equipment bonus
  keys too. S-STATS stat 0x2a is `naval_weather_penalty_factor` (token 0x3fcd in 0x13e81b0). For
  [S-MIO-INCLUDE](S-MIO-include-and-tree.md): country MIO `+0x134` is the trait points (UnlockTrait 0xdbc860 asserts
  `_TraitPoints > 0` and decrements it); a `limit_to_equipment_type` naming only unknown equipment is not empty (above), so an
  include merge keeps it; and country MIO `+0x118` points at template `+0x218` (the `allowed` / `available` / `visible` part),
  `+0x110` at template `+0x88` (MIO instance init 0xdb3c80, 0xdb3df3..0xdb3e08), not at the template itself.

## Save format (S-MIO-SAVE)

Decoded on 2026-10-20 for `tools/ingame/make_save.py` (writing a scenario's MIOs into a battle save). Token ids from the
registration 0x831c0; checked by loading a written save (TFB DEV, 1936 template, BUL and ROM given USA and JAP MIOs).

```
countries={ TAG={ production={ industrial_organisations={        # list reader 0xe59400
    <MIO token>={                                                 # looked up in the MIO database: unknown key → error.log
                                                                  # "MIO X is in save but not in DB", block skipped;
                                                                  # `allowed` is NOT checked, so any country can hold any MIO
        id={ id=N type=79 }                                       # the handle variants point at
        name="" icon="..." research_bonus=… task_capacity=… funds=… size=… points=…
        unlocked={ trait="<trait token>" }                        # one block per unlocked trait (set +0x140); the initial trait is not listed
        queued_trait={ ... }                                      # same element type, the research queue (+0x150)
        allowed_policies={ ... }
        policy=<policy token>                                     # +0x188, written unless 0x4c17 (none); read as a token id
        cooldown=...                                              # only when set (+0x1a8)
        variables={ } flags={ } upgrades=yes history={ ... } research_assign_cost=… ...
    }
} } } }
equipments={ <hull>={ ... modules={ ... }
    design_team={ id=N type=79 }                                  # the variant's MIO (variant +0x488)
    design_team_bonus={ <stat>=<value> ... }                      # the saved bonus snapshot (S-MIO-TIMING); absent when empty
} }
```

| RVA | Role |
|---|---|
| 0xdbccc0 | country MIO writer: `name`, `icon`, `research_bonus`, `task_capacity`, `funds`, `size`, `points`, `unlocked`*, `queued_trait`*, `allowed_policies`, `policy`, `cooldown`, `variables`, `flags`, `upgrades`, `history`*, `research_assign_cost`, `production_assign_cost`, `design_team_change_cost`, `add_mio_funds_gain_factor` |
| 0xdbb350 | country MIO member reader (token switch; `unlocked` 0x38a4 → 0xdb38b0 set insert, `policy` 0x28fc → `+0x188`, `queued_trait` 0x4007) |
| 0xe59400 | country `industrial_organisations` list reader: database lookup by key, MIO instance created (0xdb3c80) and read |
| 0xa50690 / 0xa51170 | trait reference reader / writer: the block's only key is `trait` (0x2e8e), a string |
| 0xdbabe0 | country MIO post-read: logs "A trait in save file for: X does not exist in database." for an unlocked token the trait database lacks |

A bare `unlocked=<trait>` is a value where the reader expects a block: the read fails, and the parser skips the rest of
the enclosing country block (every later key of the country, its `units` included, logs "Unexpected token").

## Unresolved

- 0xbd3a50 in full (the variant role mask: base argument, the archetype `+0x408` term, module `+0x88` removals).
- How the MIO roster filters candidates with `allowed`/`visible`/`available`, and when the create-variant command carries its own
  snapshot (command `+0x60`) instead of recomputing.
- The production mode (0xdb7a30) consumers and whether production lines snapshot the same way.
- The policy template layout: the reader takes the bonus map at template-pointer `+0x40`, the parser writes `+0x50` (a 0x10 base
  shift between the two objects, not traced). Semantics are unaffected.
- 0x9f8570 (default archetype of the named constructor) and the `+0x3c8`/`+0x3cc` sum along the parent chain in 0xc949d0.
