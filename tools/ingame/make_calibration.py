"""Write logged game runs (run_battles.py output) as a calibration case in data/calibration/ (docs/calibration.md).

Usage: python make_calibration.py GAME_DIR [GAME_DIR...] --id ID --scenario FILE --attacker COUNTRY
           [--design "GAME NAME=design_id" ...] [--mod NAME ...] [--notes TEXT] [--sim SIM_DIR --sim-commit SHA]

GAME_DIR folders are searched for runs (meta.json). A seed logged more than once is kept once, preferring a run with
roll logs (rolls.csv, replayable with `NavySimulator replay`). COUNTRY is the tag that plays the scenario's attacker.
Design names in the logs are the game's variant names; make_save.py names them after the simulator's design IDs, other
saves need --design. Per run the case records the battle length (the last combat hour, as the engine counts hours),
the outcome, sunk / escaped / remaining ships and HP left per design, and both sides' HP fractions every 24 hours.
--sim adds the same numbers from a simulator report folder (NavySimulator --out), tagged with --sim-commit, as the
reference the case was last compared with.
"""
import argparse
import csv
import json
import re
import statistics
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
STATES = {"1": "waiting", "2": "fighting", "4": "joining", "8": "retreating", "16": "escaped", "32": "sunk"}


def find_runs(roots):
    by_seed = {}
    for meta_path in sorted(p for root in roots for p in Path(root).rglob("meta.json")):
        run = meta_path.parent
        seed = json.loads(meta_path.read_text(encoding="utf-8"))["seed"]
        has_rolls = (run / "rolls.csv").exists()
        if seed not in by_seed or (has_rolls and not (by_seed[seed] / "rolls.csv").exists()):
            by_seed[seed] = run
    return [by_seed[seed] for seed in sorted(by_seed)]


def game_observation(run, attacker, designs):
    meta = json.loads((run / "meta.json").read_text(encoding="utf-8"))
    rows = list(csv.DictReader((run / "hours.csv").open(encoding="utf-8")))
    last_hour = max(int(r["hour"]) for r in rows)
    last = {r["ship"]: r for r in rows}
    groups = defaultdict(lambda: {"count": 0, "sunk": 0, "retreated": 0, "remaining": 0, "hpLeft": 0.0})
    in_battle = Counter()
    for ship_id, info in members(meta, last).items():
        side = "attacker" if info["country"] == attacker else "defender"
        group = groups[(side, designs.get(info["design"], info["design"]))]
        group["count"] += 1
        record = last.get(ship_id)
        state = STATES.get(record["state"], record["state"]) if record and has_hp(record) and record["state"] != "32" else "sunk"
        if record and int(record["hour"]) < last_hour and state not in ("escaped", "sunk"):
            state = "sunk" if float(record["hp"]) <= 0 else "escaped"
        if state == "sunk":
            group["sunk"] += 1
            continue
        group["hpLeft"] += float(record["hp"])
        if state == "escaped":
            group["retreated"] += 1
        else:
            group["remaining"] += 1
            in_battle[side] += 1
    outcome = ("AttackerVictory" if in_battle["attacker"] and not in_battle["defender"]
               else "DefenderVictory" if in_battle["defender"] and not in_battle["attacker"]
               else "BothSidesGone" if not in_battle["attacker"] and not in_battle["defender"] else "InProgress")
    return {
        "seed": meta["seed"],
        "log": run.resolve().relative_to(REPO).as_posix() if run.resolve().is_relative_to(REPO) else str(run),
        "rollLog": (run / "rolls.csv").exists(),
        "durationHours": last_hour,
        "outcome": outcome,
        "ships": ship_entries(groups),
        "hourly": hourly_fractions(rows, meta, attacker, last_hour),
    }


def members(meta, logged):
    """The ships of the battle: meta.json lists every ship of the save."""
    return {ship_id: info for ship_id, info in meta["ships"].items() if ship_id in logged}


def has_hp(row):
    return row.get("hp") not in ("", None)


def hourly_fractions(rows, meta, attacker, last_hour):
    max_hp = defaultdict(float)
    for info in members(meta, {r["ship"] for r in rows}).values():
        max_hp["attacker" if info["country"] == attacker else "defender"] += info["hp"]
    hp = defaultdict(lambda: defaultdict(float))
    for r in rows:
        hour = int(r["hour"])
        if hour % 24 == 0 and hour > 0 and has_hp(r):
            hp[hour]["attacker" if r["country"] == attacker else "defender"] += max(0.0, float(r["hp"]))
    return [{"hour": hour, "attackerHpFraction": round(hp[hour]["attacker"] / max_hp["attacker"], 4),
             "defenderHpFraction": round(hp[hour]["defender"] / max_hp["defender"], 4)}
            for hour in range(24, last_hour + 1, 24) if hour in hp]


def sim_observations(sim_dir):
    for run in sorted(Path(sim_dir).glob("run-*")):
        summary = (run / "summary.txt").read_text(encoding="utf-8")
        seed = int(re.search(r"seed (\d+)", summary).group(1))
        outcome, hours = re.search(r"Outcome: (\w+) after (\d+) hours", summary).groups()
        groups = defaultdict(lambda: {"count": 0, "sunk": 0, "retreated": 0, "remaining": 0, "hpLeft": 0.0})
        for r in csv.DictReader((run / "ships.csv").open(encoding="utf-8")):
            group = groups[(r["side"], r["design"])]
            group["count"] += 1
            if r["sunk"] == "True":
                group["sunk"] += 1
                continue
            group["hpLeft"] += float(r["finalHp"])
            group["retreated" if r["status"] == "Retreated" else "remaining"] += 1
        yield {"seed": seed, "durationHours": int(hours), "outcome": outcome, "ships": ship_entries(groups)}


def ship_entries(groups):
    return [{"side": side, "design": design, **{k: (round(v, 1) if k == "hpLeft" else v) for k, v in g.items()}}
            for (side, design), g in sorted(groups.items())]


def summarize(observations):
    def stat(values):
        values = list(values)
        return {"mean": round(statistics.mean(values), 2), "sd": round(statistics.stdev(values), 2) if len(values) > 1 else 0.0,
                "min": min(values), "max": max(values)}

    keys = sorted({(s["side"], s["design"]) for o in observations for s in o["ships"]})
    per_design = []
    for side, design in keys:
        entries = [next((s for s in o["ships"] if (s["side"], s["design"]) == (side, design)), None) for o in observations]
        entries = [e for e in entries if e]
        per_design.append({"side": side, "design": design, "count": entries[0]["count"],
                           **{k: stat(e[k] for e in entries) for k in ("sunk", "retreated", "remaining", "hpLeft")}})
    sides = []
    for side in ("attacker", "defender"):
        hp_left = [sum(s["hpLeft"] for s in o["ships"] if s["side"] == side) for o in observations]
        sunk = [sum(s["sunk"] for s in o["ships"] if s["side"] == side) for o in observations]
        sides.append({"side": side, "hpLeft": stat(round(v, 1) for v in hp_left), "sunk": stat(sunk)})
    outcomes = defaultdict(int)
    for o in observations:
        outcomes[o["outcome"]] += 1
    return {"runs": len(observations), "durationHours": stat(o["durationHours"] for o in observations),
            "outcomes": dict(outcomes), "sides": sides, "designs": per_design}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("game_dirs", nargs="+", type=Path)
    parser.add_argument("--id", required=True)
    parser.add_argument("--scenario", required=True, help="scenario file in data/")
    parser.add_argument("--attacker", required=True, help="country tag playing the scenario's attacker")
    parser.add_argument("--design", action="append", default=[], help="GAME NAME=design_id")
    parser.add_argument("--mod", action="append", default=[])
    parser.add_argument("--game-version", default="1.19.4")
    parser.add_argument("--notes", default="")
    parser.add_argument("--sim", type=Path, help="simulator report folder (run-*/summary.txt and ships.csv)")
    parser.add_argument("--sim-commit", default="")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()

    designs = dict(entry.split("=", 1) for entry in args.design)
    scenario = json.loads((REPO / "data" / args.scenario).read_text(encoding="utf-8"))["battleScenario"]
    rules = scenario.get("rules", {})
    observations = []
    for index, run in enumerate(find_runs(args.game_dirs), start=1):
        observations.append({"repetition": index, **game_observation(run, args.attacker, designs)})

    case = {
        "$schema": "../schemas/calibration.schema.json",
        "id": args.id,
        "gameVersion": args.game_version,
        "mods": args.mod,
        "notes": args.notes,
        "scenario": args.scenario,
        "attackerCountry": args.attacker,
        "conditions": {"startHourOfDay": rules.get("startHourOfDay"), "weather": scenario.get("weather"),
                       "weatherEnabled": rules.get("weatherEnabled", True), "terrain": scenario.get("terrain")},
        "summary": summarize(observations),
    }
    if args.sim:
        sims = list(sim_observations(args.sim))
        seeds = [s["seed"] for s in sims]
        case["simulatorReference"] = {"commit": args.sim_commit, "seeds": f"{min(seeds)}..{max(seeds)}", **summarize(sims)}
    case["observations"] = observations

    out = args.out or REPO / "data" / "calibration" / f"{args.id}.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(case, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{out}: {len(observations)} runs ({sum(o['rollLog'] for o in observations)} with roll logs)")


if __name__ == "__main__":
    main()
