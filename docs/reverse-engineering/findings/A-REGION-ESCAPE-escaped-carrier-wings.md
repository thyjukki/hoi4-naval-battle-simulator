# A-REGION-ESCAPE: an escaped carrier's air wings in the region air war

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-REGION (`rules.escapedCarrierWings`), A-REGION-LISTS, A-DETECT-MIX (rule 1 is withdrawn),
  S-REPLAY (`replay --region` dry run)
- **Confidence:** static reading (medium-high: no code path takes an escaped ship out of its task force or the task force
  out of the combat), confirmed by the logs (high: at the first air hour after a side's first carrier escape the game is
  closer to `Stay` than to `Leave` in 31 of 31 runs, within 10 % of `Stay` in 30).

Method: `tools/re/` (`hoi4re.py`, `ctx.py`) plus throw-away scripts (function callers, a scan of every
`[reg + 0xe0]` state test, the AirFX list `group+0x88`). Logs: `output/ingame/air_batch2..5` (`hours.csv` states,
`raw.jsonl` `airObjects` / `carrierRatio`), and the engine's per-wing region replays: `Stay` (the default), `Leave`, and
`Stay` with a scratch change that takes the engine's own region losses off the wings the log no longer lists.
Conventions as in [A-REGION](A-REGION-carrier-wings-in-the-region-air-war.md) and
[A-REGION-SYNC](A-REGION-SYNC-carrier-wing-region.md); BUL is the combat's defender (carrier wings scrambled only),
ROM the attacker (carrier wings also on the region list).

## Summary

- **Escaped carriers' wings stay in the region air war** (`rules.escapedCarrierWings = Stay`, the current default).
  The escape changes nothing the region air war reads: the ship stays in its task force, the task force stays in the
  combat (`InCombat`) and in its province, so its wings stay on the scrambled list `+0x140[r]` (attack as air
  superiority, defend, escort, never disrupted), and wings with a region stay on `+0x98[r]` as before.
- The escape only moves the carrier's air object (AirFX) out of the side group's strike list (`group+0x58` →
  `group+0x88`): its wings no longer strike in the naval combat, and the logger, which reads `+0x58`, no longer sees
  them. They are not reinforced (no carrier wing is while its task force is in combat, A-REINFORCE), so they keep losing
  planes in the region air war until the battle ends.
- The logs' apparent "leave" in `air_cv_land_mix` is a replay artefact: the `--region` dry run takes every wing's planes
  from the log, and an escaped carrier's wings, which the log no longer lists, keep their planes from the hour of the
  escape. Against ROM's 120 carrier fighters they really lose most of them within about 200 hours. At the first air hour
  after the escape the game matches `Stay` in every run; with the escaped wings losing planes in the dry run as well,
  the whole battle matches.
- Engine condition: a carrier's wings take part while the carrier is not sunk and the battle goes on. An escaped
  carrier is treated like a fighting one in the region air war and keeps taking region losses there.

## What the game does

### A-REGION-ESCAPE-STATE: the escape, 0x1973470 (member HourUpdate)

```
state 8 (retreating): fire; escape += escapeSpeed / 24 (0x196fe80)
    if escape >= 1.0:
        [0x19739ab: a handle at member+0x10 → object; if object+0x60 <= 0: 0xe27c80(object, …)]   // not decoded, no air effect found
        member+0xe0 = 0x10                                                        // 0x19739bd
        if member has a ship (r15) and 0xc3af20(ship) (carrier):
            0x1c6cc50(group = member+0xe8, member)
state 4 (joining): if the side-wide retreat check 0x1971760 is clear and the ship's task-force naval mission
        (0xfb4390(tf+0x360)) reports <= 0: member+0xe0 = 0x10                  // 0x1973a27, the other 0x10 write

0x1c6cc50(group, member):                                                       // naval_fire_exchange_group.cpp
    for airfx in group.+0x58 (count +0x64):
        if airfx+0x10 == member:                                                // the carrier's own air object
            assert !airfx.IsExternal (+0x81)
            remove it from +0x58 (swap with last); append it to +0x88 (count +0x94, capacity +0x90, grows ×1.5)
            0x12a2c0(airfx)                                                     // empty function (ret)
```

Nothing else runs on escape: no task-force call, no air-base, wing or mission call, no SetRegion. The only reads of
the escaped state 0x10 in the executable are 0x1c6a2ba (a per-group member count that skips escaped members) and the
masks `0x34` / `0x38` (not present: targeting, counts), all inside the naval combat.

### A-REGION-ESCAPE-TF: the ship stays in its task force, and the task force stays in the combat

- `InCombat(unit)` 0xc00000 is `unit+0x1b4 > 0`, the size of the unit's combat array `+0x1a8`. Its only remover is
  CUnit::RemoveCombat 0xc03e00. Callers: the naval combat teardown 0x15c2980 and the other combat type's 0x12ae860
  (every unit of both sides, at the end of the combat), and 0x13e3570 / 0x13e35ff / 0x13e3990 (a unit leaves one
  combat: RemoveCombat, `unit vt+0x168`, both sides `vt+0x80`). 0x13e3570 is called by the combat manager when a unit
  joins a combat in another province (0xbb93c1), by unit movement (0xbfb4a0, `unit.cpp` "Cancel movement"), 0xc04170
  and 0x12bab70; none of them is reached from the member update or the escape.
- Moving ships into a new task force goes through 0xd72580 (new task force from 0xd68d90 in the fleet, ships moved with
  0xd730b0 / 0xd71e70). Its callers: the repair split 0xd76dd0 (`REPAIR_SPLIT_TASKFORCE_SIZE`) and 0xd78ba0 /
  0xd79de0, all from the mission tick 0xd79900, which returns at once when the task force is in combat
  ([A-REGION-SYNC-TICK](A-REGION-SYNC-carrier-wing-region.md#a-region-sync-tick-the-naval-mission-tick-0xd705f0--0xd79900--0xfbcb90--0xfbe7f0--0xfa9e00));
  the sunk-ship removal 0xd77ff0; 0xd77870 (from 0xd0eec0 and the naval mission code 0xfb8ff0); 0xd77900 and the
  0xd775d0 chunk (AI, 0x1353280 / 0x1353450 / 0x1356a90). None is reached from the escape.
- The combat side's update 0x161f810 removes a fire-exchange group only when it has no members, and the group update
  removes only members with neither ship nor convoy ([S-HOUR](S-HOUR-order-and-end.md)); escaped members keep their
  ship (the logs list them every hour until the end, [LOG](LOG-ingame-logging.md)).

So until the combat ends, `ship+0x728` is the same task force, `InCombat` holds, and its location (the battle
province, so region `r`) does not change. The scrambled list 0xc66480 therefore keeps the escaped carrier's wings on
`+0x140[r]`, and SetRegion is never called, so wings with a region stay on `+0x98[r]`.

### A-REGION-ESCAPE-EXEC: nothing in the air code reads the carrier's combat state

The carrier CanExecute 0xf79c10 checks the wing (available missions `+0x30`, `+0x79`, ready timer `+0x64 >= +0x60`,
no transfer target `+0x48`, planes `+0x7c`, no training bit, `period` against the night value, AGGRESSION_THRESHOLD
0xf80930), asserts that the wing's base is a carrier whose ship's task force is in combat (`0x69c`..`0x69f`,
`airmission.cpp`), and sets `effectiveness` 0xf78500 and `effective_planes_count` 0xf78360. ChooseCarrier 0xf7acc0
reads only `wing+0x30` and the mission's `type`. The effective planes 0xf78360 read the wing, the base and the task force's stance
(`tf+0x4ec`). None of them reads the member state or the AirFX lists, so an escaped carrier's fighters keep
`executing_mission = 1` (air superiority) and its bombers `0x10`, and the region air war (attackers `executing & 5`,
defenders `executing != 0`, escorts 0xf77360, disruption 0xc4c570) treats them as before.

### Pseudocode for the engine

```
each air hour, for each side, for each carrier group:
    if carrier sunk (0x20): skip                       // its wings are deleted with it
    // escaped (0x10) and retreating (8) carriers: no difference
    scrambled += carrier wings (fighters attack, all defend, fighters escort, never disrupted)
    if the side's carrier wings fly a region mission: region list += the same wings (as before)
losses of the hour come off every listed wing, escaped carriers' included; no reinforcement while the battle lasts
naval strikes (A-WINGS): an escaped carrier's air object is gone from the side's list: its wings no longer strike
```

## Defines and constants involved

| Name | Role |
|---|---|
| member state `0x10` (escaped), written at 0x19739bd / 0x1973a27 | moves the carrier's AirFX `group+0x58` → `group+0x88` (0x1c6cc50) |
| `NNavy.REPAIR_SPLIT_TASKFORCE_SIZE` | repair split 0xd76dd0, out of combat only (not part of the escape) |
| none new | the region air war's defines are unchanged (A-REGION) |

## How it was verified

**Static:** the escape writes and their callees (above); every caller of RemoveCombat 0xc03e00, of the leave-combat
function 0x13e3570 and of the task-force split 0xd72580; a scan of the whole `.text` for `[reg + 0xe0]` compared with
`0x10` or masked with `0x1x` / `0x3x` (hits only in the naval combat: 0x161e134, 0x161eca3, 0x161fa73, 0x161fac3,
0x1c6a2ba, 0x1c6bec8, 0x1c6ce68, 0x1c6cf27); 0xf79c10, 0xf7acc0, 0xf78360 read in full.

**Logs, first air hour after a carrier's escape** (the region air war runs before the combat hour, so the first one
that can see an escape at combat hour `t` is the next air hour after `t`). Escapes from `hours.csv` (state 8 → 16).
Per run, the first escape of a BUL carrier (the hour before it, game and `Stay` agree):

| Case | Quantity | Runs | Game vs `Stay` | Game vs `Leave` |
|---|---|---|---|---|
| `air_cv_bombers_vs_fighters` (batches 2, 4) | ROM carrier bombers' disruption (BUL's escaped fighters attacking) | 13 | within 0.4 % in all 13 (e.g. s12 h89: 439.4 / 438.5) | 16–71 % low (231.0) |
| `air_cv_land_mix` (batches 3, 5) | BUL land bombers' disruption (BUL's escaped wings as strike defenders and escorts) | 18 | within 10 % in 17 (s5 h81: 17.4 / 17.0); s1 58 / 82 | 1.2–3.2 × too high (34.6) |
| `air_cv_land_mix` | ROM carrier bombers' disruption (BUL's escaped fighters attacking) | 18 | closer in 17 of 18 (s5 h81: 272 / 279) | lower (197) |

ROM carriers that escape late in `air_cv_land_mix` (region-list wings): ROM's remaining bombers' disruption moves with
`Stay` (s5 h481: game 80 → 52, `Stay` 150 → 98, `Leave` 164 → 284; s13 h361: 117 → 87, 161 → 128, 62 → 185).

**Logs, whole battles.** The drift after the escape is the escaped wings' losses, which the dry run cannot see. The
non-escaped BUL carrier 5637 of `air_cv_land_mix` s5 falls from 52 to 9 planes between hours 72 and 289
(`carrierRatio`); the dry run kept the escaped 5635 at its 56 planes of hour 73. With the scratch change that takes the
engine's own region losses off the unlisted wings each air hour (everything else as `Stay`):

| Case, wings | Game | `Stay` (stale) | `Leave` | `Stay` + losses |
|---|---|---|---|---|
| land mix, BUL land bombers: disruption | 179.3k | 56.9k | 205.3k | **192.4k** |
| land mix, ROM carrier bombers: disruption | 141.4k | 158.2k | 138.8k | **139.4k** |
| land mix, ROM land bombers: disruption | 1,479 | 1,661 | 1,564 | **1,436** |
| land mix, losses ROM fighters / BUL fighters / BUL carrier bombers | 743 / 1,448 / 2,564 | 952 / 1,264 / 2,459 | 737 / 1,530 / 2,877 | 832 / 1,347 / 2,541 |
| bombers vs fighters (batches 2, 4), ROM bombers: disruption (wing hours within 1 %) | 223.5k | 225.4k (17,623) | 174.9k (15,565) | **222.8k (18,280)** |

In `air_cv_mirror` (batches 2, 4) the same change moves ROM's losses from 269 / 671 (fighters / bombers, stale) to
181 / 465 against the game's 272 / 559, so the engine's estimate of the escaped wings' losses is not exact there
(the dry run draws its own rolls); the direction holds.

## Differences from the current spec

- A-REGION, `rules.escapedCarrierWings`: `Stay` is the game's rule ([RE + LOG]); `Leave` and `RegionMissionOnly` are
  disproved and can be removed. The engine already keeps an escaped carrier's wings, takes region losses off them and
  never refills carrier wings in battle, which is what the game does.
- [A-DETECT-MIX](A-DETECT-region-air-detection.md#a-detect-mix-air_cv_land_mix-buls-land-bombers-as-defenders-follow-up-log--re)
  rule 1 ("escaped carriers leave the region air war") is wrong: the jump it saw is the replay's stale plane counts.
  Its rule 2 (land effectiveness) is not affected.
- S-REPLAY `--region`: the dry run should take the engine's own region losses off the wings of escaped carriers each
  air hour (the log cannot give their planes), or the logger should also read the AirFX in `group+0x88` (same layout
  as `+0x58`; count at `+0x94`) and log the escaped carriers' wings.

## UNRESOLVED

- The task force's location during combat was not traced (no movement of a task force in combat was found; the logs
  have no per-hour location).
- What 0x19739ab..0x19739b8 does on escape (`member+0x10` handle → object, `object+0x60 <= 0` → 0xe27c80); no air
  effect was found.
- A task force whose ships have all escaped or sunk while other task forces of its side fight on: by the code above it
  stays in the combat; no log has that case (every logged side was one task force).
- The readers of `group+0x88` (0x1c69ba0, 0x1c6cf50, 0x1c6d0d0) and the other writer 0x1c6ae50.
- The region losses of an escaped carrier's wing go through 0xf87550 → 0x15c4500 → 0x161fe00, which looks for the
  wing's AirFX among the externals and the group lists; whether it searches `+0x88` (and so whether the combat's
  air-object loss count includes them) was not read.
