# LOG: logging battles from the running game

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-HOUR (hour counter), S-NIGHT, S-WEATHER; the calibration workflow
- **Confidence:** functions and fields read from code; the behaviour notes confirmed in the running game (TFB dev checkout,
  `light_attack_vs_torp_cruiser_battle_start.hoi4`, 2026-10-19)

The tooling in `tools/ingame/` (Frida agent `battle_logger.js`, runner `run_battles.py`) hooks the functions below in a
running hoi4.exe. This file records what it relies on and what the logged battles showed.

## What the game does

### Reaching the game objects

```
gamestate  = [0x332f260]                         ; global pointer (passed to the hourly update 0x1df400)
countries  = gamestate+0x310, count +0x31c       ; the loop in gamestate.daily 0x1d4810 (0x1d59e0)
fleets     = country+0x278, count +0x284         ; fleets.daily in country.daily 0x6e76a0 (0x6e7be2)
armies     = country+0x290, count +0x29c         ; army.daily (0x6e7b85)
taskForces = fleet+0xb8, count +0xc4             ; 0xd54840
ships      = taskForce+0x348, count +0x354
```

Lists are `{T** items; ...; int32 count at +0xc}`.

Units carry their save id as a handle `{uint32 type, uint32 id}` at `+0x8`: ships have type 51 (the save's
`id={ id=N type=51 }`), equipment 70, task forces 61. `0x221f310(handle*)` resolves a handle to the object through
per-type tables (`0x3451dc0[type]` for type < 0x64, else the maps at `0x3451db0`/`0x3451db8`); it returns null for a
deleted object. A fire-exchange member holds its ship as a handle at `member+0x8` (the shot loop resolves the target
with `0x221f310(member+0x8)` at 0x197417f).

### Shot loop and damage arguments

- `0x19740c0(shooterMember, targetMember, weapon, shooterShip, convoyShooter?, ...)`: `rcx` the shooter's member (its
  charge `+0xf0 + 8 × weapon` drives the loop), `rdx` the target member, `r8d` weapon (0 heavy, 1 torpedo, 2 light), `r9`
  the shooting ship (null for a convoy, then the 5th argument is used). Returns 1 when the target sank.
- `0x1970a90` writes the shot profile to its second argument (`rdx`).
- `0xc3e0e0(ship, strDamage, orgDamage, flag)`: both damages fx5 int64 in registers.
- `random_fixed 0x2233db0(int64* out, file, int line)`: writes `hash % 100000` to `*out`. The file argument does not
  point at a terminated file name (reading it gave neighbouring paths), so the logger records the line only. Seed at `0x3452524`, counter at `0x3452520` (incremented per
  call); the byte at `0x3452529` makes it log every call ("random_fixed: %s:%d = %s"), which the console command
  `randomlog` toggles.

### Console commands

- `combat_analyzer` (handler 0x243be0): `<TAG1> <TAG2> <OOB1> <OOB2> [runs] [days]`. It deletes all units ("Deleted N
  armies, fleets and railway guns"), loads the two OOBs from `history/units/` (0x702f80), runs a diplomatic action
  (0x1103030, presumably the war), gives each unit an order towards the other side (0x1233dd0, medium), runs the hourly
  update 0x1df400 until the combat ends and prints wins, org damage and manpower losses. It only walks the countries'
  **armies** (`country+0x290`), so it is land-only; there is no naval equivalent.
- `run <file>` (handler 0x27e220) feeds each line of a file through the console's input; `gamespeed <0-5>` (0x25d2e0)
  sets the speed (0 pauses). `navalcombatlog` and `combattest` are not in the release build.
- Launch option `-continuelastsave` loads the save named in `continue_game.json` (user folder).

## How it was verified

Three runs of the CL3 battle-start save (two with its own seed, one with `multiplayer_random_seed` edited), each
started with `-continuelastsave`, then `ai`, `weather`, `gamespeed 5`:

- **The battle is a pure function of the save.** The two runs with the save's seed logged the same 24,126 shots,
  roll for roll. The edited seed changed the very first target pick and the whole battle (395 h instead of 341 h).
  `multiplayer_random_seed` therefore seeds the combat dice; editing it gives independent repetitions of one setup.
- **Combat hour counter (`combat+0x44`).** It is 1 during the first hour's `Update` and `EndHour` increments it
  afterwards (value at `Update` = value on entering `EndHour`; +1 on leaving). This settles the open "0 or 1" question:
  1. Shots started in counter hour 3.
- **Sunk and escaped ships stay in their group** with state 0x20 / 0x10; all 150 members were listed every hour until
  the end. This settles the open question about ships leaving the group.
- **No darkness with weather off.** `combat+0xb8` (from `0xf19660`, which reads the weather manager at
  `gamestate+0x688`) was 0 in all 341 and 395 hours, across 14 to 16 nights in May in the Black Sea. The light-gun hit
  chance of the same shooter design was identical at every hour of day (9.66735 % from 14:00 through 04:00 and on).
  So after `weather` or `disable_weather` the `night` static modifier never applies.
- Hit chance in the shot profile is stored ×100 (9.66735 for 9.67 %); a light shot with roll 0.09671 missed and one with
  0.01926 hit, consistent with `100·U < hc`.

From the per-shot logs of 10 more seeds (17,699 hits, TFB defines: randomness 0.2, STR 0.5, ORG 1, torpedo crit 0.2 /
×4.25, `ship_experience_malus_min = { naval_damage_factor = -0.1 }`, both fleets at 0 experience, ARMY_EXP_BASE_LEVEL 2):

- **`naval_damage_factor` counts twice.** The shot profile's damage is already `attack × (1 + naval_damage_factor) ×
  pierce` (light 17.8605 = 19.845 × 0.9; the attack getter 0xc34a50 adds 0x1b7), and every hit is multiplied by
  `max(0, 1 − positioning penalty + naval_damage_factor − naval_defense_factor)` again. All non-crit light hits at full
  positioning fit `strDamage = profile × 0.9 × (1 + 0.2 × (U − 0.05)) × 0.5` exactly (first hit: 17.8605 × 0.9 ×
  1.140232 × 0.5 = 9.16429, logged 9.16428); torpedo hits fit the same times the torpedo factor 0.15 with no residual.
  This settles the open "counted twice?" question for the experience malus; admirals and doctrines feeding 0x1b7 would
  count twice as well.
- **Organization damage** is `d × (1 − hpBefore / maxHp) × ORG` on every normal hit and every crit without a damaged
  part (0 mismatches over 17,569 hits). The only mismatches are crits that damaged a critical part, whose multipliers
  replace the crit multiplier (S-CRIT-PART).
- Torpedo crit chance in the profile was 0.2 = `COMBAT_TORPEDO_CRITICAL_CHANCE` (0.25) × (1 + the target's torpedo crit
  modifier, −0.2 here); 81 crits in 427 hits.
- **Escape speed includes country modifiers.** The escape progress (`member+0x130`) of a retreating ROM cruiser grew by
  0.65448 a day at counter hour 66, rising 0.015/24 an hour. The S-RETREAT-SPEED terms give 0.06 + 66 × 0.015/24 +
  30.72 × 1.15/100 = 0.45453; the rest, 0.2, is TFB's technology `smoke_generator` (`naval_retreat_speed = 0.2`), which
  both countries had in the save. BUL's escaping cruisers show the same +0.2. Retreat decisions themselves matched the
  engine (same start hours and HP at the decision); only the escape took 31–35 hours instead of 42–48.

## Differences from the current spec

Both fixed in the engine and spec on 2026-10-19:

- S-NIGHT / S-WEATHER: battles fought with weather off have no night. The engine kept night on (the [ASSUMED]
  20:00–06:00 darkness) when `WeatherEnabled` was off; now `WeatherEnabled = false` also means no darkness.
- S-DMG / S-STATS-COMBAT: `naval_damage_factor` enters twice, in the attack and in the hit multiplier; the engine applied
  it once (`DamageRules.DamageMultiplier`), so with TFB's malus it dealt 1/0.9 of the game's damage. The armor getter
  is decoded the same way (`naval_defense_factor`) and now applies too; this battle cannot confirm it (TFB's experience
  blocks have no defense factor).
- Before the fixes, on the CL3 battle (10 game seeds against 200 simulator seeds, night off by hand): the game left ROM
  2804 HP (sd 80) and BUL 7090 HP (sd 170) after 336 h; the simulator 2272 and 6895 after 316 h.
- Still open: country `naval_retreat_speed` (S-RETREAT-SPEED, combat-modifier layer). With both fixes and the
  smoke generator's 0.2 stood in by `BASE_ESCAPE_SPEED = 0.26`, 200 simulator seeds give 338 h, BUL 7056 HP, ROM 2801
  HP, 0.9 ROM sunk, against the game's 336 h, 7075 (sd 161), 2802 (sd 72), 1.2 over 12 seeds.

The S-HOUR counter reading matches `BattleSession` ("tick is 1 in battle hour 0").
