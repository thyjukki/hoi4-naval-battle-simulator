# A-WINGS: carrier wings, land-based joins and air reinforcement

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-WINGS, A-REINFORCE, A-EXTERNAL, A-SORTIE, A-INTERCEPT, A-DMG, A-LOSSES (`docs/mechanics/air.md`)
- **Confidence:** read from code only (static analysis; no in-game test). Per section below.

Method: `tools/re/` (`hoi4re.py`, `ctx.py`, `modmap.py`, `who.py`), script token ids rebuilt from the static token
registration 0x831c0 (as in [S-DOCTRINE](S-DOCTRINE-doctrines.md)), RTTI names from the vtables' complete object
locators, define names from the `NDefines` registration (`lea r8, global` then `lea rdx, name`). Source files:
`airwing.cpp` (0xf59760..0xf68544), `airmission.cpp` (0xf75a00..0xf87550), `strategicair.cpp` (0xc48740..0xc66ce1),
`production\deployment.cpp` (0xd0e740..0xd12fe0), `reinforcement.cpp`, `naval\naval_fire_exchange_air.cpp`.
Conventions as in [A-air-combat](A-air-combat.md): RVAs, fixed point ×1e5, `0x24ed730` rounds half away from zero.

## Objects

- **CAirWing** (`wing`, 0xa38 bytes, ctors 0xf59760 / 0xf59c00 / 0xf59f70 / 0xf5a1f0, vtables 0x297ada8 / 0x297ade0).
  A handle to a wing resolves to `wing+0x10`, so code that walks handle lists uses `resolved − 0x10`.
- **CAirWingPool** (`pool`, 0x48 bytes, ctor 0xf5a480, vtable 0x297ae38): a group of wings of **one sub-unit type at one
  base for one country**. `pool+0x18` base handle (resolved with 0x221f310 / 0x537980), `pool+0x20` the sub-unit
  definition (`common/units/air.txt` entry such as `cv_fighter`), `pool+0x28 / +0x34` wing list / count, `pool+0x40` the
  country's CStrategicAir. `wing+0x38` is the pool ("GetPool()" asserts at 0xf5f052).
- **CAirBase** (`base`, vtable 0x2958780): the "carrier base" of [A-air-combat](A-air-combat.md). `base+0x30[countryIdx]`
  is the per-country entry, whose `+0xb8 / +0xc4` is that country's pool list at the base (0xc536b0). `base+0x60` ship
  handle and `base+0x68 == 0` mean a carrier (else a land base); `base+0x78` deck capacity; `ship+0x730` is the
  carrier's base.
- **CStrategicAir** per country (`gamestate+0x690 → +0x30[countryIdx]`, lookup 0x6cf8c0): `+0x58 / +0x64` all its pools,
  `+0x70` wings that are not ready (filled by 0xf62750), `+0x90` country tag, `+0x188` manpower budget of one delivery.
- **Air mission object** inside the wing at `wing+0x90` (ctor 0xf75380, save writer 0xf87820 / reader 0xf84d40). Save
  keys give the field names: `+0x10` **`type`** (the assigned mission bits, `wing+0xa0`), `+0x14` **`executing_mission`**
  (`wing+0xa4`), `+0x18` `period`, `+0x20` `active`, `+0x28` the owning wing, `+0xd8` **`effectiveness`** (mission
  efficiency), `+0xe0` `effective_planes_count`. `+0xa8` is not saved; read as the disruption taken (see A-WINGS-EXT).
- Mission bits are `1 << index` in the order of `NAir.MISSION_FUEL_COSTS`: 0x1 air superiority, 0x2 CAS, 0x4
  interception, 0x8 strategic bomber, 0x10 naval bomber (naval strike), 0x20 drop nuke, 0x40 paradrop, 0x80 naval
  kamikaze, 0x100 port strike, 0x200 attack logistics, 0x400 air supply, 0x800 training, 0x1000 mines planting, 0x2000
  mines sweeping, 0x4000 recon, 0x8000 naval patrol, 0x10000 barrage, 0x20000 SAM (0x400 / 0x800 / 0x1000 / 0x2000 are
  confirmed by their own code: MIN_PLANE_COUNT_AIR_SUPPLY, training, the mine DLC gates).

## A-WINGS-TYPES: type keyword bits; the fighter tests read the sub-unit type

`0xf88330` (leaf, a token switch; there is no string table) maps a `type = { ... }` keyword token to its bit. The same
function is used for sub-unit `type` (parser 0x101e130 → `subunit+0x5a8`), equipment `type` (reader 0xc99410 →
`type+0x540`) and equipment `allowed_types` (`type+0x408`). Full map (token ids from 0x831c0):

| Bit | Mask | Keyword | Bit | Mask | Keyword |
|---|---|---|---|---|---|
| 0 | 0x1 | convoy | 20 | 0x100000 | air_transport |
| 2 | 0x4 | armor | 21 | 0x200000 | maritime_patrol_plane |
| 3 | 0x8 | motorized | 22 | 0x400000 | carrier |
| 4 | 0x10 | mechanized | 23 | 0x800000 | token id 0 (not a registered keyword; UNRESOLVED) |
| 5 | 0x20 | infantry | 24 | 0x1000000 | support |
| 6 | 0x40 | capital_ship | 25 | 0x2000000 | flame |
| 7 | 0x80 | submarine | 26 | 0x4000000 | amphibious |
| 8 | 0x100 | screen_ship | 27 | 0x8000000 | anti_air |
| 9 | 0x200 | floating_harbor | 28 | 0x10000000 | artillery |
| **10** | **0x400** | **fighter** | 29 | 0x20000000 | anti_tank |
| **11** | **0x800** | **interceptor** | 30 | 0x40000000 | rocket |
| 12 | 0x1000 | tactical_bomber | 31 | 0x80000000 | train |
| 13 | 0x2000 | strategic_bomber | **32** | **0x100000000** | **heavy_fighter** |
| 14 | 0x4000 | cas | 33 | 0x200000000 | emplacement_gun_ammo |
| 15 | 0x8000 | naval_bomber | 34 | 0x400000000 | ballistic_missile |
| 16 | 0x10000 | missile | 35 | 0x800000000 | nuclear_missile |
| 17 | 0x20000 | suicide | 36 | 0x1000000000 | sam_missile |
| 18 | 0x40000 | scout_plane | 37 | 0x2000000000 | missile_launcher |
| 19 | 0x80000 | railway_gun | 38 / 39 | 0x4000000000 / 0x8000000000 | land_cruiser / support_ship |

Bit 1 and `cavalry` (token 0x2780) have no case (0).

So `0x100000c00` = fighter | interceptor | heavy_fighter, and `0x100000400` = fighter | heavy_fighter (**not**
interceptor; air.md says "fighter or interceptor").

**Which mask the tests read.** Both read the **pool's sub-unit definition**, not the equipment:
```
isFighterType(wing)      = [[wing+0x38]+0x20]+0x5a8 & 0x100000c00      // 0xf63100 (A-INTERCEPT defenders)
sortieFighterBonus(airfx)= [[firstWing+0x38]+0x20]+0x5a8 & 0x100000400 // 0x1976f55..0x1976f63 (A-SORTIE)
```
`subunit+0x5a8` is the `type = { }` of the `common/units/air.txt` sub-unit (the S-STATS sub-unit type set, the ship's
`+0x628`). Vanilla: `cv_fighter` type fighter, `cv_cas` type cas, `cv_nav_bomber` type naval_bomber. TFB dev checkout:
`cv_fighter` `{ fighter }`, `cv_cas` `{ cas strategic_bomber }`, `cv_nav_bomber` `{ naval_bomber }`. So TFB's CAS
(equipment `type = { cas fighter }`) is **not** a fighter for interception or the sortie bonus.

**The variant's own type mask** (`variant+0x408`, built by 0xbd3a50 from 0xbdf910 / 0xbe1960) is used for matching
equipment to pools and sub-units, not by the naval air tests:
```
base      = type+0x540 if nonzero else archetype(type+0x4d8)+0x540          // 0xc95710 (no add_equipment_type)
allowed   = archetype+0x408 (allowed_types; 0 = no limit)
if type has type_override = <slot> (0xc95970):  m = module in <slot>; mask = (base | m.add_equipment_type) & allowed
                                                  & m.add_equipment_type & ~m.+0x88
else:  mask = (base | Σ modules.add_equipment_type(+0x78)) & allowed & ~Σ modules.+0x88    // +0x88 probably forbid_equipment_type
```
This is the variant-level counterpart of the hull mask 0xc95730 of [S-MIO-MATCH](S-MIO-equipment-matching.md) (which
uses `+0x548`, the default modules' add types). Vanilla plane designs get their role from the `fixed_main_weapon_slot`
module (`type_override`).

**Confidence:** high for the bit map and that the tests read the sub-unit; high for the variant mask outline, medium for
module `+0x88`.

## A-WINGS-STRIKE: which carrier wings strike

**AirFX per pool, not per carrier.** When a ship joins a fire-exchange group (`0x1c6a7c0`, group AddMember), if it is not
a submarine and has a base (`ship+0x730`) with an entry for its owner (0xc57400), the game walks that country's pool
list at the base (0xc536b0) and creates **one AirFX for each non-empty pool** (0x1976630 with the carrier member, then
0x1979aa0 adds the pool's wings), pushed into `group+0x58`. The A-air-combat notes "each carrier is an air object"
and "`airfx+0x5c` counts all wings on the carrier, fighters included" **disagree**: `airfx+0x5c` is the planes of that
pool only. Each pool is one sub-unit type, so a carrier with fighters, CAS and naval bombers has three AirFX
(medium-high: pools of the same sub-unit at one base are merged when 0xc505f0 is called with its merge flag, 0xc514f0;
whether two pools of one sub-unit can coexist on a carrier is UNRESOLVED).

**Strike list membership** (0x1979aa0 at 0x1979cc0, external 0x1979770 at 0x19798c5):
```
for wing in pool.wings:  airfx.allWings += wing;  airfx.offered(+0x58) += wing.planes(+0x7c)
    if wing.availableMissions(+0x30) & 0x190:  airfx.strikeWings += wing
```
`wing+0x30` is the wing's **available** missions, recomputed by 0xf667b0, not the player's mission:
```
avail = wing.equipmentMissions(+0x2c)                       // = variant+0x410 of the pool's first variant
if subunit.type & (fighter|heavy_fighter) and countryFlag(0x6f8410(country)+0x49): avail |= 0x80   // naval kamikaze
if DLC 0x13: subunit+0x1b8 > 0 → |= 0x1000; subunit+0x1c0 > 0 → |= 0x2000                         // mines
if 0xf61600(wing) > 0: avail &= ~0x800                     // training
if wing.planes < MIN_PLANE_COUNT_AIR_SUPPLY: avail &= ~0x400
```
`variant+0x410` (0xbdf456) is the union of the equipment type's and modules' `allow_mission_type`, minus gated bits
(mines without the planting/sweeping stats, DLC gates 0x1a / 0x24 on 0x4000 / 0x200). A wing's equipment must all share
it (asserts "IsAllMatchingMissionTypes").

So the strike list is every wing whose plane allows `naval_bomber`, `port_strike`, or (fighter-type sub-units of a
kamikaze-capable country) `naval_kamikaze`. It does not depend on the mission the player set. TFB: `cv_fighter` (allows
air_superiority, interception, naval_patrol) is not a strike wing unless the country can kamikaze; `cv_CAS` and
`cv_nav_bomber` are. A pool without strike wings returns at once from the strike (0x197b8d3, `count == 0`), so a pure
fighter pool never strikes; its planes still count for A-STACK and as A-INTERCEPT defenders.

**A second filter: the executing mission.** The stat averages (0x1978e30) and the loss allocation (0x197a1dc) use only
those strike wings whose **executing mission** matches:
```
mask = combat+0x10c (port strike) ? 0x100 : 0x90                       // 0x1978ec3..0x1978ed2
0x1978e30: wing counts in avgAttack/avgTargeting/avgAgility only if wing.executing_mission(+0xa4) & mask   // 0x1978fb5
0x197a1dc: losses go only to strike wings with executing_mission & 0x190; per wing ≤ min(planes, airfx+0x58)  // 0x197a24f
```
`wing+0xa4` is the mission object's `+0x14`, saved as `executing_mission` (0xf87880). If no strike wing of the pool is
executing a naval strike / kamikaze mission, the averages are over zero wings and the pool deals no damage. **Who sets
`executing_mission` for carrier wings is UNRESOLVED**: the only direct writes in airwing/airmission/strategicair code
clear it (0xf85554, 0xf85664, 0xf859fd, 0xf87796) or copy it (0xf676b0). In-game, carrier strikes clearly do damage, so
some path sets it (probably the air mission update choosing one bit of `type`, or the carrier/fleet mission code). This
needs an in-game read of `wing+0xa4` for carrier wings in a battle (tools/ingame).

**Confidence:** high for the strike-list rule and the per-pool AirFX; high that 0x1978e30 / 0x197a1dc filter on
`executing_mission`; the default value for carrier wings is UNRESOLVED.

## A-WINGS-EXT: land-based planes joining a naval battle

**Who calls the join.** The hourly air update `0xc57d30` (called from the gamestate hourly update at 0x1dfda5, before
the combat manager) first, for every naval combat (`vt+0x58 == 2`) whose strategic region is in an air hour,
**resets both sides' `+0x3e8` (planes joined) to 0** (0xc584e4..0xc584f9). Then it runs each mission whose
`executing_mission & 0x1f7fa` (not air superiority, interception or training) through `0xf82110` (only for regions in an
air hour, same region clock as A-CADENCE). 0xf82110 dispatches on the executing mission:
- naval bomber 0x10 and naval kamikaze 0x80 (0xf82337), naval patrol 0x8000 (0xf8257b): naval targets of the region
  (0xf7d590 → 0xf74b30, which include naval combats);
- port strike 0x100: port targets (0xf7ddd0);
- strategic bomber 0x8 and 0x10000 never call the join (0xf8265f).

For each target while planes remain it calls `0xf82ca0(mission, missionBit, target, &budget)`, which for a naval combat
picks the side of the mission's country and calls the side's `vt+0xf0` = **`0x16205c0(side, wing, &budget, eff, 0)`**.
Air superiority and interception wings never join naval battles (they are not run through 0xf82110).

**How many planes are offered** (`budget`, 0xf78360 at 0xf821c9; shared by all targets of the region that hour):
```
land base:     budget = round(wing.planes × mission.effectiveness(+0xd8))            // 0x24ed730
carrier base:  budget = trunc(round(wing.planes × effectiveness) × CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[fleet+0x4ec])
(base kind 1 (missiles): MISSILE_LAUNCHER_SLOTS path; kind 2: 1)
```
**The join** (0x16205c0; the 5th argument is 0 from this caller, which settles the A-air-combat open point):
```
cap  = 0x161b5f0(side) − 0                              // A-EXTERNAL cap
room = max(0, cap); if room <= 0 or side.joined(+0x3e8) >= room: return false
add  = clamp(min(budget, room − side.joined), 0, …);  side.joined += add;  budget −= add
airfx = existing external AirFX of the side holding this wing (side+0x100) or a new one (0x1976630, then
        0x1979770(airfx, wing, base, true): strike list by wing+0x30 & 0x190, +0x58 += wing.planes)
0x197a6a0(airfx, add, now, eff): airfx.planes(+0x5c) = Σ wing planes; airfx.offered(+0x58) = min(add, +0x5c);
                                 airfx+0xc0 = eff; airfx+0x70 = date; airfx+0x80 = 0
new AirFX: pushed into the side's first group (side+0xe8[0]).+0x58; wing gets the combat (0xf5af10)
afterwards 0xf64af0(wing, used): used × CLOSE_AIR_SUPPORT_EXPERIENCE_SCALE to the country's air experience, not
the wing's (corrected by A-XP)
```
- **Cap per air hour, not cumulative** (the reset at 0xc584e4): this **disagrees** with A-air-combat "`+0x3e8` is never
  decreased". Each air hour the side can take up to `cap` newly offered planes again.
- **Planes that strike**: the main strike uses `airfx+0x5c` for an external AirFX (0x197b906, no sortie), i.e. **all
  planes of the joined wings**, not the joined count `add`. The cap only limits the bookkeeping `+0x3e8` / `+0x58`
  (`+0x58` caps the per-wing losses in 0x197a1dc). Medium-high (read from code; surprising, worth an in-game check).
- **`airfx+0xc0` (external efficiency) = `0xf78160(mission, …, planes = budget, missionBit)`**, a **disruption**
  factor, not the mission efficiency:
```
if mission.disruption(+0xa8) == 0 or missionBit & 0x4400 (air supply, recon) or planes <= 0: eff = 1
else:
  s   = 0xf5e890(wing) stats (two of them, at +0x18 and +0x20 of its output), spd = 0xf60860(wing)
  den = planes × (1 + s20 × DISRUPTION_DEFENCE_DEFENCE_FACTOR) × (1 + s18 × DISRUPTION_DEFENCE_ATTACK_FACTOR)
               × (1 + spd × DISRUPTION_DEFENCE_SPEED_FACTOR)
  eff = clamp(1 − sqrt(disruption / den) / 3.162, 0.001, 1)        // 0x24ed7d0 = fixed sqrt; divisor 316200 (= √10 ×1e5)
```
  The mission `effectiveness` (+0xd8) only sets how many planes are offered. Which two wing stats `s18` / `s20` are
  (attack and defence, by the define names) and the exact speed normalisation are UNRESOLVED (0xf5e890 not decoded).
- Naval patrol wings can join; with no strike wing (naval_patrol only planes) their AirFX does nothing (empty strike list).

**Confidence:** high for the caller chain, the reset and the budget; medium for the disruption formula details.

## A-WINGS-REINFORCE: planes from the stockpile

**When.** Once per day: `country.daily` 0x6e76a0 → `deployment.daily` 0xd0ffc0(country+0xf70, country+0xf78, +0xf80,
+0xfd0) → 0xd10310. `country+0xf78` is the country's air CReinforcementStatus (vtable 0x29d1e48, base class
CEquipmentDistributable, created in the country ctor at 0x6cad9c). There is no hourly air reinforcement.

**How.** 0xd10310 takes the stockpile (`[country+0xf68]+0x200`, entries {variant, amount}, sorted by 0xbd2420), and for
each variant builds the list of distributables that want it (divisions, the air status if its "needed" slot 0x150d9f0 →
0xc4e8a0 is > 0, others), sorts them (0xd0bdd0 / 0xd0c650, priority rule not decoded) and offers the **whole remaining
amount** to each in turn (`vt+0x10`); what was taken is removed from the stockpile (0xe6eb90). No per-day rate cap.

The air status's deliver (0x150e150) calls **0xc5c0a0(countryAir, variant, amount)**:
```
countryAir.manpowerBudget(+0x188) = available manpower (0xcfc300(country+0x328))
if not variant.type.IsAir (0xc97430): return amount
listNew, listUpgrade = [], []
for pool in countryAir.pools(+0x58):
    if !(pool.subunit.type(+0x5a8) & variant.typeMask(+0x408)) or subunit.need(+0x310)[variant.type] <= 0: continue
    for wing in pool.wings:
        if score(wing) <= 0: continue                                   // 0xf61f70
        if CanReinforce(wing, variant): listNew += wing                  // 0xf5cc60
        elif CanUpgrade(wing, variant): listUpgrade += wing              // 0xf5d0a0
stable-sort both by score descending, ties by lower wing id (+0x1c)     // 0xc3fcd0, comparator 0xc40380
for wing in listNew while amount > 0: amount = Reinforce(wing, variant, amount)   // 0xf63fa0
for wing in listUpgrade while amount > 0: amount = Swap(wing, variant, amount)    // 0xf64190
score(wing) = priority(+0x28) × 1000 + (wing.assignedMission(+0xa0) ≠ 0 ? 500 : 0) + max(0, maxSize − planes)
```
`wing+0x28` is the wing's replacement priority (low / medium / high = 0 / 1 / 2, default
`NCountry.REINFORCEMENT_AIRBASE_PRIORITY_DEFAULT`, ctor 0xf597c2).

**CanReinforce (0xf5cc60):**
```
maxSize = 0xf5f000(wing) > 0 and maxSize > wing.planes
and !wing+0x79 and wing+0x64 >= wing+0x60 and wing.transferTarget(+0x48) is null      // ready, not relocating
and wing.reinforceTimer(+0xa20) expired (0x1a2f6b0)       // REINFORCEMENT_DISABLING_DURATION_IN_LAND_CARRIER_TRANSFER h
and variant.missions(+0x410) == wing.equipmentMissions(+0x2c)
and variant+0x42c == wing+0xa28                           // meaning UNRESOLVED (0 by default in the ctors)
and NOT (base is a carrier (base+0x68 == 0, ship at +0x60) and the ship's task force is in combat (0xc00000: tf+0x1b4 > 0))
and Preference(wing, variant)                             // 0xf632c0
```
**Preference (0xf632c0, `wing+0x9cc`, default 1):** 0 "only outdated" (`AIR_REINFORCE_OBSOLETE_ONLY`): the variant's
type level (`type+0x3c4`) must not exceed any plane already in the wing (own country's variants only via `+0x424`); 1
"any" (`AIR_REINFORCE_ANY`): always; 2 "only current" (`AIR_REINFORCE_ONLY_NEW`): not obsolete (`+0x424 == 0`) and
not below any plane in the wing; 3 "only same" (`AIR_REINFORCE_ONLY_SAME`): a variant already in the wing (missiles:
`0x100eb00`). So by default **any variant** of the sub-unit's needed equipment with the same allowed missions is used;
there is no conversion.

**Reinforce (0xf63fa0):**
```
n = min(trunc(amount), maxSize − planes)
k = variant.manpower(+0x420); if k > 0: n = takeManpower(countryAir, k × n, k) / k       // 0xc4f860, from +0x188
AddAirplanes(wing, variant, n, exile ? GIE_EXILE_AIR_START_EXPERIENCE : 0)            // 0xf5add0: pool += n, planes = pool count
return amount − n
```
All missing planes come at once (limited by stock and manpower).

**Swap (0xf64190 + 0xf66dd0):** for a wing that already holds an older variant (0xbd7dc0) — this path has **no combat
check** — `n = min(amount, planes)` new planes are added and `wing+0x88 += n`; at the end of the same daily
distribution 0xc63d90 → 0xf66dd0 returns `+0x88` planes from the wing to the stockpile (0xe6f910). The plane count is
unchanged; only the variant mix changes.

**Wing target size (0xf5f000):** the sub-unit's wing size for the base kind (0x101b080): land base `land_air_wing_size`
(`subunit+0x600`); carrier `carrier_air_wing_size` (+0x604); a carrier whose design has a module with flag `+0x2d0`
(0xc3afb0) `mega_carrier_air_wing_size` (+0x608, by elimination); submarine carrier `submarine_carrier_air_wing_size`
(+0x60c). Sub-units with type bit 33 use the base capacity `base+0x78` instead. **Deck capacity is not checked**: a
carrier already over capacity still refills each wing to its wing size (10 in vanilla and TFB), and a wing above its
size gets nothing.

**Answers.**
- A carrier's wings get **no planes while its task force is in any combat** (0xf5cc60 at 0xf5cda2). They refill on the
  first day tick after the battle (and after the 48 h transfer timer if they just moved).
- Land-based wings have no combat check: a wing that joined a naval battle as an external AirFX is refilled at the day
  tick, and the AirFX sees the new planes at its next air step (`0x1976ca0` sums `wing+0x7c`).
- If a variant swap happens to a carrier wing during a battle, the AirFX's averages (0x197d4a0, 0x1978e30) use the new
  mix; the count does not change.

**Confidence:** high for the daily chain, the carrier-in-combat block, the sizes and the per-wing amount; medium for the
preference details (level field `type+0x3c4`, `+0x424`), the manpower helper and the cross-distributable priority
(UNRESOLVED).

## Defines and constants involved

| Name | Role |
|---|---|
| `NAir.REINFORCEMENT_DISABLING_DURATION_IN_LAND_CARRIER_TRANSFER` (48) | wing reinforce timer `+0xa20`, set in the wing ctors 0xf599d9 / 0xf59e0c / 0xf5a169 / 0xf5a3b9 |
| `NCountry.REINFORCEMENT_AIRBASE_PRIORITY_DEFAULT` (1) | wing priority `+0x28` (0xf597c2) |
| `NAir.MIN_PLANE_COUNT_AIR_SUPPLY` | air supply removed from `wing+0x30` below it (0xf6686c) |
| `NNavy.CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO` | carrier wings' mission budget (0xf78476) |
| `NAir.MISSILE_LAUNCHER_SLOTS` | missile base budget (0xc54e73) |
| `NAir.DISRUPTION_DEFENCE_ATTACK_FACTOR / _DEFENCE_FACTOR / _SPEED_FACTOR` | external efficiency (0xf781f1, 0xf781f9, 0xf78278) |
| `NAir.GIE_EXILE_AIR_START_EXPERIENCE` | experience of planes added to an exile wing (0xf6410b) |
| hard-coded 1000 / 500 | wing reinforcement score (0xf61fad, 0xf61fa5) |
| hard-coded 316200 (÷, fixed) | external efficiency `sqrt(x)/3.162` (0xf78309) |

## How it was verified

Static reading only. Cross-checks: the keyword bits for convoy / capital_ship / submarine / screen_ship / carrier /
missile_launcher / support_ship match S-STATS and S-DOCTRINE; the mission bit order matches the masks used by the
code (0x400 air supply with MIN_PLANE_COUNT_AIR_SUPPLY, 0x800 training, 0x1000 / 0x2000 mines); field names from the
save writer tokens (`type`, `executing_mission`, `effectiveness`); preference names from the loc keys
`AIR_REINFORCE_OBSOLETE_ONLY / ANY / ONLY_NEW / ONLY_SAME` (rdata order and vanilla localisation). Suggested in-game
checks (tools/ingame): log `wing+0xa4` of carrier wings during a battle; a land-based naval bomber wing larger than the
join cap, to see whether all its planes strike; a carrier battle across midnight with stock in the stockpile.

## Differences from the current spec

- **A-WINGS** (air.md): strike wing = plane allows `naval_bomber` or `port_strike` (and `naval_kamikaze`, which the game
  only adds to fighter-type sub-units of kamikaze-capable countries). Fighter-type is the **sub-unit** `type` (TFB
  `cv_cas` is `{ cas strategic_bomber }`, so TFB CAS is not a fighter); the sortie-bonus test is fighter or
  heavy_fighter, not interceptor. Air groups are one per carrier **and plane sub-unit**, not one per carrier.
- **A-SORTIE**: `planes` is the pool's planes; fighters in their own pool are not strikers (air.md "Strikers include
  fighters" holds only for a fighter-type sub-unit that also has strike wings). The fighter sortie bonus applies to a
  fighter-type pool.
- **A-DMG / A-LOSSES**: only strike wings whose `executing_mission` is naval strike or kamikaze (port strike in port
  strikes) enter the averages and take losses. Until the setter is found, a `BattleRules` variant "all strike wings
  execute naval strike" (default on) keeps the current behaviour.
- **A-EXTERNAL**: the cap resets every air hour; external groups strike with all planes of their wings; `externalMissionEfficiency`
  is the disruption factor above (1 without enemy interceptors), while the mission efficiency scales the offered planes.
- **A-REINFORCE**: daily at the day change, full refill up to the wing size from the stockpile (any variant with the same
  allowed missions, by default), manpower-limited; carrier wings only when their task force is in no combat; deck
  capacity ignored.

## UNRESOLVED

- Who sets `executing_mission` (`wing+0xa4`) of carrier wings (A-WINGS-STRIKE). **Answered in game (2026-10-20, the
  air hook of `tools/ingame/battle_logger.js` on 0x197b890, 10 TFB battles):** in every air step of every carrier, the
  `cv_nav_bomber` wings had `executing_mission` 0x10 (naval strike) with assigned mission `type` 0, the `cv_fighter`
  wings 0x1 (air superiority); one air object per pool (50 bombers with 5 strike wings, 30 fighters with none, which
  never struck), and the bomber pools never refilled from a 1000-plane stockpile during the battles. The setter
  itself is still not decoded.
- Whether two pools of one sub-unit can sit on one carrier (then the carrier has two AirFX of that type).
- Bit 23 of the keyword map (token id 0); module `+0x88` (probably `forbid_equipment_type`).
- `variant+0x42c` / `wing+0xa28` (must be equal to reinforce; set from the variant at 0xc4fe41 via 0xf67120).
- The two wing stats of the disruption formula (0xf5e890 output +0x18 / +0x20) and the speed term 0xf60860.
- Priority among distributables in the daily distribution (0xd0bdd0 / 0xd0c650), the stockpile order (0xbd2420), the
  swap test 0xbd7dc0, the preference level field `type+0x3c4` and `variant+0x424`.
- The meaning of the ready timer `wing+0x60 / +0x64` and `wing+0x79`.
