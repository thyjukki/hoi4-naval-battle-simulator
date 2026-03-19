import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "NavySimulator" / "Data"

SPLIT_CONFIG = {
    "hulls": {
        "legacy_file": "hulls.json",
        "folder": "hulls",
        "root_key": "hulls",
    },
    "modules": {
        "legacy_file": "modules.json",
        "folder": "modules",
        "root_key": "modules",
    },
    "mios": {
        "legacy_file": "mios.json",
        "folder": "mios",
        "root_key": "mios",
    },
    "ship-designs": {
        "legacy_file": "ship-designs.json",
        "folder": "ship-designs",
        "root_key": "shipDesigns",
    },
}


def load_items(path: Path, root_key: str) -> list[dict]:
    data = json.loads(path.read_text(encoding="utf-8"))

    if isinstance(data, list):
        return data

    if isinstance(data, dict) and isinstance(data.get(root_key), list):
        return data[root_key]

    raise ValueError(f"{path.name} must be a JSON array or an object containing '{root_key}'.")


def write_split_file(path: Path, root_key: str, items: list[dict], dry_run: bool) -> None:
    payload = json.dumps({root_key: items}, indent=2) + "\n"
    if dry_run:
        return

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(payload, encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Split legacy monolithic NavySimulator data files into folder-based files.")
    parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite existing split target files if they already exist.")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show what would happen without writing files.")

    args = parser.parse_args()

    created = 0
    skipped = 0
    missing = 0

    print(f"Data directory: {DATA_DIR}")

    for label, cfg in SPLIT_CONFIG.items():
        source = DATA_DIR / cfg["legacy_file"]
        target = DATA_DIR / cfg["folder"] / "00_split_from_legacy.json"
        root_key = cfg["root_key"]

        if not source.exists():
            missing += 1
            print(f"[missing] {source.name} not found; skipping {label}.")
            continue

        if target.exists() and not args.force:
            skipped += 1
            print(f"[skip] {target} already exists (use --force to overwrite).")
            continue

        items = load_items(source, root_key)
        write_split_file(target, root_key, items, args.dry_run)

        action = "would create" if args.dry_run else "created"
        print(f"[{action}] {target} ({len(items)} items)")
        created += 1

    print()
    print("Split summary")
    print(f"- created: {created}")
    print(f"- skipped: {skipped}")
    print(f"- missing legacy sources: {missing}")


if __name__ == "__main__":
    main()

