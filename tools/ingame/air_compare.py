"""Compare logged game battles with carriers or land-based planes (run_battles.py output) with simulator runs.

Usage: python air_compare.py GAME_DIR [GAME_DIR...] --sim SIM_DIR --attacker COUNTRY

GAME_DIR holds run folders (meta.json, raw.jsonl); SIM_DIR is a CLI report folder (run-*/ships.csv, run-*/air.csv,
run-*/summary.txt). COUNTRY is the tag that plays the scenario's attacker. Per side it prints the mean and standard
deviation over runs of: battle length, ships sunk and escaped, HP lost (in total and to air strikes), naval strikes,
strikes that hit, carrier and land-based planes at the end. Plane counts in the game are those of the last air step.
"""
import argparse
import csv
import json
import re
import statistics
from collections import defaultdict
from pathlib import Path


def mean_sd(values):
    values = list(values)
    if not values:
        return "-"
    m = statistics.mean(values)
    sd = statistics.stdev(values) if len(values) > 1 else 0
    return f"{m:8.1f} ±{sd:6.1f}"


def game_runs(roots, attacker):
    for meta_path in sorted(p for root in roots for p in Path(root).rglob("meta.json")):
        run = meta_path.parent
        meta = json.loads(meta_path.read_text(encoding="utf-8"))
        country = {int(k): v["country"] for k, v in meta["ships"].items()}
        side = lambda ship_id: "attacker" if country.get(ship_id) == attacker else "defender"
        stats = {s: defaultdict(float) for s in ("attacker", "defender")}
        hours = 0
        last = None
        planes = {}
        start_hp = {}
        for line in (run / "raw.jsonl").open(encoding="utf-8"):
            record = json.loads(line)
            if record.get("type") != "hour":
                continue
            hours = max(hours, record["hourAtUpdate"])
            if record.get("start") and not start_hp:
                for s in record["start"]:
                    for m in s["ships"]:
                        if "hp" in m:
                            start_hp[m["id"]] = m["hp"] / 1e5
            for strike in record.get("air", []):
                if not strike.get("struck"):
                    continue
                target = strike.get("target")
                if target is None:
                    continue
                own = "defender" if side(target) == "attacker" else "attacker"
                stats[own]["strikes"] += 1
                damage = strike.get("damage")
                if isinstance(damage, dict):
                    hp = (damage["before"]["hp"] - damage["after"]["hp"]) / 1e5
                    if hp > 0:
                        stats[own]["hits"] += 1
                    stats[side(target)]["air HP lost"] += hp
            for hit in record.get("damage", []) if not any(isinstance(s.get("damage"), dict) for s in record.get("air", [])) else []:
                stats[side(hit["target"])]["air HP lost"] += (hit["before"]["hp"] - hit["after"]["hp"]) / 1e5
            for s_index, objects in enumerate(record.get("airObjects") or []):
                for o in objects:
                    key = (o.get("fx") or f"{o.get('carrier')}/{o.get('external')}/{len(planes)}", o.get("carrier"), o.get("external"))
                    planes[(s_index, o.get("carrier"), o.get("external"), tuple(w["handle"][1] for w in o.get("wings", [])[:1]))] = o.get("planes", 0)
            last = record
        if last is None:
            continue
        for s in last["sides"]:
            for m in s["ships"]:
                st = stats[side(m["id"])]
                state = m["state"]
                if state == 32 or m.get("gone"):
                    st["sunk"] += 1
                    st["HP lost"] += start_hp.get(m["id"], 0)
                else:
                    st["HP lost"] += start_hp.get(m["id"], 0) - m.get("hp", 0) / 1e5
                    if state == 16:
                        st["escaped"] += 1
        sides_by_index = {}
        for s_index, s in enumerate(last["sides"]):
            if s["ships"]:
                sides_by_index[s_index] = side(s["ships"][0]["id"])
        for (s_index, carrier, external, _), count in planes.items():
            name = sides_by_index.get(s_index, "attacker")
            stats[name]["land planes" if external else "carrier planes"] += count
        for st in stats.values():
            st["hours"] = hours
        yield run, stats


def sim_runs(root):
    for run in sorted(Path(root).glob("run-*")):
        stats = {s: defaultdict(float) for s in ("attacker", "defender")}
        summary = (run / "summary.txt").read_text(encoding="utf-8")
        match = re.search(r"after (\d+) h", summary) or re.search(r"(\d+) hours", summary)
        hours = int(match.group(1)) if match else 0
        for row in csv.DictReader((run / "ships.csv").open(encoding="utf-8")):
            st = stats[row["side"]]
            st["HP lost"] += float(row["hpDamageTaken"])
            if row["sunk"] == "True":
                st["sunk"] += 1
            elif row["status"] == "Retreated":
                st["escaped"] += 1
        if (run / "air.csv").exists():
            for row in csv.DictReader((run / "air.csv").open(encoding="utf-8")):
                own = row["side"]
                other = "defender" if own == "attacker" else "attacker"
                stats[own]["strikes"] += 1
                if float(row["hpDamage"]) > 0:
                    stats[own]["hits"] += 1
                stats[other]["air HP lost"] += float(row["hpDamage"])
        for st in stats.values():
            st["hours"] = hours
        yield run, stats


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("game", nargs="+")
    parser.add_argument("--sim", required=True)
    parser.add_argument("--attacker", required=True)
    args = parser.parse_args()
    game = [stats for _, stats in game_runs(args.game, args.attacker)]
    sim = [stats for _, stats in sim_runs(args.sim)]
    print(f"game runs {len(game)}, simulator runs {len(sim)}")
    keys = ["hours", "sunk", "escaped", "HP lost", "air HP lost", "strikes", "hits", "carrier planes", "land planes"]
    print(f"{'':10} {'':16} {'game':>16} {'simulator':>16}")
    for side in ("attacker", "defender"):
        for key in keys:
            if key == "hours" and side == "defender":
                continue
            g = [s[side][key] for s in game]
            m = [s[side][key] for s in sim]
            if key in ("carrier planes", "land planes") and not any(g):
                continue
            print(f"{side:10} {key:16} {mean_sd(g):>16} {mean_sd(m) if key not in ('carrier planes', 'land planes') else '':>16}")


if __name__ == "__main__":
    main()
