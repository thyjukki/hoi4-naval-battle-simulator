"""Run recorded HOI4 naval battles unattended and log them hour by hour and shot by shot.

Usage: python run_battles.py SAVE [SAVE...] [--seeds N] [--seed-base S] [--out DIR] [--commands "ai;weather"]

For each save (a plain-text save in the HOI4 save games folder, set up so the battle starts when unpaused) and each
seed, the runner:
  1. writes a copy with multiplayer_random_seed replaced (unless --seeds 0: the save as it is);
  2. points continue_game.json at it and starts hoi4.exe -continuelastsave;
  3. waits for the game to load, attaches battle_logger.js with Frida, types the console commands (default: ai,
     weather) and unpauses at speed 5;
  4. records every naval combat hour until the combats end, then closes the game.
With --setup-only it stops after the console commands and leaves the game paused and running on the copy, to look at.
Output per run in --out (default output/ingame/<save>_<timestamp>/<seed>/): meta.json, hours.csv, sides.csv, modifiers.csv, holders.csv,
shots.csv and raw.jsonl. The game must not be running when it starts; the game window takes keyboard focus during a run.
Requires: pip install frida-tools. Checks hoi4.exe against the SHA-256 the reverse-engineering index was built for.
"""
import argparse
import csv
import ctypes
import hashlib
import json
import os
import queue
import subprocess
import sys
import time
from ctypes import wintypes
from datetime import datetime
from pathlib import Path

import frida

import savefile

GAME_DIR = Path(os.environ.get("HOI4_DIR", r"C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV"))
USER_DIR = Path(os.environ.get("HOI4_USER_DIR", Path.home() / "Documents" / "Paradox Interactive" / "Hearts of Iron IV"))
SAVE_DIR = USER_DIR / "save games"
EXE_SHA256 = "7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5"
AGENT = Path(__file__).with_name("battle_logger.js")
REPO = Path(__file__).resolve().parents[2]
FX = 100000.0

# ---- keyboard input into the game window (SendInput) ----

user32 = ctypes.WinDLL("user32", use_last_error=True)
INPUT_KEYBOARD = 1
KEYEVENTF_KEYUP = 0x2
KEYEVENTF_UNICODE = 0x4
KEYEVENTF_SCANCODE = 0x8
SCAN_CONSOLE = 0x29  # the key left of 1 (§ / ` / ^): HOI4's console key
SCAN_ENTER = 0x1C
SCAN_SPACE = 0x39


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", wintypes.WORD), ("wScan", wintypes.WORD), ("dwFlags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("dwExtraInfo", ctypes.c_size_t)]


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG), ("mouseData", wintypes.DWORD),
                ("dwFlags", wintypes.DWORD), ("time", wintypes.DWORD), ("dwExtraInfo", ctypes.c_size_t)]


class INPUTUNION(ctypes.Union):
    _fields_ = [("ki", KEYBDINPUT), ("mi", MOUSEINPUT)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", wintypes.DWORD), ("u", INPUTUNION)]


def send_key(scan=0, char=None, up=False):
    flags = (KEYEVENTF_UNICODE if char else KEYEVENTF_SCANCODE) | (KEYEVENTF_KEYUP if up else 0)
    event = INPUT(type=INPUT_KEYBOARD, u=INPUTUNION(ki=KEYBDINPUT(0, ord(char) if char else scan, flags, 0, 0)))
    user32.SendInput(1, ctypes.byref(event), ctypes.sizeof(INPUT))


def tap(scan=0, char=None):
    send_key(scan, char)
    time.sleep(0.03)
    send_key(scan, char, up=True)
    time.sleep(0.03)


def game_window(pid):
    found = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def callback(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid and user32.IsWindowVisible(hwnd):
            title = ctypes.create_unicode_buffer(256)
            user32.GetWindowTextW(hwnd, title, 256)
            if title.value.startswith("Hearts of Iron IV"):
                found.append(hwnd)
        return True

    user32.EnumWindows(callback, 0)
    return found[0] if found else None


def focus(hwnd):
    user32.keybd_event(0x12, 0, 0, 0)  # Alt lets SetForegroundWindow take focus from another process
    user32.SetForegroundWindow(hwnd)
    user32.keybd_event(0x12, 0, KEYEVENTF_KEYUP, 0)
    time.sleep(0.3)


def console(hwnd, commands):
    focus(hwnd)
    tap(SCAN_CONSOLE)
    time.sleep(0.5)
    for command in commands:
        for ch in command:
            tap(char=ch)
        tap(SCAN_ENTER)
        time.sleep(0.5)
    tap(SCAN_CONSOLE)
    time.sleep(0.3)


# ---- game process ----

def check_exe():
    digest = hashlib.sha256((GAME_DIR / "hoi4.exe").read_bytes()).hexdigest().upper()
    if digest != EXE_SHA256:
        sys.exit(f"hoi4.exe SHA-256 {digest} is not the build the logger's addresses are for ({EXE_SHA256}); "
                 "re-run the reverse-engineering tools for this patch first")


def running_game_pids():
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq hoi4.exe", "/FO", "CSV", "/NH"], capture_output=True, text=True).stdout
    return [int(line.split('","')[1]) for line in out.splitlines() if line.startswith('"hoi4.exe"')]


def point_continue_at(save_name, title):
    (USER_DIR / "continue_game.json").write_text(json.dumps({
        "title": title, "desc": "", "date": time.strftime("%a %b %d %H:%M:%S %Y\n"), "filename": save_name, "is_remote": False,
    }, indent="\t"), encoding="utf-8")


def wait_for_load(game, started, timeout):
    log = USER_DIR / "logs" / "game.log"
    deadline = time.time() + timeout
    while time.time() < deadline:
        if game.poll() is not None:
            # the game crashed while loading; a locked or disconnected Windows session has no display for it
            raise RuntimeError(f"the game exited while loading (code {game.returncode}); see the crashes folder")
        if log.exists() and log.stat().st_mtime > started:
            text = log.read_text(encoding="utf-8", errors="replace")
            launch = text.rfind("Launching SINGLEPLAYER-game")
            if launch >= 0 and "End RestoreDeviceObjects" in text[launch:]:
                return
        time.sleep(1)
    raise TimeoutError("the game did not finish loading the save")


# ---- one run ----

def run_one(save_path, seed, out_dir, args):
    save = savefile.SaveFile(save_path)
    data = save.data if seed is None else save.with_seed(seed)
    run_name = f"nsim_{save_path.stem}" + ("" if seed is None else f"_s{seed}")
    run_save = SAVE_DIR / f"{run_name}.hoi4"
    run_save.write_bytes(data)
    point_continue_at(run_save.name, run_name)
    started = time.time()
    game = subprocess.Popen([str(GAME_DIR / "hoi4.exe"), "-continuelastsave"], cwd=GAME_DIR)
    if args.setup_only:
        # load and type the commands, then leave the game paused and running, with its save copy
        wait_for_load(game, started, args.load_timeout)
        time.sleep(args.settle)
        hwnd = game_window(game.pid)
        if hwnd is None:
            raise RuntimeError("game window not found")
        console(hwnd, [c for c in args.commands.split(";") if c])
        print(f"loaded {run_save.name} and typed {args.commands}; the game is paused (unpausing starts the battle, unlogged)")
        return

    index = savefile.ship_index(save, set(args.locations) if args.locations else None)
    out_dir.mkdir(parents=True, exist_ok=True)
    messages = queue.Queue()
    session = None
    try:
        wait_for_load(game, started, args.load_timeout)
        session = frida.attach(game.pid)
        script = session.create_script(AGENT.read_text(encoding="utf-8"))
        script.on("message", lambda message, _data: messages.put(message))
        script.load()
        if args.constant_roll is not None:
            # S-REPLAY-CONSTANT: the combat rolls the engine also makes all return this value (battle_logger.js)
            script.exports_sync.force_roll(round(args.constant_roll * FX))
        time.sleep(args.settle)
        hwnd = game_window(game.pid)
        if hwnd is None:
            raise RuntimeError("game window not found")
        console(hwnd, [c for c in args.commands.split(";") if c] + [f"gamespeed {args.speed}"])
        hours = record(messages, hwnd, out_dir, args)
    finally:
        if session is not None:
            try:
                session.detach()
            except frida.InvalidOperationError:
                pass
        game.kill()
        game.wait()
        if not args.keep_saves:
            run_save.unlink(missing_ok=True)

    write_outputs(out_dir, hours, index, {
        "save": str(save_path), "seed": seed if seed is not None else save.seed, "date": save.date,
        "commands": args.commands, "speed": args.speed, "exeSha256": EXE_SHA256, "constantRoll": args.constant_roll,
    })


def record(messages, hwnd, out_dir, args):
    """Collects hour records until every combat that started has ended (plus --extra-hours)."""
    hours = []
    raw = (out_dir / "raw.jsonl").open("w", encoding="utf-8")
    last_tick_time = time.time()
    unpause_tries = 0
    ticks = 0
    ended_at_tick = None
    last_hour_tick = None
    active = set()
    errors = 0
    try:
        while True:
            try:
                message = messages.get(timeout=1)
            except queue.Empty:
                message = None
            if message is not None:
                if message["type"] != "send":
                    raw.write(json.dumps(message) + "\n")
                    errors += 1
                    continue
                payload = message["payload"]
                kind = payload.get("type")
                if kind == "tick":
                    ticks = payload["ticks"]
                    last_tick_time = time.time()
                    unpause_tries = 0
                elif kind == "hour":
                    raw.write(json.dumps(payload) + "\n")
                    hours.append(payload)
                    last_hour_tick = ticks
                    if payload["ended"]:
                        active.discard(payload["combat"])
                        if not active:
                            ended_at_tick = ticks
                    else:
                        active.add(payload["combat"])
                        ended_at_tick = None
                elif kind == "error":
                    raw.write(json.dumps(payload) + "\n")
                    errors += 1
                    print("logger error:", payload["message"].splitlines()[0], file=sys.stderr)
            # stalled: the game is paused (start, or a popup); Space toggles pause
            if time.time() - last_tick_time > args.stall_seconds:
                if unpause_tries >= 3:
                    raise RuntimeError("the game does not unpause")
                focus(hwnd)
                tap(SCAN_SPACE)
                unpause_tries += 1
                last_tick_time = time.time()
            if ended_at_tick is not None and ticks - ended_at_tick >= args.extra_hours:
                break
            if last_hour_tick is not None and ticks - last_hour_tick > 48:
                print("no combat hour for 48 hours; stopping", file=sys.stderr)
                break
            if last_hour_tick is None and ticks > args.start_hours:
                print(f"no combat started within {args.start_hours} hours; stopping", file=sys.stderr)
                break
            if ticks > args.max_hours:
                print("stopping at --max-hours", file=sys.stderr)
                break
            if errors > 50:
                raise RuntimeError("too many logger errors; see raw.jsonl")
    finally:
        raw.close()
    return hours


# ---- outputs ----

WEAPONS = {0: "heavy", 1: "torpedo", 2: "light"}
ROLL_LINES = {0x587: "hit", 0x593: "damage", 0x59F: "crit"}


def write_outputs(out_dir, hours, index, meta):
    def design(ship_id):
        return (index.get(ship_id) or {}).get("design")

    with (out_dir / "hours.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["combat", "tick", "hour", "side", "ship", "country", "design", "state", "hp", "maxHp", "org",
                    "manpower", "maxManpower", "xp", "escape", "chargeHeavy", "chargeTorpedo", "chargeLight", "joining", "subReveal"])
        for h in hours:
            for side_index, side in enumerate(h["sides"]):
                for s in side["ships"]:
                    info = index.get(s["id"]) or {}
                    if s.get("gone"):
                        w.writerow([h["combat"], h["tick"], h["hourAtEndHour"], side_index, s["id"], info.get("country"), info.get("design"), s["state"]])
                        continue
                    w.writerow([h["combat"], h["tick"], h["hourAtEndHour"], side_index, s["id"], info.get("country"), info.get("design"),
                                s["state"], s["hp"] / FX, s["maxHp"] / FX, s["org"] / FX, s["mp"], s["maxMp"], s["xp"] / FX,
                                s["escape"] / FX, *(c / FX for c in s["charge"]), s["joining"], s["reveal"]])

    with (out_dir / "sides.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["combat", "tick", "hour", "side", "positioning", "screening", "carrierScreening", "fleetAa", "joinedPenalty", "retreatOrder", "weather", "darkness", "distance"])
        for h in hours:
            for side_index, side in enumerate(h["sides"]):
                w.writerow([h["combat"], h["tick"], h["hourAtEndHour"], side_index, side["positioning"] / FX, side["screening"] / FX,
                            side["carrierScreening"] / FX, side["fleetAa"] / FX, side["joinedPenalty"] / FX, side["retreatOrder"],
                            h["weather"], h["darkness"] / FX, h["distance"] / FX])

    with (out_dir / "shots.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["combat", "tick", "hour", "shooter", "shooterDesign", "target", "targetDesign", "weapon", "loop", "shot",
                    "chargeBefore", "damage", "hitChance", "critChance", "critMult", "piercing", "armor", "reduction", "cooldown",
                    "hitRoll", "damageRoll", "critRoll", "otherRolls", "strDamage", "orgDamage", "hpBefore", "hpAfter", "orgBefore", "orgAfter", "mpBefore", "mpAfter", "sankTarget"])
        for h in hours:
            for loop_index, loop in enumerate(h["shots"]):
                p = loop["profile"] or {}
                for shot_index, shot in enumerate(loop["shots"] or [{"rolls": [], "hits": []}]):
                    rolls = {ROLL_LINES.get(r["line"]): r["value"] / FX for r in shot["rolls"] if r["line"] in ROLL_LINES}
                    other = " ".join(f"{r['line']:#x}={r['value']}" for r in shot["rolls"] if r["line"] not in ROLL_LINES)
                    hit = shot["hits"][0] if shot["hits"] else None
                    w.writerow([h["combat"], h["tick"], h["hourAtUpdate"], loop["shooter"], design(loop["shooter"]), loop["target"], design(loop["target"]),
                                WEAPONS.get(loop["weapon"], loop["weapon"]), loop_index, shot_index, loop["chargeBefore"] / FX,
                                *(p.get(k, 0) / FX for k in ("damage", "hitChance", "critChance", "critMult", "piercing", "armor", "reduction", "cooldown")),
                                rolls.get("hit"), rolls.get("damage"), rolls.get("crit"), other,
                                *((hit["str"] / FX, hit["org"] / FX, hit["before"]["hp"] / FX, hit["after"]["hp"] / FX, hit["before"]["org"] / FX,
                                   hit["after"]["org"] / FX, hit["before"]["mp"], hit["after"]["mp"]) if hit else [None] * 8),
                                loop.get("sank")])

    # Start-of-hour combat inputs (battle_logger.js shipInputs, holderValues): per ship the stats, max organization and
    # modifier sums; per side the commanding admiral and one ship's modifier holders.
    with (out_dir / "modifiers.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        ship_mods = None
        for h in hours:
            for side_index, side in enumerate(h.get("start") or []):
                for s in side["ships"]:
                    inputs = s.get("inputs")
                    if not inputs:
                        continue
                    if ship_mods is None:
                        ship_mods = list(inputs["mods"])
                        w.writerow(["combat", "hour", "side", "ship", "design", "org", "orgStat", "maxOrg", "light", "heavy", "torpedo", "armor",
                                    "lightHit", "heavyHit", "torpedoHit", *ship_mods])
                    w.writerow([h["combat"], h["hourAtUpdate"], side_index, s["id"], design(s["id"]), s["org"] / FX,
                                *(inputs[k] / FX for k in ("orgStat", "maxOrg", "light", "heavy", "torpedo", "armor", "lightHit", "heavyHit", "torpedoHit")),
                                *(inputs["mods"][m] / FX for m in ship_mods)])

    with (out_dir / "holders.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["combat", "hour", "side", "admiral", "holder", "modifier", "value"])
        for h in hours:
            for side_index, side in enumerate(h.get("start") or []):
                admiral = side.get("admiral")
                admiral_text = "" if not admiral else f"type {admiral['type']} handle {admiral['handle'][0]}/{admiral['handle'][1]}"
                for s in side["ships"]:
                    for holder, values in (s.get("holders") or {}).items():
                        for name, value in values.items():
                            if value:
                                w.writerow([h["combat"], h["hourAtUpdate"], side_index, admiral_text, holder, name, value / FX])
                w.writerow([h["combat"], h["hourAtUpdate"], side_index, admiral_text, "", "", ""])

    with (out_dir / "rolls.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["combat", "tick", "hour", "seq", "line", "value", "loop", "int"])
        for h in hours:
            for seq, r in enumerate(h.get("rolls") or []):
                # integer draws (random int31, e.g. the air target pick) as a fraction of 2^31, as the game uses them
                value = r["value"] / 2**31 if r.get("int") else r["value"] / FX
                w.writerow([h["combat"], h["tick"], h["hourAtUpdate"], seq, f"{r['line']:#x}", value, r["loop"], 1 if r.get("int") else 0])

    meta["hours"] = len({(h["combat"], h["hourAtEndHour"]) for h in hours})
    meta["combats"] = sorted({h["combat"] for h in hours})
    meta["ships"] = {str(k): v for k, v in index.items()}
    (out_dir / "meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("saves", nargs="+", help="save file names in the HOI4 save games folder, or paths")
    parser.add_argument("--seeds", type=int, default=1, help="runs per save with seeds seed-base, seed-base+1, ...; 0 = the save's own seed, once")
    parser.add_argument("--seed-base", type=int, default=1)
    parser.add_argument("--out", type=Path, default=REPO / "output" / "ingame")
    parser.add_argument("--commands", default="ai;weather", help="console commands typed after loading, ';'-separated")
    parser.add_argument("--speed", type=int, default=5)
    parser.add_argument("--locations", type=int, nargs="*", help="sea provinces whose task forces are named in the output (default: all)")
    parser.add_argument("--extra-hours", type=int, default=0, help="keep running this many hours after the last combat ends")
    parser.add_argument("--max-hours", type=int, default=2000)
    parser.add_argument("--start-hours", type=int, default=48, help="give up when no combat has started after this many hours")
    parser.add_argument("--load-timeout", type=int, default=600)
    parser.add_argument("--settle", type=float, default=5, help="seconds to wait after loading before typing")
    parser.add_argument("--stall-seconds", type=float, default=8)
    parser.add_argument("--constant-roll", type=float,
                        help="every combat roll the engine also makes is this U in [0, 1) (whole fx5 steps); "
                             "compare with NavySimulator replay --constant-roll U")
    parser.add_argument("--keep-saves", action="store_true", help="keep the nsim_* copies in the save games folder")
    parser.add_argument("--setup-only", action="store_true",
                        help="load the first save and seed, type the commands and leave the game paused and running (nothing logged)")
    args = parser.parse_args()
    if args.constant_roll is not None and not 0 <= args.constant_roll < 1:
        sys.exit("--constant-roll expects a value in [0, 1)")
    if args.setup_only and (len(args.saves) > 1 or args.seeds > 1):
        sys.exit("--setup-only loads one save with one seed")

    check_exe()
    if running_game_pids():
        sys.exit("hoi4.exe is already running; close it first (the runner starts its own game for each battle)")
    continue_file = USER_DIR / "continue_game.json"
    continue_backup = continue_file.read_bytes() if continue_file.exists() else None
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    try:
        for name in args.saves:
            path = Path(name) if Path(name).is_absolute() else SAVE_DIR / name
            seeds = [None] if args.seeds == 0 else [args.seed_base + i for i in range(args.seeds)]
            for seed in seeds:
                out_dir = args.out / f"{path.stem}_{stamp}" / ("save-seed" if seed is None else f"s{seed}")
                print(f"{path.name} seed {seed if seed is not None else 'from save'} -> {out_dir}")
                run_one(path, seed, out_dir, args)
    finally:
        if continue_backup is not None:
            continue_file.write_bytes(continue_backup)


if __name__ == "__main__":
    main()
