"""Read and minimally edit HOI4 plain-text saves (HOI4txt), keeping every other byte as it is."""
import re
from dataclasses import dataclass, field
from pathlib import Path

ID_RE = re.compile(r"id=\{ id=(\d+) type=(\d+) \}")


@dataclass
class Ship:
    id: int
    definition: str
    equipment_id: int
    strength: float
    organisation: float
    manpower: int
    max_manpower: int
    experience: float


@dataclass
class TaskForce:
    id: int
    name: str
    country: str
    location: int
    engagement_rule: int | None
    repair_mode: int | None
    ships: list[Ship] = field(default_factory=list)


@dataclass
class Variant:
    id: int
    equipment_type: str
    name: str
    creator: str
    modules: dict[str, str]


class SaveFile:
    def __init__(self, path):
        self.path = Path(path)
        self.data = self.path.read_bytes()
        if not self.data.startswith(b"HOI4txt"):
            raise ValueError(f"{self.path} is not a plain-text save (enable 'save as text' or use the -debug launch option)")
        self.text = self.data.decode("utf-8", errors="replace")
        self.lines = self.text.split("\n")

    def header(self, key):
        match = re.search(rf"^{re.escape(key)}=(.*?)\r?$", self.text, re.M)
        return match.group(1).strip('"') if match else None

    @property
    def date(self):
        return self.header("date")

    @property
    def seed(self):
        return int(self.header("multiplayer_random_seed"))

    def with_seed(self, seed):
        """The save's bytes with multiplayer_random_seed (the combat dice seed) replaced."""
        old = re.search(rb"\nmultiplayer_random_seed=(-?\d+)(\r?\n)", self.data)
        if old is None:
            raise ValueError("multiplayer_random_seed not found")
        new = b"\nmultiplayer_random_seed=%d%s" % (seed, old.group(2))
        return self.data[: old.start()] + new + self.data[old.end():]

    def _block_end(self, start):
        depth = 0
        for i in range(start, len(self.lines)):
            depth += self.lines[i].count("{") - self.lines[i].count("}")
            if depth <= 0:
                return i
        return len(self.lines) - 1

    def task_forces(self):
        result = []
        i = 0
        while i < len(self.lines):
            if self.lines[i].strip() == "task_force={":
                end = self._block_end(i)
                result.append(self._task_force(i, end))
                i = end
            i += 1
        return result

    def _task_force(self, start, end):
        tf = TaskForce(id=0, name="", country="", location=0, engagement_rule=None, repair_mode=None)
        depth = 0
        i = start
        while i <= end:
            line = self.lines[i].strip()
            if depth == 1:
                if line.startswith("id={") and tf.id == 0:
                    tf.id = int(ID_RE.search(line).group(1))
                elif line.startswith("name="):
                    tf.name = line[5:].strip('"')
                elif line.startswith("logical_country="):
                    tf.country = line.split("=", 1)[1].strip('"')
                elif line.startswith("location="):
                    tf.location = int(line.split("=", 1)[1])
                elif line.startswith("repair_mode="):
                    tf.repair_mode = int(line.split("=", 1)[1])
                elif line == "ship={":
                    ship_end = self._block_end(i)
                    tf.ships.append(self._ship(i, ship_end))
                    i = ship_end + 1
                    continue
            if line.startswith("navy_engagement_rule="):
                tf.engagement_rule = int(line.split("=", 1)[1])
            depth += line.count("{") - line.count("}")
            i += 1
        return tf

    def _ship(self, start, end):
        values = {}
        ship_id = 0
        equipment_id = 0
        depth = 0
        for i in range(start, end + 1):
            line = self.lines[i].strip()
            if depth == 1 and line.startswith("id={") and ship_id == 0:
                ship_id = int(ID_RE.search(line).group(1))
            elif depth == 1 and "=" in line and not line.endswith("{"):
                key, value = line.split("=", 1)
                values[key] = value
            if "type=70" in line and equipment_id == 0:
                equipment_id = int(ID_RE.search(line).group(1))
            depth += line.count("{") - line.count("}")
        return Ship(
            id=ship_id,
            definition=values.get("definition", ""),
            equipment_id=equipment_id,
            strength=float(values.get("strength", 0)),
            organisation=float(values.get("organisation", 0)),
            manpower=int(values.get("manpower", 0)),
            max_manpower=int(values.get("max_manpower", 0)),
            experience=float(values.get("experience", 0)),
        )

    def variants(self, ids=None):
        """Equipment variants from the top-level equipments section, by id (only `ids` if given)."""
        result = {}
        start = next(i for i, line in enumerate(self.lines) if line.rstrip("\r") == "equipments={")
        end = self._block_end(start)
        i = start + 1
        while i < end:
            line = self.lines[i]
            if line.startswith("\t") and not line.startswith("\t\t") and line.rstrip().endswith("={"):
                block_end = self._block_end(i)
                header = ID_RE.search(self.lines[i + 1])
                if header and (ids is None or int(header.group(1)) in ids):
                    result[int(header.group(1))] = self._variant(i, block_end)
                i = block_end
            i += 1
        return result

    def _variant(self, start, end):
        variant = Variant(id=0, equipment_type=self.lines[start].strip()[:-2], name="", creator="", modules={})
        in_modules = False
        for i in range(start + 1, end):
            line = self.lines[i].strip()
            if line.startswith("id={") and variant.id == 0:
                variant.id = int(ID_RE.search(line).group(1))
            elif line.startswith("name="):
                variant.name = line[5:].strip('"')
            elif line.startswith("creator="):
                variant.creator = line.split("=", 1)[1].strip('"')
            elif line == "modules={":
                in_modules = True
            elif in_modules and line == "}":
                in_modules = False
            elif in_modules and "=" in line:
                slot, module = line.split("=", 1)
                variant.modules[slot] = module
        return variant


def ship_index(save, locations=None):
    """Ship id -> {country, taskForce, design, hull} for the task forces at `locations` (all if None)."""
    forces = [tf for tf in save.task_forces() if locations is None or tf.location in locations]
    variants = save.variants({ship.equipment_id for tf in forces for ship in tf.ships})
    index = {}
    for tf in forces:
        for ship in tf.ships:
            variant = variants.get(ship.equipment_id)
            index[ship.id] = {
                "country": tf.country,
                "taskForce": tf.name,
                "taskForceId": tf.id,
                "location": tf.location,
                "engagementRule": tf.engagement_rule,
                "design": variant.name if variant else None,
                "hull": variant.equipment_type if variant else None,
                "hp": ship.strength,
                "org": ship.organisation,
                "manpower": ship.manpower,
                "maxManpower": ship.max_manpower,
                "experience": ship.experience,
            }
    return index
