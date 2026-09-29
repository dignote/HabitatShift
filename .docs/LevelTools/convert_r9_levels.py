"""Convert approved R9.2.2 candidate levels 19-23 into the production v4 catalog.

Run:
    python .docs/LevelTools/convert_r9_levels.py --check
    python .docs/LevelTools/convert_r9_levels.py

Nguon: .docs/HabitatShift_CurrentGame_UnityLevels_v1/Habitat_Shift_R9_2_2_level_editor_project.json
       -> candidateLevels -> ids 19..23   (nguon su that, da duoc nguoi dung phe duyet)
Dich : Assets/StreamingAssets/HabitatShift/approved_levels_v4.json  (byte-identical voi ban copy trong .docs)

Nguyen tac:
- L1..L18 giu nguyen RAW TEXT tung level (khong re-serialize) => khong doi byte.
- Chi ghep them 5 level moi vao mang "levels" va cap nhat 4 truong root.
- Port rong (khong co queue) bi loai nhu editor placeholder.
- direction chi la du lieu trinh bay (chon sprite); khong dung cho logic.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import sys
from collections import Counter, OrderedDict

LEVEL_IDS = (19, 20, 21, 22, 23)
EDGE_DIRECTION = {"top": "DOWN", "bottom": "UP", "left": "RIGHT", "right": "LEFT"}
SUPPORTED_COLORS = {"blue", "cyan", "green", "orange", "pink", "purple", "red", "white", "yellow"}
NEW_REVISION = "m7-20260928-levels-19-23"
NEW_APPROVAL_MODE = "user-approved-manual-playtest-2026-09-28-r9-2-2-levels-19-23"
ROOT = pathlib.Path(__file__).resolve().parents[2]
PROJECT = ROOT / ".docs" / "HabitatShift_CurrentGame_UnityLevels_v1" / "Habitat_Shift_R9_2_2_level_editor_project.json"
CATALOG_STREAMING = ROOT / "Assets" / "StreamingAssets" / "HabitatShift" / "approved_levels_v4.json"
CATALOG_DOC = ROOT / ".docs" / "HabitatShift_CurrentGame_UnityLevels_v1" / "data" / "approved_levels_v4.json"
REPORT = ROOT / ".docs" / "LevelTools" / "convert_r9_report.txt"


def read_text(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8")


def array_span(text: str, key: str) -> tuple[int, int]:
    """Tra ve (vi tri '[' , vi tri ']') cua mang JSON ung voi key, bo qua chuoi."""
    match = re.search(r'"' + re.escape(key) + r'"\s*:\s*\[', text)
    if not match:
        raise ValueError("khong tim thay mang '" + key + "'")
    start = text.index("[", match.start())
    depth, index, in_string, escaped = 0, start, False, False
    while index < len(text):
        char = text[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
        elif char == '"':
            in_string = True
        elif char == "[":
            depth += 1
        elif char == "]":
            depth -= 1
            if depth == 0:
                return start, index
        index += 1
    raise ValueError("mang '" + key + "' khong dong")


def split_objects(array_text: str) -> list[str]:
    """Tach raw text cua tung object trong mang (giu nguyen format goc)."""
    body = array_text[1:-1]
    objects: list[str] = []
    depth, start, in_string, escaped = 0, None, False, False
    for index, char in enumerate(body):
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == "{":
            if depth == 0:
                start = index
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0 and start is not None:
                objects.append(body[start:index + 1].strip("\n "))
                start = None
    return objects


def indent_block(text: str, spaces: int) -> str:
    prefix = " " * spaces
    return "\n".join(prefix + line if line else line for line in text.split("\n"))


def normalize_shape(anchor: list[int], cells: list[list[int]]) -> tuple[list[int], list[list[int]]]:
    """Chuan hoa shape ve min 0,0 va BU ANCHOR tuong ung: footprint tuyet doi khong doi."""
    x0 = min(cell[0] for cell in cells)
    y0 = min(cell[1] for cell in cells)
    norm = sorted([[cell[0] - x0, cell[1] - y0] for cell in cells])
    return [anchor[0] + x0, anchor[1] + y0], norm


def absolute_cells(anchor: list[int], cells: list[list[int]]) -> list[tuple[int, int]]:
    return sorted((anchor[0] + cell[0], anchor[1] + cell[1]) for cell in cells)


def connected(cells: list[list[int]]) -> bool:
    cellset = {tuple(cell) for cell in cells}
    seen, queue = {tuple(cells[0])}, [tuple(cells[0])]
    while queue:
        x, y = queue.pop()
        for other in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if other in cellset and other not in seen:
                seen.add(other)
                queue.append(other)
    return len(seen) == len(cellset)


def fingerprint(levels: list[dict]) -> str:
    """Cong thuc da kiem chung khop fingerprint 18 man hien tai (95665e0f...)."""
    canonical = json.dumps(levels, separators=(",", ":"), sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def classify_ports(level: dict) -> tuple[list[dict], list[dict]]:
    active, placeholders = [], []
    for port in level.get("ports") or []:
        if (port.get("queue") or []) and port.get("entryCell") is not None:
            active.append(port)
        else:
            placeholders.append(port)
    return active, placeholders


def profile_for(level_id: int, habitats: list[dict], elevators: list[dict]) -> str:
    if level_id == 23:
        return "CHAPTER_MASTERY"
    if any(habitat.get("constraint", "FREE") != "FREE" for habitat in habitats):
        return "MOVEMENT_CONSTRAINT_INTRO"
    if len(elevators) >= 3:
        return "HARD_QUEUE"
    if elevators:
        return "QUEUE_COMBINATION"
    return "CONTINUOUS_CORE"


def difficulty_for(habitats: list[dict]) -> str:
    count = len(habitats)
    if count >= 11:
        return "SUPER_HARD"
    if count >= 8:
        return "HARD"
    if count >= 5:
        return "NORMAL"
    return "EASY"


def convert_level(candidate: dict) -> tuple[dict, list[str]]:
    """Doi dinh dang editor -> catalog v4. Khong redesign, khong doi geometry."""
    notes: list[str] = []
    active_ports, placeholders = classify_ports(candidate)
    for port in placeholders:
        notes.append(f"PLACEHOLDER L{candidate['id']} {port.get('id')} (queue rong) -> khong import")
    cols, rows = candidate["cols"], candidate["rows"]
    blocked = [list(cell) for cell in (candidate.get("blocked") or [])]
    mask = [list(cell) for cell in (candidate.get("mask") or [])]
    full_rect = {(x, y) for y in range(rows) for x in range(cols)}
    playable_mask = None if set(map(tuple, mask)) == full_rect else sorted(mask, key=lambda cell: (cell[1], cell[0]))
    habitats = []
    for habitat in candidate["habitats"]:
        anchor, shape = normalize_shape(habitat["anchor"], habitat["shape"])
        habitats.append(OrderedDict([
            ("id", habitat["id"]),
            ("color", habitat["color"]),
            ("anchor", anchor),
            ("shape", shape),
            ("need", habitat["need"]),
            ("movementConstraint", habitat.get("constraint", "FREE")),
        ]))
    targets = [OrderedDict([("id", target["id"]), ("color", target["color"]),
                            ("position", [target["position"][0], target["position"][1]])])
               for target in candidate["targets"]]
    elevators = []
    for port in active_ports:
        cell = port["entryCell"]
        queue = [OrderedDict([("id", f"{port['id']}_Q{index + 1}"), ("color", color)])
                 for index, color in enumerate(port["queue"])]
        elevators.append(OrderedDict([
            ("id", port["id"]),
            ("entry", [cell[0] + 0.5, cell[1] + 0.5]),
            ("direction", EDGE_DIRECTION[port["edge"]]),
            ("queue", queue),
            ("nextIndex", 0),
            ("mandatory", True),
        ]))
    level = OrderedDict([
        ("id", candidate["id"]),
        ("title", f"Level {candidate['id']}"),
        ("profile", profile_for(candidate["id"], candidate["habitats"], elevators)),
        ("difficulty", difficulty_for(candidate["habitats"])),
        ("challenge", f"{len(habitats)} habitats, {len(targets)} sproutlings, {len(elevators)} elevator queues."),
        ("cols", cols),
        ("rows", rows),
        ("habitats", habitats),
        ("targets", targets),
        ("elevators", elevators),
        ("obstacles", blocked),
        ("playableMask", playable_mask),
        ("bonusPickups", []),
    ])
    return level, notes


def preflight(levels: list[dict], max_cols: int, max_rows: int) -> list[str]:
    problems: list[str] = []
    if [level["id"] for level in levels] != list(LEVEL_IDS):
        problems.append("ids khong dung 19..23")
    for level in levels:
        level_id = level["id"]
        cols, rows = level["cols"], level["rows"]
        if cols < 1 or rows < 1:
            problems.append(f"L{level_id}: kich thuoc board khong hop le")
        if cols > max_cols or rows > max_rows:
            problems.append(f"L{level_id}: board {cols}x{rows} vuot max {max_cols}x{max_rows}")
        full = {(x, y) for y in range(rows) for x in range(cols)}
        mask = full if level["playableMask"] is None else {tuple(cell) for cell in level["playableMask"]}
        if not mask:
            problems.append(f"L{level_id}: playableMask rong")
        if not mask <= full:
            problems.append(f"L{level_id}: playableMask co o ngoai board")
        playable = mask - {tuple(cell) for cell in level["obstacles"]}
        occupied: Counter = Counter()
        identifiers: Counter = Counter()
        for habitat in level["habitats"]:
            identifiers[habitat["id"]] += 1
            if habitat["color"] not in SUPPORTED_COLORS:
                problems.append(f"L{level_id}/{habitat['id']}: mau habitat khong ho tro {habitat['color']}")
            if not connected(habitat["shape"]):
                problems.append(f"L{level_id}/{habitat['id']}: shape khong lien thong")
            if habitat["need"] < 1:
                problems.append(f"L{level_id}/{habitat['id']}: need < 1")
            for cell in absolute_cells(habitat["anchor"], habitat["shape"]):
                occupied[cell] += 1
                if cell not in playable:
                    problems.append(f"L{level_id}/{habitat['id']}: o {cell} ngoai playable")
        for cell, count in occupied.items():
            if count > 1:
                problems.append(f"L{level_id}: habitat chong nhau tai {cell} x{count}")
        for target in level["targets"]:
            identifiers[target["id"]] += 1
            if target["color"] not in SUPPORTED_COLORS:
                problems.append(f"L{level_id}/{target['id']}: mau sproutling khong ho tro {target['color']}")
            cell = (int(target["position"][0]), int(target["position"][1]))
            if cell not in playable:
                problems.append(f"L{level_id}/{target['id']}: sproutling ngoai playable {cell}")
        for elevator in level["elevators"]:
            identifiers[elevator["id"]] += 1
            entry = (int(elevator["entry"][0]), int(elevator["entry"][1]))
            if entry not in full:
                problems.append(f"L{level_id}/{elevator['id']}: entry {entry} ngoai board")
            if not elevator["queue"]:
                problems.append(f"L{level_id}/{elevator['id']}: queue rong")
            for item in elevator["queue"]:
                identifiers[item["id"]] += 1
                if item["color"] not in SUPPORTED_COLORS:
                    problems.append(f"L{level_id}/{elevator['id']}: mau queue khong ho tro {item['color']}")
        for identifier, count in identifiers.items():
            if count > 1:
                problems.append(f"L{level_id}: id '{identifier}' xuat hien {count} lan")
        for color in {habitat["color"] for habitat in level["habitats"]}:
            need = sum(habitat["need"] for habitat in level["habitats"] if habitat["color"] == color)
            supply = sum(1 for target in level["targets"] if target["color"] == color)
            supply += sum(1 for elevator in level["elevators"] for item in elevator["queue"] if item["color"] == color)
            if need != supply:
                problems.append(f"L{level_id}: mat can bang mau {color}: need={need} vs supply={supply}")
    return problems


def main() -> None:
    parser = argparse.ArgumentParser(description="Import approved R9.2.2 candidate levels 19-23.")
    parser.add_argument("--check", action="store_true", help="Chi kiem tra, khong ghi file.")
    parser.add_argument("--project", default=str(PROJECT))
    parser.add_argument("--catalog", default=str(CATALOG_STREAMING))
    args = parser.parse_args()
    project = json.loads(read_text(pathlib.Path(args.project)))
    catalog_text = read_text(pathlib.Path(args.catalog))
    catalog = json.loads(catalog_text)
    candidates = {level["id"]: level for level in project["candidateLevels"] if level["id"] in LEVEL_IDS}
    missing = [level_id for level_id in LEVEL_IDS if level_id not in candidates]
    if missing:
        sys.exit("candidateLevels thieu id: " + ", ".join(map(str, missing)))
    old_fingerprint = fingerprint(catalog["levels"])
    assert old_fingerprint == catalog["canonicalLevelsFingerprint"], (
        "fingerprint 18 man khong khop: " + old_fingerprint + " vs " + catalog["canonicalLevelsFingerprint"])
    start, end = array_span(catalog_text, "levels")
    old_objects = split_objects(catalog_text[start:end + 1])
    assert len(old_objects) == len(catalog["levels"]) == 18, "so level cu khong dung 18"
    for index, raw in enumerate(old_objects):
        assert json.loads(raw) == catalog["levels"][index], f"level {index + 1} raw text khong khop"
    new_levels, notes, geometry_checks = [], [], []
    for level_id in LEVEL_IDS:
        candidate = candidates[level_id]
        level, level_notes = convert_level(candidate)
        new_levels.append(level)
        notes.extend(level_notes)
        for habitat, original in zip(level["habitats"], candidate["habitats"]):
            before = absolute_cells(original["anchor"], original["shape"])
            after = absolute_cells(habitat["anchor"], habitat["shape"])
            geometry_checks.append((level_id, habitat["id"], before == after))
    problems = [f"L{lid}/{hid}: footprint doi" for lid, hid, ok in geometry_checks if not ok]
    problems += preflight(new_levels, project["architecture"]["boardMaxCols"],
                          project["architecture"]["boardMaxRows"])
    levels_all = catalog["levels"] + new_levels
    new_fingerprint = fingerprint(levels_all)

    def replace_root(text: str, key: str, value: str) -> str:
        pattern = r'("' + re.escape(key) + r'"\s*:\s*)(?:"[^"]*"|-?\d+)'
        if not re.search(pattern, text):
            raise ValueError("khong tim thay root field " + key)
        return re.sub(pattern, lambda match: match.group(1) + value, text, count=1)

    def rebuild(text: str) -> str:
        span_start, span_end = array_span(text, "levels")
        blocks = old_objects + [indent_block(json.dumps(level, ensure_ascii=False, indent=2), 4)
                                for level in new_levels]
        text = text[:span_start] + "[\n" + ",\n".join(blocks) + "\n  ]" + text[span_end + 1:]
        text = replace_root(text, "catalogRevision", json.dumps(NEW_REVISION))
        text = replace_root(text, "approvalMode", json.dumps(NEW_APPROVAL_MODE))
        text = replace_root(text, "levelCount", str(len(levels_all)))
        text = replace_root(text, "canonicalLevelsFingerprint", json.dumps(new_fingerprint))
        return text

    new_text = rebuild(catalog_text)
    assert rebuild(catalog_text) == new_text, "output khong deterministic"
    parsed_new = json.loads(new_text)
    assert parsed_new["levels"][:18] == catalog["levels"], "L1..L18 bi thay doi (semantic)"
    new_start, new_end = array_span(new_text, "levels")
    new_objects = split_objects(new_text[new_start:new_end + 1])
    assert new_objects[:18] == old_objects, "L1..L18 bi thay doi (raw text)"

    color_use: Counter = Counter()
    for level in new_levels:
        for habitat in level["habitats"]:
            color_use[level["id"], "habitat", habitat["color"]] += 1
        for target in level["targets"]:
            color_use[level["id"], "target", target["color"]] += 1
        for elevator in level["elevators"]:
            for item in elevator["queue"]:
                color_use[level["id"], "queue", item["color"]] += 1
    lines = [
        "convert_r9_levels report",
        f"catalog cu : {len(catalog['levels'])} level, fingerprint {old_fingerprint}",
        f"catalog moi: {len(levels_all)} level, fingerprint {new_fingerprint}",
        f"revision   : {NEW_REVISION}",
        f"approval   : {NEW_APPROVAL_MODE}",
        "",
        "Level moi:",
    ]
    for level in new_levels:
        mask = "null" if level["playableMask"] is None else len(level["playableMask"])
        lines.append(f"  L{level['id']}: {level['cols']}x{level['rows']} profile={level['profile']} "
                     f"difficulty={level['difficulty']} habitats={len(level['habitats'])} "
                     f"targets={len(level['targets'])} elevators={len(level['elevators'])} playableMask={mask}")
    lines += ["", "Port:"] + ["  " + note for note in notes]
    lines += ["", "Mau dung (level / loai / mau / so luong):"]
    for key in sorted(color_use):
        lines.append(f"  L{key[0]} {key[1]:7} {key[2]:7} x{color_use[key]}")
    lines += ["", "Preflight: " + ("PASS" if not problems else "FAIL")]
    lines += ["  " + problem for problem in problems]
    print("\n".join(lines))
    if problems:
        sys.exit(1)
    if args.check:
        print("\n--check: khong ghi file.")
        return
    if not REPORT.parent.is_dir():
        REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    pathlib.Path(args.catalog).write_text(new_text, encoding="utf-8")
    CATALOG_DOC.write_text(new_text, encoding="utf-8")
    print("\nda ghi: " + args.catalog)
    print("da ghi: " + str(CATALOG_DOC))
    print("bao cao: " + str(REPORT))


if __name__ == "__main__":
    main()
