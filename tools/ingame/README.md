# Logging battles from the running game

Runs HOI4 battles unattended and logs them hour by hour and shot by shot, for calibration ([docs/calibration.md](../../docs/calibration.md)).
Requirements: Windows, Python 3.12, `pip install frida-tools`, HOI4 1.19.3 with the executable the reverse-engineering
index was built for (the runner checks its SHA-256), Steam running. The hooks and offsets are described in
[LOG-ingame-logging.md](../../docs/reverse-engineering/findings/LOG-ingame-logging.md).

| File | Purpose |
|---|---|
| `run_battles.py` | Runner: per save and seed, starts the game on the save, types the console commands, unpauses, logs, closes the game |
| `battle_logger.js` | Frida agent: hooks the combat hour and the shot loop, sends one record per combat hour |
| `make_save.py` | Writes a simulator scenario into a battle save: designs, ships, aggressiveness, repair priority, technologies |
| `savefile.py` | Reads plain-text saves (task forces, ships, design variants) and writes a copy with another dice seed |
| `summarize.py` | Outcome of every logged run (length, HP left, ships fighting / escaped / sunk) and the spread over runs |
| `compare.py` | Logged game runs against simulator runs: start values, outcome per design, hit chances per shooter, weapon and target |

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
the side's researches (`GAME_TECHS`: the simulator's research IDs are not the game's yet) and removes the running naval
combat, so the game starts the battle afresh in the first hour. The game picks the attacker itself (in the CL3 template
ROM). Spirits, commanders, doctrines and MIOs are not written. It reads the result back and checks ships, variants and
experience. The game starts the battle an hour after the save's time (a counter-0 update that fires nothing), so the
scenario's `rules.startHourOfDay` should be the save's hour + 1.

## Running

```
python run_battles.py my_battle.hoi4 --seeds 10 --locations 7906
python summarize.py ../../output/ingame
python compare.py ../../output/ingame/<save>_<timestamp> [more game folders] ../../output/<scenario>_<timestamp> BUL
```

For each seed the runner writes `nsim_<save>_s<seed>.hoi4` (the template with `multiplayer_random_seed` replaced) to the
save games folder, points `continue_game.json` at it, starts `hoi4.exe -continuelastsave`, waits for `game.log` to
report the loaded game, attaches the logger, types `ai`, `weather` and `gamespeed 5` into the console (`--commands`
changes the first two) and unpauses. It stops when every combat has ended (`--extra-hours` keeps going, for example to
catch a second engagement), closes the game and deletes the copy. `continue_game.json` is restored at the end.

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
- `rolls.csv`: every `random_fixed` call of the combat update in call order: hour, sequence, source line, value, and the
  index of the shot loop it fell in (-1 outside one).
- `raw.jsonl`: everything the logger sent, including every member's state at the start of each hour. With the rolls
  it lets `NavySimulator replay --log <run folder>` fight each hour with the game's own dice (S-REPLAY in
  [surface.md](../../docs/mechanics/surface.md)).

`hour` is the game's combat hour counter, which is 1 in the first hour (the engine's `tick`).
