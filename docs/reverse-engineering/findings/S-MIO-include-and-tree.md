# S-MIO-INCLUDE: how an MIO's trait tree is built with `include`, and the tree's unlock rules

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** proposed S-MIO-INCLUDE (which traits, fields and bonuses an MIO ends up with) and S-MIO-TREE
  (positions, parents, mutual exclusion); feeds S-STATS-MIO (the bonus sum itself, and how `limit_to_equipment_type`
  and policies match equipment, are in `S-MIO-equipment-matching.md`, not here)
- **Confidence:** read from code only (no in-game test yet). Per answer below.

Method: `tools/re` plus throwaway scripts (not in the repo). The reader was found from the source asserts
`production\industrial_organisations\industrial_org_template.cpp` and `industrial_org_trait.cpp` (load-time error
strings such as `, override_trait with trait token ... did not match any trait in included MIO.`) and
`industrial_organisation.cpp` (the per-country MIO). Script keys are token ids, rebuilt from the static token
registration 0x831c0 as in [S-DOCTRINE](S-DOCTRINE-doctrines.md); the readers are `switch (tokenId)` functions
(persistent vtable +0x20). Supporting sources: the game's `common/military_industrial_organization/organizations/
_documentation.md`, the vanilla organization files and the TFB dev checkout's (same results; no MIO in either includes
an MIO that itself includes one).

| Key | Token id | Key | Token id |
|---|---|---|---|
| `name` | 0x1b | `trait` | 0x2e8e |
| `position` | 0x4c | `add_trait` | 0x2e6b |
| `parent` | 0x87 | `remove_trait` | 0x2e6c |
| `icon` | 0xb5 | `override_trait` | 0x4086 |
| `background` | 0x9c | `delete_included_values` | 0x3ff2 |
| `on_complete` | 0x275a | `initial_trait` | 0x3fe6 |
| `include` | 0x27fc | `tree_header_text` | 0x3fe9 |
| `ai_will_do` | 0x2a43 | `special_trait_background` | 0x3ffb |
| `limit_to_equipment_type` | 0x2d12 | `any_parent` / `all_parents` | 0x4140 / 0x4141 |
| `visible` / `allowed` / `available` | 0x2d2a / 0x2fe7 / 0x2fe8 | `traits` / `num_parents_needed` | 0x2ff6 / 0x38b5 |
| `equipment_bonus` / `production_bonus` | 0x3167 / 0x3f5a | `token` | 0x4a47 |
| `organization_modifier` | 0x3f69 | `mutually_exclusive` | 0x33ba |
| `relative_position_id` | 0x36cd | `equipment_type` / `research_categories` | 0x3f59 / 0x4adc |
| `research_bonus` / `task_capacity` | 0x3161 / 0x3f5c | `xp_research_type` | 0x3c0d |

## Answers

1. **Plain `trait = { token = X ... }` in an including MIO.** `trait` and `add_trait` have one handler (0xa53be0:
   0x2e8e and 0x2e6b both jump to 0xa53dd3, which appends a new trait to the MIO's own trait list `+0xb8` and reads the
   block into it). When the included MIO has trait X too, the include resolver merges the included X **into** the own
   block (0xa54150 → 0xa549c6 search, 0xa549f6 merge 0xa507b0): every key the own block sets wins, every key it leaves
   empty is taken from the included trait. So BEL/NOR's `trait = { token = generic_mio_trait_submarine_hunters
   limit_to_equipment_type = { screen_ship } }` keeps the included name, icon, position, parents and `equipment_bonus`
   and only narrows the limit. It is neither a duplicate nor a replacement; it behaves like `override_trait` (the
   differences are list order and the remove_trait interplay, item 6). When X is new (SWE_gotaverken's
   `SWE_mio_trait_high_capacity_dockyard`), no included trait matches it, so it stays exactly as written and is added
   to the tree. **High** (the whole path is in two functions; no in-game check).
2. **`add_trait` with a token the included MIO has:** identical to item 1 (same handler), so it is merged, not replaced.
   Vanilla and TFB never do this (0 of 557 / 546 `add_trait` blocks). **High.**
3. **`override_trait`:** stored in a separate list (`+0x1e8`). When the resolver reaches the included trait with that
   token, the override block is moved into the tree and the included trait is merged into it with the same merge as
   item 1 (0xa547b8–0xa548e7), then removed from the override list; an override that matches nothing stays there and is
   reported (0xa51f71), and it is not in the tree. Merge rules (0xa507b0), each "if the own value is empty and the key
   is not in the trait's own `delete_included_values`, take the included one":
   - Scalars and strings replace: `name`, `icon`, `special_trait_background` (only `yes` counts as set, so an own `no`
     cannot switch off an included `yes`; it needs `delete_included_values`), `position` (the x/y pair; unset is
     −1/−1), `relative_position_id` (separate key: overriding `position` alone keeps an included
     `relative_position_id`, so the new position becomes a delta from that trait).
   - Lists replace as a whole, never append: `limit_to_equipment_type`, `mutually_exclusive`, `organization_modifier`
     (the whole container), and all parent groups together: `parent`, `any_parent` and `all_parents` are stored as one
     list of groups (`+0x370`), keyed `parent` in the merge. An own `any_parent` therefore drops every included
     `parent`/`any_parent`/`all_parents` group.
   - `equipment_bonus` and `production_bonus` share one stat list (`+0x130`) and are merged per kind: if the own trait
     has any equipment stat (or lists `equipment_bonus` in `delete_included_values`), none of the included equipment
     stats are taken; otherwise all of them are added. Same, separately, for production stats. No per-stat merge.
   - Triggers and effects (`available`, `visible`, `on_complete`, `ai_will_do`): an own non-empty block wins; otherwise
     the trait keeps a pointer to the included one and evaluates that (0xa4fcf0 / 0xa4fd20).
   - `token` and the script location (used in error messages) always come from the included trait.
   - `delete_included_values` in a trait recognises exactly these keys: `name`, `icon`, `special_trait_background`,
     `limit_to_equipment_type`, `organization_modifier`, `parent`, `mutually_exclusive`, `position`,
     `relative_position_id`, `equipment_bonus`, `production_bonus`, `available`, `visible`, `on_complete`,
     `ai_will_do`. A listed key is not taken from the included trait (it stays empty unless the own block sets it; then
     the own value is used with the warning "is both overridden and in delete_included_values"). `any_parent` and
     `all_parents` in that list match nothing; only `parent` clears the parent groups. **High** for the rules,
     **medium** for the `limit_to_equipment_type` emptiness test (it looks at the filter's resolved entry list `+0x5c`).
4. **Including MIO's own `initial_trait`:** merged, with the trait merge of item 3 (0xa54663). The own block is read
   into a new trait (a second `initial_trait` block replaces the first, 0xa540b6); the included initial trait fills the
   keys it leaves empty. NOR_akers: own `name`, `organization_modifier` and `production_bonus` (capacity 0.07) win, the
   included capacity 0.05 and research bonus are dropped. With no own block, the included one is copied (0xa54613).
   `delete_included_values = { initial_trait }` on the MIO skips the included initial trait entirely (0xa545cf); an own
   one is then kept as written. **High.**
5. **Top-level fields of the including MIO** (0xa54150 start, helper 0xa54aa0): replace, never append, each "taken from
   the included MIO only if the own one is empty and not in the MIO's `delete_included_values`". `equipment_type` and
   `research_categories` are replaced as whole lists (BEL Cockerill's `{ screen_ship convoy_1 }` replaces the generic
   `{ screen_ship }`); an empty own block counts as unset (PHI_cavite_naval_base's `equipment_type = { }` gets the
   included battleship and cruiser types); `name`, `icon`, `background`, `tree_header_text` (whole list),
   `xp_research_type`, the six `on_*` effects, `ai_will_do` likewise. `allowed`, `visible`, `available` and the
   effects are kept as fallback pointers to the included MIO's blocks, used when the own block is empty. `research_bonus` and `task_capacity` count
   as unset when they equal `NIndustrialOrganisation.DESIGN_TEAM_RESEARCH_BONUS` / `DEFAULT_INITIAL_TASK_CAPACITY`.
   The MIO's `delete_included_values` recognises those keys plus `initial_trait`; trait tokens there do nothing (use
   `remove_trait`). **High.**
6. **Order of operations.** Reading only sorts the blocks into lists; their order in the file does not matter. After all
   files are read, the database post-load (0xa53450) resolves each MIO's include once (0xa54150): top-level fields,
   then the initial trait, then for every included trait in the included MIO's order:
   1. token in `remove_trait` → skipped (even when an own `trait` or an `override_trait` names it: the own trait stays
      unmerged, the override stays unmatched and is reported, not used);
   2. else an `override_trait` with that token → moved to the end of the tree, included trait merged in;
   3. else an own `trait`/`add_trait` with that token → included trait merged into it, in place;
   4. else → appended as a copy.

   The tree is: own `trait`/`add_trait` blocks in file order, then the included traits (overridden or copied) in the
   included order. Then the load checks run (0xa51d60), which only log. **High.**
7. **Positions** (0xa4e2c0, computed on demand, so after the include): no `relative_position_id` → `position` as
   written; with it → position of the first trait in the same (including) MIO's tree with that token, computed the same
   way recursively, plus this trait's `position` as a delta. Unknown relative trait → error and (0, 0). There is no
   default position: unset is (−1, −1), and a trait whose computed position is (−1, −1) is reported "position is not
   set" (0xa52080); a relative trait without `position` adds (−1, −1). SWE_gotaverken's dockyard sits at
   semi_armor_piercing_shells (8, 2) + (0, 2) = (8, 4). **High.**
8. **Parents** (0xdba8e0): every `parent`, `any_parent` and `all_parents` block is one group `{traits, needed}`
   (`needed` = `num_parents_needed`, default 1; `any_parent` 1; `all_parents` the number of listed traits). All groups
   must hold (AND): vanilla's combat_information_center needs `all_parents = { hardened_critical_components
   secondary_fire_director_control }` **and** one of three shells. A group holds when the count of its unlocked traits
   reaches `needed` (counted in list order with an early stop at equality). Empty `all_parents = { }` needs 0 and always
   holds; empty `any_parent = { }` needs 1 and never holds (the trait cannot be unlocked). A trait with no parent group
   has its parent check pass, so it can be unlocked whenever the other checks pass (the documentation's "needs at least
   one parent" is not enforced). The unlock check (0x13af360): not yet unlocked, `visible`, parents, mutual exclusion,
   `available`, and optionally (flag) the MIO itself visible/available with `+0x134 > 0`. **High** for AND and the
   group rule; **medium** for the meaning of `+0x134` (presumably unspent trait points).
9. **`mutually_exclusive` is one-directional at run time**: the unlock check only looks at the candidate trait's own
   list against the unlocked set (0x13af515–0x13af59d, also 0xdbb090), tooltip
   `INDUSTRIAL_ORG_TRAIT_TOOLTIP_MUTUALLY_EXCLUSIVE_ALREADY_UNLOCKED`. The load check 0xa52880 logs "It needs to be
   reciprocal." for a one-sided pair but does not add the missing direction. Vanilla's pairs are reciprocal. **High.**

## Readers

The MIO reader (`ReadMember`, 0xa53be0) switches on the key's token id and stores each key in its own field; the
trait reader (0xa50000) does the same for a trait. Nothing is merged while reading.

```
MIO block key:
  name, icon, background            -> strings
  research_bonus, task_capacity     -> numbers
  equipment_type                    -> equipment filter (+0xd0), assigned (a second block replaces)
  research_categories               -> list (+0x1a0)
  xp_research_type                  -> 1 army / 2 navy / 3 air, flag +0xac
  include                           -> included MIO token (+0x10), flag +0x14
  delete_included_values            -> token list (+0x18)
  initial_trait                     -> new trait object (+0xb0) with token "initial_trait", replaces an earlier one
  trait, add_trait                  -> new trait appended to the tree list (+0xb8), owner = this MIO
  override_trait                    -> new trait appended to the override list (+0x1e8)
  remove_trait                      -> token list (+0x1d0)
  tree_header_text                  -> appended (+0x1b8)
  allowed / visible / available / on_* / ai_will_do -> trigger and effect blocks

trait block key:
  token, name, icon, special_trait_background, position (x, y), relative_position_id (+flag)
  parent = { traits = {..} num_parents_needed = n }  -> new group {traits, n}, n default 1
  any_parent = { .. }                                 -> new group {traits, 1}
  all_parents = { .. }                                -> new group {traits, count(traits)}
  mutually_exclusive, delete_included_values          -> token lists (appended)
  equipment_bonus / production_bonus  -> checked to be equipment / production stats (else error, block ignored),
                                         then added into one stat list (same stat twice adds up)
  organization_modifier, limit_to_equipment_type, available, visible, on_complete, ai_will_do
```

The trait's post-read (0xa4fab0) only logs: a group needing more traits than it lists, and organization modifiers that
are not MIO modifiers.

## Include resolution

0xa53450 (persistent vtable slot, chunk 0xa53523) walks the MIO list (`db+0x60`) once. For an MIO with `include`, it
looks the included MIO up in the token map (`db+0x48`) and calls `ResolveInclude(mio, included)` (0xa54150); then it
runs the load checks (0xa51d60) and registers the MIO's trait tokens (`db+0xa0`). An unknown include is reported and
nothing is copied. A chain (A includes B includes C) would depend on list order; no vanilla or TFB MIO has one.

```
ResolveInclude(S, I):
  TakeIncluded(key, ownEmpty) = ownEmpty and key not in S.delete_included_values
                                (own set and listed: warning, own value kept)
  for each top-level field f: if TakeIncluded(f, S.f empty): S.f = I.f        # triggers/effects: S.f_fallback = &I.f
  if "initial_trait" not in S.delete_included_values and I.initial exists:
      if S.initial missing: S.initial = new empty trait
      MergeTrait(S.initial, S, I.initial)
  for each trait t of I, in order:
      if t.token in S.remove_trait: continue
      o = first entry of S.override_traits with o.token == t.token
      if o: move o to the end of S.traits; MergeTrait(that, S, t); erase o from S.override_traits
      else if S.traits has a trait u with u.token == t.token: MergeTrait(u, S, t)      # first match
      else: append a new empty trait to S.traits; MergeTrait(it, S, t)
```

## Trait merge

The same merge serves the initial trait, `override_trait`, a plain `trait`/`add_trait` over an included token, and a
plain copy (into an empty trait). The rules per key are in answer 3.

```
MergeTrait(dst, S, src):                        # 0xa507b0
  Take(key, ownEmpty) = ownEmpty and key not in dst.delete_included_values
  dst.token = src.token; dst.owner = S; dst.location = src.location
  name, icon, special_trait_background, limit_to_equipment_type, organization_modifier,
  parent (all groups), mutually_exclusive, position (x, y), relative_position_id: copy if Take
  temp = copy of src's stat list
  if dst has an equipment stat or "equipment_bonus" in dst.delete_included_values: drop equipment stats from temp
  if dst has a production stat or "production_bonus" in dst.delete_included_values: drop production stats from temp
  add temp into dst's stat list
  available, visible, on_complete, ai_will_do: if Take, point dst's fallback at src's block
```

## Positions

```
GetPosition(trait):                              # 0xa4e2c0
  if not trait.hasRelative: return trait.position             # (-1, -1) when unset
  r = first trait in trait.owner.traits with token == trait.relative_position_id
  if none: log "could not calculate relative position", return (0, 0)
  return GetPosition(r) + trait.position
```

Used by the tree UI (0x2026630, 0x2027e30, 0x2028080) and by the load check 0xa52080 (relative trait missing, position
not set, two traits on one cell).

## Unlocking: parents and mutual exclusion

```
CanUnlock(mio, trait, unlocked, checkMio, ..., tooltip):     # 0x13af360
  if checkMio and not MioEnabled(mio): return false         # 0xdb6020: MIO visible and available, mio+0x134 > 0
  if trait.token in unlocked: return false
  if not Visible(trait): return false                       # own visible, else included fallback
  ok = ParentsHold(trait, unlocked)                         # without a tooltip, false returns at once
  if any token in trait.mutually_exclusive is in unlocked: ok = false
  return ok and Available(trait)

ParentsHold(trait, unlocked):                               # 0xdba8e0
  for each group g in trait.parent_groups:
      n = 0
      for each token in g.traits:
          n += (token in unlocked)
          if n == g.needed: break
      if n != g.needed: return false
  return true
```

Because the comparison is checked after each token, a group with `needed` = 0 and a non-empty list (only possible
with `parent = { ... num_parents_needed = 0 }`) holds only while its first trait is not unlocked; no script does this.
`unlocked` is the MIO's set of unlocked trait tokens (`mio+0x140`, ordered by token id, 0xdbaac0). The same exclusion
loop is 0xdbb090 (called from 0x456f60).

## Worked examples (vanilla 1.19.3; TFB has the same blocks)

- BEL_cockerill_shipyards_organization(_no_wuw), NOR_akers_mekaniske_verksted_organization: 11 plain `trait` blocks
  with only `token` and `limit_to_equipment_type = { screen_ship }`. Result: the generic escort tree unchanged (names,
  icons, positions, parents, mutual exclusion, bonuses), with every trait limited to `screen_ship`. The MIO's
  `equipment_type` is the own list. The included initial trait is copied for BEL (no own block) and merged for NOR.
- 47 vanilla plain `trait` blocks reuse an included token: 44 with only a limit, 2 with only `any_parent`
  (CHI_china_automobile_corporation, CHI_liaoning_arsenal_organization_sea on `generic_mio_trait_high_powered_engine`:
  the own `any_parent = { all_wheel_drive long_range_priority }` replaces the included `all_parents = { all_wheel_drive
  }`, which relaxes the requirement), 1 full copy (SWI_oerlikon).
- SWE_gotaverken_organization: one new trait relative to an included one; it is placed at (8, 4) and needs one of the
  three shell traits.

## Struct fields

| Object | Offset | Meaning |
|---|---|---|
| MIO (template) | +0x08 | token |
| MIO | +0x10 / +0x14 | `include` token / set |
| MIO | +0x18 / +0x24 | `delete_included_values` tokens / count |
| MIO | +0x30 / +0x50 / +0x88 | `name` / `icon` / `background` strings |
| MIO | +0x70 / +0x78 | `research_bonus` (fx5) / `task_capacity` (int) |
| MIO | +0x7c | script location |
| MIO | +0xa8 / +0xac | `xp_research_type` (1 army, 2 navy, 3 air) / set; unset → derived from `equipment_type` at include |
| MIO | +0xb0 | initial trait (owned pointer) |
| MIO | +0xb8 / +0xc4 | tree traits (inline, 0x3d8 each) / count |
| MIO | +0xd0 | `equipment_type` filter |
| MIO | +0x1a0 / +0x1ac | `research_categories` / count |
| MIO | +0x1b8 / +0x1c4 | `tree_header_text` (0x30 each) / count |
| MIO | +0x1d0 / +0x1dc | `remove_trait` tokens / count |
| MIO | +0x1e8 / +0x1f4 | `override_trait` traits not yet matched / count |
| MIO | +0x218, +0x270, +0x2c8 | `allowed`, `available`, `visible` |
| MIO | +0x320, +0x378, +0x3d0, +0x428, +0x480, +0x4d8 | the six `on_*` effects (order of the token list above) |
| MIO | +0x530 | `ai_will_do` |
| MIO | +0x568 … +0x5b0 | fallbacks to the included MIO's `allowed`, `available`, `visible`, `on_*`, `ai_will_do` |
| MIO database | +0x48 / +0x60 / +0xa0 | token → MIO map / MIO list (count +0x6c) / trait token map |
| trait (0x3d8) | +0x08 / +0x10 | token / owning MIO |
| trait | +0x18 / +0x38 / +0x58 | `name` / `icon` / `special_trait_background` |
| trait | +0x60 | `limit_to_equipment_type` filter (empty when `+0xbc` = 0) |
| trait | +0x130 | stat list: entries `+0x138`, count `+0x144`, 16 bytes `{stat, fx5}`; `equipment_bonus` and `production_bonus` together |
| trait | +0x150 (+0x160) | `organization_modifier` holder (container) |
| trait | +0x210 / +0x268 / +0x2c0 / +0x318 | `available` / `visible` / `on_complete` / `ai_will_do` |
| trait | +0x350 / +0x358 / +0x360 / +0x368 | their fallbacks to the included trait's blocks |
| trait | +0x370 / +0x37c | parent groups (0x20 each: token list, `needed` at +0x18) / count |
| trait | +0x388 / +0x394 | `mutually_exclusive` tokens / count |
| trait | +0x3a0 / +0x3a4 | `position` x / y (−1 = unset) |
| trait | +0x3a8 / +0x3ac | `relative_position_id` token / set |
| trait | +0x3b0 / +0x3bc | `delete_included_values` tokens / count |
| trait | +0x3c8 | script location (taken from the included trait on merge) |
| country MIO | +0x118 / +0x134 / +0x140 | template / int that must be > 0 to unlock (points?) / unlocked trait tokens (set) |

## Function map

| RVA | Role |
|---|---|
| 0x14b7a0 | trait move constructor |
| 0x63f3c0 | stat list: add the entries of one kind (mask) of another list, summing equal stats |
| 0x640ad0 | stat list: remove the entries of one kind |
| 0x641040 / 0x641090 | stat list has an equipment / production stat |
| 0xa4c3d0 | trait: append a parent group (`needed` = 1) |
| 0xa4c640 | trait `position` reader |
| 0xa4ce80 | `parent` block reader (`traits`, `num_parents_needed`) |
| 0xa4d310 | equipment filter copy |
| 0xa4d500 / 0xa4d510 | token == / token < |
| 0xa4e2c0 | trait position (relative, recursive) |
| 0xa4e480 / 0xa4fd50 | initial trait: warn for keys that initial traits ignore (icon, special background, parents, mutual exclusion, available, visible, ai_will_do, on_complete, position, relative position) |
| 0xa4f4c0 / 0xa4f710 | trait: mutually exclusive tokens / parent groups (copies) |
| 0xa4fab0 | trait post-read checks (log only) |
| 0xa4fcf0 / 0xa4fd20 | trait `available` / `visible`: own block, else the included fallback |
| 0xa50000 | trait `ReadMember` (token switch) |
| 0xa507b0 | `MergeTrait(dst, mio, src)` |
| 0xa50dc0 | trait: take the included value? (own empty and not in the trait's `delete_included_values`) |
| 0xa51300 / 0xa51450 | trait list: append an empty trait / move a range (erase) |
| 0xa519f0 | trait constructor (position −1/−1) |
| 0xa51d60 | MIO checks after include (unknown include, empty `allowed`, unmatched overrides; calls the five below) |
| 0xa52080 / 0xa525c0 / 0xa52880 / 0xa52d10 / 0xa52f50 | checks: positions / duplicate tokens / mutual exclusion exists and is reciprocal / parents exist / `remove_trait` (all log only) |
| 0xa53450 (chunk 0xa53523) | MIO database post-load: resolve includes, check, register trait tokens |
| 0xa53be0 | MIO `ReadMember` (token switch) |
| 0xa54150 | `ResolveInclude(mio, included)` |
| 0xa54aa0 | MIO: take the included value? (own empty and not in the MIO's `delete_included_values`) |
| 0xdb6020 | country MIO enabled: template visible and available, `+0x134 > 0` |
| 0xdba8e0 | trait parents hold |
| 0xdbaac0 | unlocked set contains token |
| 0xdbb090 | trait excluded by an unlocked trait |
| 0x13af360 | `CanUnlockTrait` (with tooltip) |

## Defines and constants involved

| Name | Role |
|---|---|
| `NIndustrialOrganisation.DESIGN_TEAM_RESEARCH_BONUS` (global 0x3334630) | an MIO whose `research_bonus` equals it takes the included one |
| `NIndustrialOrganisation.DEFAULT_INITIAL_TASK_CAPACITY` (global 0x333476c) | same for `task_capacity` |
| hard-coded 1 | default `num_parents_needed` (0xa4c451) |
| hard-coded (−1, −1) | unset position (0xa51b09) |

## How it was verified

Read from code only. Cross-checked against the vanilla and TFB scripts (the counts and examples above) and the game's
documentation, which agrees on the documented parts (`override_trait`, `delete_included_values`, `initial_trait`). The
undocumented parts (a plain `trait` over an included token, `add_trait` over one, parent-group replacement, per-kind
bonus replacement, one-directional exclusion) could be confirmed in game by opening BEL Cockerill's tree (the escort
traits should show their positions, parents and bonuses) and CHI China Automobile's `high_powered_engine` (it should
unlock with `long_range_priority` alone).

## Differences from the current spec

There is no MIO spec item yet; the importer `src/NavySimulator.GameData/Import/MioImporter.cs` implements the
documentation. Against this finding:

- Plain `trait` over an included token: the importer's [ASSUMED] merge is right (now [RE]).
- `add_trait` over an included token: the importer replaces the included trait; the game merges like `trait`.
- The importer merges per script key: `any_parent`, `all_parents` and `parent` are separate keys there, so an own
  `any_parent` keeps an included `all_parents` (CHI_china_automobile_corporation's `high_powered_engine` then still
  needs `all_wheel_drive`). The game replaces all parent groups at once, and `delete_included_values` only knows
  `parent`. 22 vanilla (17 TFB) `trait`/`override_trait` blocks set a different parent key than the included trait;
  one is naval: GER_deutsche_werke_organization's `override_trait` of `generic_mio_trait_splinter_protection` with
  `all_parents = { GER_mio_trait_raider_doctrine }` needs only the raider doctrine in the game, but also
  `long_range_cruising` or `high_speed_cruising` in the importer.
- An empty own block counts as unset in the game, so the included value is used; the importer takes the empty block.
  PHI_cavite_naval_base_organization (vanilla and TFB) writes `equipment_type = { }` and includes
  generic_refurbishment_repair_organization: in the game it covers `mio_cat_eq_all_battleship` and
  `mio_cat_eq_all_cruiser`; the importer sees no naval type and leaves it out (it is missing from
  `data/mios/PHI_organization.json`). The same rule applies to empty `available = { }` / `visible = { }` (18 / 15
  vanilla MIOs), which then use the included MIO's triggers.
- `special_trait_background = no` in an override wins in the importer; in the game it cannot switch off an included
  `yes`.
- `research_bonus`/`task_capacity` equal to the define default are "unset" in the game.
- `remove_trait` wins over an own `trait` with the same token in the game (the own trait stays, unmerged); the importer
  applies the entries in file order. The tree order differs too (game: own traits first), which only matters for a
  duplicate token in `relative_position_id`.
- `mutually_exclusive` is one-directional at run time; the importer's warnings already cover missing traits, not
  one-sided pairs.

Checked over all vanilla and TFB MIOs: no `add_trait` reuses an included token, no override sets
`special_trait_background = no` over `yes`, no `delete_included_values` lists `any_parent`/`all_parents`, and the empty
trait-level blocks are all in new traits. Bonuses merge the same way in both (each bonus block may only hold its own
kind, else the reader drops it), so every imported naval trait gets the same bonuses and limits. What changes combat
input: PHI Cavite is missing; GER Deutsche Werke's `splinter_protection` has the wrong parents (only which traits can
be unlocked).

## Unresolved

- The meaning of country MIO `+0x134` (checked > 0 when unlocking; presumably unspent trait points) and the other
  arguments of 0x13af360.
- Chained includes: resolution is one pass in database order, so an MIO including an MIO that itself includes one would
  depend on that order; not tested, and no script does it.
- Whether the `limit_to_equipment_type` emptiness test (resolved entry list) can treat a limit naming only unknown
  equipment as empty, letting the included limit through.
- `xp_research_type` derivation masks (0xa542e9, 0xa5430f) beyond navy = 2.
