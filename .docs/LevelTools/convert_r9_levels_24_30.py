"""Append approved R9.2.4 candidate levels 24-30 to the production v4 catalog.

Run:
    python .docs/LevelTools/convert_r9_levels_24_30.py --check
    python .docs/LevelTools/convert_r9_levels_24_30.py

Nguon: .docs/HabitatShift_CurrentGame_UnityLevels_v1/data/Habitat_Shift_R9_2_4_level_editor_project.json
       -> candidateLevels -> ids 24..30   (KHONG dung sourceLevels)
Dich : Assets/StreamingAssets/HabitatShift/approved_levels_v4.json
       + .docs/HabitatShift_CurrentGame_UnityLevels_v1/data/approved_levels_v4.json (byte-identical)

Nguyen tac (khac converter 19..23, co y tach rieng de giam rui ro):
- catalog hien tai PHAI co dung 23 level; converter chi APPEND 7 level 24..30.
- L1..L23 giu nguyen RAW TEXT tung level (khong re-serialize) => khong doi byte/semantic.
- Chi thay 4 truong root: catalogRevision, approvalMode, levelCount, canonicalLevelsFingerprint.
- Khong redesign, khong rebalance, khong recolor: moi thu lay tu candidate.
- Port rong / thieu entryCell = editor placeholder -> khong import (bao cao ro).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import sys
from collections import Counter, OrderedDict

LEVEL_IDS = (24, 25, 26, 27, 28, 29, 30)
EXPECTED_EXISTING = 23
EDGE_DIRECTION = {"top": "DOWN", "bottom": "UP", "left": "RIGHT", "right": "LEFT"}
SUPPORTED_COLORS = {"blue", "brown", "cyan", "green", "orange", "pink", "purple", "red", "white", "yellow"}
PLACEHOLDER_IDS = {"E28_R8_20"}
NEW_REVISION = "m8-20260929-levels-24-30"
NEW_APPROVAL_MODE = "editor-candidate-import-2026-09-29-r9-2-4-levels-24-30-manual-playtest-not-claimed"
ROOT = pathlib.Path(__file__).resolve().parents[2]
PROJECT = (ROOT / ".docs" / "HabitatShift_CurrentGame_UnityLevels_v1" / "data"
           / "Habitat_Shift_R9_2_4_level_editor_project.json")
CATALOG_STREAMING = ROOT / "Assets" / "StreamingAssets" / "HabitatShift" / "approved_levels_v4.json"
CATALOG_DOC = (ROOT / ".docs" / "HabitatShift_CurrentGame_UnityLevels_v1" / "data"
               / "approved_levels_v4.json")
REPORT = ROOT / ".docs" / "LevelTools" / "convert_r9_report_24_30.txt"


def array_span(text: str, key: str) -> tuple[int, int]:
    """(vi tri '[' , vi tri ']') cua mang JSON ung voi key, bo qua chuoi."""
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
    """Tach raw text tung object trong mang (giu nguyen format goc)."""
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


def connected(cells: list[list[int]] | list[tuple[int, int]]) -> bool:
    cellset = {tuple(cell) for cell in cells}
    if not cellset:
        return False
    start = next(iter(cellset))
    seen, queue = {start}, [start]
    while queue:
        x, y = queue.pop()
        for other in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if other in cellset and other not in seen:
                seen.add(other)
                queue.append(other)
    return len(seen) == len(cellset)


def fingerprint(levels: list[dict]) -> str:
    """Cong thuc da kiem chung khop fingerprint 23 man hien tai."""
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
    if level_id == LEVEL_IDS[-1]:
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
        kind = "EDITOR_PLACEHOLDER" if port.get("id") in PLACEHOLDER_IDS or port.get("unknown") else "PLACEHOLDER"
        notes.append(f"{kind} L{candidate['id']} {port.get('id')} (queue rong) -> khong import")
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
    """18 kiem tra tinh (static) truoc khi ghi production."""
    problems: list[str] = []
    if [level["id"] for level in levels] != list(LEVEL_IDS):
        problems.append("ids khong dung 24..30")
    for level in levels:
        level_id = level["id"]
        cols, rows = level["cols"], level["rows"]
        if cols < 1 or rows < 1:
            problems.append(f"L{level_id}: kich thuoc board khong hop le")
        if cols > max_cols or rows > max_rows:
            problems.append(f"L{level_id}: board {cols}x{rows} vuot max {max_cols}x{max_rows}")
        full = {(x, y) for y in range(rows) for x in range(cols)}
        mask = full if level["playableMask"] is None else {tuple(cell) for cell in level["playableMask"]}
        obstacles = {tuple(cell) for cell in level["obstacles"]}
        if not mask:
            problems.append(f"L{level_id}: playableMask rong")
        if not mask <= full:
            problems.append(f"L{level_id}: playableMask co o ngoai board")
        if not connected(sorted(mask)):
            problems.append(f"L{level_id}: playableMask khong lien thong")
        if not obstacles <= mask:
            problems.append(f"L{level_id}: blocked cell nam ngoai playableMask")
        playable = mask - obstacles
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
                if cell in obstacles:
                    problems.append(f"L{level_id}/{habitat['id']}: habitat de len obstacle {cell}")
                if cell not in playable:
                    problems.append(f"L{level_id}/{habitat['id']}: o {cell} ngoai playable")
        for cell, count in occupied.items():
            if count > 1:
                problems.append(f"L{level_id}: habitat chong nhau tai {cell} x{count}")
        target_cells: Counter = Counter()
        for target in level["targets"]:
            identifiers[target["id"]] += 1
            if target["color"] not in SUPPORTED_COLORS:
                problems.append(f"L{level_id}/{target['id']}: mau sproutling khong ho tro {target['color']}")
            cell = (int(target["position"][0]), int(target["position"][1]))
            target_cells[cell] += 1
            if cell in obstacles:
                problems.append(f"L{level_id}/{target['id']}: sproutling tren obstacle {cell}")
            if cell not in playable:
                problems.append(f"L{level_id}/{target['id']}: sproutling ngoai playable {cell}")
        for cell, count in target_cells.items():
            if count > 1:
                problems.append(f"L{level_id}: {count} sproutling cung o {cell}")
        for elevator in level["elevators"]:
            identifiers[elevator["id"]] += 1
            entry = (int(elevator["entry"][0]), int(elevator["entry"][1]))
            if entry not in playable:
                problems.append(f"L{level_id}/{elevator['id']}: entry {entry} khong nam trong playable")
            if not elevator["queue"]:
                problems.append(f"L{level_id}/{elevator['id']}: queue rong")
            for item in elevator["queue"]:
                identifiers[item["id"]] += 1
                if item["color"] not in SUPPORTED_COLORS:
                    problems.append(f"L{level_id}/{elevator['id']}: mau queue khong ho tro {item['color']}")
        for identifier, count in identifiers.items():
            if count > 1:
                problems.append(f"L{level_id}: id '{identifier}' xuat hien {count} lan")
        for color in sorted({habitat["color"] for habitat in level["habitats"]}):
            need = sum(habitat["need"] for habitat in level["habitats"] if habitat["color"] == color)
            supply = sum(1 for target in level["targets"] if target["color"] == color)
            supply += sum(1 for elevator in level["elevators"] for item in elevator["queue"] if item["color"] == color)
            if need != supply:
                problems.append(f"L{level_id}: mat can bang mau {color}: need={need} vs supply={supply}")
    return problems


def main() -> None:
    parser = argparse.ArgumentParser(description="Append approved R9.2.4 candidate levels 24-30.")
    parser.add_argument("--check", action="store_true", help="Chi kiem tra, khong ghi file.")
    parser.add_argument("--project", default=str(PROJECT))
    parser.add_argument("--catalog", default=str(CATALOG_STREAMING))
    args = parser.parse_args()

    project = json.loads(pathlib.Path(args.project).read_text(encoding="utf-8"))
    catalog_path = pathlib.Path(args.catalog)
    catalog_text = catalog_path.read_text(encoding="utf-8")
    catalog = json.loads(catalog_text)
    candidates = {level["id"]: level for level in project["candidateLevels"] if level["id"] in LEVEL_IDS}
    missing = [level_id for level_id in LEVEL_IDS if level_id not in candidates]
    if missing:
        sys.exit("candidateLevels thieu id: " + ", ".join(map(str, missing)))

    old_levels = catalog["levels"]
    old_fingerprint = fingerprint(old_levels)
    assert len(old_levels) == EXPECTED_EXISTING, f"catalog hien tai khong phai {EXPECTED_EXISTING} level"
    assert old_fingerprint == catalog["canonicalLevelsFingerprint"], (
        "fingerprint catalog hien tai khong khop: " + old_fingerprint)

    start, end = array_span(catalog_text, "levels")
    old_objects = split_objects(catalog_text[start:end + 1])
    assert len(old_objects) == len(old_levels) == EXPECTED_EXISTING, "so level cu khong dung"
    for index, raw in enumerate(old_objects):
        assert json.loads(raw) == old_levels[index], f"level {index + 1} raw text khong khop"

    new_levels, notes = [], []
    for level_id in LEVEL_IDS:
        level, level_notes = convert_level(candidates[level_id])
        new_levels.append(level)
        notes.extend(level_notes)

    problems = preflight(new_levels, project["architecture"]["boardMaxCols"],
                         project["architecture"]["boardMaxRows"])
    levels_all = old_levels + new_levels
    new_fingerprint = fingerprint(levels_all)

    def replace_root(text: str, key: str, value: str) -> str:
        pattern = r'("' + re.escape(key) + r'"\s*:\s*)(?:"[^"]*"|-?\d+)'
        if not re.search(pattern, text):
            raise ValueError("khong tim thay root field " + key)
        return re.sub(pattern, lambda match: match.group(1) + value, text, count=1)

    def rebuild(text: str) -> str:
        span_start, span_end = array_span(text, "levels")
        # Giu NGUYEN BYTE phan mang hien co (L1..L23) va chi noi them 7 block moi truoc ']'.
        blocks = [indent_block(json.dumps(level, ensure_ascii=False, indent=2), 4) for level in new_levels]
        head = text[:span_end].rstrip()
        text = head + ",\n" + ",\n".join(blocks) + "\n  ]" + text[span_end + 1:]
        text = replace_root(text, "catalogRevision", json.dumps(NEW_REVISION))
        text = replace_root(text, "approvalMode", json.dumps(NEW_APPROVAL_MODE))
        text = replace_root(text, "levelCount", str(len(levels_all)))
        text = replace_root(text, "canonicalLevelsFingerprint", json.dumps(new_fingerprint))
        return text

    new_text = rebuild(catalog_text)
    assert rebuild(catalog_text) == new_text, "output khong deterministic"
    parsed_new = json.loads(new_text)
    assert parsed_new["levels"][:EXPECTED_EXISTING] == old_levels, "L1..L23 bi thay doi (semantic)"
    old_inner = catalog_text[start + 1:end]
    new_start, new_end = array_span(new_text, "levels")
    new_inner = new_text[new_start + 1:new_end]
    assert new_inner.startswith(old_inner.rstrip()), "L1..L23 bi thay doi (raw text)"
    new_objects = split_objects(new_text[new_start:new_end + 1])
    assert new_objects[:EXPECTED_EXISTING] == old_objects, "L1..L23 bi thay doi (raw text)"
    assert len(new_objects) == EXPECTED_EXISTING + len(LEVEL_IDS), "so level moi khong dung"
    assert [level["id"] for level in parsed_new["levels"]] == list(range(1, LEVEL_IDS[-1] + 1)), "ids khong lien tuc"

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
        "convert_r9_levels_24_30 report",
        f"nguon      : {pathlib.Path(args.project).name}",
        f"catalog cu : {len(old_levels)} level, fingerprint {old_fingerprint}",
        f"catalog moi: {len(levels_all)} level, fingerprint {new_fingerprint}",
        f"revision   : {NEW_REVISION}",
        f"approval   : {NEW_APPROVAL_MODE}",
        f"sha256 moi : {hashlib.sha256(new_text.replace(chr(10), chr(13) + chr(10)).encode('utf-8')).hexdigest()}",
        "",
        "Level moi:",
    ]
    for level in new_levels:
        mask = "null" if level["playableMask"] is None else len(level["playableMask"])
        lines.append(f"  L{level['id']}: {level['cols']}x{level['rows']} profile={level['profile']} "
                     f"difficulty={level['difficulty']} habitats={len(level['habitats'])} "
                     f"targets={len(level['targets'])} elevators={len(level['elevators'])} "
                     f"obstacles={len(level['obstacles'])} playableMask={mask}")
    lines += ["", "Port / placeholder:"] + ["  " + note for note in notes]
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
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\r\n")
    catalog_path.write_text(new_text, encoding="utf-8", newline="\r\n")
    CATALOG_DOC.write_text(new_text, encoding="utf-8", newline="\r\n")
    print("\nda ghi: " + str(catalog_path))
    print("da ghi: " + str(CATALOG_DOC))
    print("bao cao: " + str(REPORT))


if __name__ == "__main__":
    main()
