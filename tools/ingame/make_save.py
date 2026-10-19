"""Write a simulator scenario into a battle save for run_battles.py.

Usage: python make_save.py SCENARIO TEMPLATE [--out NAME] [--data DIR] [--defines PATH] [--dotnet EXE] [--force]

SCENARIO is a scenario file in the data directory (for example jukki_usa_vs_bc_japan.json). TEMPLATE is a plain-text
save with one task force of each side in one sea province, at war, where the battle has started or starts at once
(for example light_attack_vs_torp_cruiser_battle_start.hoi4). The script:
  1. runs `NavySimulator list-ships` for the scenario's ships (design, hull, modules by slot, ship type, max HP,
     organization, manpower, experience);
  2. per side, rewrites one of the country's design variants on each design's hull to the design's modules and names
     it after the design (the variant whose ships have the same ship type and the most modules in common);
  3. replaces the ships of the side's task force, reusing its ship ids (new ones from the save's unit counter);
  4. sets the task force's aggressiveness (navy_engagement_rule) and repair priority (repair_mode);
  5. adds the game technologies behind the side's researches (GAME_TECHS) to the country;
  6. removes the naval combat at the battle's location, so the game starts the battle afresh when unpaused.
The attacker's task force is the template combat's attacker (else --attacker-country). Everything else in the save is
kept byte for byte. The result goes to the save games folder (default name: <scenario id>_battle_start.hoi4).
"""
import argparse
import json
import os
import random
import re
import subprocess
import sys
from pathlib import Path

import savefile

REPO = Path(__file__).resolve().parents[2]
USER_DIR = Path(os.environ.get("HOI4_USER_DIR", Path.home() / "Documents" / "Paradox Interactive" / "Hearts of Iron IV"))
SAVE_DIR = USER_DIR / "save games"

# Game technology -> the simulator researches that stand for it. The simulator's research IDs are not the game's
# (technologies are not imported yet, ROADMAP milestone 4); TFB's MTG_naval_Support.txt / MTG_naval.txt effects.
# Some game technologies are split per ship type in data/researches; all parts are needed to match the game.
GAME_TECHS = {
    "basic_light_shell": ["light_upgrade_1"],
    "improved_light_shell": ["light_upgrade_2"],
    "basic_heavy_shell": ["heavy_upgrade_1"],
    "improved_heavy_shell": ["heavy_upgrade_2"],
    "magnetic_detonator": ["torpedo_upgrade_1"],
    "homing_torpedo": ["torpedo_upgrade_2"],
    "fire_control_methods_1": ["fire_control_methods_1"],
    "fire_control_methods_2": ["fire_control_methods_2", "fire_control_methods_2_ca"],
    "fire_control_methods_3": ["fire_control_methods_3", "fire_control_methods_3_cruiser", "fire_control_methods_3_dd"],
    "damage_control_1": ["damage_control_1"],
    "damage_control_2": ["damage_control_2"],
    "smoke_generator": ["smoke_generator"],
}

ID_LINE = re.compile(r"\{ id=(\d+) type=(\d+) \}")  # a save handle: id={ id=N type=T }, unit={ id=N type=T }


def number(value):
    """A number as the game writes it: up to five decimals, no trailing zeros."""
    text = f"{value:.5f}".rstrip("0").rstrip(".")
    return "0" if text in ("", "-0") else text


class Lines:
    """The save as lines that keep their own line ending, read and written byte for byte."""

    def __init__(self, path):
        data = Path(path).read_bytes()
        if not data.startswith(b"HOI4txt"):
            raise ValueError(f"{path} is not a plain-text save")
        self.lines = data.decode("utf-8", errors="surrogateescape").split("\n")
        self.edits = []

    def text(self, i):
        return self.lines[i].rstrip("\r")

    def eol(self, i):
        return "\r" if self.lines[i].endswith("\r") else ""

    def block_end(self, start):
        depth = 0
        for i in range(start, len(self.lines)):
            line = self.lines[i]
            depth += line.count("{") - line.count("}")
            if depth <= 0:
                return i
        raise ValueError(f"block at line {start + 1} does not close")

    def top_level(self, key):
        return next(i for i, line in enumerate(self.lines) if self.text(i) == f"{key}={{")

    def children(self, start, end=None):
        """(key, line index) of the direct children of the block opening at `start`."""
        end = self.block_end(start) if end is None else end
        i = start + 1
        while i < end:
            text = self.text(i).strip()
            key = text.split("=", 1)[0] if "=" in text else text
            yield key, i
            i = self.block_end(i) + 1 if text.endswith("{") else i + 1

    def child(self, start, key):
        return next((i for k, i in self.children(start) if k == key), None)

    def replace(self, start, end, new_lines):
        """Replaces lines start..end (inclusive; end = start - 1 inserts before start). Applied by save()."""
        self.edits.append((start, end, new_lines))

    def save(self, path):
        lines = list(self.lines)
        edits = sorted(self.edits, key=lambda e: e[0], reverse=True)
        for (start, end, _), (next_start, _, _) in zip(edits[1:], edits):
            if end >= next_start:
                raise ValueError(f"overlapping edits at lines {start + 1} and {next_start + 1}")
        for start, end, new_lines in edits:
            lines[start:end + 1] = new_lines
        Path(path).write_bytes("\n".join(lines).encode("utf-8", errors="surrogateescape"))


def list_ships(args):
    command = [args.dotnet, "run", "--project", str(REPO / "src" / "NavySimulator.Cli"), "--", "list-ships",
               "--data", str(args.data), "--scenario", args.scenario]
    for source in args.defines:
        command += ["--defines", source]
    result = subprocess.run(command, capture_output=True, text=True, cwd=REPO)
    if result.returncode != 0:
        sys.exit(f"list-ships failed:\n{result.stdout}{result.stderr}")
    return json.loads(result.stdout[result.stdout.index("{"):])


def find_task_force(save, tf_id):
    for i, line in enumerate(save.lines):
        if save.text(i).strip() == "task_force={" and save.text(i + 1).strip() == f"id={{ id={tf_id} type=61 }}":
            return i
    raise ValueError(f"task force {tf_id} not found")


def battle_task_forces(save, parsed, args):
    """(attacker task force, defender task force, location) from the template's combat, else from --attacker-country."""
    combat = save.top_level("combat")
    for key, i in save.children(combat):
        if key != "naval_combat":
            continue
        units = {}
        for side in ("attacker", "defender"):
            side_line = save.child(i, side)
            unit = save.child(side_line, "unit") if side_line is not None else None
            if unit is not None:
                units[side] = int(ID_LINE.search(save.text(unit)).group(1))
        location = int(save.text(save.child(i, "location")).split("=")[1])
        if len(units) == 2:
            by_id = {tf.id: tf for tf in parsed.task_forces()}
            return by_id[units["attacker"]], by_id[units["defender"]], location
    if not args.attacker_country:
        sys.exit("the template has no naval combat; pass --attacker-country and --location")
    forces = [tf for tf in parsed.task_forces() if tf.location == args.location]
    attacker = [tf for tf in forces if tf.country == args.attacker_country]
    defender = [tf for tf in forces if tf.country != args.attacker_country]
    if len(attacker) != 1 or len(defender) != 1:
        sys.exit(f"need one task force per side at {args.location}, found {[(tf.country, tf.name) for tf in forces]}")
    return attacker[0], defender[0], args.location


def variant_blocks(save):
    """Variant id -> (block start, block end) in the top-level equipments section."""
    blocks = {}
    for _, i in save.children(save.top_level("equipments")):
        match = ID_LINE.search(save.text(i + 1))
        if match and match.group(2) == "70":
            blocks[int(match.group(1))] = (i, save.block_end(i))
    return blocks


def pick_variants(save, parsed, country, designs, taken):
    """Design id -> variant id: per design, the country's variant on the hull whose ships have the design's ship type
    and that has the most modules in common with it."""
    variants = [v for v in parsed.variants().values() if v.creator == country and v.name]
    blocks = variant_blocks(save)
    types_by_variant = {}
    for tf in parsed.task_forces():
        if tf.country == country:
            for ship in tf.ships:
                types_by_variant.setdefault(ship.equipment_id, set()).add(ship.definition)
    chosen = {}
    for design in designs:
        candidates = [v for v in variants if v.equipment_type == design["hull"] and v.id not in taken
                      and "is_frame=yes" not in (save.text(j).strip() for j in range(*blocks[v.id]))]
        if not candidates:
            sys.exit(f"{country} has no free design variant on {design['hull']} to hold {design['design']}")
        best = max(candidates, key=lambda v: (design["shipType"] in types_by_variant.get(v.id, ()),
                                              sum(v.modules.get(slot) == module for slot, module in design["modules"].items()),
                                              -v.id))
        chosen[design["design"]] = best.id
        taken.add(best.id)
    return chosen


def rewrite_variant(save, start, design):
    end = save.block_end(start)
    name = next(i for k, i in save.children(start, end) if k == "name")
    indent = save.text(name)[: len(save.text(name)) - len(save.text(name).lstrip("\t"))]
    eol = save.eol(name)
    save.replace(name, name, [f'{indent}name="{design["design"]}"{eol}'])
    modules = next(i for k, i in save.children(start, end) if k == "modules")
    modules_end = save.block_end(modules)
    new = [f"{indent}modules={{{eol}"] + [f"{indent}\t{slot}={module}{eol}" for slot, module in design["modules"].items()] + [f"{indent}}}{eol}"]
    save.replace(modules, modules_end, new)


def ship_block(save, start, ship_id, design, variant_id, name, new_officer):
    """The lines of the ship block at `start`, rewritten for a ship of `design`."""
    end = save.block_end(start)
    out = []
    depth = 0
    i = start
    section = None
    while i <= end:
        text = save.text(i)
        stripped = text.strip()
        indent = text[: len(text) - len(text.lstrip("\t"))]
        eol = save.eol(i)
        if depth == 1:
            key = stripped.split("=", 1)[0]
            section = key if stripped.endswith("{") else None
            if key == "id" and stripped.startswith("id={"):
                text = f"{indent}id={{ id={ship_id} type=51 }}"
            elif key == "definition":
                text = f"{indent}definition={design['shipType']}"
            elif key == "organisation":
                text = f"{indent}organisation={number(design['organization'])}"
            elif key == "strength":
                # the ship's experience follows its strength (held_officer's experience is the officer's)
                out.append(f"{indent}strength={number(design['hp'])}{eol}")
                if design["experience"] > 0:
                    out.append(f"{indent}experience={number(design['experience'])}{eol}")
                i += 1
                continue
            elif key in ("max_manpower", "manpower"):
                text = f"{indent}{key}={design['manpower']}"
            elif key == "experience":
                i += 1
                continue
        elif section == "equipment" and stripped.startswith("id={") and "type=70" in stripped:
            text = f"{indent}id={{ id={variant_id} type=70 }}"
        elif section == "ship_name" and stripped.startswith("override="):
            text = f'{indent}override="{name}"'
        elif section == "officer" and new_officer and stripped.startswith("seed="):
            text = f"{indent}seed={random.randint(1, 2**31 - 1)}"
        out.append(text + eol)
        depth += stripped.count("{") - stripped.count("}")
        i += 1
    return out


def rewrite_task_force(save, tf, side, variants, unit_counter):
    """Replaces the task force's ships with the side's; returns the advanced unit counter."""
    start = find_task_force(save, tf.id)
    end = save.block_end(start)
    ships = [i for k, i in save.children(start, end) if k == "ship"]
    if not ships:
        sys.exit(f"task force {tf.name} has no ship to copy")
    old_ids = [int(ID_LINE.search(save.text(i + 1)).group(1)) for i in ships]
    new_lines = []
    n = 0
    for design in side["designs"]:
        for k in range(design["count"]):
            if n < len(ships):
                template, ship_id, fresh = ships[n], old_ids[n], False
            else:
                template, ship_id, fresh = ships[0], unit_counter, True
                unit_counter += 1
            new_lines += ship_block(save, template, ship_id, design, variants[design["design"]], f"{design['design']} {k + 1}", fresh)
            n += 1
    save.replace(ships[0], save.block_end(ships[-1]), new_lines)
    if any(k != "ship" for k, i in save.children(start, end) if ships[0] < i < ships[-1]):
        sys.exit(f"task force {tf.name}: other entries between its ships; not supported")
    for key, i in save.children(start, end):
        text = save.text(i)
        indent = text[: len(text) - len(text.lstrip("\t"))]
        if key == "repair_mode":
            save.replace(i, i, [f"{indent}repair_mode={side['repairMode']}{save.eol(i)}"])
        elif key == "mission":
            rule = next((j for j in range(i, save.block_end(i)) if save.text(j).strip().startswith("navy_engagement_rule=")), None)
            if rule is None:
                sys.exit(f"task force {tf.name} has no engagement rule in its mission")
            rule_indent = save.text(rule)[: len(save.text(rule)) - len(save.text(rule).lstrip("\t"))]
            save.replace(rule, rule, [f"{rule_indent}navy_engagement_rule={side['engagementRule']}{save.eol(rule)}"])
    return unit_counter


def add_technologies(save, country, research_ids):
    """Adds the game technologies of `research_ids` the country lacks; returns (added, warnings)."""
    warnings = []
    known = {r for parts in GAME_TECHS.values() for r in parts}
    unknown = [r for r in research_ids if r not in known]
    if unknown:
        sys.exit(f"no game technology known for researches {unknown}; extend GAME_TECHS")
    wanted = []
    for tech, parts in GAME_TECHS.items():
        present = [p for p in parts if p in research_ids]
        if present:
            wanted.append(tech)
            if len(present) < len(parts):
                warnings.append(f"{country}: {tech} stands for {parts}; the scenario has only {present}")
    countries = save.top_level("countries")
    block = save.child(countries, country)
    technology = save.child(block, "technology")
    technologies = save.child(technology, "technologies")
    have = {k: i for k, i in save.children(technologies)}
    for tech, parts in GAME_TECHS.items():
        if tech in have and tech not in wanted:
            warnings.append(f"{country} has {tech} in the save, which the scenario lacks ({parts})")
    first = next(iter(have.values()))
    sample = [save.text(j) for j in range(first, save.block_end(first) + 1)]
    eol = save.eol(first)
    added = [tech for tech in wanted if tech not in have]
    new_lines = []
    for tech in added:
        new_lines += [sample[0].replace(sample[0].strip().split("=")[0], tech, 1) + eol] + [line + eol for line in sample[1:]]
    if new_lines:
        close = save.block_end(technologies)
        save.replace(close, close - 1, new_lines)
    return added, warnings


def remove_combat(save, location):
    combat = save.top_level("combat")
    removed = 0
    for key, i in list(save.children(combat)):
        if key == "naval_combat" and save.text(save.child(i, "location")).split("=")[1] == str(location):
            save.replace(i, save.block_end(i), [])
            removed += 1
    return removed


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("scenario", help="scenario file in the data directory")
    parser.add_argument("template", help="template save (name in the save games folder, or a path)")
    parser.add_argument("--out", help="output save name (default: <scenario id>_battle_start.hoi4 in the save games folder)")
    parser.add_argument("--data", type=Path, default=REPO / "data")
    parser.add_argument("--defines", action="append", default=[], help="define source for list-ships (experience levels)")
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"), help=".NET 10 SDK executable (env DOTNET)")
    parser.add_argument("--attacker-country", help="attacker's country tag when the template has no combat")
    parser.add_argument("--location", type=int, help="battle province when the template has no combat")
    parser.add_argument("--force", action="store_true", help="overwrite an existing output save")
    args = parser.parse_args()

    template = Path(args.template) if Path(args.template).is_absolute() else SAVE_DIR / args.template
    ships = list_ships(args)
    out = Path(args.out) if args.out and Path(args.out).is_absolute() else SAVE_DIR / (args.out or f"{ships['scenario']}_battle_start.hoi4")
    if out.exists() and not args.force:
        sys.exit(f"{out} exists; pass --force to overwrite")

    parsed = savefile.SaveFile(template)
    save = Lines(template)
    attacker_tf, defender_tf, location = battle_task_forces(save, parsed, args)
    unit_line = next(i for i in range(200) if save.text(i).startswith("unit="))
    unit_counter = start_counter = int(save.text(unit_line).split("=")[1])
    blocks = variant_blocks(save)
    warnings = []
    taken = set()

    for role, tf in (("attacker", attacker_tf), ("defender", defender_tf)):
        side = ships[role]
        if side["spiritIDs"] or side["commander"] or side["doctrine"]:
            warnings.append(f"{role}: spirits, commander and doctrine are not written to the save")
        if any(d["mio"] for d in side["designs"]):
            sys.exit(f"{role}: designs with an MIO are not supported yet")
        variants = pick_variants(save, parsed, tf.country, side["designs"], taken)
        for design in side["designs"]:
            rewrite_variant(save, blocks[variants[design["design"]]][0], design)
        unit_counter = rewrite_task_force(save, tf, side, variants, unit_counter)
        added, tech_warnings = add_technologies(save, tf.country, side["researchIDs"])
        warnings += tech_warnings
        print(f"{role}: {tf.country} task force {tf.id} '{tf.name}', {sum(d['count'] for d in side['designs'])} ships (was {len(tf.ships)})")
        for design in side["designs"]:
            print(f"  {design['count']:>3} {design['design']} ({design['shipType']}, {number(design['hp'])} HP, org {number(design['organization'])}, "
                  f"xp {number(design['experience'])}) -> variant {variants[design['design']]}")
        print(f"  technologies added: {', '.join(added) or 'none'}")

    if unit_counter != start_counter:
        save.replace(unit_line, unit_line, [f"unit={unit_counter}{save.eol(unit_line)}"])
    removed = remove_combat(save, location)
    print(f"battle location {location}; naval combats removed: {removed}")
    save.save(out)
    for warning in warnings:
        print("warning:", warning)

    check = savefile.SaveFile(out)
    variants = check.variants()
    for role, tf in (("attacker", attacker_tf), ("defender", defender_tf)):
        written = next(t for t in check.task_forces() if t.id == tf.id)
        counts = {}
        for ship in written.ships:
            counts[variants[ship.equipment_id].name] = counts.get(variants[ship.equipment_id].name, 0) + 1
        expected = {d["design"]: d["count"] for d in ships[role]["designs"]}
        if counts != expected:
            sys.exit(f"check failed: {role} ships {counts}, expected {expected}")
        experience = {d["design"]: round(d["experience"], 5) for d in ships[role]["designs"]}
        if any(round(ship.experience, 5) != experience[variants[ship.equipment_id].name] for ship in written.ships):
            sys.exit(f"check failed: {role} ship experience differs from {experience}")
        for design in ships[role]["designs"]:
            variant = next(v for v in variants.values() if v.creator == tf.country and v.name == design["design"])
            if variant.modules != design["modules"] or variant.equipment_type != design["hull"]:
                sys.exit(f"check failed: {role} variant {variant.id} does not match {design['design']}")
    print(f"wrote {out}")


if __name__ == "__main__":
    main()
