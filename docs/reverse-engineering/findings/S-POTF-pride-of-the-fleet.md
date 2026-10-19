# S-POTF: pride of the fleet in naval combat

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-POTF (new), S-STATS-COMBAT, S-STATS-COUNTRY, S-ADMIRAL-MOD, S-CRIT, A-CRIT, S-XP, S-TARGET-WEIGHT,
  A-TARGET, S-DMG-APPLY (sinking)
- **Confidence:** read from code only. High for every pseudocode block (each term read off the disassembly) and for the
  reader list (every call of the getter 0x6f70f0, of the two predicates built on it, and every `+0x250` access in the
  country, unit, ship, task-force and naval combat code). Medium where marked: that a task force never leaves a running
  naval combat except at its teardown or by moving away, the meaning of bit 37 in the eligibility test, and which medal
  holder the experience sum reads.

Builds on [S-STATS-COMBAT-GETTER](S-STATS-design-stats.md#s-stats-combat-getter-the-attack-and-armor-getter-0xc34a50-fully-decoded)
(the attack/armor getter, where the PotF term was first seen), [S-STATS-COUNTRY](S-STATS-COUNTRY-modifier-sources.md)
(the ship modifier sum 0xc38760 and the modifier name method), [S-ADMIRAL](S-ADMIRAL-leader-combat.md) (container A
`leader+0x418`, efficiency scaling) and [S-XP](S-XP-experience.md#s-xp-gain-experience-gain-in-combat) (the per-hour gain
0xc338a0). Method: `tools/re/` (`hoi4re.py`), callers of 0x6f70f0 and of the predicates 0xc3b0e0 / 0xd70270, a scan for
`+0x250]` in 0x6c0000–0x720000, 0xb00000–0xe00000, 0xfa0000–0x1010000, 0x1300000–0x13f0000, 0x15a0000–0x1630000,
0x1960000–0x19a0000, 0x1ad0000–0x1ae0000 and 0x1c60000–0x1c70000, the static token table 0x831c0 and the modifier
registration 0x55f7f0 joined by script, RTTI for object names; checked against vanilla and TFB
`common/modifiers/00_static_modifiers.txt`, `common/units`, `history/units` and a TFB 1936 save. Fixed point 1e5 with
truncating multiply (findings README) unless fx15 is stated.

## Summary: what the simulator must do

When one ship of a side is its country's pride of the fleet (PotF):

1. **The PotF ship itself** carries the static modifier block `pride_of_the_fleet` in every read of its ship modifier sum
   (0xc38760), whatever its member state (waiting, joining, fighting, retreating, escaped). Vanilla and TFB (identical):
   `critical_receive_chance = -0.5`, `experience_gain_navy_unit_factor = 0.25`. Effects:
   - **Gun, depth-charge and torpedo crits against it** (S-CRIT, 0x19742a2): chance × `(1 + shooter naval_critical_score_chance_factor + against + target critical_receive_chance)`,
     so −0.5 halves it (TFB with `damage_control_1` and `_2`: `1 − 0.2 − 0.5 = 0.3`).
   - **Air crits against it** (A-CRIT, 0x197af20): chance × `max(0, 1 − critical_receive_chance)`, so −0.5 makes it
     ×1.5 (TFB with damage control: ×1.7). The sign is the opposite of the surface rule; this is the game's behaviour.
   - **Experience gain**: `+0.25` in the gain factor (section 3).
   - Any other key a mod puts in the block reaches the PotF through the same sum. Read the block from the game files.
2. **Every ship of the PotF's country in the battle** (the PotF included), while the PotF ship is afloat and its task force
   is in the same combat, adds the side's **commanding admiral's** container A values to its attack Σ and armor Σ in the
   attack/armor getter 0xc34a50: `naval_has_potf_in_combat_attack` to light, heavy, torpedo and sub (depth-charge) attack;
   `naval_has_potf_in_combat_defense` to armor (also for the target's armor in the piercing check, although that read
   passes no admiral). The PotF's member state does not matter (it may be waiting, retreating or escaped). No commanding
   admiral, no term; country sources of the two keys never count. Vanilla/TFB source: admiral trait `navy_media_personality`
   (0.1 / 0.1, scaled by leader efficiency like any A entry). Read live at every getter call; nothing is cached.
3. **When the PotF sinks** (inside the shot that sinks it): from the next getter call on, both terms above are gone
   (the ship is deleted, the country's handle is cleared). Nothing else changes in the battle: no organization, morale,
   positioning or targeting effect on either side. Country-level only: `pride_of_the_fleet_country`
   (`war_support_factor 0.05`) is removed, `pride_of_the_fleet_sunk_temporary` (`war_support_factor -0.10`) applies for
   `PRIDE_OF_THE_FLEET_LOST_TEMP_MODIFIER_DURATION` (30) days, the on_action `on_pride_of_the_fleet_sunk` fires (vanilla:
   achievement flags only). The simulator can record the event and ignore the rest.
4. **Nothing else reads it in battle.** Surface target weights (0x1971440), the target pick (0x1971cc0), air target
   weights (0x197a700), retreat decision and roll, escape speed, positioning and screening have no PotF term. Enemies do
   not focus the PotF.
5. **Eligibility**: a ship whose sub-unit `type` has `capital_ship`, or a carrier (sub-unit `carrier`, or carrier hull
   equipment); bit 37 (`missile_launcher`) too. Vanilla: battleship, battlecruiser, heavy cruiser, carrier; TFB adds super
   heavy battleship and super heavy carrier. Not light cruisers, destroyers or submarines. One per country.
6. **Save**: in the country block, `pride_of_the_fleet={ id=<ship id> type=51 }` (written only when set) followed by
   `pride_of_the_fleet_date_lost="1.1.1.1"` (never lost). The ship block has no key.

## 1. Readers of the pride of the fleet

### The handle and its setter

```
country+0x250 : handle {id, type 51} of the PotF ship (null handle = none)
0x6f70f0(country)          = &country+0x250                                   // leaf
0x712700(country, ship)    : country+0x250 = ship ? ship.handle : null; 0x6dade0(country)   // the only writer besides
                                                                                // the country ctor 0x6c9cc0, reset 0x6e2de0
                                                                                // and the save reader 0x709d36
```

Two predicates wrap the getter:

```
0xc3b0e0 ship.IsPrideOfTheFleet: country(ship+0x728 -> tf+0x1d8 owner tag).0x250 == {ship+8, ship+0xc}
0xd70270 tf.HoldsPrideOfTheFleet: s = resolve(country(tf+0x1d8)+0x250); s and s+0x728 == tf
```

The owner is `tf+0x1d8` (owner tag), not the operating country `tf+0x1dc` that 0xc3acc0 prefers.

### Every reader

| Site | Function | What it does | Battle effect |
|---|---|---|---|
| 0xc35087 | 0xc34a50 attack/armor getter | PotF in the same combat → own commanding admiral A `0x1d1`/`0x1d2` (section 2) | yes |
| 0xc387e2 | 0xc38760 ship modifier sum | `ship == owner+0x250` → static `pride_of_the_fleet` (+0xb0) (section 3) | yes |
| 0xc3b333 | 0xc3b240 experience add | same test → static block's `0x35` flat and `0x34` factor (section 3) | yes (XP) |
| 0x196f607 | 0x196f500 CFEXMember ctor | caches `IsPrideOfTheFleet` in the member's `SCachedInfo` (+0x18) at +0x28 = member+0x40; saved as `pride_of_the_fleet=yes` in the combat's member block (writer 0x1976579, reader 0x1974fa7) | no gameplay reader in the combat code (UI / save only) |
| 0xd78408, 0xd78629 | 0xd77ff0 remove sunk ship | PotF lost bookkeeping, on_action, stat (section 6) | no (country level) |
| 0xc304cc | 0xc30380 CShip dtor | `0x712700(owner, null)` unless gamestate+0xa3a (state being cleared, set by 0x1d0e30) | ends the terms |
| 0xc3d309 | 0xc3d090 RefreshStats | calls 0x6f70f0 and discards the result (rax overwritten at 0xc3d319) | none (dead read) |
| 0x6dba8d | 0x6dade0 country modifier rebuild | adds `pride_of_the_fleet_country` while the handle resolves (section 6) | no (war support) |
| 0xd76c75 | 0xd76bd0 scuttle ship | scuttling the PotF = lost (0x70c760) | outside battle |
| 0xc3c2bc | 0xc3c060 naval accident (from the mission update 0xfbcb90, `navalmission.cpp`) | chance × `NAVAL_ACCIDENT_CHANCE_REDUCTION_ON_POTF`, accident crit × `NAVAL_ACCIDENT_CRITICAL_HIT_CHANCE_REDUCTION_POTF` | outside battle |
| 0x1359420 / 0x1356850 | `set_pride_of_the_fleet_command` IsValid / Execute | section 5 | outside battle |
| 0x1365dad | 0x1365d60 delete-ship command | deleting the PotF first unsets it (command with null ship) | outside battle |
| 0x1a68d88 | 0x1a68d40 AI | an AI country without a PotF picks one (candidates by 0xc33a50) | outside battle |
| 0x13115bb | 0x1310de0 AI (CAIInteriorMinister vt+0x70 → 0x1306560) | per-ship weight 1.0 for the PotF, 0.5 for a ship in its task force (purpose not decoded) | outside battle |
| 0x7192cb / 0x709d36 | CCountry Write 0x7191b0 / ReadMember 0x705ce0 | save (section 7) | — |
| UI | 0x18d35c0, 0x18d9f80, 0x18fe470, 0x1900650, 0x198eba0, 0x198fac0, 0x17ef0c0, 0x1bbbdd0, 0x1bbd630, 0x1beaba0, 0x1beb270, 0x1beca70, 0x1becfc0, 0x1bed5f0, 0x1d0ba00, 0x1d0c8d0, 0x1d0cd50, 0x1d0d030, 0x1d36240, 0x1d3b430, 0x1df6580, 0x1df6900, 0x1e13050, 0x1e29de0, 0x1e2d400, 0x1f08600, 0x1f08ba0, 0x1f8ff34, 0x1f90540, 0x1f96700, 0x1f98820 | icons, tooltips, the assign/unassign popup | — |

No other `+0x250` access in the scanned ranges is on a country (the shot-profile chunk's `[r13+0x250]` at 0x1971111 is
the ship's heavy-gun hit chance factor stat). The static blocks (`0x55b160()` / `0x5574b0()` → global 0x3330098)
`+0xb0` / `+0xb8` / `+0xc0` are read only at 0xc38811, 0xc3b34c, 0xc3b372, 0x6dbac0, 0x6dbd39 and the tooltip 0x1beaba0.

### Target selection has no PotF term

- Surface weight 0x1971440..0x1971604: base by type (0xc3af10 capital, 0xc3b210 screen, 0xc3b220 sub, 0xc3af20 carrier),
  ×15 / ×2, `× (2 − hpRatio)`, state ×1.5 / ×0.5; no other input.
- PickTarget 0x1971cc0 and air pick 0x197a700: their callees (0xc3a580, 0xc3a5d0, 0xc3ac00, 0xc3b220, 0x1971440,
  0x19723b0, 0x1972c00; 0xc3a470, 0xc3ac60, 0xc3af10, 0xc3af20, 0xc3b210, 0x1622d40, 0x1973d30, 0x24f08a0, …) contain
  no PotF read, and none of them reads the member's cached flag (no `+0x40` / `+0x28` byte read).
- The retreat decision 0x19717f0, the roll 0x1975280 and the escape speed 0x196fe80 read modifiers through 0x1972570 →
  0xc38760, so the PotF static block reaches them; vanilla and TFB put no retreat key in it.
- The attack getters (0xc3a5d0 / 0xc3a580 / 0xc3ac00 / 0xc3a890 → 0xc34a50) are called by the shot profile 0x1970b48
  (damage; armor 0xc3a520 for the piercing tier) and otherwise only as "has this attack" tests (PickTarget 0x1971cc0,
  member update 0x1973521, the retreat chunk 0x19719fc, the group update 0x1c6b560, 0x1972900). The getter returns 0 when
  the base is 0 and `max(0, base × (1 + Σ))` otherwise, so the PotF term changes damage and armor only.

## 2. In the same combat

```
// 0xc34a50, after the admiral adjusters (0xc35060..0xc351c1); tf = the getter's ship's task force
combat = 0xd6d0c0(tf)                                // first combat in tf+0x1a8 (count +0x1b4), if its type (vt+0x58) is 2
if combat:
    h = country(tf+0x1d8)+0x250                      // 0xbb48f0, 0x6f70f0: the shooter's (or target's) owner
    if h not null and (p = 0x221f310(h)):            // resolve: null if the ship object is gone
        assert p+0x728 "Pride of the fleet has no task force!"   // ship.cpp line 0x254
        if 0xd6d0c0(p+0x728) == combat:              // the PotF's task force is in this combat
            side = 0x15c3010(combat, tf+0x1e0)       // the side holding this task force's country
            cmd  = side+0x130                        // commanding admiral; NOT the getter's admiral argument
            if cmd: Σ += cmd+0x418 .Get(kind == 4 ? 0x1d2 : 0x1d1)
```

Ids by registration + token table (0x55f7f0 → 0x5574d0, tokens 0x831c0): 0x1d1 `MODIFIER_NAVAL_HAS_POTF_IN_COMBAT_ATTACK`
token 0x3b59 `naval_has_potf_in_combat_attack` (0x5e7aff), 0x1d2 `..._DEFENSE` token 0x3b5a
`naval_has_potf_in_combat_defense` (0x5e7f24), both flags 3 (positive is good: scaled by leader efficiency).

**The cases.** The test is on the PotF's task force, never on its fire-exchange member:

- (a) **Retreating, still in the battle** (member state 8): the ship is in its task force, the task force in the combat →
  the term applies. The static block applies too.
- (b) **Escaped** (state 0x10, written at 0x19739bd after `member+0x130 >= 1.0`): the escape code writes only the member
  state (and for a carrier removes its AirFX entries from the group, 0x1c6cc50). `ship+0x728` is not written anywhere in
  the naval combat code (0x15c0000–0x1630000, 0x1960000–0x19a0000, 0x1c60000–0x1c70000). The task force stays in the
  combat: units leave a combat only through `CUnit::RemoveCombat` 0xc03e00, called by the naval combat teardown 0x15c2980
  (CNavalCombat vt+0x38, every unit of both sides) or by the generic leave/join routines 0x13e3570 / 0x13e3990 (a unit
  that starts moving, 0xbfb4a0, or joins another combat). So an escaped PotF keeps the term for its country until the
  battle ends (medium: the movement paths were not traced to rule out a mid-battle move order).
- (c) **Sunk**: the shot loop 0x19740c0 → sink handler 0x1975480 (member state 0x20 at 0x1975bea) → 0xd77ff0 (at
  0x1975879) → the ship object is deleted at 0xd78a64 (`vt+0`, 0xc30cc0 → dtor 0xc30380) → `0x712700(owner, null)`:
  `country+0x250` is cleared and the country modifiers rebuilt. All within the shot that sank it. Even before that the
  handle would resolve to null (0x221f310 returns null for a deleted object). From the next getter call on, no term.
- (d) **Not yet active** (waiting, state 1) or **joining** (state 4): the task force is in the combat from the moment it is
  added (start of battle, or the join path CNavalCombat vt+0x90 0x15c53f0 → 0x13e3d90 for reinforcements, medium), so the
  term applies from that hour.
- Live or cached: live. 0xc34a50 runs in every shot profile (shooter attack with the commanding admiral, target armor
  with admiral 0); each call re-resolves the handle. The member's cached flag (member+0x40) is not read by any of this.

Other consequences of the condition:

- It is the owner of the getter's ship's task force whose PotF counts. Ships of an allied country on the same side use
  their own country's PotF; an enemy PotF never matters.
- A PotF in another task force of the same country counts if that task force is in the same combat.
- `naval_has_potf_in_combat_*` enters only Σ of 0xc34a50, not the per-hit multiplier, so it counts once (unlike
  `naval_damage_factor`).

## 3. The static block and experience gain

### The ship modifier sum 0xc38760

```
// 0xc387cc..0xc38818
if {ship+8, ship+0xc} == country(ship+0x728 -> tf+0x1d8)+0x250:     // 0x6f70f0, owner tag
    v += static(0x55b160())+0xb0 holder .Get(id)                     // 0xc327e0
```

No state, combat or location test: the PotF carries the block in every read, in battle and out, retreating or escaped. The
owner is `tf+0x1d8`. The block is loaded by name in 0x60d830 (`pride_of_the_fleet` → +0xb0, 0x60ea80).

### The experience add 0xc3b240 and its caller 0xc31840

The S-XP "global block via 0x55b160 in one branch" is this block: two reads of static +0xb0 (0xc3b34c for 0x35, 0xc3b372
for 0x34), taken only when the ship is its owner's PotF.

```
// 0xc31840(ship, gain /*fx5*/, mode)   mode 0 combat (0xc3bde0), 1 mission, 2 training
g15 = fx15(gain)                                                     // 0x24ed3f0, truncating
o15 = 0xc3b240(ship, &out, &g15, mode, tooltip = 0)
d   = fx5(o15)                                                       // 0x24ed580
ship+0x708 (xp) = min(xp + d, ship+0x710 /*NAVY_MAX_XP*/); ship+0x800 = 1   // level modifiers rebuilt later (S-XP-EFFECT)
if d > 0 and mode != 2:                                              // the ship's captain (officer record), not ship XP
    c = d × CAPTAIN_EXPERIENCE_ON_SHIP_MULT × (1 + medals × CAPTAIN_EXPERIENCE_ON_SHIP_PER_MEDAL_MULT)   // 0xc318c9, 0xc31916
    if feature 0x3a: 0x13e8e50(ship+0x848, c)                        // medals = count of [ship+0x8f8] medal list (+8, +0xc)

// 0xc3b240(ship, out, &g15, mode, tooltip)
C = country(tf+0x1dc > 0 ? tf+0x1dc : tf+0x1d8)+0x5b8                // operating country container (as 0xc3acc0)
M = 0xc0dc90(ship+0x8f8)+0x1b0                                       // the ship's medal object, holder +0x1a0 (see below)
flat   = C.Get(0x35) + M.Get(0x35)                                   // 0xc3b2bb..0xc3b2ec
factor = C.Get(0x34) + M.Get(0x34)                                   // 0xc3b2f0..0xc3b31e
if ship.handle == country(tf+0x1d8)+0x250:                           // 0xc3b321..0xc3b38c, owner's PotF
    flat   += static.pride_of_the_fleet.Get(0x35)                    // vanilla/TFB: 0
    factor += static.pride_of_the_fleet.Get(0x34)                    // vanilla/TFB: 0.25
factor += C.Get(mode 0: [ship+0x66c], 1: [ship+0x668], 2: [ship+0x664])   // 0xc3b6a4..0xc3b6da
out = ((g15 + fx15(flat)) × fx15(1 + factor)) >> 15                 // 0xc3b6dd..0xc3b71a (sar 15 = floor)
```

So the full combat gain per hour is `xp += fx5(floor15((fx15(g) + fx15(flat)) × fx15(1 + factor)))`, capped at
`NAVY_MAX_XP`, with `g` from 0xc338a0 (S-XP-GAIN). Sources:

| Term | Id / source | Notes |
|---|---|---|
| flat | 0x35 `experience_gain_navy_unit` (flags 2) | country (ideas, doctrines, dynamic modifiers, …), ship medals, PotF block |
| factor | 0x34 `experience_gain_navy_unit_factor` (flags 3) | country, ship medals (vanilla medals' `ship_modifiers` give 0.05 each), PotF block 0.25 |
| factor | id in `ship+0x66c` | the country's `experience_gain_<sub-unit>_combat_factor` for the ship's sub-unit (e.g. `experience_gain_destroyer_combat_factor`); country only |

- Ids by registration + token table: 0x34 `MODIFIER_XP_GAIN_NAVY_UNIT_FACTOR` token 0x323e
  `experience_gain_navy_unit_factor` (0x5e1fd1, flags 3); 0x35 `MODIFIER_XP_GAIN_NAVY_UNIT` token 0x323d
  `experience_gain_navy_unit` (0x5e23f6, flags 2). Confirms S-XP's guess and STC's table.
- `ship+0x664` / `+0x668` / `+0x66c` = sub-unit definition +0x5e4 / +0x5e8 / +0x5ec (the sub-unit copy starts at
  `ship+0x80`): modifier ids built in the sub-unit definition constructor 0x1018080 from the strings
  `experience_gain_` + sub-unit name + `_training_factor` / `_mission_factor` / `_combat_factor` (0x1483b40 at
  0x101844e / 0x101867d / 0x101885e). Tooltip lines `EXP_TRAINING_BONUS`, `EXP_MISSION_BONUS`, `EXP_COMBAT_BONUS`.
- `ship+0x8f8` is not a leader: it is the `+0x20` scoped pointer of the ship's `CUnitHistory` at `ship+0x8d8` (RTTI
  `.?AVCUnitHistory@@`, ctor 0x1445e00, dtor 0x1446450), i.e. the medal object built by 0xf9e530 / 0x14463a0 (holders
  +0x20, +0xe0, +0x1a0; `0xc0dc90` asserts the pointer is set). The UI shows this record as the ship's captain
  (`captain_window`, `CAPTAIN_EXPERIENCE_GAIN_TOOLTIP`, `PER_MEDAL`). The holder read is +0x1a0; that it is the medals'
  `ship_modifiers` is inferred from `common/unit_medals/00_default.txt` (medium).
- **No admiral term**: neither the commanding nor the fleet admiral, and no task-force, terrain or experience holder; the
  function does not call 0xc38760. The ship modifier sum would add the PotF block for id 0x34 too, but nothing reads 0x34
  through it.
- The PotF test uses the owner `tf+0x1d8`, the modifier country the operating one (`tf+0x1dc` if set).

## 4. Air crit sign

```
// 0x197ac80, target member r15 (ship r13 = 0x1622d40(r15), convoy r12 = 0x535c40(r15)); 0x197aea5..0x197af7a
rel    = min(reliability, 1)                                         // 0xa3b460 (convoy 0x1ad8180)
base   = COMBAT_BASE_CRITICAL_CHANCE × (1 + COMBAT_ARMOR_PIERCING_CRITICAL_BONUS) × (1 − rel)
v      = 0x1972570(r15, 0x18c)                                       // member getter: ship → 0xc38760(ship, 0x18c, side+0x130)
                                                                     //   + the screening bonuses
f      = 1 − v; if f < 0: f = 0                                      // 0x197af20..0x197af2d (cmovs)
chance = base × f
if base > 0: chance = max(chance, 0.001)                             // 0x197af4c..0x197af59: gated on base, applied after f
crit   = U < chance                                                  // random line 0x487
```

The target's `critical_receive_chance` (0x18c, `MODIFIER_NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR`, token 0x3b0f, flags 1)
comes through the ship modifier sum, which holds the PotF block. So the PotF's −0.5 gives `f = 1.5`: air crits against the
PotF are 1.5 times as likely (vanilla and TFB put no `critical_receive_chance` in `screening_bonus` or
`capital_screening_bonus`). The surface shot loop adds the same read with the other sign (`1 + score + against + receive`,
0x19741e2..0x19742ca, target member r14), so the two rules disagree in the game itself. A-air-combat.md's reading is
confirmed. Small detail for A-CRIT: the 0.001 floor applies after the factor and only when the base chance is positive.

## 5. Who can be the pride of the fleet

### The eligibility test 0xc33a50 (UI button, AI)

```
// 0xc33a50 CanBecomePrideOfTheFleet(ship)
if not 0x1aeb50(0xf): false                                          // feature/DLC flag: bit 4 of 0x332f248 (PotF feature)
t = ship+0x628                                                       // sub-unit `type` bits (0xf88330)
if not (t bit 6 capital_ship
        or t bit 22 carrier or 0x10109b0(ship+0x40, 1<<22)          // = 0xc3af20 inlined: sub-unit or equipment
        or t bit 37 or 0x10109b0(ship+0x40, 1<<37)):                 //   with `type = carrier` (bit 37: missile_launcher)
    false
g = ship+0x804                                                       // government_in_exile_tag (ship save key)
if g > 0 and g != tf+0x1d8:
    if tf+0x1d8 == 0 or not 0xbb52f0(g, tf+0x1d8): false            // same original tag (gamestate+0x340 table)
true
// 0x10109b0(list, mask): any {equipment*, amount} in list+0x20 (count +0x2c, 16 bytes) with equipment+0x408 & mask and amount > 0
```

- The ship panel shows the "Assign as Pride of the Fleet" button only if 0xc33a50 holds (0x1bed715 → hide at 0x1bed840).
  The click (0x1becfc0) does nothing if a PotF exists and political power < `PRIDE_OF_THE_FLEET_UNASSIGN_COST`; otherwise
  it opens the set / replace (`REPLACE_SHIP_PRIDE_OF_FLEET`) / unset popup (0x1f083d0), whose accept builds the command
  (0x1f08b69 → 0x134a550).
- So the limit is by **sub-unit type**, not role or hull: `capital_ship` (vanilla/TFB battleship, battlecruiser,
  heavy_cruiser; TFB super_heavy_battleship) or carrier (vanilla carrier; TFB super_heavy_carrier; or a ship with carrier
  hull equipment). Screens (light cruiser, destroyer) and submarines cannot. The ship's own equipment counts only for the
  carrier bits; `capital_ship` is tested on the sub-unit only.

### The command `set_pride_of_the_fleet_command` (token 0x3a99)

```
// command: +0x28 ship handle (null = unassign), +0x30 country tag; ctor 0x134a550
IsValid 0x1359420:
    c = country(cmd+0x30); if c+0x484 <= 0: false
    if resolve(c+0x250) and politicalPower(0x6cf890(c)+0xe0) < PRIDE_OF_THE_FLEET_UNASSIGN_COST: false
    if cmd.ship == c+0x250: false
    if cmd.ship is null: true
    s = resolve(cmd.ship); if not s: false
    return owner(s) == cmd.country or (both set and 0xbb52f0(owner(s), cmd.country))
Execute 0x1356850:
    if resolve(c+0x250): add political power −PRIDE_OF_THE_FLEET_UNASSIGN_COST (0xba7380)
    0x712700(c, resolve(cmd.ship))
```

The command has **no type check**; the UI and the AI enforce 0xc33a50. Other users: the delete-ship command 0x1365d60 and
the task-force disband 0x1d0cd50 unassign first; the AI 0x1a68d40 (feature 0xf, country without a PotF) scores every
eligible ship (0xc33a50) and assigns one.

### Script and history

- No script effect, trigger or console command exists: the only PotF strings are the static blocks, the defines, the
  loc keys, the on_action `on_pride_of_the_fleet_sunk` and the tokens `pride_of_the_fleet` (0x3a9a),
  `pride_of_the_fleet_date_lost` (0x3b0d), `set_pride_of_the_fleet_command` (0x3a99, the command class) and
  `sunk_pride_of_the_fleet` (0x3e2d, a career-profile statistic). The tokens' globals are only touched by their
  destructors; the ids appear only in the readers and writers listed here.
- History / OOB: `ship = { … pride_of_the_fleet = yes … }` (vanilla `history/units`: 62 such entries across the start
  dates and DLC variants, on `battleship` 28, `heavy_cruiser` 27 and `battle_cruiser` 7 definitions). The ship reader 0xc3c5a0 sets `ship+0x814 = 1` on the key (0xc3c8f2; the value is not checked). The
  ship init 0xc34280 (from InitPostRead 0xc3ad50 and the deployment 0xd0fc86) consumes the flag: if feature 0xf, it runs
  the 0xc33a50 tests and on failure logs `Setting a ship that can't become potf as potf.: <name> <country>`, then calls
  `0x712700(owner, ship)` **anyway**. A script `load_oob` of such a file presumably goes through the same reader.

## 6. Losing the pride of the fleet

```
// 0xd77ff0 (remove a sunk ship from its task force; called by the combat sink handler 0x1975480)
...manpower returned (0x1a1)...
if ship.IsPrideOfTheFleet():                                        // 0xd78408
    0x70c760(owner):  owner+0x260 = now (gamestate+0x468); 0x6dade0(owner)
    if sinker tag > 0: fire on_pride_of_the_fleet_sunk (ROOT = owner, FROM = sinker)   // 0xd7857b..0xd785bb
if 0xcc6980(sinker) and ship.IsPrideOfTheFleet(): 0xcd6800(sinker)  // career statistic `sunk_pride_of_the_fleet`
...ship removed from tf+0x348 (0xd7825f), 0xd74cb0, 0xc3d900...
ship->vt+0(1)                                                        // 0xd78a64: dtor 0xc30380 → 0x712700(owner, null)
                                                                     //   → country+0x250 = null, 0x6dade0(owner)
// scuttling (0xd76bd0) also calls 0x70c760 for the PotF.

// 0x6dade0 country modifier rebuild ("country.calc_modifier"), into country+0x5b8:
if resolve(country+0x250):                     add static +0xb8 `pride_of_the_fleet_country`        // 0x6dba8d..0x6dbaca
country+0x270 = 0
if country+0x260 != 1.1.1.1 (0x29c77f8) and PRIDE_OF_THE_FLEET_LOST_TEMP_MODIFIER_DURATION > 0
   and days(now) − days(country+0x260) < PRIDE_OF_THE_FLEET_LOST_TEMP_MODIFIER_DURATION:
    add static +0xc0 `pride_of_the_fleet_sunk_temporary`; country+0x270 = 1                          // 0x6dbb48..0x6dbd48
```

Vanilla and TFB: `pride_of_the_fleet_country = { war_support_factor = 0.05 }`,
`pride_of_the_fleet_sunk_temporary = { war_support_factor = -0.10 }`. Both are country modifiers (they would reach every
ship's modifier sum if a mod put naval keys in them). On the sinking hour the rebuild in 0x70c760 still sees the handle,
so for a moment both apply; the dtor's rebuild drops the first. Vanilla `on_pride_of_the_fleet_sunk` only sets achievement
flags (ENG / SIA). Nothing in the naval combat code reacts: the sink handler 0x1975480 has no PotF test, and no
organization, morale or retreat change follows.

## 7. Save format

Country block (writer CCountry 0x7191b0, vtable 0x27e7360 slot +0x10; reader 0x705ce0, jump table at 0x709cf2):

```
		command_power=18.018
		pride_of_the_fleet={ id=47 type=51 }          # 0x7192d4, only when country+0x250 is set; handle writer 0x2220180
		pride_of_the_fleet_date_lost="1.1.1.1"        # 0x7192f7, always; "1.1.1.1" = never lost
```

- Reader: `pride_of_the_fleet` → handle reader 0x1b2240 into `country+0x250` (0x709d36); `pride_of_the_fleet_date_lost` →
  the date object's persistent base at `country+0x268` (0x709d4a; the CDate at `country+0x258`, hours at `+0x260`,
  default 0x29c77f8 = 1.1.1.1, set in the ctor 0x6ca05a).
- `type=51` is the ship handle type (`ship+8` / `+0xc`, LOG). The ship block carries no PotF key; a save only needs the
  country key pointing at a ship of that country. Checked in a TFB 1936 save (`jukki_usa_vs_bc_japan_battle_start`):
  GER 47, ITA 142, JAP 381, ENG 645, SOV 1097, USA 1214, … each `pride_of_the_fleet={ id type=51 }` after
  `command_power`, countries without one write only the date.
- For `tools/ingame/make_save.py`: write or replace `pride_of_the_fleet={ id=<ship id> type=51 }` before
  `pride_of_the_fleet_date_lost` in the side's country block (remove it for a side without a PotF). The running
  `naval_combat` block's members cache the flag (`pride_of_the_fleet=yes` in each member's cached info, 0x1976579), but the
  script removes that block and the game rebuilds it.

## Defines and constants involved

| Name | Global | Read at | Role |
|---|---|---|---|
| `NNavy.PRIDE_OF_THE_FLEET_UNASSIGN_COST` (100) | 0x3332110 | 0x1359473, 0x1356891; UI 0x1bed049, 0x1bed754 | political power to replace or unassign |
| `NNavy.PRIDE_OF_THE_FLEET_LOST_TEMP_MODIFIER_DURATION` (30) | 0x33321a8 | 0x6dbb59, 0x6dbcfe | days of `pride_of_the_fleet_sunk_temporary` |
| `NNavy.NAVAL_ACCIDENT_CHANCE_REDUCTION_ON_POTF` (0.01) | 0x3332b18 | 0xc3c2d2 | accident chance factor (missions, not battle) |
| `NNavy.NAVAL_ACCIDENT_CRITICAL_HIT_CHANCE_REDUCTION_POTF` (0.01) | 0x3332bc0 | 0xc3c342 | accident crit factor (missions, not battle) |
| `NMilitary.CAPTAIN_EXPERIENCE_ON_SHIP_MULT` (1.55) | 0x3337a98 | 0xc318c9 | captain XP per ship XP |
| `NMilitary.CAPTAIN_EXPERIENCE_ON_SHIP_PER_MEDAL_MULT` (0.1) | 0x3337b48 | 0xc31916 | captain XP per medal |
| `NNavy.NAVY_MAX_XP` | — | ship+0x710 | XP cap (S-XP) |
| static `pride_of_the_fleet` | +0xb0 | 0xc38811, 0xc3b34c, 0xc3b372 | the PotF ship's block |
| static `pride_of_the_fleet_country` | +0xb8 | 0x6dbac0 | country block while a PotF is assigned |
| static `pride_of_the_fleet_sunk_temporary` | +0xc0 | 0x6dbd39 | country block after losing it |
| hard-coded 0x29c77f8 | — | 0x6ca05a, 0x6dbb48 | the date 1.1.1.1 = never lost |

## How it was verified

Static reading only. Every caller of 0x6f70f0 (30), 0xc3b0e0 (16) and 0xd70270 (10) was classified; the `+0x250` scan
covered the country, unit, ship, task-force, mission and naval combat code; the static block slots were traced through
both accessors (0x55b160, 0x5574b0) and the global 0x3330098. Modifier names come from the registration table joined
with the static token table (no hand-copied names). Object names from RTTI (`CShip` vtable 0x2957f28, `CCountry`
0x27e7360, `CFEXMember::SCachedInfo` 0x2a20ee8, `CUnitHistory` 0x29c1868, `CAIInteriorMinister` 0x29abad0). The save
layout was checked in a TFB save. Not tested in game. A logged battle with a PotF carrying no admiral trait would show
torpedo crits against it at 0.5 × the profile's chance (TFB with damage control: 0.3 ×) in the replay's roll log
(S-REPLAY); one with a `navy_media_personality` admiral would show every own attack and armor value +0.1 × base.

## Differences from the current spec

- **New S-POTF** (`docs/mechanics/surface.md`): a side may name one ship of its fleet as the country's PotF; eligibility
  as in section 5; the effects as in the summary. The spec currently lists pride of the fleet as not modelled
  (S-STATS-COMBAT line on the attack/armor getter, S-ADMIRAL "Not modelled").
- **S-STATS-COMBAT**: "pride of the fleet (read only from the commanding admiral)" is now decoded: the term needs the
  country's PotF to be afloat with its task force in the same combat (any member state, escaped included), uses the side's
  commanding admiral even for the target's armor (admiral argument 0), and ends at once when the PotF sinks.
- **S-ADMIRAL-MOD**: "the own admiral's A `naval_has_potf_in_combat_attack`/`_defense` needs the pride of the fleet in
  the battle, which is not modelled" → condition as above; still A only (country values stay without effect, S-STATS-COUNTRY).
- **S-CRIT**: the PotF's own `critical_receive_chance −0.5` (static block) joins the `1 + score + receive` factor.
- **A-CRIT** (`air.md`): "`critical_receive_chance` is not modelled. In the game it acts as `(1 − v)`" → confirmed; with a
  PotF modelled the air crit chance against it is ×`max(0, 1 − v)` = 1.5 (game behaviour). The 0.001 floor comes after
  the factor and only when the base chance is positive (the spec applies it before).
- **S-XP**: the gain has modifiers the spec omits: `xp += (gain + flat) × (1 + factor)` in fx15 (floor), with
  `flat = experience_gain_navy_unit` and `factor = experience_gain_navy_unit_factor` from the operating country and the
  ship's medals, plus the PotF block (factor 0.25) for the PotF, plus the country's
  `experience_gain_<sub-unit>_combat_factor`. No admiral term. S-XP-experience.md's "captain/leader at `ship+0x8f8`" is
  the ship's unit-history medal object.
- **S-TARGET-WEIGHT / A-TARGET**: match (no PotF term).
- **S-STATS-COUNTRY**: `pride_of_the_fleet_country` and `pride_of_the_fleet_sunk_temporary` are country modifiers
  (war support only in vanilla and TFB); no battle effect.

## Unresolved

- Whether any order can move a task force out of a running naval combat (0xbfb4a0 callers from the combat manager
  0xbb7660 / 0xbb77e0 / 0xbb85d0 / 0xbb99d0 were not followed); the escaped case (b) assumes not.
- Bit 37 of the sub-unit/equipment type set in 0xc33a50 / 0xc3af20 (`missile_launcher` per 0xf88330; no ship uses it).
- Which of the medal object's holders (+0x20, +0xe0, +0x1a0) holds `ship_modifiers`; the experience sum reads +0x1a0.
- `country+0x484` (must be > 0 for the command) and the purpose of the AI weight in 0x1310de0.
- Readers of the member's cached flag (`SCachedInfo+0x28`) outside the naval combat code (combat UI presumably).
