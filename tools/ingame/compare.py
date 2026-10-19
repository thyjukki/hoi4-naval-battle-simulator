"""Compare logged game runs (run_battles.py output) with simulator runs (a CLI report folder).

Usage: python compare.py GAME_DIR [GAME_DIR...] SIM_DIR ATTACKER_COUNTRY

ATTACKER_COUNTRY is the country tag that plays the simulator scenario's attacker. Prints battle length; start values per
design (max HP, HP, org, manpower, experience); sunk / escaped / other per design; HP left and dealt per side; the hit
chance per shooter design, weapon and target design in the first 24 combat hours; the game's shot profiles; shots per run.
Designs are matched by name: make_save.py names the game variants after the simulator's design IDs.
"""
import csv
import json
import re
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

STATES = {"1": "waiting", "2": "fighting", "4": "joining", "8": "retreating", "16": "escaped", "32": "sunk"}


def mean(values):
    values = list(values)
    return statistics.mean(values) if values else float("nan")


def game_runs(roots):
    for meta_path in sorted(p for root in roots for p in Path(root).rglob("meta.json")):
        run = meta_path.parent
        meta = json.loads(meta_path.read_text(encoding="utf-8"))
        hours = list(csv.DictReader((run / "hours.csv").open(encoding="utf-8")))
        shots = list(csv.DictReader((run / "shots.csv").open(encoding="utf-8")))
        yield run, meta, hours, shots


def sim_design(ship_id, fleet_ids):
    for fleet in fleet_ids:
        if ship_id.startswith(fleet + "_"):
            return re.sub(r"_\d+$", "", ship_id[len(fleet) + 1:])
    return ship_id


def main():
    game_roots, sim_root, attacker_country = sys.argv[1:-2], Path(sys.argv[-2]), sys.argv[-1]
    side_of_country = {}

    # ---- game ----
    game = []
    start_stats = defaultdict(list)
    game_hc = defaultdict(list)
    game_profile = defaultdict(list)
    game_shot_counts = defaultdict(int)
    for run, meta, hours, shots in game_runs(game_roots):
        ships = meta["ships"]
        for info in ships.values():
            side_of_country[info["country"]] = "attacker" if info["country"] == attacker_country else "defender"
        last_hour = max(int(r["hour"]) for r in hours)
        first = {}
        last = {}
        for r in hours:
            first.setdefault(r["ship"], r)
            last[r["ship"]] = r
        sides = {r["side"]: ships[r["ship"]]["country"] for r in hours[:200] if r["ship"] in ships}
        outcome = {"hours": last_hour, "sides": sides, "designs": defaultdict(Counter), "hp": Counter(), "dealt": Counter()}
        for ship_id, info in ships.items():
            key = (side_of_country[info["country"]], info["design"])
            r = last.get(ship_id)
            if r is None or r["hp"] == "":
                state = "sunk"
            else:
                state = STATES.get(r["state"], r["state"])
                if int(r["hour"]) < last_hour and state not in ("escaped", "sunk"):
                    state = "sunk" if float(r["hp"]) <= 0 else "left"
            outcome["designs"][key][state] += 1
            outcome["designs"][key]["ships"] += 1
            if state != "sunk" and r is not None and r["hp"] != "":
                outcome["hp"][key[0]] += float(r["hp"])
            f = first.get(ship_id)
            if f is not None and f["hp"] != "":
                start_stats[key].append((float(f["maxHp"]), float(f["hp"]), float(f["org"]), int(f["maxManpower"]), float(f["xp"])))
        for s in shots:
            shooter = ships.get(s["shooter"], {})
            key = (side_of_country.get(shooter.get("country")), s["shooterDesign"], s["weapon"], s["targetDesign"])
            if s["strDamage"] not in ("", None):  # HP actually lost: the logged damage includes overkill
                outcome["dealt"][key[0]] += float(s["hpBefore"]) - float(s["hpAfter"])
            game_shot_counts[(key[0], s["shooterDesign"], s["weapon"])] += 1
            if int(s["hour"]) <= 25:
                game_hc[key].append(float(s["hitChance"]) / 100)
                game_profile[(key[0], s["shooterDesign"], s["weapon"])].append((float(s["damage"]), float(s["piercing"]), float(s["cooldown"])))
        game.append(outcome)

    # ---- simulator ----
    sim = []
    sim_hc = defaultdict(list)
    sim_shot_counts = defaultdict(int)
    sim_start = defaultdict(list)
    runs = sorted(p.parent for p in sim_root.rglob("ships.csv"))
    for run in runs:
        ships = list(csv.DictReader((run / "ships.csv").open(encoding="utf-8")))
        outcome = {"hours": None, "designs": defaultdict(Counter), "hp": Counter(), "dealt": Counter()}
        for r in ships:
            key = (r["side"], r["design"])
            state = "sunk" if r["sunk"] == "True" else ("escaped" if r["status"] == "Retreated" else r["status"].lower())
            outcome["designs"][key][state] += 1
            outcome["designs"][key]["ships"] += 1
            if state != "sunk":
                outcome["hp"][r["side"]] += float(r["finalHp"])
            outcome["dealt"][r["side"]] += float(r["hpDamageDealt"])
            sim_start[key].append((float(r["maxHp"]), float(r["maxOrg"]), int(r["manpower"]), float(r["startXp"])))
        if len(sim) < 20:
            fleet_ids = sorted({r["ship"][: r["ship"].index(r["design"]) - 1] for r in ships}, key=len, reverse=True)
            for s in csv.DictReader((run / "shots.csv").open(encoding="utf-8")):
                shooter_design = sim_design(s["shooter"], fleet_ids)
                target_design = sim_design(s["target"], fleet_ids)
                weapon = s["weapon"].lower()
                sim_shot_counts[(s["side"], shooter_design, weapon)] += 1
                if int(s["hour"]) + 1 <= 25:
                    sim_hc[(s["side"], shooter_design, weapon, target_design)].append(float(s["hitChance"]))
        sim.append(outcome)
    hours_line = next((l for l in (runs[0].parent / "summary.txt").read_text(encoding="utf-8").splitlines() if l.startswith("Hours:")), "")

    # ---- report ----
    print(f"game runs: {len(game)}; simulator runs: {len(sim)}")
    print(f"game sides (combat side index -> country): {[g['sides'] for g in game]}")
    print(f"hours: game {[g['hours'] for g in game]} mean {mean(g['hours'] for g in game):.1f}; simulator {hours_line}")
    print("\nstart values per design (game first logged hour: maxHp, hp, org, maxManpower, xp | simulator maxHp, org, manpower, xp)")
    for key in sorted(start_stats):
        g = start_stats[key]
        s = sim_start.get(key, [])
        print(f"  {key[0]:<8} {key[1]:<26} game " + " ".join(f"{mean(x[i] for x in g):8.2f}" for i in range(5)) +
              "  | sim " + " ".join(f"{mean(x[i] for x in s):8.2f}" for i in range(4)))
    print("\noutcome per design (mean per run): game sunk / escaped / other  |  simulator sunk / escaped / other")
    keys = sorted({k for o in game + sim for k in o["designs"]})
    for key in keys:
        def stats(outcomes):
            sunk = mean(o["designs"][key]["sunk"] for o in outcomes)
            esc = mean(o["designs"][key]["escaped"] for o in outcomes)
            total = mean(o["designs"][key]["ships"] for o in outcomes)
            return sunk, esc, total - sunk - esc
        gs, ss = stats(game), stats(sim)
        print(f"  {key[0]:<8} {key[1]:<26} game {gs[0]:6.2f} {gs[1]:6.2f} {gs[2]:6.2f}  | sim {ss[0]:6.2f} {ss[1]:6.2f} {ss[2]:6.2f}")
    for side in ("attacker", "defender"):
        print(f"  {side} HP left: game {[round(o['hp'][side]) for o in game]} mean {mean(o['hp'][side] for o in game):.0f};"
              f" sim mean {mean(o['hp'][side] for o in sim):.0f} sd {statistics.stdev(o['hp'][side] for o in sim):.0f}")
        print(f"  {side} HP dealt: game mean {mean(o['dealt'][side] for o in game):.0f}; sim mean {mean(o['dealt'][side] for o in sim):.0f}")
    print("\nhit chance in the first 24 combat hours (shooter design, weapon, target design): game mean (n) | sim mean (n)")
    for key in sorted(set(game_hc) | set(sim_hc), key=lambda k: tuple(str(x) for x in k)):
        g, s = game_hc.get(key, []), sim_hc.get(key, [])
        if len(g) + len(s) < 20:
            continue
        print(f"  {str(key[0]):<8} {key[1]:<26} {key[2]:<7} -> {key[3]:<26} game {mean(g):.4f} ({len(g):>5}) | sim {mean(s):.4f} ({len(s):>5})")
    print("\nshot profile in the first 24 combat hours (game): damage, piercing, cooldown")
    for key in sorted(game_profile, key=lambda k: tuple(str(x) for x in k)):
        p = game_profile[key]
        print(f"  {str(key[0]):<8} {key[1]:<26} {key[2]:<7} damage {mean(x[0] for x in p):8.3f} (min {min(x[0] for x in p):.3f} max {max(x[0] for x in p):.3f})"
              f" piercing {mean(x[1] for x in p):7.3f} cooldown {mean(x[2] for x in p):.2f}")
    print("\nshots per run (game) vs per run (simulator, first 20 runs)")
    for key in sorted(set(game_shot_counts) | set(sim_shot_counts), key=lambda k: tuple(str(x) for x in k)):
        print(f"  {str(key[0]):<8} {key[1]:<26} {key[2]:<7} game {game_shot_counts.get(key, 0) / max(1, len(game)):8.1f} | sim {sim_shot_counts.get(key, 0) / min(20, len(sim)):8.1f}")


if __name__ == "__main__":
    main()
