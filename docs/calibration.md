# Calibration against the game

Reverse-engineering tells us what the code says; calibration tells us whether the simulator *behaves* like the game. Where the spec offers variants (`BattleRules`), calibration decides between them.

## Recording a battle

Aim for controlled, repeatable fights rather than battles from a real campaign.

**Fix the setup and write it down:**
- Game version and every active mod, in load order. Unless the mods are part of the test, use none.
- Both fleets: ship designs (hull and every module), counts, experience level, and the task force's aggressiveness (engagement rule) and repair priority. Plain-text saves record them in the task force as `navy_engagement_rule` inside `mission` (0 Do Not Engage to 4 Always Engage) and `repair_mode` (presumably 0 Never repair to 3 High, the order of the game's repair priority tooltips). Set them in the scenario as `aggressiveness` and `repairPriority`.
- Researches, doctrines, national spirits and MIOs on both sides. No admirals, or record their skills and traits.
- Sea region, weather, and the in-game time when the battle starts. Night and weather matter (S-HIT-MODS, A-CADENCE). Weather is not modelled yet, so prefer battles fought with weather off: run the console command `disable_weather` (or `weather`, which toggles it) before the battle, check that the sea region shows no weather, and set `"rules": { "weatherEnabled": false }` in the scenario (S-WEATHER).
- Who attacked whom.

**Record the result** from the battle screen and the ship list:
- Battle length in hours; who won; whether it ended by sinking or retreat.
- Per ship: sunk (and roughly when), escaped, or remaining HP and org at the end.
- If you can, a snapshot every hour or every few hours (pause and read the battle screen). Hourly HP and org curves are the strongest signal for cadence, activation and firing-order questions.
- For air: when carrier strikes happen (hours) and planes lost.

**Repeat.** Single battles are noisy; 10 or more repetitions of the same setup give a usable distribution. Check whether reloading a save replays the identical battle. If it does, vary something irrelevant (for example engage an hour later) to get independent samples, and record what you varied.

## File format

One JSON file per setup under `data/calibration/` (milestone 5 adds the runner that reads it):

```json
{
  "id": "cl-vs-cl-1936-no-mods",
  "gameVersion": "1.18.3",
  "mods": [],
  "notes": "Two task forces of 10 identical 1936 light cruisers, Engage At Medium Risk, repair priority Medium, North Sea, clear weather.",
  "scenario": "battle-scenario-cl-vs-cl.json",
  "conditions": { "startHourOfDay": 14, "weather": "clear", "terrain": "ocean" },
  "observations": [
    {
      "repetition": 1,
      "durationHours": 41,
      "outcome": "DefenderVictory",
      "ships": [
        { "side": "attacker", "design": "cl_1936", "count": 10, "sunk": 7, "retreated": 3 },
        { "side": "defender", "design": "cl_1936", "count": 10, "sunk": 2, "retreated": 0 }
      ],
      "hourly": [
        { "hour": 8, "attackerHpFraction": 0.98, "defenderHpFraction": 0.97 }
      ]
    }
  ]
}
```

`scenario` points to a normal scenario file in `data/` that rebuilds the same setup in the simulator. `hourly` is optional.

## How cases are scored (milestone 5)

For each calibration case and each `BattleRules` variant combination:
1. Simulate the scenario many times (hundreds of seeds).
2. Compare the simulated distributions with the observations: battle length, ships sunk per design, outcome share, and hourly HP fractions when recorded. Use the observations' standard error for tolerances.
3. Rank the variants. A variant clearly outside the observed range on several cases is removed from the engine, and the spec item gets the [CAL] tag with a link to the cases.

## Cheap, high-value experiments

- **Mirror match** (identical fleets): the attacker's win rate tests S-ORDER directly. The decoded attacker-first order predicts a clear attacker edge; the game confirming it validates the side order and the "who is attacker" mapping.
- **Screens only, no capitals or carriers**: whether shooting starts at hour 0 or hour 8 settles S-ACTIVATE.
- **One weapon type per side** (for example only torpedo cruisers): isolates cooldown, hit profile and crit rules.
- **Carrier vs carrier at different start times**: when strikes happen settles A-CADENCE and the night penalty.
