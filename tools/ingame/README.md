# Logging battles from the running game

Runs HOI4 battles unattended and logs them hour by hour and shot by shot, for calibration ([docs/calibration.md](../../docs/calibration.md)).
Requirements: Windows, Python 3.12, `pip install frida-tools`, HOI4 1.19.3 or 1.19.4 with one of the executables the
logger has addresses for (the runner checks its SHA-256 against `EXE_BUILDS`; `battle_logger.js` keeps one address table
per build, `BUILDS`, and the runner passes it the running build's hash), Steam running. The hooks and offsets are described in
[LOG-ingame-logging.md](../../docs/reverse-engineering/findings/LOG-ingame-logging.md).

| File | Purpose |
|---|---|
| `run_battles.py` | Runner: per save and seed, starts the game on the save, types the console commands, unpauses, logs, closes the game |
| `battle_logger.js` | Frida agent: hooks the combat hour and the shot loop, sends one record per combat hour |
| `make_save.py` | Writes a simulator scenario into a battle save: designs, ships, aggressiveness, repair priority, technologies, doctrines, spirits, MIOs, the admiral, the pride of the fleet, the region's naval dominance, carrier wings, land-based wings on their missions |
| `savefile.py` | Reads plain-text saves (task forces, ships, design variants) and writes a copy with another dice seed |
| `summarize.py` | Outcome of every logged run (length, HP left, ships fighting / escaped / sunk) and the spread over runs |
| `compare.py` | Logged game runs against simulator runs: start values, outcome per design, hit chances per shooter, weapon and target |
| `air_compare.py` | Logged carrier / land-based air battles against simulator runs: length, sunk and escaped, HP lost in total and to air, strikes, planes left |
| `make_calibration.py` | Writes logged runs as a calibration case (baseline) in `data/calibration/`, with a simulator report as the reference |

## Setting up a battle

1. In the game (plain-text saves on), set up the two task forces in one sea province at war, with their missions and
   aggressiveness, so the battle starts within an hour of unpausing. Save. This is the template save.
2. Close the game. The runner starts its own game for each run and the game window takes keyboard focus while it runs.

### From a simulator scenario

```
python make_save.py jukki_usa_vs_bc_japan.json light_attack_vs_torp_cruiser_battle_start.hoi4 --dotnet <.NET 10 dotnet> --defines "<mod folder>"
```

writes `<scenario id>_battle_start.hoi4` to the save games folder from a template save that already holds one task
force per side in one sea province, at war (here the CL3 battle start). It runs `NavySimulator list-ships` for the
scenario's ships (ship type, HP, organization, manpower, experience; `--defines` because experience levels come from the
defines), then per side rewrites one of the country's design variants on each design's hull to the design's modules
(named after the design, so the logs show the simulator's design IDs), replaces the task force's ships, sets
`navy_engagement_rule` and `repair_mode` from the aggressiveness and repair priority, adds the game technologies behind
the side's researches (`GAME_TECHS`: the simulator's research IDs are not the game's yet), writes the side's doctrines
into the save's `doctrine` block (per folder the grand doctrine and, per track in the grand doctrine's order, the
subdoctrine, its mastery and unlocked reward count, plus the completed subdoctrines; S-DOCTRINE-SAVE), adds its spirits
to the country's `politics={ ideas={ } }` list, makes the first ship of the side's `prideOfTheFleet` design the
country's pride of the fleet (`pride_of_the_fleet={ id type=51 }` before `pride_of_the_fleet_date_lost`, or removes the
template's when the side has none; S-POTF-SAVE), sets both countries' naval dominance in the battle region to their new
ships' totals (`list-ships` gives each design's `navalDominance`; S-DOMINANCE), and removes the running naval combat, so
the game starts the battle afresh in the first hour. The game picks the attacker itself (in the CL3 template ROM).

Naval dominance: the region's `dominance={ values={ … } }` keeps each country's `current`, `target`, `base_target`,
`previous` and `decline_from`; the game moves `current` toward the target once a day. Saves written before this kept
the template's values (the CL3 fleets', BUL 17377 against ROM 17238), so in their battles the USA side regained
organization faster until Japan's value passed it (hour 133, 157 or 181). The script now writes the new fleets' totals
into the one region whose dominance lists both countries (it warns when there is none), as after a long patrol, which
is what `rules.navalDominanceEnabled` assumes; the side with more dominance then keeps it until its ships' losses
bring its value below the other side's (4 % of the old total a day at most).

Loading a save rebuilds only the doctrine state and the country modifiers from that block; the doctrines' sub-unit
bonuses come back from the country's `deployment={ unit_modifiers={ … } }` records, one per active part (`type` =
`grand_doctrine`, `sub_doctrine`, `rewards` with the reward index or `milestones` with the track index, `id`), which the
script writes from `data/doctrines`. A ship clamps its organisation to its maximum when it is read, and the country's
`units` come before `deployment` in the save, so the script moves `deployment` before `units` (`--keep-block-order`
leaves it); ship organisation is written with the doctrines' bonuses. Both steps follow the decoded load path
([S-DOCTRINE-SAVE](../../docs/reverse-engineering/findings/S-DOCTRINE-doctrines.md)) but are not confirmed in a run yet:
check the logged hour-0 organisation against `list-ships`.

MIOs ([S-MIO-SAVE](../../docs/reverse-engineering/findings/S-MIO-equipment-matching.md#save-format-s-mio-save)): each
MIO the side uses goes into the country's `production={ industrial_organisations={ … } }` with the side's unlocked
traits (`unlocked={ trait="…" }`, one block each) and policy (`policy=…`). The template's countries stand in for the
scenario's (BUL for the USA, ROM for Japan), so the script copies the MIO's block from the country that owns it in the
save with a new handle; the game reads any MIO the database knows, whatever its `allowed`. Each rewritten variant gets
`design_team={ id type=79 }` and `design_team_bonus={ … }`, the MIO bonus by game key from `list-ships` (`mioBonus`; the
game keeps that snapshot and does not recompute it, S-MIO-TIMING). Keys the simulator has no stat for (production
capacity, mines) are left out of the snapshot; none acts in combat.

The admiral ([S-ADMIRAL-SAVE](../../docs/reverse-engineering/findings/S-ADMIRAL-leader-combat.md#save-format-s-admiral-save)):
the country's first navy leader stands in for the side's: its name, skill levels and traits are rewritten (the portrait
stays the stand-in's), and it becomes the leader of the fleet holding the battle task force (`leader={ id type=4713 }`
after the fleet's id). No assignment date is saved, so the admiral loads without the reassignment cooldown.

Carriers (A-WINGS-SAVE, [A-WINGS](../../docs/reverse-engineering/findings/A-WINGS-carrier-wings-and-reinforcement.md)):
each carrier gets an `air_base` in `strategic_air` (`carrier={ id type=51 }`, capacity = deck size × 10) and, per plane
sub-unit on it, an `air_wing_pool` under the country with the carrier's wings (`count`, `manpower` = 40 per plane, the
country's variant of the plane, `allow_mission_type` from the plane's missions, experience 0, no mission). The side's
plane stockpile goes into the country's `production={ equipments={ … } }`, and its carrier stance into the task force's
`sortie_efficiency`. New `{ id type=4713 }` references must stay at or below the header's `debug_current_ref_id`
(the script raises it), or the game hands out the same ids at load. A design on a hull the country has no free
variant of (Bulgaria has no `ship_hull_carrier_3` variant in 1936) gets a copy of its variant on a hull of the same
family. The logger records every air object's strike (planes, wings with their executing mission) in raw.jsonl `air`.
The game picks the attacker (ROM, so Japan in the USA vs Japan saves): with TFB's stance ratios of 1.0 the defender's
carriers launch only their bonus sortie efficiency in the first 24 h, so compare with a simulator scenario that has
the same attacker and the battle region's `airRegionPhase` (region 30: 6). `--attacker-country ROM` gives the
scenario's attacker the task force the game will make attacker (`jukki_usa_vs_bc_japan_ingame.json` and the `air_*`
test scenarios are written so).

Land-based wings ([A-MISSION-SAVE](../../docs/reverse-engineering/findings/A-MISSION-SAVE-air-mission-save-format.md)):
each `landBasedAirwings` entry becomes one wing of the country's variant of the plane at the country's air base
(`--air-base TAG=ID`; defaults Burgas 329 for BUL and Dobrogea 350 for ROM in the CL3 template), in the country's pool
of that sub-unit there (a new pool if none), with `mission={ type=16 (naval strike) or 1 (air superiority) period=2
active=yes strategic_region=30 region_change_penalty=0 missions_done=0 }` (`--air-region`). `region_change_penalty=0`
must follow `strategic_region`, which sets it to 0.9 on load. Only wings in range join a battle: land CAS (range 640)
from either base never joined in province 7906; tactical bombers did (mission effectiveness 0.6 from Burgas, 1.0 from
Dobrogea).

Game rules: every written save turns TFB's Admiral Command Limit off (`game_rules={ navy_cap="DISABLED" }`). Under its
default (cap 150) TFB weighs the ships under each admiral (destroyer and submarine 1, cruisers 2, battleships and
battlecruisers 6, carriers and super-heavy battleships 8, super-heavy carriers 14) and gives the whole country a daily
`naval_damage_factor` and torpedo hit chance penalty of 0.01 per point above 145; the simulator has one fleet per side
and no such cap.

Doctrine equipment bonuses (three lists of named bonus records) are not written, so the script warns when one reaches a
design of the scenario. It reads the result back and checks ships, variants, experience, doctrines, spirits, MIOs,
design teams and the fleet's admiral. After loading a new kind of save, check the game's `logs/error.log`: a block the
game cannot read makes it skip the rest of the country (its fleet included), with one "Unexpected token" line per key. The game starts the battle an hour after the save's time (a counter-0 update that fires nothing), so the
scenario's `rules.startHourOfDay` should be the save's hour + 1.

## Running

```
python run_battles.py my_battle.hoi4 --seeds 10 --locations 7906
python summarize.py ../../output/ingame
python compare.py ../../output/ingame/<save>_<timestamp> [more game folders] ../../output/<scenario>_<timestamp> BUL
python make_calibration.py ../../output/ingame/<save>_<timestamp> [more game folders] --id <case> --scenario <scenario>.json --attacker BUL --sim ../../output/<scenario>_<timestamp> --sim-commit <commit>
```

`make_save.py` removes the two battle countries' planes that the scenario does not define (the template's air wings
and plane stock; `--keep-template-planes` keeps them), so a land wing's air base holds only the scenario's planes and
`otherPlanes` is 0; it writes the scenario's air base level into the save. The air saves before 2026-10-26 kept the CL3
template's planes (1,000 BUL fighters at Burgas, refilled from the 10,011-fighter stock at every day change); they are
kept as `<scenario>_battle_start_template_planes.hoi4`, and their batches (`air_batch1` … `air_batch7`) replay with a
copy of the scenario whose Burgas base has `otherPlanes` 1000.

`make_calibration.py` keeps each seed once (the copy with roll logs if there are several), maps the countries to the
scenario's sides (`--attacker`) and the game's variant names to design IDs (`--design "39LC LA=39lc_la_aa3"`; saves from
`make_save.py` already use the IDs of their time). A design edited after its battles were recorded keeps the recorded
version under a new ID (docs/calibration.md); `NavySimulator replay --design <logged name>=<design id>` maps it.

For each seed the runner writes `nsim_<save>_s<seed>.hoi4` (the template with `multiplayer_random_seed` replaced) to the
save games folder, points `continue_game.json` at it, starts `hoi4.exe -continuelastsave`, waits for `game.log` to
report the loaded game, attaches the logger, types `ai`, `weather` and `gamespeed 5` into the console (`--commands`
changes the first two) and unpauses. It stops when every combat has ended (`--extra-hours` keeps going, for example to
catch a second engagement), closes the game and deletes the copy. `continue_game.json` is restored at the end.

`--setup-only` loads one save and seed, types the console commands and leaves the game paused and running on the copy
(nothing is logged; unpausing starts the battle), for looking at a new setup in the game first. `--keep-open` logs one
save and seed as usual, then pauses the game and leaves it running on the copy, to look at the result.

`--force-recovery C` (S-RECOVERY probe) forces the ship recovery chance before the country's factor to C at every
sinking, and `--force-recovery-factor F` also the factor (0 in every country of 1.19.3 and 1.19.4, which makes
recovery impossible); every sinking's chance, factor, roll and outcome is logged in `raw.jsonl` (`recovery` of the hour record).

`--force-carrier-cooldown H` (A-CADENCE probe) forces every carrier cooldown the game writes after a strike to H
hours; `raw.jsonl` logs each written cooldown (`cooldowns`: computed and forced) and each read of it (`cooldownChecks`).

`--force-controller SHIP_ID` (S-STATS-AGAINST probe) learns the country tag of that ship's task force the first time the
attack/armor getter runs for it, then before every getter call writes it into the controller field (`+0x188`) of the
province the fighting task force is in, so a sea province counts as controlled by that country; `raw.jsonl` logs each
change (`controllers`) and the probe's counters (`controllerProbe`: getter calls, first ships, learned tag).

`--constant-roll U` makes every roll the engine also makes return U (whole 1e-5 steps; the air target pick's integer
draw floor(U × 2^31)), for a battle the engine must fight identically: the surface and air rolls of the combat update,
and the region air war's rolls outside it (forced by call site). It also switches the game's air accident pass off
(`--keep-accidents` leaves it on): its generator is shared by every air base in the world, so the engine cannot follow
it. Check a run with `NavySimulator replay --log <run> --constant-roll U`, which fights it free from the game's first
hour, region air war included and accidents off (S-REPLAY-CONSTANT in docs/mechanics/surface.md). With U = 0.05 the
mixed-fleet save gives a 137-hour battle with losses on both sides; at 0.5 almost nothing hits and no battle ends. Keep
U above `ACE_EARN_CHANCE_BASE` (0.01): an ace earned takes its template from the game's own hash, which the engine does
not have. A replay that needs `--design` or a weather-on scenario: see docs/calibration.md and the notes below.

### Constant-roll air checks (runbook)

Written and first run 2026-10-26 (results: docs/mechanics/surface.md, S-REPLAY-CONSTANT): the carrier and land-based
plane battles with constant rolls. On the computer with the game:

1. Prerequisites: hoi4.exe 1.19.3 or 1.19.4 with a SHA-256 in `run_battles.py` (the logger's addresses are per build),
   Python with `frida-tools`, the Windows session active and unlocked on the console (the game crashes on a locked or
   disconnected session), the game closed. `HOI4_DIR` / `HOI4_USER_DIR` if the game or the user folder are elsewhere.
2. Copy the four saves into the save games folder (from this computer's `Documents/Paradox Interactive/Hearts of Iron
   IV/save games`): `air_cv_mirror_battle_start.hoi4`, `air_cv_bombers_vs_fighters_battle_start.hoi4`,
   `air_land_vs_fleet_battle_start.hoi4`, `air_cv_land_mix_battle_start.hoi4`. The TFB dev mod must be the mod the
   saves load with, as here.
3. Run (about a minute a battle; the game window takes focus):
   ```
   cd tools/ingame
   python run_battles.py air_cv_mirror_battle_start.hoi4 air_cv_bombers_vs_fighters_battle_start.hoi4 air_land_vs_fleet_battle_start.hoi4 air_cv_land_mix_battle_start.hoi4 --seeds 0 --constant-roll 0.05 --out ../../output/ingame/constant_roll_air/u005
   python run_battles.py air_cv_mirror_battle_start.hoi4 air_cv_bombers_vs_fighters_battle_start.hoi4 air_land_vs_fleet_battle_start.hoi4 air_cv_land_mix_battle_start.hoi4 --seeds 0 --constant-roll 0.07 --out ../../output/ingame/constant_roll_air/u007
   ```
   Each run folder's `meta.json` should say `"constantRoll": 0.05` and `"accidentsDisabled": true`; in `raw.jsonl`
   the hour records' `forcedOutside` counts the region air war rolls forced in that hour (above 0 in air hours with a
   region fight), and `rolls.csv` should hold only U (and the bookkeeping lines 0x40e, 0x96 (0x95 in 1.19.4), 0x1c9, 0xb72, 0x369).
4. Bring `output/ingame/constant_roll_air/` back (or replay there) and compare, per run:
   ```
   dotnet run --project src/NavySimulator.Cli -- replay --log output/ingame/constant_roll_air/u005/air_cv_mirror_battle_start_<stamp>/save-seed --data data --scenario air_cv_mirror.json --defines "<TFB dev mod folder>" --constant-roll 0.05 --air <scratch>/air.csv --states <scratch>/states.csv
   ```
   with `air_cv_bombers_vs_fighters.json`, `air_land_vs_fleet.json` and `air_cv_land_mix.json` for the other saves
   (the replay turns the sides round when the game's attacker is the scenario's defender). Expected: every shot and
   every air strike matching, both sides' damage equal and the engine's battle over in the game's last hour; the
   surface runs so far left only the last hour's regain and experience and fixed-point rounding. A first difference
   shows where a rule differs; `--air` lists the strikes side by side. `air_cv_land_mix` holds the open gap of
   calibration.md (the USA's air damage 35 % above the game in free runs): an identical constant-roll battle there
   says the rules agree and the gap is luck.

A save replays the identical battle every time it is loaded with the same console commands; the seed is what makes
repetitions independent. `--seeds 0` runs the save's own seed once. A run gives up when no combat has started after
`--start-hours` (48) game hours.

## Output

One folder per run under `output/ingame/<save>_<timestamp>/<seed>/` (or `--out`):

- `meta.json`: save, seed, start date, and every ship of the task forces at `--locations` with country, task force,
  design (the in-game design name) and starting HP, org, manpower and experience.
- `hours.csv`: per combat hour and ship: state (1 waiting, 2 fighting, 4 joining, 8 retreating, 16 escaped, 32 sunk), HP,
  org, manpower, experience, escape progress, weapon charges.
- `sides.csv`: per combat hour and side: positioning, screening, carrier screening, fleet AA, newly joined penalty,
  weather type, darkness, fleet distance.
- `shots.csv`: one row per shot: shooter, target, weapon, the shot profile (damage, hit chance ×100, crit chance, crit
  multiplier, piercing, armor, damage reduction, cooldown), the hit / damage / crit rolls, and the HP and org damage
  with the target's HP, org and manpower before and after.
- `modifiers.csv`: per combat hour (at its start) and ship: organization, the `max_organisation` stat, the live max
  organization (0xc37e70), attack, armor and hit chance factor stats, and the ship modifier sum (0xc38760, with the
  side's commanding admiral) for the hit chance, damage, defense and organization modifiers.
- `holders.csv`: per combat hour and side: the commanding admiral (empty: none) and, for the side's first ship, every
  non-zero value of the modifier sum's holders (country, task force, experience, captain, admiral) for the modifiers the
  combat terms read. A change here shows where a game-side difference comes from.
- `rolls.csv`: every `random_fixed` call of the combat update in call order: hour, sequence, source line, value, and the
  index of the shot loop it fell in (-1 outside one); since 2026-10-20 also the generator's integer draws (`int` = 1,
  value = n / 2^31: the air target pick 0x43f and bookkeeping draws). In 1.19.4 RemovePlanes draws one per lost plane
  (A-MIXED-REMOVE, line 0x95 like the `random.h` bookkeeping draws); `raw.jsonl` marks those `planeLoss`, and
  `--constant-roll` forces them. `meta.json` names the game version (`gameVersion`, since 2026-10-28; older runs are
  1.19.3), and `NavySimulator replay` uses that version's rules.
- `landwings.csv` (since 2026-10-26): every land-based wing with a mission over `--air-region` (default 30), read by its
  handle at the start of each combat hour, after that hour's region air war: planes, mission type and executing, the
  region air war's disruption taken and escort value, the disruption after escorts, the region change penalty, the
  mission effectiveness and the wing's experience. Air superiority wings never enter the combat, so this is their only
  record. Carrier and naval-strike wings carry the same fields (and `xp`) in `raw.jsonl`'s air records.
- `raw.jsonl`: everything the logger sent, including every member's state at the start of each hour. With the rolls
  it lets `NavySimulator replay --log <run folder>` fight each hour with the game's own dice (S-REPLAY in
  [surface.md](../../docs/mechanics/surface.md)). Air (since 2026-10-20): per strike attempt (`air`) the air object's
  pointer, carrier, planes, stacking factor, sortie efficiency, target, planes intercepted and shot down by AA,
  strikers, the stat averages (attack, targeting, agility), the damage it applied, and per wing its planes, missions,
  disruption, mission effectiveness and ace (alive, template chance); per hour the carrier-ratio helper's sums
  (`carrierRatio`) and every air object of both sides at the end of the hour (`airObjects`).

`hour` is the game's combat hour counter, which is 1 in the first hour (the engine's `tick`).
