"""Author the M12 L31 interlock candidate without changing gameplay rules.

Run with --apply only after the Unity legality, trace and alternate-order audits pass.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PRODUCTION = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v5.json"
MIRROR = ROOT / ".docs/HabitatShift_CurrentGame_UnityLevels_v1/data/approved_levels_v5.json"
CANDIDATE = ROOT / ".docs/LevelTools/fixtures/m12_l31_l34_candidate_v5.json"
PREVIEW = ROOT / ".docs/Verification/M12_L31_L34/l31_interlock_preview_v5.json"


def h(name: str, color: str, need: int, anchor: tuple[int, int], shape: list[tuple[int, int]],
      next_color: str | None = None, next_need: int = 0) -> dict:
    item = {"id": "L31_H_" + name, "anchor": list(anchor), "shape": [list(c) for c in shape],
            "movementConstraint": "FREE"}
    if next_color:
        item["stages"] = [{"color": color, "need": need}, {"color": next_color, "need": next_need}]
    else:
        item.update(color=color, need=need)
    return item


def targets(color: str, cells: list[tuple[int, int]]) -> list[dict]:
    return [{"id": f"L31_S_{color.upper()}_{i:02}", "color": color,
             "position": [x + .5, y + .5]} for i, (x, y) in enumerate(cells, 1)]


def level31() -> dict:
    habitats = [
        h("GATE", "orange", 4, (3, 3), [(0, 0)]),
        h("BLUE_SQUARE", "blue", 4, (1, 2), [(0, 0), (1, 0), (0, 1), (1, 1)]),
        h("GREEN_L", "green", 4, (5, 2), [(0, 0), (1, 0), (0, 1)]),
        h("YELLOW_KEY", "yellow", 4, (4, 3), [(0, 0), (0, 1)]),
        h("PURPLE_SQUARE", "purple", 5, (0, 5), [(0, 0), (1, 0)]),
        h("ORANGE_TRIO", "orange", 4, (2, 0), [(0, 0), (1, 0), (2, 0)]),
        h("CYAN_RING", "cyan", 2, (5, 5), [(0, 0), (1, 0), (0, 1)], "red", 2),
        h("PINK_RING", "pink", 2, (2, 5), [(0, 0), (0, 1), (0, 2)], "white", 2),
    ]
    groups = {
        "orange": [(0, 0), (1, 0), (5, 0), (6, 0), (1, 1), (2, 1), (6, 4), (7, 4)],
        "blue": [(3, 1), (4, 1), (3, 2), (4, 2)],
        "green": [(5, 1), (7, 2), (6, 3), (7, 3)],
        "yellow": [(4, 6), (4, 7), (5, 7), (6, 7)],
        "purple": [(0, 2), (0, 3), (0, 6), (7, 1), (7, 7)],
        "cyan": [(6, 6), (7, 6)],
        "pink": [(1, 7), (3, 7)],
    }
    sproutlings = [item for color, cells in groups.items() for item in targets(color, cells)]
    elevators = [
        {"id": "L31_E_RED", "direction": "RIGHT", "entry": [7.5, .5],
         "queue": [{"id": f"L31_Q_RED_{i}", "color": "red"} for i in (1, 2)],
         "nextIndex": 0, "mandatory": True},
        {"id": "L31_E_WHITE", "direction": "UP", "entry": [.5, 7.5],
         "queue": [{"id": f"L31_Q_WHITE_{i}", "color": "white"} for i in (1, 2)],
         "nextIndex": 0, "mandatory": True},
    ]
    result = {"id": 31, "title": "Interlock Garden", "profile": "DUAL_RING_INTERLOCK",
              "difficulty": "M12_FOUNDATION", "cols": 8, "rows": 8,
              "challenge": "Open the center, trade space around the paired rings, then clear both queues.",
              "habitats": habitats, "targets": sproutlings, "elevators": elevators,
              "obstacles": [[0, 1]], "playableMask": None}
    occupied = set()
    for habitat in habitats:
        ax, ay = habitat["anchor"]
        for dx, dy in habitat["shape"]:
            cell = (ax + dx, ay + dy)
            assert cell not in occupied, cell
            occupied.add(cell)
    for target in sproutlings:
        x, y = (int(v) for v in target["position"])
        assert (x, y) not in occupied, target
        occupied.add((x, y))
    for obstacle in result["obstacles"]:
        assert tuple(obstacle) not in occupied
        occupied.add(tuple(obstacle))
    assert len(habitats) == 8 and len(sproutlings) == 29 and len(occupied) == 51
    for color, cells in groups.items():
        positions = set(cells)
        for x, y in positions:
            assert all(not all((x + dx * step, y + dy * step) in positions for step in range(4))
                       for dx, dy in ((1, 0), (0, 1))), (color, x, y)
    return result


def write_catalog(source: dict, output: Path) -> None:
    source["levels"][30] = level31()
    canonical = json.dumps(source["levels"], ensure_ascii=False, sort_keys=True,
                           separators=(",", ":")).encode("utf-8")
    source["canonicalLevelsFingerprint"] = hashlib.sha256(canonical).hexdigest()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(source, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(output, hashlib.sha256(output.read_bytes()).hexdigest(), source["canonicalLevelsFingerprint"])


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    production = json.loads(PRODUCTION.read_text(encoding="utf-8"))
    old = json.loads(CANDIDATE.read_text(encoding="utf-8"))
    if args.apply:
        write_catalog(production, PRODUCTION)
        MIRROR.write_bytes(PRODUCTION.read_bytes())
        write_catalog(old, CANDIDATE)
    else:
        write_catalog(production, PREVIEW)
    level = level31()
    board = [["." for _ in range(8)] for _ in range(8)]
    for x, y in level["obstacles"]: board[y][x] = "#"
    for habitat in level["habitats"]:
        color = habitat.get("color") or habitat["stages"][0]["color"]
        for dx, dy in habitat["shape"]:
            board[habitat["anchor"][1] + dy][habitat["anchor"][0] + dx] = color[0].upper()
    for target in level["targets"]:
        board[int(target["position"][1])][int(target["position"][0])] = target["color"][0].lower()
    for elevator in level["elevators"]:
        x, y = (int(v) for v in elevator["entry"])
        if board[y][x] == ".": board[y][x] = "E"
    print("\n".join("".join(row) for row in reversed(board)))


if __name__ == "__main__":
    main()
