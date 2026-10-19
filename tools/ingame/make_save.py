"""Write a simulator scenario into a battle save for run_battles.py.

Usage: python make_save.py SCENARIO TEMPLATE [--out NAME] [--data DIR] [--defines PATH] [--dotnet EXE] [--force]

SCENARIO is a scenario file in the data directory (for example jukki_usa_vs_bc_japan.json). TEMPLATE is a plain-text
save with one task force of each side in one sea province, at war, where the battle has started or starts at once
(for example light_attack_vs_torp_cruiser_battle_start.hoi4). The script:
  1. runs `NavySimulator list-ships` for the scenario's ships (design, hull, modules by slot, ship type, max HP,
     organization, manpower, experience);
  2. per side, puts the MIOs it uses into the country's MIO list with its unlocked traits and policy (S-MIO-SAVE), and
     rewrites one of the country's design variants on each design's hull to the design's modules, names it after the
     design (the variant whose ships have the same ship type and the most modules in common) and sets its design team
     and MIO bonus; makes one of the country's navy leaders the side's admiral and the leader of the battle task
     force's fleet (S-ADMIRAL-SAVE);
  3. replaces the ships of the side's task force, reusing its ship ids (new ones from the save's unit counter);
  4. sets the task force's aggressiveness (navy_engagement_rule) and repair priority (repair_mode);
  5. adds the game technologies behind the side's researches (GAME_TECHS) to the country;
  6. writes the side's doctrines into the save's doctrine block (per folder: grand doctrine, and per track the
     subdoctrine, its mastery and unlocked reward count, S-DOCTRINE-SAVE), their sub-unit bonuses as the country's
     deployment unit_modifiers records (loading a save rebuilds the sub-unit stats from those records only), moves the
     country's deployment block before its units (ships clamp their organisation to the sub-unit maximum when they are
     read), and adds its spirits to the country's ideas;
  7. makes the first ship of the side's pride-of-the-fleet design the country's pride of the fleet
     (pride_of_the_fleet={ id type=51 } in the country block, S-POTF-SAVE), or removes the template's when it has none;
  8. sets both countries' naval dominance in the battle region (the one region whose dominance lists both) to their
     ships' total (S-DOMINANCE, as after a long patrol); the template's values are its own fleets';
  9. sets the game rules of GAME_RULES (TFB's admiral command limit off);
 10. removes the naval combat at the battle's location, so the game starts the battle afresh when unpaused;
 11. gives each carrier its air wings (A-WINGS-SAVE): an air base for the ship in strategic_air and, per plane sub-unit,
     an air wing pool of the country with the carrier's wings of the country's variant of each plane; adds the side's
     plane stockpile to the country's equipment and sets the task force's carrier stance (sortie_efficiency).
A design on a hull the country has no free variant of gets a copy of the country's variant on a hull of the same family
(e.g. ship_hull_carrier_2 for ship_hull_carrier_3) with a new id.
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

# Game rules every battle save sets: TFB's Admiral Command Limit (navy_cap) turns a fleet's weighted ship count above
# the cap into a daily naval_damage_factor and torpedo hit chance penalty for the whole country; the simulator models
# one fleet per side and no such cap, so it is off.
GAME_RULES = {"navy_cap": "DISABLED"}

ID_LINE = re.compile(r"\{ id=(\d+) type=(\d+) \}")  # a save handle: id={ id=N type=T }, unit={ id=N type=T }

# A-WINGS-SAVE: mission bits in the order of NAir.MISSION_FUEL_COSTS (A-WINGS finding); a wing's allow_mission_type is
# the OR of its plane's allowed missions (cv_fighter: training, air superiority, interception, naval patrol = 34821, as
# the game writes it).
MISSION_BITS = ["air_superiority", "cas", "interception", "strategic_bomber", "naval_bomber", "drop_nuke", "paradrop",
                "naval_kamikaze", "port_strike", "attack_logistics", "air_supply", "training", "naval_mines_planting",
                "naval_mines_sweeping", "recon", "naval_patrol", "barrage_mission", "sam_mission"]
# A carrier air base's allow_equipment_type as the game writes it for every carrier in the CL3 template.
CARRIER_BASE_EQUIPMENT_TYPES = 4298570752


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
    clones = {}
    for design in designs:
        candidates = [v for v in variants if v.equipment_type == design["hull"] and v.id not in taken
                      and "is_frame=yes" not in (save.text(j).strip() for j in range(*blocks[v.id]))]
        if not candidates:
            # A copy of the country's variant on a hull of the same family (ship_hull_carrier_2 for ship_hull_carrier_3).
            family = re.sub(r"_\d+$", "", design["hull"])
            sources = [v for v in variants if re.sub(r"_\d+$", "", v.equipment_type) == family
                       and "is_frame=yes" not in (save.text(j).strip() for j in range(*blocks[v.id]))]
            if not sources:
                sys.exit(f"{country} has no free design variant on {design['hull']} (or its family) to hold {design['design']}")
            source = max(sources, key=lambda v: (v.equipment_type, v.id))
            new_id = max(max(blocks), max(clones.values(), default=0)) + 1
            clones[design["design"]] = new_id
            chosen[design["design"]] = new_id
            taken.add(new_id)
            design["cloneOf"] = source.id
            continue
        best = max(candidates, key=lambda v: (design["shipType"] in types_by_variant.get(v.id, ()),
                                              sum(v.modules.get(slot) == module for slot, module in design["modules"].items()),
                                              -v.id))
        chosen[design["design"]] = best.id
        taken.add(best.id)
    return chosen, clones


def clone_variant(save, source_start, hull, new_id, design, mio_id):
    """Inserts a copy of the variant at `source_start` after it, on `hull` with id `new_id`, rewritten for `design` (as
    rewrite_variant does; the copy has no parent)."""
    end = save.block_end(source_start)
    eol = save.eol(source_start)
    lines = [save.text(i) for i in range(source_start, end + 1)]
    indent = lines[0][: len(lines[0]) - len(lines[0].lstrip("\t"))]
    inner = indent + "\t"
    out = [f"{indent}{hull}={{"]
    i = 1
    while i < len(lines) - 1:
        line = lines[i]
        key = line.strip().split("=", 1)[0]
        block_end = i
        if line.strip().endswith("{"):
            depth = 0
            for j in range(i, len(lines)):
                depth += lines[j].count("{") - lines[j].count("}")
                if depth <= 0:
                    block_end = j
                    break
        if line.startswith(inner) and not line.startswith(inner + "\t"):
            if key == "id" and "type=70" in line:
                out.append(f"{inner}id={{ id={new_id} type=70 }}")
            elif key == "name":
                out.append(f'{inner}name="{design["design"]}"')
            elif key in ("parent_id", "design_team", "design_team_bonus"):
                pass
            elif key == "modules":
                out += [f"{inner}modules={{"] + [f"{inner}\t{slot}={module}" for slot, module in design["modules"].items()] + [f"{inner}}}"]
                if mio_id is not None:
                    out.append(f"{inner}design_team={{ id={mio_id} type=79 }}")
                    if design["mioBonus"]:
                        out += [f"{inner}design_team_bonus={{"] + [f"{inner}\t{k}={number(v)}" for k, v in design["mioBonus"].items()] + [f"{inner}}}"]
            else:
                out += lines[i:block_end + 1]
        else:
            out += lines[i:block_end + 1]
        i = block_end + 1
    out.append(lines[-1])
    save.replace(end + 1, end, [line + eol for line in out])


def max_handle(save, handle_type):
    """The largest id of save handles `{ id=N type=T }` of one type (new handles take the next ones)."""
    pattern = re.compile(r"\{ id=(\d+) type=" + str(handle_type) + r" \}")
    return max((int(m.group(1)) for line in save.lines for m in pattern.finditer(line)), default=0)


def plane_variants(save, parsed, country, plane_ids):
    """Plane id -> the country's variant of it (not obsolete first, then the newest)."""
    blocks = variant_blocks(save)
    result = {}
    for plane in plane_ids:
        candidates = [v for v in parsed.variants().values() if v.creator == country and v.equipment_type == plane]
        if not candidates:
            sys.exit(f"{country} has no variant of {plane}; research it in the template save first")
        result[plane] = max(candidates, key=lambda v: ("obsolete=yes" not in (save.text(j).strip() for j in range(*blocks[v.id])), v.id)).id
    return result


def mission_mask(missions):
    return sum(1 << MISSION_BITS.index(m) for m in missions if m in MISSION_BITS)


def write_carrier_wings(save, country, side, carrier_ids, variants, ids):
    """A-WINGS-SAVE: per carrier an air base (strategic_air, `carrier={ id type=51 }`, capacity = deck size x
    CARRIER_SIZE_STAT_INCREMENT) and, per plane sub-unit on it, an air wing pool of the country holding the carrier's
    wings (count, manpower, the variant, allowed missions; experience 0, no mission). Returns (bases, pools, planes)."""
    air = save.top_level("strategic_air")
    air_end = save.block_end(air)
    children = list(save.children(air, air_end))
    last_base = max(i for k, i in children if k == "air_base")
    country_line = next((i for k, i in children if k == country), None)
    eol = save.eol(air)
    bases, pools, total = [], [], 0
    for design in side["designs"]:
        for k, wings in enumerate(design.get("carrierWings") or []):
            if not wings:
                continue
            ship_id = carrier_ids[design["design"]][k]
            ids[65] += 1
            ids[4713] += 1
            capacity = round(design["stats"]["CarrierSize"] * 10)
            bases += [f"\tair_base={{", f"\t\tid={{ id={ids[65]} type=65 }}", f"\t\tcarrier={{ id={ship_id} type=51 }}",
                      f"\t\tcapacity={capacity}", "\t\tcountries={", f"\t\t\tid={{ id={ids[4713]} type=4713 }}",
                      "\t\t\tmotorization_level=0", f'\t\t\tcountry="{country}"', "\t\t\tcapacity_penalty=1", "\t\t}",
                      "\t\tbase=0", "\t\tlevel=1", f"\t\tallow_equipment_type={CARRIER_BASE_EQUIPMENT_TYPES}", "\t}"]
            by_pool = {}
            for wing in wings:
                by_pool.setdefault(wing["plane"]["subUnit"] or wing["plane"]["id"], []).append(wing)
            for sub_unit, pool_wings in by_pool.items():
                ids[4713] += 1
                pools += ["\t\tair_wing_pool={", f"\t\t\tid={{ id={ids[4713]} type=4713 }}", f"\t\t\tdefinition={sub_unit}",
                          f"\t\t\tair_base={{ id={ids[65]} type=65 }}"]
                n = 0
                for wing in pool_wings:
                    for _ in range(wing["airwings"]):
                        ids[69] += 1
                        n += 1
                        planes = wing["planes"]
                        total += planes
                        pools += ["\t\t\tair_wings={", f"\t\t\t\tid={{ id={ids[69]} type=69 }}", f"\t\t\t\tcount={planes}",
                                  "\t\t\t\texperience=0", "\t\t\t\treinforcement_setting=1", "\t\t\t\ttimed_disabling={",
                                  "\t\t\t\t\tremaining_hours=0", "\t\t\t\t\tshould_start_on_transfer=no", "\t\t\t\t}",
                                  f"\t\t\t\tmanpower={planes * 40}", "\t\t\t\tmission={", "\t\t\t\t\ttype=0", "\t\t\t\t\tperiod=2",
                                  "\t\t\t\t\tactive=no", "\t\t\t\t\tmissions_done=0", "\t\t\t\t}", f'\t\t\t\ttag="{country}"',
                                  "\t\t\t\tequipment={", "\t\t\t\t\tequipment={", f"\t\t\t\t\t\tid={{ id={variants[wing['plane']['id']]} type=70 }}",
                                  f"\t\t\t\t\t\tamount={planes}", "\t\t\t\t\t}", "\t\t\t\t\tallow_zero_entries=yes", "\t\t\t\t}",
                                  f'\t\t\t\tname="{design["design"]} {k + 1} {sub_unit} {n}"', "\t\t\t\tpriority=1",
                                  f"\t\t\t\tallow_mission_type={mission_mask(wing['plane']['missions'])}",
                                  "\t\t\t\tair_untrained_pilots_penalty_factor=1", "\t\t\t}"]
                pools.append("\t\t}")
    if not bases:
        return 0, 0, 0
    save.replace(save.block_end(last_base) + 1, save.block_end(last_base), [line + eol for line in bases])
    if country_line is not None:
        save.replace(country_line + 1, country_line, [line + eol for line in pools])
    else:
        save.replace(air_end, air_end - 1, [line + eol for line in [f"\t{country}={{"] + pools + ["\t}"]])
    return len(bases) // 14, sum(1 for line in pools if line == "\t\tair_wing_pool={"), total


def write_stockpile(save, country, stockpile, variants):
    """A-REINFORCE: adds the side's planes in stock to the country's production equipments (variant, amount)."""
    if not stockpile:
        return
    block = save.child(save.top_level("countries"), country)
    production = save.child(block, "production")
    equipments = save.child(production, "equipments")
    if equipments is None:
        sys.exit(f"{country} has no production equipments block; unknown save layout")
    indent = save.text(equipments)[: len(save.text(equipments)) - len(save.text(equipments).lstrip("\t"))] + "\t"
    eol = save.eol(equipments)
    lines = []
    for entry in stockpile:
        lines += [f"{indent}equipment={{", f"{indent}\tid={{ id={variants[entry['plane']['id']]} type=70 }}", f"{indent}\tamount={entry['count']}", f"{indent}}}"]
    save.replace(equipments + 1, equipments, [line + eol for line in lines])


def write_carrier_stance(save, tf, stance):
    """A-STANCE: the task force's carrier stance is its sortie_efficiency (an index into the stance ratios)."""
    if stance is None:
        return
    start = find_task_force(save, tf.id)
    line = save.child(start, "sortie_efficiency")
    if line is None:
        sys.exit(f"task force {tf.name} has no sortie_efficiency; unknown save layout")
    text = save.text(line)
    save.replace(line, line, [f"{text[: len(text) - len(text.lstrip(chr(9)))]}sortie_efficiency={stance}{save.eol(line)}"])


def rewrite_variant(save, start, design, mio_id):
    """Names the variant after the design, sets its modules and its design team: S-MIO-SAVE `design_team={ id type=79 }`
    (the MIO's handle in the country's list, `mio_id`; None removes it) and `design_team_bonus={ ... }`, the MIO bonus
    the game took when the variant got its MIO (S-MIO-TIMING; written only when not empty, as the game does)."""
    end = save.block_end(start)
    name = next(i for k, i in save.children(start, end) if k == "name")
    indent = save.text(name)[: len(save.text(name)) - len(save.text(name).lstrip("\t"))]
    eol = save.eol(name)
    save.replace(name, name, [f'{indent}name="{design["design"]}"{eol}'])
    modules = next(i for k, i in save.children(start, end) if k == "modules")
    modules_end = save.block_end(modules)
    new = [f"{indent}modules={{{eol}"] + [f"{indent}\t{slot}={module}{eol}" for slot, module in design["modules"].items()] + [f"{indent}}}{eol}"]
    if mio_id is not None:
        new.append(f"{indent}design_team={{ id={mio_id} type=79 }}{eol}")
        if design["mioBonus"]:
            new += [f"{indent}design_team_bonus={{{eol}"] + [f"{indent}\t{key}={number(value)}{eol}" for key, value in design["mioBonus"].items()] + [f"{indent}}}{eol}"]
    save.replace(modules, modules_end, new)
    for key, i in save.children(start, end):
        if key in ("design_team", "design_team_bonus"):
            save.replace(i, save.block_end(i) if save.text(i).endswith("{") else i, [])


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
    """Replaces the task force's ships with the side's; returns the advanced unit counter and the save id of the side's
    pride of the fleet (the first ship of its design; None without one)."""
    start = find_task_force(save, tf.id)
    end = save.block_end(start)
    ships = [i for k, i in save.children(start, end) if k == "ship"]
    if not ships:
        sys.exit(f"task force {tf.name} has no ship to copy")
    old_ids = [int(ID_LINE.search(save.text(i + 1)).group(1)) for i in ships]
    new_lines = []
    n = 0
    pride = side.get("prideOfTheFleet")
    pride_id = None
    side["shipIds"] = {}
    for design in side["designs"]:
        for k in range(design["count"]):
            if n < len(ships):
                template, ship_id, fresh = ships[n], old_ids[n], False
            else:
                template, ship_id, fresh = ships[0], unit_counter, True
                unit_counter += 1
            if pride and design["design"] == pride["design"] and k == 0:
                pride_id = ship_id
            side["shipIds"].setdefault(design["design"], []).append(ship_id)
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
    return unit_counter, pride_id


def write_pride_of_the_fleet(save, country, ship_id):
    """S-POTF-SAVE: sets the country's pride_of_the_fleet={ id=<ship id> type=51 }, which the game writes before
    pride_of_the_fleet_date_lost and only when set; with no ship it removes the line, so no template ship is the pride."""
    block = save.child(save.top_level("countries"), country)
    current = save.child(block, "pride_of_the_fleet")
    date = save.child(block, "pride_of_the_fleet_date_lost")
    anchor = current if current is not None else date
    if anchor is None:
        sys.exit(f"{country} has no pride_of_the_fleet_date_lost; unknown save layout")
    text = save.text(anchor)
    indent = text[: len(text) - len(text.lstrip("\t"))]
    new = [] if ship_id is None else [f"{indent}pride_of_the_fleet={{ id={ship_id} type=51 }}{save.eol(anchor)}"]
    if current is not None:
        save.replace(current, current, new)
    elif new:
        save.replace(date, date - 1, new)


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


def doctrine_entry(save, country):
    """The line of the country's entry in doctrine={ countries={ { ... country="TAG" } } }."""
    countries = save.child(save.top_level("doctrine"), "countries")
    for key, i in save.children(countries):
        if key == "{" and any(save.text(j).strip() == f'country="{country}"' for _, j in save.children(i)):
            return i
    sys.exit(f"{country} has no entry in the save's doctrine block")


def folder_lines(indent, eol, setup):
    """S-DOCTRINE-SAVE: one folder entry: grand doctrine, folder, then one track entry per track of the grand doctrine in
    its order (rewards = unlocked count, mastery cumulative, empty tracks without sub_doctrine), then the subdoctrines of
    the complete tracks."""
    slotted = {track["track"]: track for track in setup["tracks"]}
    t = "\t"
    lines = [f"{indent}{{", f'{indent}{t}grand_doctrine="{setup["grandDoctrine"]}"', f'{indent}{t}folder="{setup["folder"]}"',
             f"{indent}{t}tracks={{"]
    for track_id in setup["trackOrder"]:
        track = slotted.get(track_id)
        lines.append(f"{indent}{t * 2}{{")
        if track:
            lines.append(f'{indent}{t * 3}sub_doctrine="{track["subdoctrine"]}"')
        lines += [f"{indent}{t * 3}rewards={track['rewards'] if track else 0}",
                  f"{indent}{t * 3}mastery={number(track['mastery']) if track else 0}",
                  f"{indent}{t * 3}mastery_bank=0", f"{indent}{t * 3}daily_mastery=0", f"{indent}{t * 2}}}"]
    lines.append(f"{indent}{t}}}")
    complete = [track["subdoctrine"] for track in setup["tracks"] if track["complete"]]
    if complete:
        lines += [f"{indent}{t}completed_subdoctrines={{"] + [f'{indent}{t * 2}"{sub}"' for sub in complete] + [f"{indent}{t}}}"]
    lines.append(f"{indent}}}")
    return [line + eol for line in lines]


def doctrine_folders(data):
    """The imported doctrine folders (data/doctrines/*.json) by folder ID."""
    folders = {}
    for path in sorted((data / "doctrines").glob("*.json")):
        folder = json.loads(path.read_text(encoding="utf-8"))
        folders[folder["id"]] = folder
    return folders


def unit_modifier_records(setup, folder):
    """S-DOCTRINE-SAVE: (sub-unit blocks, type, number, id) for every active part with sub-unit bonuses: the grand doctrine,
    each slotted subdoctrine, its unlocked rewards (number = reward index) and the milestone of each complete track
    (number = the track's index in the grand doctrine)."""
    grand = next(g for g in folder["grandDoctrines"] if g["id"] == setup["grandDoctrine"])
    subdoctrines = {s["id"]: s for s in folder["subdoctrines"]}
    records = []

    def add(effects, kind, index, owner):
        units = (effects or {}).get("unitBonuses") or {}
        if units:
            records.append((units, kind, index, owner))

    add(grand.get("effects"), "grand_doctrine", 0, grand["id"])
    for track in setup["tracks"]:
        sub = subdoctrines[track["subdoctrine"]]
        add(sub.get("effects"), "sub_doctrine", 0, sub["id"])
        for index, reward in enumerate(sub.get("rewards", [])[: track["rewards"]]):
            add(reward.get("effects"), "rewards", index, sub["id"])
        if track["complete"]:
            add(grand.get("milestones", {}).get(track["track"]), "milestones", setup["trackOrder"].index(track["track"]), grand["id"])
    return records


def record_lines(indent, eol, records):
    t = "\t"
    lines = []
    for units, kind, index, owner in records:
        lines.append(f"{indent}{{")
        for unit, stats in units.items():
            lines += [f"{indent}{t}{unit}={{"] + [f"{indent}{t * 2}{stat}={number(value)}" for stat, value in stats.items()] + [f"{indent}{t}}}"]
        lines += [f"{indent}{t}type={kind}", f"{indent}{t}number={index}", f"{indent}{t}id={owner}", f"{indent}}}"]
    return [line + eol for line in lines]


def write_unit_modifiers(save, country, records, replaced_ids, move_deployment):
    """Adds the records to the country's deployment={ unit_modifiers={ } }, dropping the template's records of
    `replaced_ids` (the doctrines of the folders written anew), and, with move_deployment, moves the deployment block
    before the units block. Returns where the deployment block now is ("before units" or "after units")."""
    block = save.child(save.top_level("countries"), country)
    children = {key: i for key, i in save.children(block)}
    deployment, units = children.get("deployment"), children.get("units")
    if deployment is None:
        sys.exit(f"{country} has no deployment block")
    end = save.block_end(deployment)
    lines = [save.lines[i] for i in range(deployment, end + 1)]
    existing = save.child(deployment, "unit_modifiers")
    if existing is not None:
        stale = [i for key, i in save.children(existing)
                 if key == "{" and any(save.text(j).strip() in (f"id={owner}" for owner in replaced_ids) for _, j in save.children(i))]
        for i in reversed(stale):
            del lines[i - deployment: save.block_end(i) - deployment + 1]
    if records:
        sample = save.text(deployment + 1)
        indent = sample[: len(sample) - len(sample.lstrip("\t"))]
        eol = save.eol(deployment)
        if existing is not None:
            at = save.block_end(existing) - deployment - sum(save.block_end(i) - i + 1 for i in stale)
            lines[at:at] = record_lines(indent + "\t", eol, records)
        else:
            hq = save.child(deployment, "default_hq_template")
            at = (hq if hq is not None else deployment) - deployment + 1
            lines[at:at] = [f"{indent}unit_modifiers={{{eol}"] + record_lines(indent + "\t", eol, records) + [f"{indent}}}{eol}"]
    # Without records the block stays where it is, so saves of scenarios without doctrines are unchanged.
    if records and move_deployment and units is not None and units < deployment:
        save.replace(deployment, end, [])
        save.replace(units, units - 1, lines)
        return "before units"
    if lines != save.lines[deployment:end + 1]:
        save.replace(deployment, end, lines)
    return "before units" if units is None or deployment < units else "after units"


def write_doctrines(save, country, doctrines):
    """Replaces the country's folder entries of the side's doctrine folders; returns the folders written."""
    entry = doctrine_entry(save, country)
    folders = save.child(entry, "folder")
    if folders is None:
        sys.exit(f"{country}'s doctrine entry has no folder block")
    by_folder = {}
    for key, i in save.children(folders):
        if key == "{":
            name = next((save.text(j).strip().split("=", 1)[1].strip('"') for k, j in save.children(i) if k == "folder"), None)
            by_folder[name] = i
    written = []
    for setup in doctrines:
        start = by_folder.get(setup["folder"])
        if start is None:
            sys.exit(f"{country}'s doctrine entry has no {setup['folder']} folder")
        text = save.text(start)
        save.replace(start, save.block_end(start), folder_lines(text[: len(text) - len(text.lstrip("\t"))], save.eol(start), setup))
        written.append(setup["folder"])
    return written


def add_spirits(save, country, spirit_ids):
    """S-SPIRIT: adds the spirits to the country's politics={ ideas={ ... } } list (the game rebuilds idea modifiers from it)."""
    politics = save.child(save.child(save.top_level("countries"), country), "politics")
    ideas = save.child(politics, "ideas") if politics is not None else None
    if ideas is None:
        sys.exit(f"{country} has no ideas block")
    end = save.block_end(ideas)
    present = {word for j in range(ideas + 1, end) for word in save.text(j).split()}
    added = [spirit for spirit in spirit_ids if spirit not in present]
    if added:
        line = end - 1
        save.replace(line, line, [save.text(line).rstrip() + " " + " ".join(added) + " " + save.eol(line)])
    return added


MIO_HANDLE = re.compile(r"\{ id=(\d+) type=79 \}")
MIO_STATE_KEYS = ("unlocked", "queued_trait", "policy", "cooldown")


def mio_list(save, country):
    """S-MIO-SAVE: the line of the country's production={ industrial_organisations={ ... } } block."""
    production = save.child(save.child(save.top_level("countries"), country), "production")
    organisations = save.child(production, "industrial_organisations") if production is not None else None
    if organisations is None:
        sys.exit(f"{country} has no industrial_organisations block")
    return organisations


def find_mio_block(save, mio):
    """The first block of `mio` in any country's MIO list (every country's own MIOs are in the save)."""
    for i, line in enumerate(save.lines):
        if save.text(i).strip() == f"{mio}={{" and save.text(i + 1).strip().startswith("id={") and MIO_HANDLE.search(save.text(i + 1)):
            return i
    sys.exit(f"MIO {mio} is in no country's list of the template")


def mio_lines(save, start, mio_id, setup, keep_history):
    """S-MIO-SAVE [RE] (writer 0xdbccc0, reader 0xdbb350): the MIO block at `start` with handle `mio_id`, one
    `unlocked={ trait="<trait>" }` block per unlocked trait (after `points`; the trait reference reads only `trait`,
    0xa50690; the initial trait is not listed) and `policy=<policy>` (a token, before `variables`), dropping its former
    trait, queue, policy and cooldown state; without `keep_history` also its production history (another country's
    variants)."""
    end = save.block_end(start)
    eol = save.eol(start)
    indent = save.text(start + 1)[: len(save.text(start + 1)) - len(save.text(start + 1).lstrip("\t"))]
    out = [save.text(start) + eol]
    for key, i in save.children(start, end):
        last = save.block_end(i) if save.text(i).endswith("{") else i
        if key in MIO_STATE_KEYS or (key == "history" and not keep_history):
            continue
        if key == "variables" and setup and setup["policy"]:
            out.append(f"{indent}policy={setup['policy']}{eol}")
        if key == "id":
            out.append(f"{indent}id={{ id={mio_id} type=79 }}{eol}")
        else:
            out += [save.text(j) + save.eol(j) for j in range(i, last + 1)]
        if key == "points" and setup:
            for trait in setup["traits"]:
                out += [f"{indent}unlocked={{{eol}", f'{indent}\ttrait="{trait}"{eol}', f"{indent}}}{eol}"]
    out.append(save.text(end) + save.eol(end))
    return out


def write_mios(save, country, side, next_id):
    """S-MIO-SAVE: puts each MIO the side uses (its designs' and the ones it set up) into the country's MIO list with the
    side's unlocked traits and policy: the country's own block in place, else a copy of another country's block with a
    new handle (the reader 0xe59400 creates an MIO for any key the MIO database knows; `allowed` is not checked on load).
    Returns ({MIO: handle}, the next free handle)."""
    organisations = mio_list(save, country)
    own = {key: i for key, i in save.children(organisations)}
    setups = {mio["id"]: mio for mio in side["mios"]}
    used = list(dict.fromkeys([d["mio"] for d in side["designs"] if d["mio"]] + list(setups)))
    handles, added = {}, []
    for mio in used:
        if mio in own:
            start = own[mio]
            handles[mio] = int(MIO_HANDLE.search(save.text(start + 1)).group(1))
            save.replace(start, save.block_end(start), mio_lines(save, start, handles[mio], setups.get(mio), True))
        else:
            handles[mio] = next_id
            next_id += 1
            added += mio_lines(save, find_mio_block(save, mio), handles[mio], setups.get(mio), False)
    if added:
        close = save.block_end(organisations)
        save.replace(close, close - 1, added)
    return handles, next_id


def find_character(save, country):
    """(character line, navy_leader line) of the country's first character with a navy_leader block."""
    manager = save.top_level("character_manager")
    for _, group in save.children(manager):
        if not save.text(group).endswith("{"):
            continue
        for key, i in save.children(group):
            if key != "character":
                continue
            children = dict((k, j) for k, j in save.children(i))
            if "navy_leader" in children and "country" in children and save.text(children["country"]).strip() == f'country="{country}"':
                return i, children["navy_leader"]
    sys.exit(f"{country} has no admiral (navy_leader character) in the template to stand in for the side's")


def write_admiral(save, country, tf, admiral):
    """S-ADMIRAL-SAVE [RE] (fleet writer 0xd5ebc0, reader 0xd582b0; leader writers 0xc1ce70, 0xc283f0): the side's
    admiral is one of the country's navy leaders rewritten with the admiral's name, skill levels and traits, set as the
    leader of the fleet holding the battle task force (`leader={ id type=4713 }`, the navy_leader handle, first in the
    fleet block after its id). The game saves no assignment date, so the admiral loads with his modifiers enabled.
    Returns the leader's handle id, or None when the side has no admiral."""
    start = find_task_force(save, tf.id)
    fleet = next(j for j in range(start, -1, -1) if save.text(j).strip() == "fleet={")
    old_leader = save.child(fleet, "leader")
    if admiral is None:
        if old_leader is not None:
            save.replace(old_leader, old_leader, [])
        return None
    character, leader = find_character(save, country)
    eol = save.eol(leader)
    for key, i in save.children(character):
        if key == "name":
            text = save.text(i)
            save.replace(i, i, [f'{text[: len(text) - len(text.lstrip())]}name="{admiral["name"]}"{eol}'])
    handle = int(ID_LINE.search(save.text(leader + 1)).group(1))
    indent = save.text(leader + 1)[: len(save.text(leader + 1)) - len(save.text(leader + 1).lstrip("\t"))]
    skills = admiral["skills"]
    out = [save.text(leader) + eol]
    for key, i in save.children(leader):
        last = save.block_end(i) if save.text(i).endswith("{") else i
        if key == "traits":
            continue
        if key == "name":
            out.append(f'{indent}name="{admiral["name"]}"{eol}')
        elif key in skills:
            out.append(f"{indent}{key}={skills[key]}{eol}")
        elif key == "in_progress":
            # progress toward traits the admiral already has is dropped
            out += [save.text(j) + save.eol(j) for j in range(i, last + 1) if save.text(j).strip().split("=")[0] not in admiral["traits"]]
        else:
            out += [save.text(j) + save.eol(j) for j in range(i, last + 1)]
        if key == "script_id" and admiral["traits"]:
            out.append(f"{indent}traits={{ {' '.join(admiral['traits'])} }}{eol}")
    out.append(save.text(save.block_end(leader)) + eol)
    if not any(save.text(i).strip().startswith("script_id=") for _, i in save.children(leader)):
        sys.exit(f"{country}'s navy leader {handle} has no script_id line to put the traits after")
    missing = [key for key in skills if not any(k == key for k, _ in save.children(leader))]
    if missing:
        sys.exit(f"{country}'s navy leader {handle} has no {', '.join(missing)} line")
    save.replace(leader, save.block_end(leader), out)
    fleet_id = fleet + 1
    text = save.text(fleet_id)
    line = f"{text[: len(text) - len(text.lstrip())]}leader={{ id={handle} type=4713 }}{save.eol(fleet_id)}"
    if old_leader is not None:
        save.replace(old_leader, old_leader, [line])
    else:
        save.replace(fleet_id + 1, fleet_id, [line])
    return handle


DOMINANCE_KEYS = ("current", "target", "base_target", "previous", "decline_from")
DOMINANCE_ENTRY = re.compile(r'\{ "(\w+)"')  # a region dominance entry opens with { "TAG", its values follow in a block


def region_dominance_lines(save, countries):
    """S-DOMINANCE-SAVE: [(region id, {country: {key: line}})] for the regions whose dominance lists all `countries`."""
    result = []
    for region, i in save.children(save.top_level("region")):
        dominance = save.child(i, "dominance")
        listed = None if dominance is None else save.child(dominance, "countries")
        if listed is None or not all(country in save.text(listed).split("=", 1)[1].strip("{} ").split() for country in countries):
            continue
        lines, tag = {}, None
        for j in range(dominance + 1, save.block_end(dominance)):
            text = save.text(j).strip()
            match = DOMINANCE_ENTRY.fullmatch(text)
            if match:
                tag = match.group(1)
            elif tag is not None and text.split("=", 1)[0] in DOMINANCE_KEYS + ("individual_ratio",):
                lines.setdefault(tag, {})[text.split("=", 1)[0]] = j
        result.append((region, lines))
    return result


def write_dominance(save, totals):
    """S-DOMINANCE-SAVE: sets each country's current, target, base_target, previous and decline_from in the battle region
    to its ships' total; individual_ratio (current / (current + the region's own value)) is rescaled to it. Returns the
    region id, or None when not exactly one region lists all the countries with values."""
    regions = region_dominance_lines(save, list(totals))
    if len(regions) != 1 or any("current" not in regions[0][1].get(country, {}) for country in totals):
        return None
    region, lines = regions[0]
    for country, total in totals.items():
        entry = lines[country]
        old = float(save.text(entry["current"]).split("=", 1)[1])
        for key in DOMINANCE_KEYS:
            if key in entry:
                text = save.text(entry[key])
                save.replace(entry[key], entry[key], [f"{text[: len(text) - len(text.lstrip())]}{key}={number(total)}{save.eol(entry[key])}"])
        if "individual_ratio" in entry:
            text = save.text(entry["individual_ratio"])
            ratio = float(text.split("=", 1)[1])
            own = old * (1 / ratio - 1) if ratio > 0 else 0
            save.replace(entry["individual_ratio"], entry["individual_ratio"],
                         [f"{text[: len(text) - len(text.lstrip())]}individual_ratio={number(total / (total + own) if total + own > 0 else 0)}{save.eol(entry['individual_ratio'])}"])
    return region


def write_game_rules(save):
    """Sets GAME_RULES in the top-level game_rules={ <rule>="<option>" } block (CGameRulesInstance::ReadMember 0xa37dc0:
    the key is the rule, the value its option; rules left out keep their default)."""
    block = save.top_level("game_rules")
    eol = save.eol(block)
    present = {key: i for key, i in save.children(block)}
    for rule, option in GAME_RULES.items():
        line = f'\t{rule}="{option}"{eol}'
        if rule in present:
            save.replace(present[rule], present[rule], [line])
        else:
            close = save.block_end(block)
            save.replace(close, close - 1, [line])


def remove_combat(save, location):
    combat = save.top_level("combat")
    removed = 0
    for key, i in list(save.children(combat)):
        if key == "naval_combat" and save.text(save.child(i, "location")).split("=")[1] == str(location):
            save.replace(i, save.block_end(i), [])
            removed += 1
    return removed


def check_mios_and_admiral(save, tf, side):
    """Reads the written MIOs, design teams and fleet leader back."""
    organisations = mio_list(save, tf.country)
    blocks = {key: i for key, i in save.children(organisations)}
    for mio, handle in side["mioHandles"].items():
        if mio not in blocks or int(MIO_HANDLE.search(save.text(blocks[mio] + 1)).group(1)) != handle:
            sys.exit(f"check failed: {tf.country} MIO {mio} with handle {handle} not in its list")
        values = [save.text(i).strip() for _, i in save.children(blocks[mio])]
        unlocked = [save.text(i + 1).strip() for key, i in save.children(blocks[mio]) if key == "unlocked"]
        setup = next((m for m in side["mios"] if m["id"] == mio), None)
        expected = [f'trait="{trait}"' for trait in setup["traits"]] if setup else []
        if unlocked != expected or \
                [v for v in values if v.startswith("policy=")] != ([f"policy={setup['policy']}"] if setup and setup["policy"] else []):
            sys.exit(f"check failed: {tf.country} MIO {mio} traits or policy differ from the scenario")
    teams = {}
    for _, i in save.children(save.top_level("equipments")):
        children = {k: j for k, j in save.children(i)}
        if "creator" in children and save.text(children["creator"]).strip() == f'creator="{tf.country}"' and "name" in children:
            team = children.get("design_team")
            teams[save.text(children["name"]).strip()[len('name="'):-1]] = int(MIO_HANDLE.search(save.text(team)).group(1)) if team is not None else None
    for design in side["designs"]:
        if teams.get(design["design"]) != side["mioHandles"].get(design["mio"]):
            sys.exit(f"check failed: {tf.country} variant {design['design']} design team {teams.get(design['design'])}, expected MIO {design['mio']}")
    start = find_task_force(save, tf.id)
    fleet = next(j for j in range(start, -1, -1) if save.text(j).strip() == "fleet={")
    leader = save.child(fleet, "leader")
    handle = int(ID_LINE.search(save.text(leader)).group(1)) if leader is not None else None
    if handle != side["leaderHandle"]:
        sys.exit(f"check failed: {tf.country} fleet leader {handle}, expected {side['leaderHandle']}")


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
    parser.add_argument("--keep-block-order", action="store_true",
                        help="do not move the countries' deployment blocks (doctrine sub-unit records) before their units")
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
    data_folders = doctrine_folders(args.data)
    warnings = []
    taken = set()
    next_mio = max(int(m.group(1)) for line in save.lines for m in MIO_HANDLE.finditer(line)) + 1
    ids = {65: max_handle(save, 65), 69: max_handle(save, 69), 4713: max_handle(save, 4713)}

    for role, tf in (("attacker", attacker_tf), ("defender", defender_tf)):
        side = ships[role]
        for bonus in side["equipmentBonuses"]:
            warnings.append(f"{role}: equipment bonus {bonus['bonus']} (on {', '.join(bonus['designs'])}) is not written to the save")
        if any(setup["folder"] != "naval" for setup in side["doctrines"]):
            warnings.append(f"{role}: air doctrines are written, but the simulator does not simulate them yet")
        mio_handles, next_mio = write_mios(save, tf.country, side, next_mio)
        side["mioHandles"] = mio_handles
        variants, clones = pick_variants(save, parsed, tf.country, side["designs"], taken)
        for design in side["designs"]:
            if design["design"] in clones:
                clone_variant(save, blocks[design["cloneOf"]][0], design["hull"], clones[design["design"]], design, mio_handles.get(design["mio"]))
            else:
                rewrite_variant(save, blocks[variants[design["design"]]][0], design, mio_handles.get(design["mio"]))
        side["leaderHandle"] = write_admiral(save, tf.country, tf, side.get("admiral"))
        unit_counter, pride_id = rewrite_task_force(save, tf, side, variants, unit_counter)
        write_pride_of_the_fleet(save, tf.country, pride_id)
        side["prideId"] = pride_id
        plane_ids = {w["plane"]["id"] for d in side["designs"] for wings in (d.get("carrierWings") or []) for w in wings}
        plane_ids |= {entry["plane"]["id"] for entry in side["planeStockpile"]}
        planes = plane_variants(save, parsed, tf.country, sorted(plane_ids))
        side["air"] = write_carrier_wings(save, tf.country, side, side["shipIds"], planes, ids)
        write_stockpile(save, tf.country, side["planeStockpile"], planes)
        write_carrier_stance(save, tf, side.get("carrierStanceIndex"))
        if side["landBasedAirwings"]:
            warnings.append(f"{role}: land-based planes are not written to the save")
        added, tech_warnings = add_technologies(save, tf.country, side["researchIDs"])
        warnings += tech_warnings
        folders = write_doctrines(save, tf.country, side["doctrines"])
        records = [record for setup in side["doctrines"] for record in unit_modifier_records(setup, data_folders[setup["folder"]])]
        replaced = {doctrine["id"] for setup in side["doctrines"] for key in ("grandDoctrines", "subdoctrines")
                    for doctrine in data_folders[setup["folder"]][key]}
        deployment = write_unit_modifiers(save, tf.country, records, replaced, not args.keep_block_order)
        spirits = add_spirits(save, tf.country, side["spiritIDs"])
        print(f"{role}: {tf.country} task force {tf.id} '{tf.name}', {sum(d['count'] for d in side['designs'])} ships (was {len(tf.ships)})")
        for design in side["designs"]:
            print(f"  {design['count']:>3} {design['design']} ({design['shipType']}, {number(design['hp'])} HP, org {number(design['organization'])}, "
                  f"xp {number(design['experience'])}) -> variant {variants[design['design']]}"
                  + (f" (new, copied from {design['cloneOf']})" if design['design'] in clones else ""))
        bases, pool_count, plane_total = side["air"]
        if bases:
            print(f"  carriers: {bases} air bases, {pool_count} wing pools, {plane_total} planes ({', '.join(f'{k} variant {v}' for k, v in planes.items())}); "
                  f"stance {side.get('carrierStanceIndex')}")
        if side["planeStockpile"]:
            print("  plane stockpile: " + ", ".join(f"{e['count']} {e['plane']['id']}" for e in side["planeStockpile"]))
        for mio, handle in mio_handles.items():
            setup = next((m for m in side["mios"] if m["id"] == mio), None)
            print(f"  MIO {mio} (handle {handle}): {len(setup['traits']) if setup else 0} traits unlocked, policy {(setup or {}).get('policy') or 'none'}")
        admiral = side.get("admiral")
        print(f"  admiral: {admiral['name'] + ' (navy leader ' + str(side['leaderHandle']) + ', ' + ', '.join(admiral['traits']) + ')' if admiral else 'none'}")
        print(f"  technologies added: {', '.join(added) or 'none'}")
        print(f"  pride of the fleet: {'ship ' + str(pride_id) + ' (' + side['prideOfTheFleet']['design'] + ')' if pride_id is not None else 'none'}")
        for setup in side["doctrines"]:
            tracks = ", ".join(f"{t['track']} {t['subdoctrine']} {number(t['mastery'])} ({t['rewards']} rewards)" for t in setup["tracks"])
            print(f"  {setup['folder']} doctrine: {setup['grandDoctrine']}{'; ' + tracks if tracks else ''}")
        print(f"  doctrine folders written: {', '.join(folders) or 'none'}; sub-unit bonus records: {len(records)} (deployment {deployment}); "
              f"spirits added: {', '.join(spirits) or 'none'}")
        if records and deployment == "after units":
            warnings.append(f"{role}: {tf.country}'s deployment block stays after its units; ships load clamped to their organisation without the doctrine bonus")

    dominance = {tf.country: sum(d["count"] * d["navalDominance"] for d in ships[role]["designs"])
                 for role, tf in (("attacker", attacker_tf), ("defender", defender_tf))}
    region = write_dominance(save, dominance)
    if region is None:
        warnings.append("no single region lists both countries' naval dominance; the game keeps the template's values")
    else:
        print(f"naval dominance in region {region}: " + ", ".join(f"{country} {number(value)}" for country, value in dominance.items()))
    if unit_counter != start_counter:
        save.replace(unit_line, unit_line, [f"unit={unit_counter}{save.eol(unit_line)}"])
    # The game hands out new type-4713 references after debug_current_ref_id; the new air bases' and pools' must be
    # below it, or the game creates its own with the same ids at load ("Failed to create id ... Already exists").
    ref_line = next(i for i in range(200) if save.text(i).startswith("debug_current_ref_id="))
    if ids[4713] > int(save.text(ref_line).split("=")[1]):
        save.replace(ref_line, ref_line, [f"debug_current_ref_id={ids[4713]}{save.eol(ref_line)}"])
    write_game_rules(save)
    print("game rules: " + ", ".join(f"{rule}={option}" for rule, option in GAME_RULES.items()))
    removed = remove_combat(save, location)
    print(f"battle location {location}; naval combats removed: {removed}")
    save.save(out)
    for warning in warnings:
        print("warning:", warning)

    check = savefile.SaveFile(out)
    written_lines = Lines(out)
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
        doctrines = check.doctrines(tf.country)
        for setup in ships[role]["doctrines"]:
            folder = doctrines.get(setup["folder"])
            slotted = {t["track"]: t for t in setup["tracks"]}
            expected = [(slotted[t]["subdoctrine"], slotted[t]["rewards"], round(slotted[t]["mastery"], 5)) if t in slotted else (None, 0, 0)
                        for t in setup["trackOrder"]]
            if folder is None or folder.grand_doctrine != setup["grandDoctrine"] or \
                    [(t.sub_doctrine, t.rewards, round(t.mastery, 5)) for t in folder.tracks] != expected:
                sys.exit(f"check failed: {role} {setup['folder']} doctrines {folder}, expected {setup['grandDoctrine']} {expected}")
        expected_records = sorted((kind, index, owner) for setup in ships[role]["doctrines"]
                                  for _, kind, index, owner in unit_modifier_records(setup, data_folders[setup["folder"]]))
        if sorted(check.unit_modifier_records(tf.country)) != expected_records:
            sys.exit(f"check failed: {role} unit_modifiers {check.unit_modifier_records(tf.country)}, expected {expected_records}")
        order = [key for key, _ in check.country_children(tf.country) if key in ("units", "deployment")]
        if expected_records and not args.keep_block_order and order != ["deployment", "units"]:
            sys.exit(f"check failed: {role} {tf.country} block order {order}, expected deployment before units")
        pride_lines = [check.lines[i].strip() for key, i in check.country_children(tf.country) if key == "pride_of_the_fleet"]
        expected_pride = [] if ships[role]["prideId"] is None else [f"pride_of_the_fleet={{ id={ships[role]['prideId']} type=51 }}"]
        if pride_lines != expected_pride:
            sys.exit(f"check failed: {role} {tf.country} pride of the fleet {pride_lines}, expected {expected_pride}")
        missing = [spirit for spirit in ships[role]["spiritIDs"] if spirit not in check.ideas(tf.country)]
        if missing:
            sys.exit(f"check failed: {role} spirits {missing} not in the country's ideas")
        check_mios_and_admiral(written_lines, tf, ships[role])
    if region is not None:
        written = check.region_dominance(region)
        for country, value in dominance.items():
            if any(round(written.get(country, {}).get(key, -1), 4) != round(value, 4) for key in DOMINANCE_KEYS):
                sys.exit(f"check failed: {country} dominance in region {region} is {written.get(country)}, expected {number(value)}")
    print(f"wrote {out}")


if __name__ == "__main__":
    main()
