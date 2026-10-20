# A-REGION-SYNC: what gives carrier wings a mission region, and their mission effectiveness

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-REGION (which side's carrier wings are on the region mission list `strategicAir+0x98`,
  scenario input `carrierWingsOnRegionMission`), A-WINGS, A-EXTERNAL (the mission effectiveness, scenario input
  `carrierMissionEfficiency`), `tools/ingame/make_save.py` (A-WINGS-SAVE)
- **Confidence:** read from code only (static analysis), checked against the game's own mid-battle save
  (`autosave.hoi4`, 1936.6.1.2, a 614-hour CV battle at province 8131 started from `air_cv_mirror*_battle_start.hoi4`)
  and the generated start saves. Per section below.

Method: `tools/re/` (`hoi4re.py`, `ctx.py`, `who.py`, `defmap.json`) plus throw-away scripts: callers of every
SetRegion call site resolved to their primary function through chained unwind info, save token ids rebuilt from the
static token registration 0x831c0 (`mov edx, [id]` / `lea r8, name`), modifier id 0x11 named from the registration
0x55f7f0 (`MODIFIER_AIR_MISSION_EFFICIENCY`), naval mission types from the map icon table 0xfb6710. Conventions as in
[A-REGION](A-REGION-carrier-wings-in-the-region-air-war.md) and [A-MISSION-SAVE](A-MISSION-SAVE-air-mission-save-format.md):
RVAs, fixed point ×1e5, `mission` = the air mission at `wing+0x90` (`mission+0x10` type, `+0x14` executing, `+0x20`
active, `+0x30` region, `+0x44` provinces in range, `+0xc0` region_change_penalty, `+0xd8` effectiveness), `nm` = a
task force's naval mission object at `tf+0x360` (`nm+8` = the task force). Defines are given as vanilla / TFB.

## Summary

- A carrier wing gets a mission region (and so goes on `strategicAir+0x98[r]`) almost only from its **task force's
  hourly naval-mission tick**. Every hour, for a task force that is **not in combat**, the mission update assigns all
  its carrier wings (that are not already on an active region mission) the region of the task force's sea location,
  for the naval missions hold, strike force, patrol (only inside its spotting region), convoy raiding / escort, mine
  laying / sweeping and training; reserve fleet and naval invasion support clear it. The same tick then starts
  engagements, so **the side whose mission tick starts the battle has its carrier wings on the region list**, while a
  side that is already in combat at its first tick never gets them (the mission tick is skipped in combat).
- Once set, the region follows the task force: crossing into another strategic region moves wings on the old region
  to the new one (0xc647b0), and on every province change a wing whose region has no province in range is re-pointed
  to the fleet's region (0xf643a0). Nothing at combat start, no daily update and no save load assigns a region to a
  carrier wing that has none (a load keeps a saved `strategic_region`).
- **The region change penalty does not apply to carrier wings.** Every game caller of the mission effectiveness
  0xf78500 passes `isCarrier = (base+0x68 == 0)`, and the isCarrier branch drops both the coverage factor and
  `× (1 − region_change_penalty)`. A carrier wing's effectiveness is
  `clamp(capacity × supply × (1 + air_mission_efficiency) × fuel × (1 + targeted), 0, 1)`; the 0.734 of the in-game
  attacker is the carrier air base's **supply ratio** (received / asked supply), not the 0.9 penalty.
- `region_change_penalty` decays every hour by `EFFICIENCY_REGION_CHANGE_DAILY_GAIN_<type> / 24`, only for missions
  with `type != 0` (so a carrier wing assigned by its task force, `type = 0`, keeps 0.9 forever, as in the save); it
  matters for land wings only.

## A-REGION-SYNC-CALLERS: every caller of SetRegion 0xf859d0

SetRegion (`mission, region, recompute`) is the only caller of the register / unregister functions 0xc5daa0 /
0xc5dcb0. Its callers (call site → primary function):

| Call site | Function | When | Effect on a carrier wing |
|---|---|---|---|
| 0xfaa14a (via the air command 0x1946880 → 0x1941b10 → 0xf859d0 at 0x1941c84) | **0xfa9e00**, from the naval mission update 0xfbcb90 → 0xfbe7f0 | every hour a task force is not in combat (A-REGION-SYNC-TICK) | **sets the region** (or clears it) |
| 0xc64861 | **0xc647b0** region sync, from 0xd6fff0 (unit movement 0xc02781) | the task force enters a province of another strategic region | wings on the old region → new region |
| 0xf64572 (command 0x19413d0 → 0x1946880) / 0xf6473a | **0xf643a0** wing refresh, from the task force's SetLocation 0xd77c70 (vtable +0xe8) → 0xc5c000 → 0xf63f50, and from the carrier transfer 0xc648b0 | every province change; carrier transfers | coverage recomputed; a wing with a region but no province of it in range gets the fleet's region (carrier) or none (land base) |
| 0xf850d6 | 0xf84d40 mission reader | save load: `strategic_region` id in 1..regionCount−1 | sets the saved region (recompute = false) |
| 0xf5d210 | 0xf5d1d0 via 0xc4eaa0, from 0xd72000 (taskforce.cpp, make a task force the child of another) | task forces are merged | clears (type 0, region none) |
| 0xc4fe8d | 0xc4fd20 (from 0x11565e0, a command vtable 0x29951e8) | a new wing created from a template wing | copies the template's region and type |
| 0xf6767b | 0xf674e0 (peace conference 0xe45310, country transfer 0x1425ab0, wing transfer commands 0x1946bd0 / 0x1946c40) | wing copied to another owner / base | copies region and type |
| 0xc573dd | 0xc57320 from wing creation 0xc505f0 | SAM wings only (`allow == 0x20000`) | — |
| 0xf6290f | 0xf62750 wing hourly update | paradrop re-assignment only | — |
| 0xf64f64 | 0xf64b70 transfer arrival, from 0xf62af0 | a transferring wing arrives: `region_to_assign` / `mission_to_assign` | land wings (transfers) |
| 0xf5d66d | 0xf5d5d0 from 0xc4a100 | land bases only (`base+0x68 != 0`), empty wing | clears |
| 0xf877ff | 0xf877b0 from 0xf5d240 (transfer start) and 0xf64b70 | wing leaves for another base, or region out of range | clears |
| 0xf6730d | 0xf67250 via 0x1466ce0 (raids, `raid_instance.cpp`) | raid ends | clears |
| 0xf755fc / 0xf76a33 | 0xf755e0 / 0xf76a10 mission destructors (from the wing destructor 0xf5a5b0) | wing destroyed | unregisters |
| 0x1941c84 | 0x1941b10, the air command 0x1946880 (vtable 0x2a1d570), also built by 0xbefea0 (land army orders, `ordersgroup.cpp`) and interface code (0x10b5…, 0x12c…, 0x15b…, 0x1fd…) | player orders, army air support | sets the commanded region |

**Confidence:** high for the list (all 16 xrefs of 0xf859d0 resolved; the command execute 0x1946880 has 5 direct
callers and one vtable slot, all listed).

## A-REGION-SYNC-TICK: the naval mission tick, 0xd705f0 → 0xd79900 → 0xfbcb90 → 0xfbe7f0 → 0xfa9e00

```
0xd705f0  task force update (vtable 0x2963000 slot), every hour:
    ... 0xd79900(tf):
        if InCombat(tf) (0xc00000: tf+0x1b4 > 0): return            // nothing below runs in combat
        state = tf+0x4b8 (0 = on its mission; 1..4 other activities: repair / move / ... via 0xd7a460 / 0xd79de0 / 0xd7a180)
        if state == 0: 0xfbcb90(nm = tf+0x360)                       // (0xd7a460 can also reach it in one branch)

0xfbcb90 naval mission update (navalmission.cpp):
    0xfbe7f0(nm)                                                     // FIRST: is_in_regions + carrier wing regions
    ...
    if !InCombat(tf) and nm.is_in_regions(+0x30):
        nm+0x44 = 1; per mission type: look for hostile task forces in the province (prov+0xf8, 0xc01660, 0xfb8200 …)
                                                                     // the engagement is started here
        hours_in_mission(+0x3c) += 1; at 24: daily mission work, reset to 0

0xfbe7f0(nm):   t = nm.type(+0x14);  R = region(tf location)         // tf+0x1f0 → +0xc8
    is_in_regions(+0x30) = t in {0 hold, 2 strike force, 8 reserve fleet}  or  (t == 9 and nm+0x60 == null)
                         or R != null and ( t == 1 patrol:           R == spotting_region (+0x70)
                                          | t == 7 training:         R == 0xfb2fc0(nm)
                                          | t == 9 invasion support: R == nm+0x60
                                          | else:                    R in the mission's region list (+0xc8, +0xd4) )
    rid = t in {0, 2}: R.id, only if 0xd6d160(tf) (the region of the sea province; in port the port's sea zone) is R
          t == 1:      is_in_regions ? R.id : 0
          t in {8, 9}: 0
          else (3 convoy raiding, 4 convoy escort, 5 mines planting, 6 mines sweeping, 7 training): R.id
    0xfa9e00(nm, rid)

0xfa9e00(nm, rid):   atWar = 0x700520(country of tf+0x1d8)
    for ship in tf (0xd6eeb0) that is a carrier (0xc3af20) with an air base (ship+0x730) giving the country access (0xc57400):
        for pool, for wing (0xc536b0):
            m = wing.mission
            if rid == 0: take m if m.region != null                                   // clear
            else:        take m if not (m.region and m.active) (0xf80920)
                                and 0xf7af10(m, atWar)                                 // base has the country, no training
                                                                                       // bit, and (0xf61600(wing) > 0
                                                                                       // or recon / paradrop or atWar)
    if any: command 0x1941470(wings, rid); if 0x19488c0 (per wing 0x1941e60: region reachable, 0xf776d0 / 0xf80b10):
        0x1946880: per wing 0x1941b10 → SetRegion(m, region rid, recompute = true)
                                        (region changed: executing = 0, region_change_penalty = EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR,
                                         unregister / register +0x98, range, coverage, +0x58 list)
                   then EnableMission bit by bit with the wing's own type → active recomputed (yes at war)
```
Naval mission types (map icons 0xfb6710 by index; the saves' "Reserve fleet" task forces have `mission=8`): 0 hold,
1 patrol, 2 strike force, 3 convoy raiding, 4 convoy escort, 5 mines planting, 6 mines sweeping, 7 training, 8 reserve
fleet, 9 naval invasion support. Naval mission save keys from its writer 0xfbea80: `mission` +0x14, `is_in_regions`
+0x30, `hours_in_mission` +0x3c, `num_convoys_in_regions` +0x40, `navy_engagement_rule` +0x34, `spotting_region` +0x70,
`hours_in_spotting_region` +0x6c, `accessible_regions_range` +0xa8.

So the mission `type` of the wing stays as it was (0 for carrier wings), and with a region and `type = 0` the wing is
on **both** lists in combat: `+0x98[r]` from the registration, `+0x140[r]` from the hourly scrambled list 0xc66480
(which skips only wings with a region **and** `type != 0`). A wing that is already on an active region mission is not
re-assigned, so its penalty is not reset every hour.

**Confidence:** high for 0xfbe7f0, 0xfa9e00 and the in-combat skip in 0xd79900; medium for the meaning of the task force
state `+0x4b8` and for the hold / strike force condition 0xd6d160 (at sea it is the location's region); medium-high
that the engagement is started later in the same 0xfbcb90 call (the hold branch walks the province's task forces with
0xc01660 / 0xfb8200; the battle start itself was not traced).

## A-REGION-SYNC-MOVE: region change and province change

```
unit movement (unit.cpp 0xc01dc0), a naval unit entering province P (0xc02781):
    0xd6fff0(tf, P): old = region(tf location), new = region(P); if both set and different:
        for each carrier ship with an air base whose byte +0x80 is set: 0xc647b0(base, old, new):
            every wing of the base whose mission region == old → SetRegion(new, recompute = true)
    tf SetLocation (vtable +0xe8 = 0xd77c70): ... for each ship with an air base: 0xc5c000 → 0xf63f50 → 0xf643a0(wing):
        UpdateCoverage(force)                                       // in-range provinces of the region from the new position
        if mission+0x44 == 0 (no province of its region in range):
            carrier base: if m.region and m.region != region of the fleet (0xd6d160) → command: SetRegion(fleet region)
            land base:    SetRegion(null)
```
A wing without a region (`strategic_region` not written) is never given one by these two; they only carry an
existing region along. **Confidence:** high.

## A-REGION-SYNC-EFF: mission effectiveness 0xf78500(mission, &out, isCarrier, noSupply, tooltip)

```
isCarrier = (the wing's base+0x68 == 0)          // every game caller: 0xf79a00, 0xf79c10 (1), 0xf862a0, 0xbb1070, UI
if the owner is in the debug lock table (0x332f5d8): return the locked value
cov  = isCarrier ? 1 : coverage(+0x50)                                        // AIRBASE_DISTANCE_PENALTY_DETAIL
cap  = capacity_penalty of the base entry (0xc53fd0 → entry+0xd8)             // AIRBASE_CAPACITY_PENALTY_DETAIL
sup  = noSupply ? 1 : supply ratio of the base entry (0xc54ed0 → entry+0x30)   // AIRBASE_SUPPLY_PENALTY_DETAIL
e    = cov × cap × sup
if !isCarrier and !(type & 0x60): e ×= 1 − region_change_penalty(+0xc0)       // AIRWING_RECENTLY_ASSIGNED_NEW_REGION
AME  = [region set] region weather holder (0xf19fd0)[0x11] + country[0x11]
       + [carrier base] the fleet's admiral (0xd64500 → 0xd50ee0, +0x418)[0x11] unless 0xc1d640(admiral)
e   ×= 1 + AME                                                                 // 0x11 = air_mission_efficiency
f    = fuel of the base entry (0xc543e0): consumption(+0xe0) > 0 ? clamp(received(+0xf0) / consumption, 0, 1) : 1
if f < 1: e ×= MISSION_EFFICIENCY_MULT_AT_LACK_OF_FUEL + (1 − MULT) × f        // AIRBASE_LACKS_FUEL_PENALTY_DETAIL
if region set: e ×= 1 + 0x6f2650(country, region, 0x11)                        // targeted air_mission_efficiency (0 by default)
return clamp(e, 0, 1)                                                          // fixed point, each product truncated
```
- The base entry (`base+0x30[country]`, built by 0xc60df0) is a `CSupplyConsumer` (base ctor 0xc482a0: +0x20, +0x28,
  +0x30 = 1.0, +0x38 = −1, +0x40 = 1.0). `supply_consumer.cpp` 0x1a0afb0 sets `+0x30 = _ReceivedSupply(+0x40) /
  _AskedSupply(+0x28)` (1 when nothing is asked). Its save block (0xc668b0 after the base writer 0xed0400) writes
  `motorization_level`, `country` (+0xb0), `operational_status` (+0xd0, if ≠ 1), `capacity_penalty` (+0xd8),
  `fuel_consumption` (+0xe0), `received` (+0xf0), `base_fuel_consumption` (+0xe8); the supply ratio is not saved.
- `capacity_penalty` is the A-SORTIE crowd factor (0xc579f0 → 0xc57b70; `CAPACITY_PENALTY`; 0 if `operational_status
  <= 0`), 1 while the planes fit the carrier (`capacity = carrier_size × CARRIER_SIZE_STAT_INCREMENT`).
- The weather term and the targeted term need a mission region: **a carrier wing without a region (scrambled only)
  gets no weather `air_mission_efficiency`**, one with a region gets its region's (rain light −0.1, rain heavy −0.3,
  snow −0.1, blizzard −0.3 vanilla / −0.5 TFB, sandstorm −0.5).
- Effectiveness is recomputed whenever the carrier CanExecute 0xf79c10 runs (the `executing_mission` refresh of each
  air hour, A-REGION-ORDER step 2) and feeds `effective_planes_count = trunc(round(planes × e) ×
  CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[stance])` and the per-hour loss cap of 0xf862a0. It does not enter the naval
  strike itself.

**Region change penalty decay 0xf80490** (from the wing update 0xf62750, which runs every hour from the strategic-air
job 0x2959f80, A-REGION-ORDER; the defines' own comments say "Gain applied hourly"):
```
if penalty > 0 and type != 0 and !(type & 0x800 training):
    penalty -= trunc(gain × 1e5 / (24 × 1e5));  clamp [0, 1]
gain (0xf7ec90, by the sub-unit definition flags +0x5a8): bit 14 CAS, bit 15 naval bomber, bit 12 tactical bomber,
     0x100000400 fighter, bit 13 strategic bomber, bit 21 maritime patrol, else default
```
A land naval-bomber wing therefore needs `0.9 / (0.192 / 24) = 112.5` hours (4.7 days) to fly at full effectiveness
after a region change; a fighter 24.3 hours. Reinforcement blends the penalty by plane count (0xf63e00: new planes
come in at `min(p, EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR)`, callers pass the factor), so new planes also start
penalised.

**Confidence:** high for the isCarrier branch, the factors and their order (tooltip keys confirm each one); high for the
supply ratio field; medium for the admiral term's skip condition 0xc1d640 and the targeted term 0x6f2650 (not
decoded); high for the decay (hourly, A-REGION's reading of 0xc46030 holds).

## Defines and constants involved

| Name | Vanilla / TFB | Role |
|---|---|---|
| `NAir.EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR` | 0.9 / 0.9 | `region_change_penalty` set by SetRegion (0xf85a04); new planes in 0xf63e00 and siblings |
| `NAir.EFFICIENCY_REGION_CHANGE_DAILY_GAIN_DEFAULT / CAS / NAVAL_BOMBER / TACTICAL_BOMBER / FIGHTER / STRATEGIC_BOMBER / MARITIME_PATROL_PLANE` | 1 / 0.888 / 0.192 / 0.192 / 0.888 / 0.072 / 1; TFB strategic 0.1, others the same | hourly decay `gain / 24` (0xf7ec90, 0xf80490); land wings only |
| `NAir.CAPACITY_PENALTY` | 2 / 2 | crowd factor `capacity_penalty` (0xc57a82) |
| `NAir.CARRIER_SIZE_STAT_INCREMENT` | 10 / 10 | carrier capacity per `carrier_size` point |
| `NAir.MISSION_EFFICIENCY_MULT_AT_LACK_OF_FUEL` | 0.25 / 0.25 | fuel factor (0xf78a67) |
| `NNavy.CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO` | {0, .25, .5, .75, 1} / all 1 | effective planes (0xf78476), unchanged |
| static modifiers `weather_*` `air_mission_efficiency` | −0.1 / −0.3 / −0.1 / −0.3 / −0.5; TFB blizzard −0.5 | region weather term, only for wings with a region |
| modifier 0x11 `air_mission_efficiency` | — | country, region weather, fleet admiral, targeted |
| hard-coded 24, 0x18 | | decay ÷ 24; `hours_in_mission` wraps at 24 |

## How it was verified

Static reading, then the saves:

- **`hours_in_mission` / `hours_in_spotting_region`** (written from `nm+0x3c` / `+0x6c`; +0x3c is incremented once per
  out-of-combat mission update): the generated start save `air_cv_mirror_battle_start.hoi4` has ROM's patrol task
  force at 7 / 46 and BUL's at 18 / 2658; the game's `autosave.hoi4`, 614 combat hours later, has ROM at 8 / 47 and
  BUL at 18 / 2658. So ROM's task force ran **exactly one** out-of-combat mission tick after the load (the one that
  assigned its wings and started the battle: ROM is the combat's attacker, unit 76) and BUL's (the defender, unit 73)
  none.
- **ROM's carrier wings** in the autosave: `type=0 active=yes strategic_region=30 region_change_penalty=0.9` (penalty
  not decayed after 614 h: `type = 0`), `executing_mission` naval_bomber / air_superiority. In the start save they had
  `type=0 active=no` and no region (make_save writes them so). Province 7906 and 8131 are both in region 30
  (`map/strategicregions/30-Black Sea.txt`, vanilla and TFB), and the patrol's `spotting_region=30`, so the patrol
  rule gives rid 30.
- **BUL's carrier wings**: no region in either save. The A-REGION logs already showed that BUL's carrier bombers were
  never disrupted (only `+0x98` missions get disruption). In the autosave all of BUL's carriers are sunk (ships 5633 /
  5635 / 5637 / 5639 survive only as air base references; base 979's wings were moved to land base 329) and its wings
  read `active=yes` (an EnableMission ran through one of the transfer / command paths; which one is UNRESOLVED).
- **Effectiveness**: ROM's wings have 0.7344 on bases 973 and 976, 0.73436 on 974, 0.73428 on 975 (one 0-plane wing
  0.73432, a stale value); `capacity_penalty=1`, `received = fuel_consumption` (fuel factor 1) on every ROM carrier
  base. All other factors are per country or per region and equal for the four carriers, so the per-base spread can
  only be the base's supply ratio: ROM's carriers drew about 73 % of the supply they asked for. BUL's wings carry
  effectiveness 1 (and 0.96428 / 0.78571 on a few, stale from hours with less supply), although they never had a
  region; the penalty 0.9 would have given at most 0.1.

Suggested in-game checks (tools/ingame, Frida): log 0xf859d0 entries (`rcx` mission, `rdx` region, caller) during the
first hours after load; at each air hour read `mission+0x30`, `+0xd8`, and the base entry `+0x30` / `+0x28` / `+0x40`
of each carrier; then load a save where the defender's wings have `strategic_region=30` and confirm their bombers get
disrupted.

## Differences from the current spec

- `carrierWingsOnRegionMission` is not arbitrary: it is true for a side whose task force had an out-of-combat mission
  tick in the battle region before the battle (hold, strike force, patrol inside its spotting region, convoy raiding /
  escort, mines, training), i.e. **always the side that starts the battle from its mission** and, in a real game, also
  a defender that was on such a mission for at least one hour before the enemy arrived. It is false for a side that
  was already in combat at its first tick (the defender in a generated battle-start save), on reserve fleet or naval
  invasion support, on patrol outside its spotting region, or whose wings were never given a region. The region is
  the battle's region (the task force's location; region crossings carry it along).
- `carrierMissionEfficiency` is not the region change penalty. Model it as
  `clamp(capacity × supplyRatio × (1 + AME) × fuelFactor, 0, 1)`: capacity from the carrier's planes and
  `carrier_size` (A-SORTIE crowd), AME = country + fleet admiral `air_mission_efficiency` + (only for region-mission
  wings) the region's weather `air_mission_efficiency` this air hour; supply ratio and fuel stay inputs (default 1;
  0.734 for the logged CL3 attacker in the weather run). It does not decay over the battle hours; it changes only
  with the weather, the plane count and the supply.
- make_save.py (A-WINGS-SAVE): to put a side's carrier wings on the list regardless of who attacks, write their mission
  block as `type=0 period=2 active=yes strategic_region=<air region> region_change_penalty=0.9 missions_done=0`
  (the reader registers it on load; the game's tick then leaves it alone; the penalty value is ignored for carriers).
  Leaving `strategic_region` out (today's output) gives the game's default: the attacker's wings join the list at its
  first tick, the defender's do not.

## UNRESOLVED

- A save-only way to keep the **attacker's** wings off the list: its first mission tick assigns them. Candidates to
  test in game: give its wings an active mission over a neighbouring sea region that is in range
  (`strategic_region=<R ≠ battle region> active=yes`: 0xf80920 skips them; they are then not on `+0x98[battle region]`,
  only scrambled, but they get region R's weather term and may defend in the battle region through their `+0x58`
  list, 0xf7a760 not decoded), or a task force mission that yields rid 0 (reserve fleet 8, invasion support 9; whether
  the task force then still engages was not decoded).
- Who exactly starts the engagement inside 0xfbcb90 (the per-type branches after 0xfbe7f0; 0xfb8200 / 0xfb8e00) and so
  which side becomes the attacker; the task force update order within one hour.
- The task force state `+0x4b8` values and the 0xd7a460 branch that also reaches 0xfbcb90.
- 0xf61600 (a tag tied to the base's location, compared with the country list at country+0x1428) and the command
  check 0x1941e60 → 0xf776d0 / 0xf80b10 (region reachability).
- What set BUL's wings `active=yes` without a region during the battle (carrier sinking / transfer paths 0xc648b0,
  0xc62d60; not followed).
- The supply network side: how `_AskedSupply` / `_ReceivedSupply` of a carrier air base are filled (supply_system.cpp).
- 0xc1d640 (admiral term skipped when true) and the targeted modifier 0x6f2650.
