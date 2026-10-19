# Logging battles from the running game

Runs HOI4 battles unattended and logs them hour by hour and shot by shot, for calibration ([docs/calibration.md](../../docs/calibration.md)).
Requirements: Windows, Python 3.12, `pip install frida-tools`, HOI4 1.19.3 with the executable the reverse-engineering
index was built for (the runner checks its SHA-256), Steam running. The hooks and offsets are described in
[LOG-ingame-logging.md](../../docs/reverse-engineering/findings/LOG-ingame-logging.md).

| File | Purpose |
|---|---|
| `run_battles.py` | Runner: per save and seed, starts the game on the save, types the console commands, unpauses, logs, closes the game |
| `battle_logger.js` | Frida agent: hooks the combat hour and the shot loop, sends one record per combat hour |
| `savefile.py` | Reads plain-text saves (task forces, ships, design variants) and writes a copy with another dice seed |
| `summarize.py` | Outcome of every logged run (length, HP left, ships fighting / escaped / sunk) and the spread over runs |

## Setting up a battle

1. In the game (plain-text saves on), set up the two task forces in one sea province at war, with their missions and
   aggressiveness, so the battle starts within an hour of unpausing. Save. This is the template save.
2. Close the game. The runner starts its own game for each run and the game window takes keyboard focus while it runs.

## Running

```
python run_battles.py my_battle.hoi4 --seeds 10 --locations 7906
python summarize.py ../../output/ingame
```

For each seed the runner writes `nsim_<save>_s<seed>.hoi4` (the template with `multiplayer_random_seed` replaced) to the
save games folder, points `continue_game.json` at it, starts `hoi4.exe -continuelastsave`, waits for `game.log` to
report the loaded game, attaches the logger, types `ai`, `weather` and `gamespeed 5` into the console (`--commands`
changes the first two) and unpauses. It stops when every combat has ended (`--extra-hours` keeps going, for example to
catch a second engagement), closes the game and deletes the copy. `continue_game.json` is restored at the end.

A save replays the identical battle every time it is loaded with the same console commands; the seed is what makes
repetitions independent. `--seeds 0` runs the save's own seed once.

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
- `raw.jsonl`: everything the logger sent.

`hour` is the game's combat hour counter, which is 1 in the first hour (the engine's `tick`).
