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
class Track:
    sub_doctrine: str | None
    rewards: int
    mastery: float


@dataclass
class DoctrineFolder:
    folder: str
    grand_doctrine: str | None
    tracks: list[Track] = field(default_factory=list)
    completed: list[str] = field(default_factory=list)


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

    def _children(self, start, end):
        """(stripped line, line index) of the direct children of the block opening at `start`."""
        i = start + 1
        while i < end:
            line = self.lines[i].strip()
            yield line, i
            i = self._block_end(i) + 1 if line.endswith("{") else i + 1

    def doctrines(self, country):
        """S-DOCTRINE-SAVE: the country's doctrine folders, by folder ID, from the global doctrine block."""
        start = next(i for i, line in enumerate(self.lines) if line.rstrip("\r") == "doctrine={")
        countries = next(i for line, i in self._children(start, self._block_end(start)) if line == "countries={")
        for line, entry in self._children(countries, self._block_end(countries)):
            children = list(self._children(entry, self._block_end(entry)))
            if line != "{" or f'country="{country}"' not in (text for text, _ in children):
                continue
            folders = next((i for text, i in children if text == "folder={"), None)
            result = {}
            for text, i in self._children(folders, self._block_end(folders)) if folders is not None else []:
                values = dict(t.split("=", 1) for t, _ in self._children(i, self._block_end(i)) if "=" in t and not t.endswith("{"))
                folder = DoctrineFolder(values.get("folder", "").strip('"'), values["grand_doctrine"].strip('"') if "grand_doctrine" in values else None)
                for t, j in self._children(i, self._block_end(i)):
                    if t == "tracks={":
                        for _, k in self._children(j, self._block_end(j)):
                            track = dict(x.split("=", 1) for x, _ in self._children(k, self._block_end(k)) if "=" in x)
                            folder.tracks.append(Track(track["sub_doctrine"].strip('"') if "sub_doctrine" in track else None,
                                                       int(track.get("rewards", 0)), float(track.get("mastery", 0))))
                    elif t == "completed_subdoctrines={":
                        folder.completed = [w.strip('"') for x, _ in self._children(j, self._block_end(j)) for w in x.split()]
                result[folder.folder] = folder
            return result
        return {}

    def ideas(self, country):
        """The idea IDs in the country's politics={ ideas={ ... } } block."""
        start = next(i for i, line in enumerate(self.lines) if line.rstrip("\r") == "countries={")
        block = next(i for line, i in self._children(start, self._block_end(start)) if line == f"{country}={{")
        politics = next((i for line, i in self._children(block, self._block_end(block)) if line == "politics={"), None)
        ideas = None if politics is None else next((i for line, i in self._children(politics, self._block_end(politics)) if line == "ideas={"), None)
        return [] if ideas is None else [w for j in range(ideas + 1, self._block_end(ideas)) for w in self.lines[j].split()]

    def country_children(self, country):
        """The direct children of the country's block, in file order: [(key, line index)]."""
        start = next(i for i, line in enumerate(self.lines) if line.strip() == "countries={" and not line.startswith("\t"))
        block = next(i for line, i in self._children(start, self._block_end(start)) if line == f"{country}={{")
        return [(line.split("=", 1)[0], i) for line, i in self._children(block, self._block_end(block))]

    def region_dominance(self, region):
        """S-DOMINANCE-SAVE: {country: {current, target, ...}} of region={ <region>={ dominance={ values={ ... } } } }."""
        start = next(i for i, line in enumerate(self.lines) if line.rstrip("\r") == "region={")
        block = next((i for line, i in self._children(start, self._block_end(start)) if line == f"{region}={{"), None)
        result, tag = {}, None
        for j in range(block + 1, self._block_end(block)) if block is not None else []:
            line = self.lines[j].strip()
            match = re.fullmatch(r'\{ "(\w+)"', line)
            if match:
                tag = match.group(1)
            elif tag is not None and "=" in line and not line.endswith("{"):
                key, value = line.split("=", 1)
                result.setdefault(tag, {})[key] = float(value)
        return result

    def unit_modifier_records(self, country):
        """S-DOCTRINE-SAVE: (type, number, id) of the country's deployment={ unit_modifiers={ ... } } records."""
        deployment = next((i for key, i in self.country_children(country) if key == "deployment"), None)
        if deployment is None:
            return []
        modifiers = next((i for line, i in self._children(deployment, self._block_end(deployment)) if line == "unit_modifiers={"), None)
        records = []
        for line, i in self._children(modifiers, self._block_end(modifiers)) if modifiers is not None else []:
            values = dict(t.split("=", 1) for t, _ in self._children(i, self._block_end(i)) if "=" in t and not t.endswith("{"))
            records.append((values.get("type"), int(values.get("number", 0)), values.get("id")))
        return records

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
