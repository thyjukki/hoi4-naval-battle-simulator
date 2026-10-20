# A-MISSION-SAVE: how an air wing's mission and target region are saved, loaded and executed

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-EXTERNAL, A-WINGS (`docs/mechanics/air.md`); the save writer of `tools/ingame/make_save.py`
  (A-WINGS-SAVE)
- **Confidence:** read from code only, with one check against a save the game wrote itself (`autosave.hoi4`, a
  carrier battle in region 30: its carrier wings' `mission` blocks have exactly the writer's keys in the writer's
  order). No in-game test of a hand-written land mission yet. Per section below.

Method: `tools/re/` (`hoi4re.py`, `ctx.py`, `modmap.py`), script token ids rebuilt from the static token registration
0x831c0 (10,765 names; the save writers here use the ids as `mov edx, imm32` immediates), define names from the
`NDefines` registration (`lea r8, global` then `lea rdx, name`), assert strings (`_ActiveMissions[ pRegion->GetID() ]`,
`_AllActiveMissions`, `pAirWing`, `pAirBase`, `airmanager.daily`). Conventions as in
[A-WINGS](A-WINGS-carrier-wings-and-reinforcement.md): RVAs, fixed point ×1e5, `mission` = the air mission object at
`wing+0x90` (`mission+0x28` = the wing), a wing handle resolves to `wing+0x10` (the wing's second vtable 0x297ade0, so
the wing's ReadMember 0xf651b0 and InitPostRead 0xf62bc0 get `this = wing+0x10`).

## Token ids

| Id | Key | Id | Key |
|---|---|---|---|
| 0x8d | `priority` | 0x31e9 | `missions_done` |
| 0xe1 | `type` | 0x373c | `executing_mission` |
| 0x2c7e | `active` | 0x3740 | `effectiveness` |
| 0x2e92 | `aggressiveness` | 0x3741 | `effective_planes_count` |
| 0x2eee | `strategic_region` | 0x3745 | `effective_air_superiority` |
| 0x2f0a | `period` | 0x3ab7 | `region_change_penalty` |
| 0x4a84 | `stop_training_at_max_xp` | 0x2b31 / 0x2cba | wing keys `allow_mission_type` / `mission` |
| 0x393c / 0x393d | wing keys `region_to_assign` / `mission_to_assign` | 0x2fc3 / 0x4ded / 0x2edc / 0x4dee | `air_group` / `air_theatre` / `air_group_index` / `air_theatre_index` |

`executing_mission` is saved as a **keyword**, not a number: 0x1014010 maps a mission bit to its keyword, 0x1013de0 back
(leaf token switches). Bit → keyword: 0x1 `air_superiority`, 0x2 `cas`, 0x4 `interception`, 0x8 `strategic_bomber`,
0x10 `naval_bomber`, 0x20 `drop_nuke`, 0x40 `paradrop`, 0x80 `naval_kamikaze`, 0x100 `port_strike`, 0x200
`attack_logistics`, 0x400 `air_supply`, 0x800 `training`, 0x1000 `naval_mines_planting`, 0x2000
`naval_mines_sweeping`, 0x4000 `recon`, 0x8000 `naval_patrol`, 0x10000 `barrage_mission`, 0x20000 `sam_mission` (this
also confirms the A-WINGS bit order). `type` stays a number (the bit mask).

## A-MISSION-SAVE-WRITE: the writer 0xf87820

Keys in the order written; "if" = only written when the condition holds.

```
type                      = +0x10 (int, mission bits)                      always
period                    = +0x18 (int: 0 day, 1 night, 2 day and night)   always
active                    = +0x20 (yes/no)                                 always
executing_mission         = keyword of +0x14                               if +0x14 != 0
effectiveness             = +0xd8 (fixed)                                  if != 0
effective_planes_count    = +0xe0 (int)                                    if != 0
effective_air_superiority = +0xe8 (fixed)                                  if != 0
strategic_region          = [+0x30]+0x58 (the region id)                   if a region is set (+0x30 != null)
region_change_penalty     = +0xc0 (fixed)                                  if a region is set, even when 0
missions_done             = +0x74 (int)                                    always
aggressiveness            = +0x1c (int, index into NAir.AGGRESSION_THRESHOLD)  if != 0
priority                  = string of [entry]+0x270, one per entry of +0x80 (count +0x8c)   per entry
stop_training_at_max_xp   = +0x7c (yes)                                    if set
```

`region+0x58` is the strategic region's id: the naval mission writer (0xfbec69) writes the same field as
`spotting_region`, the reader below indexes the region array with it, and the game's own carrier battle save in the
Black Sea writes `strategic_region=30`. So `strategic_region=30` is the file id of `map/strategicregions/30-Black Sea.txt`
(province 7906 is in it, vanilla and TFB). Numbers are written as the game writes other fixed-point values: up to five
decimals, trailing zeros dropped, 1.0 as `1` (`effectiveness=0.73434`, `region_change_penalty=0.9`).

Observed in `autosave.hoi4` (ROM carrier bombers in the battle at 7906; not copied here): `type=0`, `period=2`,
`active=yes`, `executing_mission=naval_bomber`, `effectiveness=0.73434`, `effective_planes_count=7`,
`effective_air_superiority=3.5`, `strategic_region=30`, `region_change_penalty=0.9`, `missions_done=0`, in that order.

**Confidence:** high (writer read in full; matches the game's own save).

## A-MISSION-SAVE-READ: the reader 0xf84d40 and what it does on load

The mission reader is a token switch; each key writes its field directly, except `executing_mission` and
`strategic_region`:

```
type, period, active, aggressiveness, missions_done, effective_planes_count -> the field (ints / byte / bool)
effectiveness, effective_air_superiority, region_change_penalty            -> the field (fixed, 0x24c0a70)
executing_mission = keyword -> bit (0x1013de0) into +0x14        (asserts at airmission.cpp line 0xc22 if the keyword is unknown)
stop_training_at_max_xp -> +0x7c;  priority = string, looked up (0x319d60 / 0x683660), appended to +0x80
strategic_region = id:
    if 1 <= id < gamestate.regionCount(+0x2ec): SetRegion(mission, gamestate.regions(+0x2e0)[id], recompute = false)
    (an out-of-range id is ignored silently)
```

**SetRegion 0xf859d0 (mission, region, recompute)** — the same function the game uses when the player picks a region:
```
if region == current region (+0x30, compared by id +0x58): return
executing_mission(+0x14)     = 0
region_change_penalty(+0xc0) = NAir.EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR          // 0.9 vanilla and TFB
strategicAir = [wing.pool(+0x38)]+0x40
if old region: 0xc5dcb0(strategicAir, mission)              // remove from the lists below
+0x30 = region
if region: 0xc5daa0(strategicAir, mission, region)           // register (see "Where missions are kept")
if recompute: range +0x78 = wing range (0xf5e5c0), +0x44 = 0, coverage +0x50 (as 0xf87480)
+0x64 = 0; base = 0xf5f830(wing) ("pAirBase" assert); if region: 0xf7a760(base position, region, &+0x58)
```

So **the order of the keys inside `mission={ }` matters**:
- `executing_mission` written before `strategic_region` (the writer's own order) is wiped by SetRegion on load. It is
  recomputed in the next air step anyway (below), so it does not need to be written.
- `region_change_penalty` must come **after** `strategic_region`; otherwise SetRegion leaves the 0.9 penalty. The
  writer's order already does this.

**Post-read of the wing, 0xf62bc0** (vtable 0x297ade0 + 0x40, runs at the end of the wing's block): ace check, …,
0xf661e0, then **0xf87480(mission, force = true)** and **0xf87290(mission, 0)**:

```
0xf87480 UpdateCoverage(mission, force):
    r = wing range (0xf5e5c0, whole km)
    if force or r != +0x78:
        +0x78 = r; +0x44 = 0
        if region and not training (type & 0x800):
            provinces of the region within range of the base -> list +0x38 (16-byte entries {province, distance}, count +0x44)
                                                                 (0x10132d0 -> 0x1012f80; base position = land base
                                                                  province, or the carrier's fleet location)
            coverage +0x50 = min(1, count / region.provinceCount(+0x24))
0xf87290 ReserveCommandPower(mission, flag):
    cost = 0xf79e40(wing, type)            // NAir.MISSION_COMMAND_POWER_COSTS per plane; 0 for naval bomber and air superiority
    if cost > 0 and the country cannot reserve it (0x714830, NCountry.BASE_MAX_COMMAND_POWER + modifiers): type = 0
```

Before that, the pool reader 0xf65c70 (the pool's ReadMember) builds each wing (0xf5a1f0), reads it and calls
**0xf667b0**: the wing's available missions `wing+0x30` are computed from its saved `allow_mission_type` (`wing+0x2c`,
read straight from the save by 0xf651b0) as in A-WINGS.

So after a load: the region link, the per-region registration, the in-range province list and coverage are all
rebuilt; `effectiveness` / `effective_planes_count` are only stale values until the next air step; `active` and `type`
are taken as saved.

**Confidence:** high for the reader, SetRegion and the post-read calls (read in full); medium for the range test details
(0x1012f80 compares squared distances with the range; not decoded further).

## A-MISSION-SAVE-LISTS: where region missions are kept

Per country, in its CStrategicAir (assert strings in 0xc5daa0 / 0xc5dcb0):

| Field | Meaning |
|---|---|
| `strategicAir+0x98` | `_ActiveMissions[regionId]`: 24-byte list per region id (`+0x0` items, `+0xc` count) of land-based missions targeting that region |
| `strategicAir+0xb0` | `_AllActiveMissions` |
| `strategicAir+0xc8` | `_ActiveMissionRegions`: sorted region ids with missions |
| `strategicAir+0x140` | a second per-region list read by the same refresh (carrier missions; who registers into it is not decoded) |
| `strategicAir+0xe0` | per-region records 0xa0 bytes; `+0x9a` = "a mission other than AS / interception / training executes here" (0xc4a075) |

SetRegion registers the mission (0xc5daa0) when the save is read; nothing else is needed. **Air theatres and air
groups** are a country-level organisation (`country+0x180` written as `air_theatre` by the country writer 0x7191b0; a
theatre 0x14ec490 holds regions and groups, a group 0x14ec5a0 holds `air_group` members) and the top-level
`air_theatre_index` / `air_group_index` are only id counters (0xc66c60). None of the functions on the execution path
below reads them, and the template save has none. A wing does not have to be in an air group to fly a mission.

**Confidence:** high for the lists (asserts name them); high that the execution path does not read theatres/groups
(none of 0xc49e80, 0xf79a00, 0xf7a420, 0xf796e0, 0xf82110 touches `country+0x180`).

## A-MISSION-SAVE-EXEC: what must hold for the wing to execute the mission

**Refresh of `executing_mission`.** The hourly strategic-air update 0xc57d30 calls **0xc58660** (0xc58068) before it
runs the missions (0xc58582). 0xc58660 builds, per region id, the list of countries' CStrategicAir that have missions
there, stores each region's night coverage (0xe21640) and runs a parallel job (task vtable 0x2959ec0, body
**0xc49e80**) over the regions:

```
for each region id, for each country's strategicAir with missions there:
    for mission in strategicAir+0x98[region]:                         // land-based missions
        if CanExecute(mission, nightCoverage[region]):  executing_mission = ChooseLand(mission)   // 0xf79a00, 0xf7a420
        elif the wing's base is a land base (base+0x68 != 0): executing_mission = 0
    for mission in strategicAir+0x140[region]:                        // carrier missions
        if CanExecuteCarrier(mission, …): executing_mission = ChooseCarrier(mission); 0xf661e0(wing)   // 0xf79c10, 0xf7acc0
        else executing_mission = 0
    region record +0x9a = (OR of the executing bits) & 0xf7fa != 0
```

Then the execution loop (A-WINGS-EXT) runs every land mission of a region in its air step whose
`executing_mission & 0x1f7fa` through 0xf82110, which joins naval combats for 0x10 / 0x80 / 0x8000.

**CanExecute 0xf79a00 (mission, nightCoverage):**
```
type(+0x10) != 0 and region(+0x30) set and active(+0x20)          // assert: not a training mission
coverage(+0x50) != 0                                                  // at least one region province in range
wing.availableMissions(+0x30) != 0
not 0xb0f470(wing): not (wing+0x79) and ready timer +0x64 >= +0x60 and no transfer target (+0x48)
wing.planes(+0x7c) > 0 (or a missile sub-unit, 0xf63140)
period(+0x18): 0 -> nightCoverage <= NAir.DAY_NIGHT_COVERAGE_FACTOR; 1 -> nightCoverage > it; 2 -> always
0xf80930: wing.planes / wingMaxSize >= NAir.AGGRESSION_THRESHOLD[aggressiveness(+0x1c)]      // index 0 = 0.0
carrier base whose task force is in combat: only if type & 5 (AS / interception) and the region is the fleet's
effectiveness(+0xd8)          = 0xf78500(mission, &out, isCarrier, 0, 0)
effective_planes_count(+0xe0) = 0xf78360(mission, base)                // round(planes × effectiveness), A-WINGS-EXT
return effective_planes_count > 0
```

**ChooseLand 0xf7a420 → 0xf796e0** (first match; each needs the bit in `type` **and** in the wing's available missions
`wing+0x30`):
```
0x1 air superiority                  -> 0x1    (no target needed)
0x4 interception                     -> 0x4
0x2 CAS             if 0xf7e600 finds targets   -> 0x2
0x8 strategic bomber if 0xf7e290 ...            -> 0x8;    0x10000 barrage likewise
0x10 naval bomber or 0x80 kamikaze  if 0xf7d590(mission, 0, 0, 0, stopAtFirst = 1) finds a target -> 0x80 if kamikaze else 0x10
0x100 port strike (0xf7ddd0), 0x200 logistics (0xf7cef0), 0x400 air supply (0xf7cac0), 0x1000 / 0x2000 mines,
0x4000 recon, 0x8000 naval patrol (needs +0x44 > 0 and region+0xa0), 0x40 paradrop
else 0
```

**Naval targets, 0xf7d590.** Returns nothing unless `+0x44 > 0` (provinces in range) and `region+0xa0 != 0`. It walks
the mission's **in-range province list** `+0x38` only. For each province it first looks at the province's combats
(`province+0x170`): a naval combat (`vt+0x58 == 2`) that passes 0x13e27f0 and 0x161f620 (the mission country's side
checks) is a target, and with `stopAtFirst` the search ends there. Then it scores spotted fleets in the province
(`province+0xf8`, not in combat, NAir.NAVAL_STRIKE_DETECTION_BALANCE_FACTOR / NAVAL_RECON_DETECTION_BALANCE_FACTOR). So a
running naval battle is a target **only if its province (7906 here) is in the wing's range from its base**, not merely
somewhere in region 30.

**ChooseCarrier 0xf7acc0** (reads only the available missions `wing+0x30`, not `type`; settles the A-WINGS question of
who sets `executing_mission` for carrier wings):
```
fighter-type sub-unit (type & 0x100000c00):  avail & 1 -> 0x1; avail & 4 -> 0x4;
                                              type & 0x80 and avail & 0x80 -> 0x80; else avail & 0x10 ? 0x10 : 0
other sub-units:  type & 0x80 and avail & 0x80 -> 0x80; avail & 0x10 -> 0x10; avail & 1 -> 0x1; avail & 4 -> 0x4; else 0
```
This matches what tools/ingame logged (carrier bombers 0x10 and fighters 0x1 with `type` 0) and the autosave above.

**What `active` is.** Only EnableMission 0xf852a0 writes `+0x20` (the player's or AI's mission command): it sets `type`
(AS 0x1, interception 0x4, kamikaze 0x80, barrage, SAM replace the mask; other bits are added), then `active = 1` when
the wing's base has an entry for the country (0xc57400), `type` has no training bit, and either `0xf61600(wing) > 0`
(the value that also removes training from the available missions; meaning not decoded), or `type` has recon /
paradrop, or `0x700520(country)` (`country+0xf88` → 0xd3fd60; most probably "at war"); otherwise `active = 0` and
`executing_mission = 0`. A load never recomputes it, so a hand-written mission needs `active=yes`.

**Confidence:** high for the refresh call chain, CanExecute, ChooseLand/ChooseCarrier and that `executing_mission` is
recomputed (the only writers in airwing/airmission/strategicair code are 0xc49e80 and the clears); medium that 0xc49e80
runs only for regions in their air step (0xc58660 filters regions by a per-region flag array; which flag is not
decoded); medium for the combat test in 0xf7d590 (0x13e27f0 / 0x161f620 not decoded; the mission's country is a
participant here).

## A-MISSION-SAVE-EFF: effectiveness is recomputed

`effectiveness` (+0xd8) is overwritten by 0xf78500 every time CanExecute passes, before the plane budget uses it, so the
saved value does not matter. Two inputs of 0xf78500 that the save does set:
- **`region_change_penalty`**: for missions other than paradrop / drop nuke (`type & 0x60`), the result is multiplied by
  `(1 − region_change_penalty)` (0xf789e9..0xf78a16). With the 0.9 that SetRegion sets, a fresh mission flies at
  10 % effectiveness, and `effective_planes_count = round(planes × effectiveness)` is the plane budget of the join.
- **coverage** `+0x50`: enters as `1 − coverage` (0xf786fa); the exact term is not decoded.

The penalty decays in 0xf80490 (called from the wing update 0xf62750): `penalty −= gain / 24` (clamped to [0, 1]),
`gain = NAir.EFFICIENCY_REGION_CHANGE_DAILY_GAIN_<type>` from 0xf7ec90 (naval bomber 0.192, fighter 0.888; TFB changes
only the strategic bomber's, to 0.1), and only for missions with `type != 0` (so carrier wings with `type=0` keep 0.9,
as in the autosave). **disagrees** with A-WINGS, which calls 0xf62750 the wing hourly update (its own assert says
"hourly update"): the only caller path found statically is the air manager's daily update ("airmanager.daily" 0xc50040,
from `gamestate.daily` 0x1d4810) → 0xc46440 / 0xc46030 → 0xf62a50 → 0xf62750, which would make the decay one
twenty-fourth of the define's daily gain per day. Writing `region_change_penalty=0` sidesteps the question.

**Confidence:** high for the overwrite and the `(1 − penalty)` factor; medium for the coverage term; the decay rate
(hourly or daily) is UNRESOLVED.

## What to write: one naval-strike wing and one air-superiority wing over region 30

Inside the country's block of `strategic_air={ }` (e.g. `BUL={`), one pool per sub-unit at one base. Ids are new ones
(`type=4713` pool, `type=69` wing, `type=70` the variant's equipment id). The mission block replaces the template's
`mission={ type=0 period=2 active=no missions_done=0 }`, in this key order:

Naval strike (TFB land sub-unit `nav_bomber`, `land_air_wing_size = 100`; its equipment must be a naval bomber variant
the country owns):
```
		air_wing_pool={
			id={ id=<new> type=4713 }
			definition=nav_bomber
			air_base={ id=329 type=65 }
			air_wings={
				id={ id=<new> type=69 }
				count=100
				experience=0
				reinforcement_setting=1
				timed_disabling={
					remaining_hours=0
					should_start_on_transfer=no
				}
				manpower=<planes × the variant's manpower per plane>
				mission={
					type=16
					period=2
					active=yes
					strategic_region=30
					region_change_penalty=0
					missions_done=0
				}
				tag="BUL"
				equipment={
					equipment={
						id={ id=<variant id> type=70 }
						amount=100
					}
					allow_zero_entries=yes
				}
				name="<any>"
				priority=1
				allow_mission_type=<the variant's allowed missions; must contain 16>
				air_untrained_pilots_penalty_factor=1
			}
		}
```

Air superiority (e.g. one of BUL's existing `fighter` wings at base 329, `allow_mission_type=2053` = AS | interception |
training): only its mission block changes:
```
				mission={
					type=1
					period=2
					active=yes
					strategic_region=30
					region_change_penalty=0
					missions_done=0
				}
```

Notes:
- `executing_mission`, `effectiveness`, `effective_planes_count`, `effective_air_superiority` are not needed (recomputed
  at the region's next air step). If written, `executing_mission` only survives when placed after `strategic_region`.
- `allow_mission_type` is read as is and becomes the wing's available missions at load (0xf667b0); use the value the
  game derives for the variant (`variant+0x410`; TFB `cv_nav_bomber` wings are saved with 35088 = naval_bomber |
  port_strike | training | naval_patrol). It must also equal the variant's value or the wing will not reinforce
  (0xf5cc60). Without bit 0x10 the wing never executes the naval strike.
- The base must be close enough: at least one province of region 30 in the plane's range (else coverage 0 and the
  mission never executes), and province 7906 itself in range (else the battle is not a target).
- `period=2` flies day and night; `period=0` would skip the battle's night air steps (night coverage above
  NAir.DAY_NIGHT_COVERAGE_FACTOR 0.5).
- A **naval strike** wing that executes becomes an external air group in the battle (A-WINGS-EXT). An **air
  superiority** wing executes (bit 0x1 needs no target) but is never run through the join 0xf82110 (mask 0x1f7fa), so it
  is not part of the naval battle; it only fights in the region's air war (enemy planes, including enemy naval bombers
  heading for the battle).

## Defines and constants involved

| Name | Role |
|---|---|
| `NAir.EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR` (0.9) | `region_change_penalty` set by SetRegion (0xf85a04), also on load |
| `NAir.EFFICIENCY_REGION_CHANGE_DAILY_GAIN_*` | penalty decay per wing update ÷ 24 (0xf80490 via 0xf7ec90) |
| `NAir.DAY_NIGHT_COVERAGE_FACTOR` (0.5) | `period` 0 / 1 day / night test (0xf79acd, 0xf79af1) |
| `NAir.AGGRESSION_THRESHOLD` ({0, 0.25, 0.5}) | wing fill needed per `aggressiveness` (0xf8093e) |
| `NAir.MISSION_COMMAND_POWER_COSTS` | command power reserved per plane at load / assignment (0xf79e40, 0xf87290); 0 for AS and naval bomber, vanilla and TFB |
| `NCountry.BASE_MAX_COMMAND_POWER` | the reservation cap (0x714830) |
| `NAir.NAVAL_STRIKE_DETECTION_BALANCE_FACTOR`, `NAVAL_RECON_DETECTION_BALANCE_FACTOR` | spotted-fleet targets in 0xf7d590 |

## How it was verified

Static reading. Cross-checks: the writer's keys and order equal the `mission` blocks the game wrote in `autosave.hoi4`
(carrier battle in region 30); the bit ↔ keyword switch confirms the A-WINGS mission bit order; `region+0x58` is written
as `spotting_region` by the naval mission writer and both saves use 30 for the Black Sea; ChooseCarrier reproduces the
in-game logged `executing_mission` of carrier wings. Suggested in-game check (tools/ingame): write the two blocks above,
load, and read `wing+0xa4` / `+0x50` / `+0xc0` of the BUL wings and the battle's external AirFX in the first air steps.

## Differences from the current spec

- `make_save.py` can put land wings on missions with the block above; no air group or theatre is needed.
- A-EXTERNAL: a land wing joins only if the battle's own province is in its range from the base; its mission
  effectiveness includes `× (1 − region_change_penalty)` (fresh mission: 0.1).
- A-WINGS: the carrier `executing_mission` setter is 0xc49e80 → 0xf7acc0 (above), based on the wing's available
  missions only.

## UNRESOLVED

- How often 0xf62750 runs (hourly by its assert; only a daily caller path found), hence the penalty decay rate.
- The coverage term in 0xf78500 and the rest of the effectiveness formula (fuel, air superiority, base).
- 0xc58660's region filter (air-step flag array or all regions with missions).
- `region+0xa0` (needed by 0xf7d590 and naval patrol; non-null for sea regions?), the combat tests 0x13e27f0 /
  0x161f620, and 0xf7a760's list `mission+0x58` (built from the base position and the region).
- `priority` list entries (looked up by name through 0x319d60 / 0x683660; `[entry]+0x270` is written back) and
  `stop_training_at_max_xp` use.
- The carrier list `strategicAir+0x140`: who registers into it; CanExecuteCarrier 0xf79c10 not decoded.
- `0x700520` = "at war" is inferred from its use, not decoded (0xd3fd60).
- The wing keys `region_to_assign` / `mission_to_assign` (wing+0x70 / +0x74, read by 0xf651b0) and the paradrop
  re-assignment at 0xf628c7 (wing +0x9e8 / +0x9ec / +0x9f0) were not followed.
