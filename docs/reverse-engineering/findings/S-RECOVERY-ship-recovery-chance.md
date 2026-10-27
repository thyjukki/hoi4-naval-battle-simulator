# S-RECOVERY: the ship recovery chance (`naval_ship_recovery_chance`)

- **Game version:** 1.19.3
- **Executable SHA-256:** 7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5
- **Spec items affected:** S-RECOVERY (new), S-MANPOWER (the refill after a sinking), S-DOMINANCE
  (NAVAL_DOMINANCE_SHIP_RECOVERY_CHANCE), S-REPLAY (a new roll line)
- **Confidence:** high for the chance, the factor, the roll and the recovered HP (read in full; two probe battles in
  game, 71 sinkings, every logged value reproduced); medium for the dominance term's province lookup 0xe796b0 (read, and
  one logged case where it did not apply at sea); low for what 0xd72580 does with the recovered ship outside the battle.
- **Why it matters:** Japan's focus (via the dynamic modifier `JAP_navy_naval_ship_recovery_chance`), the static
  modifier `naval_repair_support` (scaled by a task force's repair support ratio), naval dominance
  (NAVAL_DOMINANCE_SHIP_RECOVERY_CHANCE 0.1) and five unit medals give `naval_ship_recovery_chance`. The question was
  whether the simulator must model it.

**Result:** in 1.19.3 no ship is ever recovered. The chance is multiplied by the country's
`naval_ship_recovery_chance_factor` itself, not by 1 + it, and nothing in the game or in TFB sets that factor, so the
final chance is always 0. The game still makes the roll whenever the chance before the factor is above 0.

## Where: the task force's remove-sunk-ship function 0xd77ff0

The combat's sink handler 0x1975480 sets the member's state to sunk, takes the carrier's air objects off the group's
list (A-ORDER) and calls `0xd77ff0(taskForce, ship, …, sinkerTag)` (at 0x1975879). It starts with the recovery:

```
owner   = 0xbb48f0(tf+0x1dc > 0 ? tf+0x1dc : tf+0x1d8)                       // the task force's country
chance  = 0xd6fa50(tf, 0x27f)                                                 // the task force's support entries
        + owner.modifiers(0x27f)                                              // naval_ship_recovery_chance (0x55e360)
province = tf+0x1f0; region = province+0xc8
if region and (tag = 0xe796b0(province)) > 0 and 0x1003f50(region+0xe8, tag) == 1:
    chance += NNavy.NAVAL_DOMINANCE_SHIP_RECOVERY_CHANCE                      // 0xd780cf
if chance > 0:                                                                 // 0xd780ed
    final = trunc(chance × owner.modifiers(0x280))                           // naval_ship_recovery_chance_factor, 0xd7810d
    draw  = random_int(taskforce.cpp, 0xb31)                                  // the generator of random_fixed
    roll  = mix(draw) % 100000                                                // 0xd78140..0xd781bb
    if roll < final:                                                           // 0xd781c1
        hp = trunc(tf.vt+0x108() × NNavy.SUPPORT_SHIP_RECOVERY_BASE_STRENGTH_FACTOR)   // 0xd781e4
        0xc3dc10(ship, hp)                                                     // set its strength (at most its maximum)
        0xd72580(tf, &ship, 1, tf, 1, 2, 2, 0, 0, 0)                          // the ship leaves the task force
        return                                                                 // 0xd78247 → 0xd78b76
// not recovered: remove the ship (0x1b33b0 on tf+0x348), return part of its crew to the pool, the pride of the fleet
// on_action, delete the ship, … and refill the task force 0xd7a7e0 (0xd78b71, S-MANPOWER)
```

- **Modifier ids** (0x55f7f0 and the script token table 0xafdb8 share the id constants 0x27d6ae8 / 0x27d6aec):
  0x27f `naval_ship_recovery_chance`, 0x280 `naval_ship_recovery_chance_factor`. The container getter 0x55e360 returns
  the stored sum (0 when absent), so a country with no factor gets a final chance of 0.
- **Task force support, 0xd6fa50(tf, out, id, tooltip):** for each entry of the map `tf+0x640` (the task force's
  support types): `ratio = clamp(0x196a360(entry) / (entry+0x28 × 1), 0, 1)` (the support it has over the support it
  needs), `out += ratio × 0x196a3f0(staticModifier(entry+0x30)[id])`. These are the static modifiers
  `naval_general_support` and `naval_repair_support` of `00_static_modifiers.txt` ("scaled based on support ratio");
  `naval_repair_support` has `naval_ship_recovery_chance = 0.1`. What 0x196a360 / 0x196a3f0 count (support and repair
  ships) is not decoded; the simulator has no support ships.
- **Dominance, 0xe796b0(province):** returns `[[province+0xc0]+0xc8]` (presumably the province's state and its owner's
  tag) or no tag (0xbb3e00) when `province+0xc0` is empty. The tag checked is that country's, not the task force's
  (the organization regain 0xc395a0 checks the task force's own tag). A sea province has no state, so the dominance
  bonus never applies to a battle at sea; only a task force in a port province could get it.
- **The roll:** `mix` is the bit mix at 0xd78140..0xd781a1 (multiplies by 0x7eb8da44, 0x6d22f31b, 0x1b56c4e9,
  0x92d68ca2 with xor-shifts by 8 and the constants 0x10d8365e, 0x68e31da4), bit 31 cleared, then the remainder of
  100000 (0xd781a1..0xd781bb). Code: `GameBattleLog.RecoveryRoll`.
- **The recovered HP:** the task force's vt+0x108 is its summed maximum strength over the ships still in it. The
  define's comment ("Percentage of strength of max strength a recovered ship gets") suggests the ship's own maximum;
  the code reads the task force's.

## Logged in game: the probe

`battle_logger.js` hooks 0xd77ff0 and the instructions above (`run_battles.py --force-recovery C [--force-recovery-factor F]`
forces the chance before the factor, and optionally the factor, in the game itself). Save
`jukki_usa_vs_bc_japan_no_doctrines_battle_start.hoi4`, seed 1, `output/ingame/recovery_probe/`:

1. `forced050` (chance forced to 0.5): 28 sinkings, both countries' factor read as **0** every time, final chance 0,
   no ship recovered (rolls as low as 3,063 of 100,000). Without the forcing the chance before the factor was 0 at every
   sinking, including two ROM ships sunk while ROM's organization regain showed it controlled the region (0.22 an hour
   instead of 0.2): the dominance term did not apply at sea.
2. `forced050_factor1` (chance 0.5, factor 1): 43 rolls, 18 ships recovered, each exactly when the roll was below
   50,000. Every recovered HP equals 0.01 × the summed maximum HP of the ships still in the task force (the recovered
   one included, earlier losses and recoveries not), capped at the ship's own maximum: the first BUL light cruiser
   (132 HP) of the 41-ship task force came back with 120.52 HP, the next ones 1.32 HP less each, a ROM destroyer with
   its full 50. All 43 rolls equal `mix(draw) % 100000` of the logged integer draw on line 0xb31.
3. A recovered ship's member stays in the combat with state sunk (0x20), its HP and its organization, regaining 0.2
   an hour like other ships that are not fighting; it never fires or is targeted again in that battle. After the
   battle ended (hour 192) the recovered BUL ships, now in another task force, fought a second battle four hours later
   (ship 5713, a super-heavy battleship recovered with 57.62 of 770 HP, sank there again).

## Rules for the engine

1. At every sinking (a shot, or inside an air strike before its ace rolls): `chance = country naval_ship_recovery_chance`
   (support ships and port dominance not modelled; 0 at sea anyway for dominance). If `chance > 0`, roll
   (`RollKind.ShipRecovery`); recovered if `raw(roll) < raw(trunc(chance × naval_ship_recovery_chance_factor))`.
2. A recovered ship: HP = min(its maximum, trunc(SUPPORT_SHIP_RECOVERY_BASE_STRENGTH_FACTOR × Σ maximum HP of its side's
   ships not sunk, itself included)) (one task force per side); out of the battle as if sunk; no task force refill.
3. Results report it as recovered, not sunk. Code: `RecoveryRules`, `BattleSession.LeaveBySinking`, `AirPhase.Strike`.
4. Replay: the integer draw on line 0xb31 is `RollKind.ShipRecovery` with U = mix(draw) % 100000 / 100000.

## Unresolved

- 0xd72580: where the recovered ship goes (a new task force, its orders, the repair); outside the battle.
- 0x196a360 / 0x196a3f0: how a task force's support ratio is counted; needs support and repair ship hulls.
- Which container the unit medals' `naval_ship_recovery_chance` reaches (country or task force); irrelevant while the
  factor is 0.
