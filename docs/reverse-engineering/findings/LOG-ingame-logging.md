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
- Country `naval_retreat_speed` (S-RETREAT-SPEED, S-STATS-COUNTRY), modelled since 2026-10-19: researches carry
  `countryModifiers`, and the CL3 scenario gives both sides `smoke_generator`. With TFB's defines and no override, 200
  simulator seeds give 338 h, BUL 7056 HP, ROM 2801 HP, 0.9 ROM sunk, against the game's 336 h, 7075 (sd 161), 2802
  (sd 72), 1.2 over 12 seeds. Time from the retreat decision to the escape, ROM ships by decision hour (simulator /
  game): before 100, 36.0 / 36.1 h; 100–200, 33.7 / 33.7; 200–300, 31.7 / 31.7; later, 30 / 30. One BUL ship in the
  game took 54 h (seed 3, ship 5755): its progress grew by 0.43 a day instead of the formula's 0.69, which fits a
  speed penalty from a damaged critical part (the parts carry `naval_speed_factor` -0.2 to -0.9; S-CRIT-PART, not
  modelled). No simulated escape took more than 37 h.

The S-HOUR counter reading matches `BattleSession` ("tick is 1 in battle hour 0").

## Second case: a generated mixed fleet (jukki_usa_vs_bc_japan, 2026-10-19)

`tools/ingame/make_save.py` wrote `data/jukki_usa_vs_bc_japan.json` into the CL3 battle-start save
(`jukki_usa_vs_bc_japan_battle_start.hoi4`): BUL got 8 `39_battleship_pure_la`, 27 `39lc_la` and 6 `shbb_4battery_2aa`,
ROM 12 `39_battlecruiser_pure_la`, 54 `dd_3_la` and 5 `shbb_4battery_2aa`, both with the scenario's researches as TFB
technologies, Always Engage, Never repair. One game seed (1) was logged.

What the save showed:
- **The battle restarts when the naval combat is removed.** With the `naval_combat` block deleted and both task forces
  still in province 7906, the game started a new battle in the first hour. It made ROM the attacker (side 0); the
  template's battle had BUL. In the simulator the attacker order changes nothing measurable here (200 seeds each way).
- **Ship experience is the ship's `experience=` line** after `strength=` (history ships carry it, e.g. Italian ships
  25; the CL3 ships had about 6.3 after the battle, as logged). `held_officer={ experience=… }` is the officer's: written
  there, the ships started at 0, so the logged run had 0 experience on both sides.
- **Start values and attacks match.** Max HP, organization and manpower of every design were the simulator's
  (`NavySimulator list-ships`), and every logged shot profile equals the simulator's attack × 0.9 (TFB's 0-experience
  `naval_damage_factor`) × piercing factor (heavy 156.8 / 112, light 33.6 / 32.4 / 13.8, torpedo 35.7; piercing 60.5 /
  48.4, 8.8 / 7.071 / 6.38).
- **TFB's `screening_bonus` per-weapon factors apply.** Capital ships hit 1.4–1.7 times (heavy) and 1.13–1.22 times
  (light) as often as simulated, screens exactly as often. Dividing the logged hit chances by the base and profile
  factor gives modifier sums of 1.9857 (heavy) and 1.5356 (light) for BUL's capitals at screening 0.64285, 2.4130 and
  1.7328 for ROM's at 0.97144: exactly `1 + (0.3 + 1) × screening + 0.15` and `1 + (0.3 + 0.3) × screening + 0.15`.
  TFB's `screening_bonus` has `naval_hit_chance = 0.3`, `naval_heavy_gun_hit_chance_factor = 1` and
  `naval_light_gun_hit_chance_factor = 0.3`; the engine applied only the first. So the per-weapon modifiers are read
  through the member modifier getter `0x1972570` like `naval_hit_chance` (S-HIT-MODS). Fixed in the engine
  (`MemberModifierRules`): every simulated hit chance now lies within 1 % of the logged one.
- Not modelled and present on both sides: `damage_control_1` and `_2` (`critical_receive_chance` and
  `naval_critical_effect_factor` −0.1 each; S-CRIT-PART).

Outcome after the fix, the game run against 200 simulator seeds at 0 experience with ROM attacking: 208 h against 231
(175..421); ROM kept 8693 HP (simulator 7945, sd 484) and lost 7 destroyers sunk (4.3), 11 escaped (13.8); BUL kept 384 HP
(667, sd 156) with 6/8 battleships, 13/27 light cruisers and all 6 SHBB sunk (5.5, 9.9, 4.3). The one game run went 1.5
to 1.8 sd worse for BUL than the simulator's mean; a batch of seeds is needed before calling it a difference.

Two more seeds with the scenario's experience (20, written as the ship's `experience=`; the game showed 20 in the first
hour) and the scenario set to weather off, against 200 simulator seeds: 174 and 181 h (simulator 190, sd 18; 31 % of
runs end by 181 h); BUL lost 27 and 29 ships sunk (25.5, sd 3.1) and kept 435 and 250 HP (474, sd 130); ROM kept 8532 and
8272 HP (8129, sd 568) and lost 5 and 4 destroyers sunk (4.2). Hit chances again within 1–2 %. Escape progress of every
retreating ship (BB, SHBB, light cruiser, destroyer; 2,300 ship-hours) grew by exactly the S-RETREAT-SPEED formula
every hour, with TFB's escape defines, `smoke_generator` and `screening_bonus.naval_retreat_speed` (0.25 × screening)
for capitals only. One thing to watch: all 6 of BUL's SHBB were sunk in all three game runs (11 of 11 retreating SHBB
sunk before escaping in the two runs at 20 experience), while the simulator sinks all 6 in 24 % of runs and 75 % of
retreating SHBB.

Ten more seeds (3–12), so 12 game runs at 20 experience against the same 200 simulator seeds:

| | game (12) | simulator (200) |
|---|---|---|
| hours | 188.6 (163..214) | 189.9 (sd 17.9) |
| BUL ships sunk / HP left | 25.2 / 474 | 25.5 / 474 (sd 130) |
| BUL sunk: BB, CL, SHBB | 6.25, 14.42, 4.50 | 5.99, 14.76, 4.72 |
| ROM DD sunk / escaped | 3.92 / 17.58 | 4.16 / 17.89 |
| ROM HP left | 8421 (sd 404) | 8129 (sd 568) |

The SHBB losses were luck (4.50 against 4.72). ROM's damage dealt is the same to the HP (11,710 both); BUL dealt 8 %
less in the game (3,409 against 3,701, about 2.5 standard errors), mostly with the SHBB's heavy guns, whose hit chance
fell faster because their organization fell faster in the first 100 hours. No per-shot mechanic explains it: over the 12
runs the logged hits equal the summed hit chances per weapon within noise; target shares of every shooter design match
per 25-hour window; organization damage is `damage × (1 − hpBefore / maxHp)` on every hit (median ratio 1.000 over
4,400 capital hits, no other org loss); experience falls the same way (final xp per design within about 0.5), and the
attack in the shot profile steps with it exactly as TFB's levels give (−5 % `naval_damage_factor` per level below 2:
20+ xp full attack, 10–20 xp 0.95, under 10 xp 0.9).

**Resolved: dice luck.** In the 12 game runs BUL's hits fell 5.5 per run short of the sum of their logged hit chances
(103.3 against 108.8, z −1.9; the rolls themselves are uniform, mean 0.501), while ROM's matched (+0.8). Over the 200
simulator runs, BUL's damage dealt rises by 34 HP per hit of such luck (`3722 + 34.2 × BUL luck − 5.4 × ROM luck against
BUL's light cruisers in the first 60 hours`, residual sd 426). With the game runs' luck the simulator predicts 3,526 HP;
the game's 3,409 is 117 below (SE 123, z −0.95). The knock-on effects were all traced and are consistent with it:
ROM's capitals' light guns hit BUL's light cruisers a little above their odds early (z +0.6 and +1.2), the cruisers
fell below 40 % HP and retreated sooner (9.3 against 7.8 by hour 60), BUL's screening fell faster (0.455 against 0.503 at
hour 75), which under TFB's `screening_bonus` (+1.3 × screening on heavy-gun hit chance) lowered BUL's capital hit
chance; BUL also lost ships faster, keeping ROM above the 60-ship fleet-size threshold longer (ROM positioning 0.775
against 0.823 at hours 80–99), so ROM's damage per hit was lower and its total equal.

Rules this case confirmed against the logs (12 runs, TFB):
- S-TARGET-WEIGHT: the weight-proportional pick, replayed shot by shot with live HP and states, predicts every
  shooter → target split and every HP and state bin within noise (about 90,000 ROM and 37,000 BUL shots).
- S-POS: positioning is exact in all 2,240 side-hours with the engine's counting (waiting, fighting, joining and
  retreating ships at the end of the previous hour; escaped and sunk ones not) and TFB's values: fleet-size penalty
  0.4 per +100 % ratio, at most 1.2, only from 60 own ships; detection bonus `Δ surface detection / 100 × 0.01`, at most
  0.1.
- S-DMG: every gun hit fits `profile × (1 − 0.8 × (1 − positioning) + naval_damage_factor) × (1 + 0.2 × (U − 0.05)) ×
  0.5` (TFB's `DAMAGE_PENALTY_ON_MINIMUM_POSITIONING` 0.8), at positioning 0.65 to 1.
- S-DMG-APPLY: organization damage is `d × (1 − hpBefore / maxHp)` on every hit, and nothing else costs organization.
- S-RETREAT: below the threshold (0.4, capitals × 0.5) 20 % of fighting ship-hours start retreating, above it none.
- S-RETREAT-SPEED, S-XP: exact escape progress every hour; experience losses and their level steps as simulated.
- The logged `strDamage` includes overkill (a 59-damage hit on a 50 HP ship logs 59); compare HP lost
  (`hpBefore − hpAfter`), as `tools/ingame/compare.py` does.

## Replay: the game's own rolls (2026-10-19)

The logger now records every `random_fixed` call of the combat update (on its own thread, in call order, with the
source line and the shot loop it fell in) and every member's state at the start of the hour. `NavySimulator replay`
fights each logged hour from the game's start state with the game's rolls (S-REPLAY). The roll lines, from their
place in the call order: 0x37b target pick (before every shot loop), 0x3d9 torpedo line, 0x587 hit, 0x593 damage
randomness, 0x59f crit, 0x2f4 part damage, 0x78f part pick, **0xa83 retreat decision** (between one ship's last shot
and the next ship's target pick, from hour 8; 252 in the run), **0x40e** (6–9 calls in exactly the 32 shot loops
that sank their target and no others: the bookkeeping of a sinking). A battle the game creates during play starts with
an update at hour counter 0 that rolls and fires nothing.

The first replay of `jukki_usa_vs_bc_japan` seed 1 (174 hours, 10,257 shots) found four rule differences, each fixed:
- **Critical receive chance** (S-CRIT): torpedo crits landed for rolls up to 0.785 × the profile's chance and failed
  from 0.83: TFB's damage_control_1 and _2 (`critical_receive_chance` −0.1 each) make it 0.8 ×. The part-damage roll
  follows only real crits, which removed every roll divergence.
- **Experience level at the start of the hour** (S-XP): the attack in all 8,572 shot profiles matched the shooter's
  start-of-hour level; the engine re-read the level after every hit.
- **Refill on sinking** (S-MANPOWER): every manpower rise outside 00:00 came with a sinking on that side, 5 % per ship,
  already seen by later shots in the same hour. The code: the sink handler `0x1975480` calls `0xd77ff0`, which ends with
  the task-force refill `0xd7a7e0(tf, false)`.
- **Organization regain** (S-ORG-REGAIN): retreating and escaped ships regain 0.2 an hour (0.22 for the side with
  naval dominance: BUL until about hour 125, then ROM); fighting ships none.
- The battle's first engine hour is the game hour after the save's (daily refill after engine hour 11 with the save at
  12:00), so the scenario's `startHourOfDay` is 13.

After the fixes the engine makes exactly the game's rolls in all 174 hours, and 10,249 of 10,257 shots match (shooter,
weapon, target, hit, HP and organization lost). The 8 others are damaged critical parts: crits whose part multiplier
replaced the crit multiplier (3.77 against 5.66, a torpedo crit 17.6 against 74.9) and later shots of ships with
damaged heavy guns (× 0.5) or secondaries (light × 0.75) (S-CRIT-PART, not modelled). Per side, the engine's shots
took off 0.5 % (ROM) and 2.3 % (BUL) more HP than the game's, all from those shots. End-of-hour differences left: the
0.02 organization a hour of naval dominance, 17 manpower values off by one (rounding), and the parts.

Free-running, 200 simulator seeds against 18 game runs: 190.5 h against 185.4, BUL 25.7 ships sunk against 25.5
(per design within 0.5), ROM dealt 11,724 HP against 11,726; BUL dealt 3,768 against 3,384. About 170 HP of that is
BUL's dice in the game runs (5.1 hits a run short of their logged odds) and about 85 HP the part effects; the rest,
about 125 HP, is 1.4 standard errors.

