import json
import re
from collections import defaultdict
from pathlib import Path

SOURCE_PATH = Path(
    r"C:\Program Files (x86)\Steam\steamapps\workshop\content\394360\2559317737\common\units\equipment\modules\00_ship_modules.txt"
)
OUTPUT_PATH = Path(r"C:\Users\Jukki\worspace\hoi4-navy-vibing\NavySimulator\Data\modules\00_imported_ship_modules.json")

# Only mapped keys are written to modules.json.
STAT_MAP = {
    "lg_attack": "lightAttack",
    "lg_armor_piercing": "lightPiercing",
    "hg_attack": "heavyAttack",
    "hg_armor_piercing": "heavyPiercing",
    "torpedo_attack": "torpedoAttack",
    "sub_attack": "depthChargeAttack",
    "build_cost_ic": "productionCost",
    "naval_speed": "speed",
    "armor_value": "armor",
    "max_strength": "hp",
    "surface_visibility": "surfaceVisibility",
    "sub_visibility": "subVisibility",
    "surface_detection": "surfaceDetection",
    "sub_detection": "subDetection",
    "carrier_sub_detection": "carrierSubDetection",
    "carrier_surface_detection": "carrierSurfaceDetection",
    "carrier_size": "carrierSize",
    "anti_air_attack": "antiAir",
    "reliability": "reliability",
    "naval_torpedo_damage_reduction_factor": "torpedoDamageReductionFactor",
    "naval_torpedo_enemy_critical_chance_factor": "torpedoEnemyCriticalChanceFactor",
    "naval_torpedo_hit_chance_factor": "torpedoHitChangeFactor",
    "naval_weather_penalty_factor": "navalWeatherPenaltyFactor",
    "naval_light_gun_hit_chance_factor": "lightHitChangeFactor",
    "naval_heavy_gun_hit_chance_factor": "heavyHitChangeFactor",
}

RENAME_PREFIX = [
    ("ship_fire_control_system_", "fire_control_"),
    ("light_ship_engine_", "engine_dd_"),
    ("cruiser_ship_engine_", "engine_cruiser_"),
    ("heavy_ship_engine_", "engine_heavy_"),
    ("carrier_ship_engine_", "engine_carrier_"),
    ("ship_armor_cruiser_", "armor_cruiser_"),
    ("ship_light_medium_battery_", "medium_light_battery_"),
    ("ship_light_battery_", "light_battery_"),
]

NON_MODULE_BLOCKS = {
    "equipment_modules",
    "limit",
    "add_stats",
    "multiply_stats",
    "add_average_stats",
    "build_cost_resources",
    "can_convert_from",
    "critical_parts",
}


def normalize_module_id(module_id: str) -> str:
    for source_prefix, target_prefix in RENAME_PREFIX:
        if module_id.startswith(source_prefix):
            return target_prefix + module_id[len(source_prefix) :]

    if module_id.startswith("ship_"):
        return module_id[len("ship_") :]

    return module_id


def parse_object_body(source: str, start_index: int) -> tuple[str, int]:
    depth = 1
    index = start_index

    while index < len(source) and depth > 0:
        character = source[index]

        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1

        index += 1

    return source[start_index : index - 1], index


def parse_modules() -> tuple[list[dict], dict[str, set[str]]]:
    text = SOURCE_PATH.read_text(encoding="utf-8", errors="ignore")
    text = "\n".join(line.split("#", 1)[0] for line in text.splitlines())

    modules = []
    unknown_keys: dict[str, set[str]] = defaultdict(set)

    for match in re.finditer(r"^\s*([A-Za-z0-9_\.]+)\s*=\s*\{", text, flags=re.M):
        module_id = match.group(1)

        if module_id in NON_MODULE_BLOCKS:
            continue

        body, _ = parse_object_body(text, match.end())

        if not re.search(r"\bcategory\s*=", body):
            continue

        module_entry = {"id": normalize_module_id(module_id)}

        for block_name, output_key in (
            ("add_stats", "statModifiers"),
            ("add_average_stats", "statAverages"),
            ("multiply_stats", "statMultipliers"),
        ):
            block_match = re.search(r"\b" + re.escape(block_name) + r"\s*=\s*\{", body)

            if not block_match:
                continue

            block_body, _ = parse_object_body(body, block_match.end())
            parsed_stats = {}

            for stat_match in re.finditer(r"\b([A-Za-z0-9_\.]+)\s*=\s*([^\n\r{}]+)", block_body):
                source_key = stat_match.group(1).strip()
                source_value = stat_match.group(2).strip()

                if source_key not in STAT_MAP:
                    unknown_keys[source_key].add(module_id)
                    continue

                target_key = STAT_MAP[source_key]

                try:
                    value = float(source_value)
                except ValueError:
                    continue

                parsed_stats[target_key] = int(value) if value.is_integer() else value

            if parsed_stats:
                module_entry[output_key] = parsed_stats

        if any(key in module_entry for key in ("statModifiers", "statAverages", "statMultipliers")):
            modules.append(module_entry)

    modules.sort(key=lambda item: item["id"])
    return modules, unknown_keys


def main() -> None:
    modules, unknown_keys = parse_modules()

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps({"modules": modules}, indent=2) + "\n", encoding="utf-8")

    print(f"Wrote {len(modules)} modules to {OUTPUT_PATH}")

    if unknown_keys:
        print("Source modifier keys not written to modules.json:")

        for key in sorted(unknown_keys):
            print(f"- {key} (modules: {len(unknown_keys[key])})")


if __name__ == "__main__":
    main()

