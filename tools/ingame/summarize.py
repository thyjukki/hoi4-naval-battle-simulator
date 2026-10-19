"""Usage: python summarize.py DIR [DIR...]  - outcome of every logged run under the given folders (run_battles.py output).

Per run: battle length and, per country at the last combat hour, HP left and ships fighting / retreating / escaped / sunk;
then the mean and range over the runs.
"""
import csv
import json
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

STATES = {"1": "waiting", "2": "fighting", "4": "joining", "8": "retreating", "16": "escaped", "32": "sunk"}


def run_outcome(run_dir):
    rows = list(csv.DictReader((run_dir / "hours.csv").open(encoding="utf-8")))
    meta = json.loads((run_dir / "meta.json").read_text(encoding="utf-8"))
    last_hour = max(int(r["hour"]) for r in rows)
    seen = {r["ship"]: r for r in rows}  # last record of every ship, including ships that left the groups
    outcome = {"run": str(run_dir), "seed": meta["seed"], "hours": last_hour, "countries": {}}
    by_country = defaultdict(lambda: {"hp": 0.0, "states": Counter()})
    for ship_id, info in meta["ships"].items():
        last = seen.get(ship_id)
        country = by_country[info["country"]]
        if last is None or last["hp"] == "":
            country["states"]["sunk"] += 1
            continue
        state = STATES.get(last["state"], last["state"])
        if int(last["hour"]) < last_hour and state not in ("escaped", "sunk"):
            state = "sunk" if float(last["hp"]) <= 0 else "left"
        country["hp"] += float(last["hp"]) if state != "sunk" else 0.0
        country["states"][state] += 1
    outcome["countries"] = {k: {"hp": round(v["hp"], 1), **v["states"]} for k, v in by_country.items()}
    return outcome


def main():
    runs = sorted({p.parent for d in sys.argv[1:] for p in Path(d).rglob("meta.json")})
    outcomes = [run_outcome(r) for r in runs]
    for o in outcomes:
        print(f"seed {o['seed']:>12}  {o['hours']:>4} h  " + "  ".join(
            f"{c}: {v['hp']:7.1f} HP " + " ".join(f"{k} {n}" for k, n in v.items() if k != "hp") for c, v in sorted(o["countries"].items())))
    if len(outcomes) < 2:
        return
    print(f"\n{len(outcomes)} runs")
    print(f"hours: mean {statistics.mean(o['hours'] for o in outcomes):.1f}  range {min(o['hours'] for o in outcomes)}..{max(o['hours'] for o in outcomes)}")
    for country in sorted({c for o in outcomes for c in o["countries"]}):
        for key in ("hp", "sunk", "escaped", "retreating", "fighting"):
            values = [o["countries"].get(country, {}).get(key, 0) for o in outcomes]
            print(f"{country} {key:<10} mean {statistics.mean(values):8.1f}  range {min(values)}..{max(values)}  sd {statistics.stdev(values):.1f}")


if __name__ == "__main__":
    main()
