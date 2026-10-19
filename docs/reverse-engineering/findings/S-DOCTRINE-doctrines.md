# S-DOCTRINE: doctrines (tracks, mastery, rewards, milestones, effects, triggers) and navy spirit slots

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** new S-DOCTRINE-MASTERY, S-DOCTRINE-SAVE, S-DOCTRINE-EFFECTS, S-DOCTRINE-TRIGGERS, S-SPIRIT-SLOTS; touches S-STATS-SHIP, S-STATS-BONUS, S-STATS-COMBAT
- **Confidence:** read from code only (no in-game test yet), per section below. S-SPIRIT-SLOTS is from the scripts.

Method: `tools/re` plus throwaway scripts (not in the repo). Doctrine code was found from the source asserts
`doctrines\track_status.cpp`, `folder_status.cpp`, `country_doctrine_status.cpp`, `script\doctrine_triggers.cpp`,
`script\doctrine_effects.cpp`, `core_types\*_template.cpp`. Script keyword token ids were rebuilt from the static token
registration 0x831c0 (each name is preceded by `mov edx, dword ptr [rip+X]` holding its id; doctrine keys are
0x4182–0x41a7), which decodes the save reader jump tables. Supporting sources: the game's
`common/doctrines/**/_documentation.md`, `documentation/effects_documentation.md` and `triggers_documentation.md`,
`localisation/english/doctrines_l_english.yml`, vanilla and TFB doctrine scripts, `common/idea_tags/00_idea.txt`, and
the plain-text saves in `Documents/Paradox Interactive/Hearts of Iron IV/save games/`.

Fixed point is the usual int64 × 1e5 (`DEFAULT_REWARD_MASTERY` and the other `NDoctrines` defines are read by the fx5
reader 0x2072500).

## Function map

| RVA | Role |
|---|---|
| 0x147bfd0 | `TrackStatus::AddToBank(track, x)`: `bank = clamp(bank + x, 0, MASTERY_BANK_MAX)` (leaf, no unwind entry) |
| 0x147c000 | `TrackStatus::AddMastery(track, x)`: cumulative mastery, unlocks rewards in order, completes the track |
| 0x147c1d0 | mastery still needed for the next reward: `cum(unlocked) − mastery`, 0 when complete (UI) |
| 0x147c2a0 | `TrackStatus::IsCompleted`: `sub != null && unlocked >= sub.rewardCount` (leaf) |
| 0x147c2c0 | track status save reader (jump table over tokens 0x4182–0x41a2) |
| 0x147c600 | unassign: `Reset`, then remove the subdoctrine's effects (source 4) |
| 0x147c650 | `TrackStatus::Reset`: remove unlocked rewards' effects (source 5), zero unlocked / mastery / bank, remove the milestone (0xfc8ce0) |
| 0x147c8a0 | `TrackStatus::SetSubdoctrine(track, sub, trackTemplate)`: keeps only the bank (see pseudocode) |
| 0x147cb00 | `TrackStatus::UnlockReward(track, reward, i)`: apply the reward's effects (source 5, index i) |
| 0x147cd00 | track status save writer |
| 0xfc69e0 | folder: track status by index (null if out of range) |
| 0xfc6a20 | `FolderStatus::SetGrandDoctrine`: reset and unassign every track, resize the track list, swap grand doctrine effects (source 3) |
| 0xfc6d90 | `FolderStatus::SetSubdoctrine(folder, trackIndex, sub)` → 0x147c8a0 |
| 0xfc6eb0 | `FolderStatus::GetDailyMastery(folder, out, trackIndex, tooltip)`: gain, min-per-day floor, × bank conversion rate for complete or empty tracks |
| 0xfc7d90 | compute the daily gain of every active track into `track+0x48` |
| 0xfc7eb0 | apply the daily gain: bank (complete or empty track) or `AddMastery` |
| 0xfc8120 / 0xfc8170 / 0xfc81c0 | track i: unlocked count / subdoctrine / track template (`grand+0x318[i]`) |
| 0xfc8250 | subdoctrine is in the folder's completed list (`folder+0x38`) |
| 0xfc85d0 / 0xfc8620 | track i has a subdoctrine and is complete / has a subdoctrine |
| 0xfc8670 | track filter match `{track, folder, grand doctrine, subdoctrine, index (−1 any)}` (filters of add_mastery and the mastery triggers) |
| 0xfc8730 | track index valid |
| 0xfc87b0 | `FolderStatus::CompleteTrack(track)`: append the subdoctrine to `completed_subdoctrines`, apply the grand doctrine's milestone for that track index (source 6) |
| 0xfc8ce0 | `FolderStatus::RemoveMilestone(track)` (called by every `Reset`) |
| 0xfc99f0 | folder status save writer |
| 0x13c60b0 / 0x13c6600 | country (by handle / tag) → country doctrine status |
| 0x13ccee0 / 0x13ccf90 | per country: compute (0xfc7d90) / apply (0xfc7eb0) daily mastery for every folder; called from the doctrine system update 0xd7ed90 |
| 0x13cd100 | console `mastery` worker: per matching track, bank if complete or empty, else `AddMastery` |
| 0x13cd240 | `ApplyDoctrineEffects(cds, effects, source, name, id, index)`: effect block, modifiers dirty, tactics, sub-unit block, equipment bonuses |
| 0x13cda80 | collect the track statuses matching a filter (0xfc8670) over all folders with a grand doctrine |
| 0x13cdc50 | `AddDoctrineModifiers(cds, countryModifiers)`: grand doctrine, subdoctrines, unlocked rewards, completed milestones, weight 1.0 |
| 0x13cee00 | `RemoveDoctrineEffects(cds, effects, source, id, index)` (sub-unit block 0xd13280, named equipment bonuses 0xe6f170) |
| 0x13cf486 | country doctrine status save writer (`folder`, `cost_reduction`, `daily_mastery`, `country`, `enable_tactic`, `equipment_bonus`) |
| 0x9cda20 | grand doctrine milestone i: `grand+0x300 + i·0x190`, bounded by the track count `grand+0x324` |
| 0x9e4da0 | reward cost: `reward+0x1d0` if non-zero, else `DEFAULT_REWARD_MASTERY` (leaf) |
| 0x9e5240 | reward reader: activation effects, then `mastery` → `reward+0x1d0` |
| 0x9e5a50 | cumulative cost `Σ_{j≤i} cost_j` (UI "Unlocked at X Mastery", 0x147c1d0) |
| 0x9e6770 | subdoctrine reader (`rewards`, `track`, `xor`, `mastery`) |
| 0x1529a80 | activation effects reader (grand doctrine, milestone, subdoctrine, reward): `effect`, `modifier`, `equipment_bonus`, `enable_tactic`, sub-unit/category blocks, other keys as modifiers |
| 0x6431d0 | sub-unit (0xac4fa0) or sub-unit category (0xac4c70) stat block reader, the block type technologies use |
| 0x52ba80 | `has_completed_track` evaluate |
| 0x52bc50 | `has_completed_subdoctrine` evaluate (0xfc8250 over folders) |
| 0x52bce0 | `has_doctrine` evaluate |
| 0x52be70 | `has_mastery_level` evaluate |
| 0x52bf90 | `has_subdoctrine_in_track` evaluate |
| 0x52c060 | `has_mastery` evaluate |
| 0x52c240 / 0x52d6d0 / 0x52d900 | readers of `has_doctrine` / `has_mastery_level` / `has_mastery` |
| 0x5297d0 + 0x529000 | `add_mastery` effect: collect matching tracks (0x13cda80), `AddMastery(track, amount)` on each (vtable slot +0xd0; no bank) |
| 0xd0e490 | add a doctrine sub-unit block to the country sub-unit manager (`[country+0xf70]`) and rebuild the affected sub-units |
| 0xd07660 | append a 0x70-byte bonus source `{block, source, id, index}` to `mgr+0x08` |
| 0xd12d20 / 0xd12b20 | rebuild the country copies of the sub-units (types and categories) named in a block → 0x101b1b0 |
| 0x101b1b0 (second loop) | after the technologies (× level), add every `mgr+0x08` bonus block through 0x193d2a0 with weight 1.0 (0x186a0) |
| 0xe60110 | add named equipment bonuses to `[country+0xf68]+0x178` (one handle each, `+0x548` counter); `instant` → push to every matching variant (0xbd2f30) |
| 0x49b830 | `add_equipment_bonus` effect: also calls 0xe60110 |
| 0xc97e00 | equipment bonus key match (0x631e70 tail-calls it) |
| 0xf88330 | equipment `type` keyword token → type bit |
| 0xbe1550 | set a variant's idea bonuses (`+0xd0`) and named-bonus handles (`+0xe8`) from given lists, filtered by 0x631e70 |
| 0x1492ac0 | variant builder (`equipmentvariantbuilder.cpp`): a new variant copies its parent's bonuses via 0xbe1550 ("Failed to get the parent from which to copy bonuses.") |
| 0x75540 / 0x117b280 | console command `mastery` ("Give doctrine mastery, globally or to a specific track") registration / handler → 0x13cd100 |
| 0x6dade0 (call at 0x6dba79) | `CCountry::UpdateModifiers` adds the doctrine modifiers: 0x13cdc50(cds, country+0x5a8), i.e. into country+0x5b8 |

Source type argument of 0x13cd240 / 0x13cee00: 3 grand doctrine, 4 subdoctrine, 5 reward, 6 milestone.

## Struct fields

| Object | Offset | Meaning |
|---|---|---|
| track status (0x60 bytes, inline in the folder) | +0x08 | subdoctrine template (null = empty track) |
| | +0x10 | int: number of unlocked rewards (save key `rewards`) |
| | +0x18 | int64 fx5: mastery, cumulative since the subdoctrine was assigned (save `mastery`) |
| | +0x20 | int64 fx5: banked ("reserved") mastery (save `mastery_bank`) |
| | +0x28 | int64 fx5: base daily gain from units (save `daily_mastery`; tooltip `DOCTRINE_MONTHLY_MASTERY_GAIN_FROM_UNITS` shows it × 30) |
| | +0x30 / +0x3c | `leaders_daily_mastery` list / count |
| | +0x48 | daily gain computed by 0xfc7d90, consumed by 0xfc7eb0 (not saved) |
| | +0x50 / +0x58 | country doctrine status / folder status |
| folder status (0x50 bytes) | +0x08 / +0x10 | folder template / active grand doctrine |
| | +0x18 / +0x20 / +0x24 | track statuses (stride 0x60) / capacity / count (= the grand doctrine's track count) |
| | +0x30 | country doctrine status |
| | +0x38 / +0x44 | completed subdoctrines (never removed) / count |
| country doctrine status | +0x08 | country tag |
| | +0x10 / +0x1c | folder statuses (stride 0x50) / count |
| | +0x28 / +0x34 | unlocked tactics / count |
| | +0x58 / +0x64 | timed mastery sources from `add_daily_mastery` / `add_mastery_bonus` (stride 0x70: +0x08 name, +0x28 filter, +0x58 daily amount, +0x60 bonus factor, +0x68 days; save `daily_mastery`, written by 0x13cf549) |
| | +0x70 / +0x7c | equipment bonus records (0x18 each: +0x08 doctrine id, +0x0c index, +0x10 named-bonus handle; save `{ id index equipment_bonus }`) |
| grand doctrine template | +0x08 / +0x28 | id / name |
| | +0x88 | activation effects (modifier block at +0xe0) |
| | +0x300 | milestones (stride 0x190; effects at +0x10, modifiers at +0x68) |
| | +0x318 / +0x324 | track templates / count |
| subdoctrine template | +0x08 / +0x28 | id / name |
| | +0x88 | activation effects (modifier block at +0xe0) |
| | +0x310 / +0x31c | rewards (stride 0x1d8; effects at +0x50, modifiers at +0xa8, `mastery` at +0x1d0) / count |
| | +0x340 | mastery conditions |
| activation effects | +0x58 | modifier container (`modifier = {}`); +0x68 its entry list (top-level modifier keys) |
| | +0x118 | sub-unit stat block (sub-units +0x08/+0x14, categories +0x20/+0x2c) |
| | +0x150 | `enable_tactic` list (stride 0x28) |
| | +0x168 / +0x174 | `equipment_bonus` list (stride 0x78) / count |
| equipment manager `[country+0xf68]` | +0xa0 / +0xac | the country's variants / count |
| | +0x160 | idea equipment bonuses (0xe5f600) |
| | +0x178 | named equipment bonuses (0xc8 bytes each) |
| | +0x548 | last named-bonus handle |
| sub-unit manager `[country+0xf70]` (save `deployment`) | +0x08 / +0x14 | extra sub-unit bonus sources (save `unit_modifiers`; stride 0x70: +0x08 block, +0x40 source type, +0x44 doctrine id, +0x48 index, +0x50 localization key) |
| | +0x38 | unlocked sub-units (save `unlocked_subunits`) |
| | +0x20 / +0x2c | country sub-unit copies |
| | +0x118 | technology status |
| equipment bonus (0x78) | +0x10 / +0x70 / +0x74 | key string / `instant` / key token |
| equipment type | +0x4d8 | archetype |
| | +0x540 / +0x548 | `type` bit masks (archetype's +0x540 if 0) |
| trigger `has_doctrine` | +0x58 / +0x60 | subdoctrine / grand doctrine |
| trigger `has_mastery` | +0x58 / +0x60 | amount (fx5) / track template |
| trigger `has_mastery_level` | +0x58 / +0x60 | amount (int) / subdoctrine |
| trigger `has_completed_track` | +0x58 | track template |

---

## S-DOCTRINE-MASTERY

### What the game does

```
cost(r) = r.mastery if r.mastery != 0 else NDoctrines.DEFAULT_REWARD_MASTERY          // 0x9e4da0

AddMastery(track, x):                                                                // 0x147c000
    track.mastery += x                                        // cumulative; unlocking never subtracts
    n = 0; rest = track.mastery
    for r in track.sub.rewards (script order):                // none if the track is empty
        if rest < cost(r): break
        n += 1; rest -= cost(r)
    if n != track.unlocked:
        old = track.unlocked; track.unlocked = n
        for i in old .. n-1: UnlockReward(track, i)           // 0x147cb00: reward effects, source 5
        if track.unlocked == track.sub.rewardCount:
            folder.CompleteTrack(track)                       // 0xfc87b0: milestone, completed_subdoctrines

IsCompleted(track) = track.sub != null and track.unlocked >= track.sub.rewardCount    // 0x147c2a0
```

So reward i (0-based) is unlocked exactly when `mastery ≥ Σ_{j≤i} cost_j`. Progress does not reset per reward; the
UI shows the cumulative threshold ("Unlocked at X Mastery", 0x9e5a50) and the remainder `cum(unlocked) − mastery`
(0x147c1d0). A reward's `mastery = 0` falls back to the default.

Daily gain (0xd7ed90 → 0x13ccee0 / 0x13ccf90, once per day):

```
base   = track.dailyFromUnits (+0x28) + Σ matching add_daily_mastery amounts
       + other terms (attaché, faction sharing, theater commander: 0x1a33390, 0x1a32f40, 0x1a33830; not decoded)  // 0xfc6eb0
factor = 1 + Σ matching add_mastery_bonus + mastery gain factor modifiers (0x1a33240, 0x1a33ac0, 0x1a33210)
gain   = base × factor
if gain > 0: gain = max(gain, MIN_MASTERY_GAIN_PER_DAY)                               // 0xfc774b
if IsCompleted(track) or track.sub == null: gain ×= MASTERY_BANK_CONVERSION_RATE     // 0xfc7890
apply, only for tracks whose `active` trigger holds (0x9e6b80):                       // 0xfc7eb0
    if gain > 0: (IsCompleted or empty) ? AddToBank(track, gain) : AddMastery(track, gain)
AddToBank(track, x): track.bank = clamp(track.bank + x, 0, MASTERY_BANK_MAX)          // 0x147bfd0
```

Other ways mastery moves:

- `add_mastery` effect (0x5297d0 → 0x529000): `AddMastery(track, amount)` on **every** matching track, also complete
  and empty ones. No bank, no cap. On an empty track the amount is lost when a subdoctrine is assigned.
- Console `mastery` (0x13cd100): complete or empty track → `AddToBank(amount)` (no conversion rate), else `AddMastery`.
- Assigning a subdoctrine (0x147c8a0):

```
if new == track.sub: return
bank = track.bank
Reset(track): remove effects of rewards 0..unlocked-1; unlocked = mastery = bank = 0; RemoveMilestone
remove old subdoctrine effects (source 4); track.sub = new; apply new subdoctrine effects (source 4)
AddMastery(track, bank)                       // loc MASTERY_INITIAL_FROM_RESERVE "Initial Mastery from reserve"
```

- Changing the grand doctrine (0xfc6a20): every track is reset and unassigned (mastery, bank, rewards, milestones
  lost), the track list is resized to the new grand doctrine's tracks, grand doctrine effects are swapped (source 3).
  Loc `SWITCH_GRAND_DOCTRINE_MASTERY_RESET`: "All Mastery is reset when switching Grand Doctrine."

**Maximum mastery.** There is no cap. From daily gain the mastery stops at the value reached on the completing day,
`Σ cost + that day's overshoot` (the overshoot stays in `mastery`, it is not banked); after that the gain goes to the
bank at `MASTERY_BANK_CONVERSION_RATE`, capped at `MASTERY_BANK_MAX` (vanilla 0.25 / 200, TFB 0.5 / 300). The bank is
only used as the starting mastery of the next subdoctrine assigned to that track. `add_mastery` can push mastery above
the total without bound; the excess only matters for `has_mastery`.

**Milestone.** Granted exactly on the transition to "all rewards unlocked" (0x147c136..0x147c1a7 → 0xfc87b0). The
milestone is the grand doctrine's `milestones` entry with the same index as the track in its `tracks` list
(0x9cda20). Its modifiers count while `IsCompleted` holds (0x13cdd80); its sub-unit, equipment and tactic parts are
applied once at completion (source 6, index = track index) and removed by `Reset`. The subdoctrine also goes into the
folder's `completed_subdoctrines`, which is never cleared (`has_completed_subdoctrine`, "ever completed").

**Triggers** (all evaluate every folder and every instance of a track):

- `has_mastery = { track = T amount = A }` (0x52c060): true if any track status of track type T has `mastery ≥ A`
  (int64 fx5 compare at 0x52c113). Unit: mastery points, the cumulative value. No subdoctrine required.
- `has_mastery_level = { sub_doctrine = S amount = N }` (0x52be70): true if any track holding S has
  `unlocked ≥ N` (int compare at 0x52bf23). "Level" = number of unlocked rewards (loc `DOCTRINE_TRACK_NUM_REWARDS`
  "N/TOTAL Mastery levels reached").
- `has_completed_track = T` (0x52ba80): any track of type T, in a folder with a grand doctrine, with a subdoctrine and
  `IsCompleted` (the current subdoctrine; changing it un-completes the track).
- `has_subdoctrine_in_track = T` (0x52bf90): any track of type T with a subdoctrine.

### Evidence

0x147c079..0x147c0f0 (mastery add, reward loop with 0x9e4da0 and local subtraction), 0x147c0f5..0x147c1a7 (unlock loop
0x147cb00, completion test against `sub+0x31c`, 0xfc87b0); 0x9e4da0 (`[rcx+0x1d0]`, else `[0x3333e60]` =
DEFAULT_REWARD_MASTERY); 0x9e5240 (`mastery` token 0x418c → `+0x1d0`); 0x147bfd0..0x147bff6 (bank clamp, MASTERY_BANK_MAX
global 0x3334558); 0xfc774b/0xfc7752 (MIN_MASTERY_GAIN_PER_DAY 0x33341f0, `cmovl`), 0xfc7761..0xfc7890
(MASTERY_BANK_CONVERSION_RATE 0x3334480 for complete or empty tracks); 0xfc7f7c..0xfc7fab (bank or AddMastery);
0x147c934 (bank read before Reset at 0x147c93d) and 0x147c99e (AddMastery(bank)); 0x147c6c9..0x147c6df (Reset zeroes
+0x10/+0x18/+0x20); 0xfc6ad9 (Reset+unassign every track on grand doctrine change); 0x529000 (`jmp 0x147c000`);
0x13cd18e..0x13cd1b6 (console path). Triggers: 0x52c110..0x52c117 (`cmp [track+0x18], amount; jge`),
0x52bf20..0x52bf26 (`cmp [track+0x10], amount; jge`), 0x52bb16 (0xfc85d0 → 0x147c2a0). Filter layout 0xfc8670.
Localisation (`doctrines_l_english.yml`): `MILESTONE_UNLOCK_EXPLANATION` ("unlocks when the track has reached full
Mastery"), `MASTERY_BANK_EXPLANATION_FULL` / `_EMPTY` (reserve while completed / until a subdoctrine is unlocked, up to
a maximum), `HAS_MASTERY_TRIGGER`, `HAS_MASTERY_LEVEL_TRIGGER`. The tracks documentation ("Milestone … activate once
all of the rewards are unlocked") agrees.

### Verdict and confidence

NEW. High for the unlock rule, completion, milestone timing, bank handling and the trigger comparisons (all read
directly). Medium for the daily-gain sources (only the bank, floor and active-trigger steps were decoded).

## S-DOCTRINE-SAVE

The doctrine state is one global block (not inside the country block). Writers 0x13cf486, 0xfc99f0, 0x147cd00; layout
confirmed in `jukki_usa_vs_bc_japan_battle_start.hoi4` (plain text, `HOI4txt`):

```
doctrine={
    countries={
        {
            folder={
                {
                    grand_doctrine="new_base_strike"      # omitted when none
                    folder="naval"
                    tracks={                              # one entry per track of the grand doctrine, in its order
                        {
                            sub_doctrine="..."            # omitted when the track is empty
                            rewards=0                     # unlocked reward count (int)
                            mastery=0                     # cumulative mastery
                            mastery_bank=0
                            daily_mastery=0
                            leaders_daily_mastery={ ... } # only when non-empty
                        }
                        ...
                    }
                    completed_subdoctrines={ "..." }      # only when non-empty
                }
                ...                                       # one per folder: land, naval, air, special_forces
            }
            cost_reduction=... daily_mastery=...          # when present
            country="USA"
            enable_tactic={ 62 }
            equipment_bonus={ ... }                       # named bonus records, when present
        }
        ...
    }
}
```

All seven local saves are 1936 starts: grand doctrines are set (JAP `grand_battleplan` + `new_base_strike`, USA
`superior_firepower` + `new_base_strike`, GER `new_mobile_warfare` + `new_convoy_raiding`), no subdoctrine is slotted
and every mastery is 0, so a slotted track was not observed in a save. Token ids: `rewards` 0x4182, `tracks` 0x4184,
`grand_doctrine` 0x4187, `sub_doctrine` 0x418a, `mastery` 0x418c, `daily_mastery` 0x418e, `completed_subdoctrines`
0x4197, `mastery_bank` 0x419c, `leaders_daily_mastery` 0x41a2. Confidence: high.


### Loading

Every persistent object's `InitPostRead` (vtable +0x40; the name comes from `CTechnologyStatus::InitPostRead()` at
0xee0551) runs as soon as its own block has been read: the generic block reader 0x24be690 calls it at 0x24beb7b when
the parser has no error (0x24c0050). Nothing runs later that rebuilds doctrine effects: 0x13cd240, the only function
that applies doctrine sub-unit blocks, tactics and equipment bonuses, has exactly four callers, all activation paths
(0xfc6c2e grand doctrine, 0x147c993 subdoctrine, 0x147cb3c reward, 0xfc8921 milestone). What each part does on load:

| Part | Restored from | Evidence |
|---|---|---|
| doctrine state (grand doctrine, tracks, `rewards`, `mastery`, `completed_subdoctrines`) | the `doctrine` block, fields taken as written | readers 0x147c2c0 (track), 0xfc9020 (folder), 0x13ced70 (country). InitPostRead: track none (0x12a2c0); folder 0xfc8530 (drops the tracks of a folder without a grand doctrine); country 0x13cde50 (drops unknown folders, invalid cost reductions and timed sources, adds missing folders, sets back pointers via 0xfc8590) |
| country modifiers | **the restored state**: `CCountry::UpdateModifiers` 0x6dade0 → 0x13cdc50 reads the grand doctrine, slotted subdoctrines, the `rewards` count and `IsCompleted` every time it runs | 0x13cdc50 |
| sub-unit and category blocks | **only the records** in the country's `deployment={ unit_modifiers={ … } }` | reader 0x197f890 / writer 0x197fa10 (`subunit_stat_bonus_persistent.cpp`); the deployment InitPostRead 0xd11bd0 rebuilds every country sub-unit copy from technologies + these records (0xd0e200, then 0x101b1b0) |
| equipment bonuses | **only the records**: the named bonuses in the country's `production={ named_equipment_bonuses={ … } last_named_equipment_bonus=N }` (0xe71cb0), the handles in each variant's `named_equipment_bonuses={ … }` (0xbe3c80), and the doctrine's `equipment_bonus={ … }` list (0x13cf616, used only to remove them later) | writers above; named entry writer 0x633010 |
| tactics | the `enable_tactic` list | 0x13cf5b9 |

Sub-unit bonus record (as written in `jukki_usa_vs_bc_japan_battle_start.hoi4`, GER):

```
deployment={
    ...
    unit_modifiers={
        {
            submarine={                    # the doctrine's sub-unit / category block, script syntax
                surface_detection=0.05
                max_organisation=5
            }
            type=grand_doctrine            # grand_doctrine (3), sub_doctrine (4), rewards (5), milestones (6); also effect (2), project (1)
            number=0                       # 0 for grand doctrine and subdoctrine, reward index, or milestone track index
            id=new_convoy_raiding          # grand doctrine for grand_doctrine and milestones, subdoctrine for sub_doctrine and rewards
            # localization_key="..."       # only when set (not used by doctrines)
        }
    }
    unlocked_subunits={ ... }
}
```

Named equipment bonus records (syntax from the writers; no local save has one):

```
production={
    named_equipment_bonuses={
        {
            name="<doctrine name key>"        # the name 0x13cd240 receives (template +0x28)
            prefix="MODIFIER_DOCTRINE_PREFIX"
            ship_hull_cruiser={ instant=no naval_weather_penalty_factor=-0.125 }   # key token, instant, then the stats
            id=0                              # handle
        }
    }
    ...                                       # each matching variant: named_equipment_bonuses={ 0 } (its handles)
    last_named_equipment_bonus=0
}
doctrine={ countries={ { ... equipment_bonus={ { id=<grand or subdoctrine> index=<n> equipment_bonus=<handle> } } } } }
```

Consequences for a hand-written save:

- `rewards` is trusted as written; nothing recomputes it from `mastery` at load. The next `AddMastery` (daily gain or
  effect) recomputes it: if `mastery` implies more rewards, they unlock then (with their effects, and the milestone if
  that completes the track); if fewer, the count drops without removing effects. Write consistent values.
- Milestone modifiers follow `rewards == reward count` (`IsCompleted`); `completed_subdoctrines` only feeds
  `has_completed_subdoctrine`. A milestone's sub-unit block needs its own `type=milestones` record.
- `tracks` must hold exactly one entry per track of the grand doctrine, in its order: the entry count is the folder's
  track count (0xfc8730: index valid iff `< folder+0x24`) and entry i is matched to `grand.tracks[i]` (0xfc81c0, which
  asserts past the grand doctrine's track count). Empty tracks as `rewards=0 mastery=0 mastery_bank=0 daily_mastery=0`.
  A slotted track needs only `sub_doctrine` besides those (track reader keys: `sub_doctrine`, `rewards`, `mastery`,
  `mastery_bank`, `daily_mastery`, `leaders_daily_mastery`); no XP or other list is involved.
- **Ship organisation.** The ship's InitPostRead 0xc3ad50 → 0xc3ba50 runs `RefreshStats(ship, 1)` (0xc3bb91), which
  clamps `organisation = min(organisation, maxOrg)` (0xc3d5f2), and sets organisation to maxOrg if it is 0 (0xc3bda6, skipped when gamestate+0xa35 is set).
  Inside a country block the save has `technology` → `units` → `deployment` (BUL at lines 823142 / 829759 / 997458 of
  `light_attack_vs_torp_cruiser_battle_start.hoi4`), so ships are refreshed before `unit_modifiers` are read. The
  sub-unit stats used then (0xd11ae0) are the copies the technology InitPostRead 0xee0400 built (technologies only),
  or the database definition if there is no copy. The saved organisation is therefore clamped to the max organisation
  **without** the doctrine sub-unit bonus, and only the hourly regain raises it afterwards. Whether the country
  modifier sum is already computed at that moment (navy_org / navy_org_factor terms) was not traced.

Smallest save content for full doctrine effects at battle start: the `doctrine` block (modifiers come back by
themselves), the matching `unit_modifiers` records (sub-unit blocks), and for the organisation either the country's
`deployment` block moved before its `units` block (untested: the reader dispatches by key, but other deployment
contents may refer to later blocks) or a check of the hour-0 organisation with `tools/ingame`. Equipment bonuses need
all three record lists above. Confidence: high for the call graph and the record formats (read from the writers and
one save); medium for the load-order consequence (order read from the save file, not observed in game).

## S-DOCTRINE-EFFECTS

### What the game does

The activation effects reader 0x1529a80 (used by grand doctrines, milestones, subdoctrines and rewards) sorts keys:

| Key | Stored | Applied |
|---|---|---|
| `effect = { … }` (0x59) | effect block | run once in country scope on activation (0x13cd240, vtable +0x60); never undone |
| `modifier = { … }` (0x2965) | modifier container `+0x58` | country modifier sum (below) |
| a sub-unit or sub-unit category name (`battleship = {}`, `category_fighter = {}`) | `+0x118`, read by 0x6431d0 (the technology block type) | country sub-unit stats (below) |
| `enable_tactic` (0x3357) | `+0x150` | unlocked tactics (`cds+0x28`) |
| `equipment_bonus = { <key> = { … instant = yes } }` (0x3167) | `+0x168` | named equipment bonuses (below) |
| any other key | 0x6120a0 → 0x610de0 (modifier key parser, "Unknown modifier") into the same container (`+0x68`) | country modifier sum |

The documented `modifiers = { … }` form (token 0x31e6) has no case in 0x1529a80; it would reach the modifier key parser.
No vanilla or TFB doctrine uses it (every `modifier = {` in those files is inside `ai_will_do`).

Activation 0x13cd240 (`cds, effects, source, name, id, index`) and the modifier gather:

```
run effects.effect in country scope
if effects.modifiers not empty: mark country modifiers dirty (0x6dade0)
for tactic in effects.tactics: add to cds unlocked tactics if absent
if effects.subunitBlock not empty:                                                    // 0xd0e490
    [country+0xf70].bonusSources.append({block, source, id, index})                   // 0xd07660
    rebuild every country sub-unit named in the block (types and categories)          // 0xd12d20 → 0x101b1b0
if effects.equipmentBonuses not empty:                                                // 0xe60110
    for b in list: new handle in [country+0xf68]+0x178; if b.instant: add to every existing variant v with AppliesTo(b, v.type)
    record {doctrine id, index, handle} in cds+0x70 (removal: 0x13cee00 → 0xe6f170)

country sub-unit copy (0x101b1b0) = definition
    + Σ technology blocks × level           (0x193d370 → 0x193d2a0, weight level × 1.0)
    + Σ bonusSources blocks × 1.0           (0x193d2a0, weight 0x186a0)        // doctrines: no level factor

CCountry::UpdateModifiers 0x6dade0: clear country+0x5b8, add every source; at 0x6dba79:  // 0x13cdc50
    for folder with a grand doctrine:
        add grand.effects.modifiers × 1.0
        for track i: if track.sub: add track.sub.effects.modifiers
                     for j < track.unlocked: add track.sub.rewards[j].effects.modifiers
                     if IsCompleted(track): add grand.milestones[i].effects.modifiers
```

So:

- **Sub-unit and category blocks** (`battleship = { max_organisation = 5 hg_attack = 0.05 }`) land in the same
  country per-sub-unit stats as technology bonuses, through the same 0x193d2a0 (sub-unit type match 0x643110 and
  every category of the sub-unit 0x6430e0), added once with weight 1.0. They reach ships through 0xc3d090 exactly like
  a technology: `ship[s] = variant[s] × (1 + sub[s])`, plus `sub[s]` for the additive set (S-STATS-SHIP). They are
  rebuilt immediately and removed with the source (0xd13280).
- **Top-level keys and `modifier = {}`** go into the country modifier sum at `country+0x5b8` with weight 1.0, the same
  sum ideas and technologies feed; ships read it through the ship modifier gather 0xc38760 (S-STATS-COMBAT,
  S-HIT-MODS). Subdoctrine modifiers count as soon as the subdoctrine is slotted (any mastery, the track's `active`
  trigger is not checked); reward modifiers follow `track.unlocked`; milestone modifiers follow `IsCompleted`.
- **`equipment_bonus`** is the **named** equipment bonus path (0xe60110, the same function the `add_equipment_bonus`
  effect 0x49b830 uses, `[country+0xf68]+0x178`, handles in `variant+0xe8`), not the idea path 0xe5f600
  (`[country+0xf68]+0x160`, `variant+0xd0`). Both sit in the same `(1 + Σ)` multiplier of the variant (S-STATS-BONUS),
  so the arithmetic is the same: every stat multiplicative, hit chance factors included. Unlike `add_equipment_bonus`,
  the doctrine records the handles and removes them when the source goes (documentation: "use this instead of the
  add_equipment_bonus effect, since the effect will not remove the bonus if the doctrine is changed").
- **Key match** (`AppliesTo`, 0x631e70 → 0xc97e00) accepts: an equipment name equal to the variant's type, the type's
  archetype (`type+0x4d8`, e.g. `ship_hull_heavy`, `ship_hull_cruiser`, TFB `ship_hull_torpedo_cruiser`) or the
  archetype's `+0x4e8` link (unidentified); or an equipment `type` keyword checked against the type bit masks
  (`type+0x540` or the archetype's, `| type+0x548`): `convoy` 0x1, `capital_ship` 0x40, `submarine` 0x80,
  `screen_ship` 0x100, `carrier` 0x400000 (0xf88330, jump table 0xf88584/0xf88538); or a third lookup (0x22f740 +
  virtual check, not decoded). So both `capital_ship` and `ship_hull_heavy` work.

### Evidence

0x1529aad..0x1529ad8 (token switch), 0x1529ada..0x1529b00 (sub-unit block, then modifier parser on `+0x68`),
0x1529eed (`modifier` → `+0x58`), 0x1529c7e..0x1529e14 (`equipment_bonus` list `+0x168`, stride 0x78), 0x1529b0b
(`enable_tactic` `+0x150`). 0x13cd296 (effect block), 0x13cd2aa..0x13cd2ba (`+0x68` empty test, 0x6dade0),
0x13cd2bf..0x13cd3f0 (tactics), 0x13cd402..0x13cd43c (sub-unit block → `[country+0xf70]`, 0xd0e490, entry +0x44/+0x48),
0x13cd440..0x13cd61c (equipment bonuses → 0xe60110 with `[country+0xf68]`, "MODIFIER_DOCTRINE_PREFIX", records in
`cds+0x70`). 0xd0e490 → 0xd07660 + 0xd12d20; 0x101b217..0x101b24f (bonus list loop, `r9d = 0x186a0`, 0x193d2a0);
0x193d3b4 (technology weight `level × 0x186a0`). 0x557840 checks `container+0x10` (= effects `+0x68`) for emptiness, so
top-level keys and `modifier = {}` fill one container. 0x6dae2d/0x6dae66 (`country+0x5b8` cleared, `country+0x5a8`
passed), 0x6dba79 → 0x13cdc50 (grand `+0xe0`, sub `+0xe0`, rewards `+0xa8`, milestone `+0x68`, weight 0x186a0).
0xe60110 (handle counter `+0x548`, list `+0x178`, `instant` `+0x70` → variants `+0xa0`, 0xbd2f30); 0x49b9fd..0x49ba29
(`add_equipment_bonus` → 0xe60110); 0xbafd1c..0xbafd56 (ideas → 0xe5f600 with `[country+0xf68]`). 0xc97e00 key
match; 0xf88330 type bits. Script use: TFB `navy_capital_subdoctrines.txt` (`capital_ship`, `ship_hull_heavy`),
`navy_carrier_doctrines.txt` (`carrier`, `convoy`, `ship_hull_escort_carrier`), `navy_screen_doctrines.txt`
(`ship_hull_cruiser`, `ship_hull_torpedo_cruiser`).

### Verdict and confidence

NEW. High for where each key goes, for the weight 1.0 and for the modifier gather. Medium for the third key lookup and
the `modifiers = {}` case. The S-STATS rows "country `+0x160`" (idea bonuses) and "country `+0xa0`" (variants) are
offsets in the equipment manager `[country+0xf68]`, not in the country (0xbafd23, 0xe60110 `[r15+0xa0]`).

## S-DOCTRINE-TRIGGERS: `has_doctrine`

```
has_doctrine = X                                                                      // 0x52bce0
    for folder in cds.folders:
        if X is a grand doctrine and folder.grandDoctrine == X: true
        if X is a subdoctrine and any track in folder.tracks has track.sub == X: true
    false
```

True for the active grand doctrine of any folder, or a subdoctrine slotted in any track of any folder. No mastery,
reward or `active` check: a subdoctrine counts the moment it is assigned, with 0 mastery. Evidence: 0x52bd99..0x52bdf5
(grand at trigger `+0x60` vs `folder+0x10`; subdoctrine at `+0x58` vs `track+0x08` over `folder+0x18`, stride 0x60).
Loc `HAS_SUBDOCTRINE` "Has active Subdoctrine", `HAS_GRAND_DOCTRINE` "Has active Grand Doctrine"; the triggers
documentation says "currently active". TFB navy spirits use it in `available` (e.g. `has_doctrine =
new_fleet_in_being`, `has_doctrine = torpedo_primacy`). Confidence: high.

## S-SPIRIT-SLOTS (from the scripts)

`common/idea_tags/00_idea.txt` (vanilla; TFB has no `common/idea_tags`, so vanilla applies) defines the three navy
spirit categories, each `type = navy_spirit` with exactly one `slot` line:

| Category | `slot` | `cost` (navy XP) | `removal_cost` |
|---|---|---|---|
| `naval_academy_spirit` | `naval_academy_spirit` | 20 | −1 |
| `navy_spirit` | `navy_spirit` | 35 | −1 |
| `naval_command_spirit` | `naval_command_spirit` | 50 | −1 |

The number of slots is the number of `slot` / `character_slot` lines in the category (the `government` category lists
`character_slot = political_advisor` three times for three advisors). So a country holds at most one spirit per
category, three navy spirits in all. Nothing else changes that count: `NDefines` has only AI keys
(`DESIRE_USE_XP_TO_UNLOCK_NAVY_SPIRIT`, `UNLOCK_SPIRIT_*`), the documentation lists no slot effect or modifier for
ideas (only `<type>_category_type_cost_factor` and `<category>_cost_factor`, which change the cost), and TFB's only
references are cost factors (e.g. `navy_spirit_category_type_cost_factor` in `toa_shared_military_branch.txt`). TFB
`common/ideas/navy_spirits.txt` offers 11 academy, 10 navy and 15 naval command spirits; their `available` triggers use
`has_doctrine` and `has_naval_academy` (a scripted trigger that only restricts Ethiopia). Confidence: medium-high (script
semantics; the idea slot reader in hoi4.exe was not decoded).

---

## Defines and constants involved

| Name | Global | Read at | Role |
|---|---|---|---|
| `NDoctrines.DEFAULT_REWARD_MASTERY` (100) | 0x3333e60 | 0x9e4dac | reward cost when the reward has no `mastery` |
| `NDoctrines.MASTERY_BANK_CONVERSION_RATE` (0.25; TFB 0.5) | 0x3334480 | 0xfc7890 (×gain), 0xfc790c and 0x1f35e37 (UI) | share of daily gain banked on complete or empty tracks |
| `NDoctrines.MASTERY_BANK_MAX` (200; TFB 300) | 0x3334558 | 0x147bfe6, 0x1f35e54 (UI) | bank cap |
| `NDoctrines.MIN_MASTERY_GAIN_PER_DAY` (0) | 0x33341f0 | 0xfc774b | floor of a positive daily gain |
| `NDoctrines.MAX_MONTHLY_MASTERY_GAIN` (40; TFB 75) | 0x3334110 | 0x1a329e0, 0x1a32bc0, 0x1a32da0, 0x1f3744e | gain cap (not decoded) |
| `NDoctrines.BASE_MASTERY_GAIN_TARGET_MANPOWER` | 0x3333f48 | 0x1a329f1, 0x1a32bd1, 0x1a32db1 | diminishing returns of unit gain (not decoded) |
| `NDoctrines.TRAINING_MASTERY_GAIN_FACTOR`, `NAVAL_MISSION_MASTERY_GAIN_FACTORS` | 0x3334028, 0x3338668 | 0x9e39ea, 0x9e39cf | mission factors (not decoded) |
| `NDoctrines.MILITARY_ATTACHE_MASTERY_TRANSFER_FACTOR`, `THEATER_COMMANDER_UNITS_MASTERY_GAIN_FACTOR_PER_SKILL` | 0x3334640, 0x3334708 | 0x11212ee, 0x1a33466; 0x1184558 | other gain sources (not decoded) |
| hard-coded source types 3 / 4 / 5 / 6 | — | 0xfc6bf3, 0x147c960, 0x147cb1e, 0xfc890a | grand doctrine / subdoctrine / reward / milestone effect sources |

## How it was verified

Static reading only; the save layout was checked against the local plain-text saves, and the documentation and
localisation strings cited above agree with every rule. Suggested in-game check with `tools/ingame`: in a 1936 save,
`set_sub_doctrine` a naval subdoctrine, then use the console `mastery` command (0x75540) in steps (e.g. 150 then 60
with default rewards) and watch rewards unlock at 100, 200, … cumulative; complete a track and confirm the milestone
modifier appears and further gain shows as "reserving".

## Differences from the current spec

There was no doctrine spec. For the scenario setup this means:

1. A track's state is `(subdoctrine, mastery)`. Unlocked rewards = the greedy count above (cumulative thresholds,
   `DEFAULT_REWARD_MASTERY` when a reward has none); the track is complete when all are unlocked; the grand
   doctrine's milestone for that track index is active iff complete. Mastery above the total has no effect except
   `has_mastery`. The bank only matters when a subdoctrine is changed, so it can be ignored for a battle snapshot.
2. Active effects = grand doctrine + every slotted subdoctrine (any mastery) + unlocked rewards + milestones of complete
   tracks. Each effect block contributes: sub-unit/category blocks to the research-like sub-unit sums (weight 1, no
   level), modifiers to the country modifier sum (combat-time, S-STATS-COMBAT), `equipment_bonus` to the design's
   multiplier sum (named-bonus semantics, keys by equipment name, archetype or type keyword). `effect = {}` blocks are
   scripted side effects (TFB uses them to set `*_milestone_var` variables) and do not change stats directly.
3. `has_doctrine` (spirit availability) is true for the active grand doctrine or any slotted subdoctrine.
4. Navy spirits: at most one per category (academy, navy, naval command).

## Unresolved

- How a non-instant equipment bonus first reaches a variant. Existing variants get only `instant` bonuses; a new
  variant copies its parent's bonus lists (0x1492ac0 → 0xbe1550). Several TFB doctrine bonuses are not instant
  (`blue_water_training` `ship_hull_cruiser`, the carrier `naval_weather_penalty_factor` bonuses).
- `modifiers = { }` (the documented spelling): no case in 0x1529a80; whether 0x610de0 accepts it was not checked.
- The archetype's `+0x4e8` link and the 0x22f740 lookup in the key match.
- How the unit gain `track+0x28` is computed (`MAX_MONTHLY_MASTERY_GAIN` / `BASE_MASTERY_GAIN_TARGET_MANPOWER` in
  0x1a328f0, 0x1a32ad0, 0x1a32cb0) and the attaché, leader and gain-factor terms (0x1a33390, 0x1a32f40, 0x1a33830,
  0x1a33240, 0x1a33ac0, 0x1a33210).
- Edge cases read from code, not tested: a negative `AddMastery` lowers `unlocked` without removing the reward's
  sub-unit and equipment parts (only its modifiers follow the count); a subdoctrine with no rewards is `IsCompleted`
  at once (milestone modifiers count) but `CompleteTrack` never runs.
- Whether the country modifier sum is computed before the ships' InitPostRead on load, and whether moving `deployment`
  before `units` in a country block is accepted (S-DOCTRINE-SAVE, Loading).
- Variant stat recalculation after loading named bonus handles (assumed from the writer order: named bonuses before
  `available_equipments`).
- The idea slot reader in hoi4.exe (S-SPIRIT-SLOTS is from script semantics).

## Index entries

See the "Doctrines (S-DOCTRINE)" rows in [../index.md](../index.md) (functions, struct fields, define read sites).
