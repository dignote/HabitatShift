"""Author isolated v5/v3 L31-L34 candidates; never writes the production catalog.

Usage: python .docs/LevelTools/author_m12_l31_l34.py
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
from rework_l31_interlock import level31

ROOT = Path(__file__).resolve().parents[2]
PRODUCTION = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"
FIXTURES = ROOT / ".docs/LevelTools/fixtures"
OUTPUT = FIXTURES / "m12_l31_l34_candidate_v5.json"
RULES = FIXTURES / "m11_ruleset_v3.json"
CONTRACT = ROOT / ".docs/LevelTools/m12_l31_l50_authoring_contract_v1.json"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def target(level: int, row: int, x: int, color: str, sequence: int) -> dict:
    return {"id": f"L{level:02d}_S_{row}_{sequence}", "color": color,
            "position": [x + .5, row + .5]}


def habitat(level: int, row: int, shape: list[list[int]], anchor_x: int,
            color: str, need: int, next_color: str | None = None, next_need: int = 0) -> dict:
    record = {"id": f"L{level:02d}_H_{row}", "anchor": [anchor_x, row], "shape": shape,
              "movementConstraint": "FREE"}
    if next_color is None:
        record.update(color=color, need=need)
    else:
        record["stages"] = [{"color": color, "need": need},
                            {"color": next_color, "need": next_need}]
    return record


def rotate_level(record: dict, clockwise: bool) -> None:
    """Turn a proven lane layout into a vertical composition on the same 8x8 grid."""
    def cell(x: int, y: int) -> list[int]:
        return [7 - y, x] if clockwise else [y, 7 - x]

    def point(x: float, y: float) -> list[float]:
        return [8 - y, x] if clockwise else [y, 8 - x]

    for h in record["habitats"]:
        ax, ay = h["anchor"]
        cells = [cell(ax + dx, ay + dy) for dx, dy in h["shape"]]
        min_x = min(c[0] for c in cells)
        min_y = min(c[1] for c in cells)
        h["anchor"] = [min_x, min_y]
        h["shape"] = [[x - min_x, y - min_y] for x, y in cells]
    for t in record["targets"]:
        t["position"] = point(*t["position"])
    for e in record["elevators"]:
        e["entry"] = point(*e["entry"])
        e["direction"] = ({"RIGHT": "UP", "LEFT": "DOWN"} if clockwise
                          else {"RIGHT": "DOWN", "LEFT": "UP"})[e["direction"]]
    record["obstacles"] = [cell(*c) for c in record["obstacles"]]


def level(level_id: int) -> dict:
    # The first two pairs form compact ordering dependencies. The lower-right
    # pair teaches stage transition plus clearing an elevator entry between releases.
    orientations = {
        31: {4: "left", 5: "right", 6: "left", 7: "right"},
        32: {4: "right", 5: "left", 6: "right", 7: "left"},
        33: {4: "left", 5: "left", 6: "right", 7: "right"},
        34: {4: "right", 5: "right", 6: "left", 7: "right"},
    }[level_id]
    palette = ["orange", "blue", "green", "yellow", "purple", "orange", "cyan", "pink"]
    stage_two = {6: "red", 7: "white"}
    records: list[dict] = []
    targets: list[dict] = []
    elevators: list[dict] = []
    obstacles: list[list[int]] = []

    # Square: row 1's reversed domino must clear its targets before this can sweep.
    records.append(habitat(level_id, 0, [[0, 0], [1, 0], [0, 1], [1, 1]], 0,
                           palette[0], 5))
    targets.extend(target(level_id, 0, x, palette[0], i) for i, x in enumerate(range(2, 7), 1))
    records.append(habitat(level_id, 1, [[0, 0], [1, 0]], 6, palette[1], 3))
    targets.extend(target(level_id, 1, x, palette[1], i) for i, x in enumerate(range(5, 2, -1), 1))

    # L shape: its upper cell crosses row 3 after that row's Habitat completes.
    records.append(habitat(level_id, 2, [[0, 0], [1, 0], [0, 1]], 0, palette[2], 5))
    targets.extend(target(level_id, 2, x, palette[2], i) for i, x in enumerate(range(2, 7), 1))
    records.append(habitat(level_id, 3, [[0, 0], [1, 0]], 6, palette[3], 5))
    targets.extend(target(level_id, 3, x, palette[3], i) for i, x in enumerate(range(5, 0, -1), 1))

    for row, width, count in ((4, 3, 4), (5, 2, 5), (6, 3, 2), (7, 2, 2)):
        direction = orientations[row]
        anchor_x = 0 if direction == "left" else 8 - width
        shape = [[dx, 0] for dx in range(width)]
        if row < 6:
            records.append(habitat(level_id, row, shape, anchor_x, palette[row], count))
            xs = range(width, 7) if direction == "left" else range(7 - width, 0, -1)
            targets.extend(target(level_id, row, x, palette[row], i) for i, x in enumerate(xs, 1))
        else:
            records.append(habitat(level_id, row, shape, anchor_x, palette[row], 2,
                                   stage_two[row], 2))
            xs = [width, width + 1] if direction == "left" else [7 - width, 6 - width]
            targets.extend(target(level_id, row, x, palette[row], i) for i, x in enumerate(xs, 1))
            entry_x = 6 if direction == "left" else 1
            elevators.append({"id": f"L{level_id:02d}_E_{row}",
                              "direction": "RIGHT" if direction == "left" else "LEFT",
                              "entry": [entry_x + .5, row + .5],
                              "queue": [{"id": f"L{level_id:02d}_Q_{row}_{i}",
                                         "color": stage_two[row]} for i in (1, 2)],
                              "nextIndex": 0, "mandatory": True})

    # Blockers occupy the far end of selected lanes and prevent overshooting.
    for row in (4, 5, 6)[:level_id - 31]:
        obstacles.append([7 if orientations[row] == "left" else 0, row])

    names = {31: "Crossgrain", 32: "Inner Current", 33: "Offset Bloom", 34: "Double Shift"}
    record = {"id": level_id, "title": names[level_id], "profile": "DUAL_RING_CANDIDATE",
              "difficulty": "M12_FOUNDATION", "cols": 8, "rows": 8,
              "challenge": "Clear the paired footprint lanes, then uncover two staged elevator queues.",
              "habitats": records, "targets": targets, "elevators": elevators,
              "obstacles": obstacles, "playableMask": None}
    if level_id in (32, 34):
        rotate_level(record, clockwise=level_id == 32)
    density = (sum(len(h["shape"]) for h in records) + len(targets) + len(obstacles)) / 64
    # L32-L34 are frozen in the L31-only interlock correction. L34's existing
    # 85.9% is just above the revised 85% target and must not be redesigned here.
    assert 8 <= len(records) <= 12 and .76 <= density <= .87, (level_id, density)
    assert len({h.get("color", h.get("stages", [{}])[0].get("color")) for h in records}) >= 7
    return record


def main() -> None:
    source = json.loads(PRODUCTION.read_text(encoding="utf-8"))
    rules_bytes = RULES.read_bytes()
    contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
    assert source["levelCount"] == 30 and contract["productionCutoverInM11"] is False
    candidate_levels = source["levels"] + [level31() if i == 31 else level(i) for i in range(31, 35)]
    l31 = candidate_levels[30]
    l31_density = (sum(len(h["shape"]) for h in l31["habitats"]) + len(l31["targets"]) + len(l31["obstacles"])) / 64
    assert .76 <= l31_density <= .85
    canonical = json.dumps(candidate_levels, ensure_ascii=False, sort_keys=True,
                           separators=(",", ":")).encode("utf-8")
    catalog = {
        "schema": "habitat-shift-levels-v5",
        "catalogRevision": "m12-l31-l34-candidate-v1",
        "rulesetVersion": "continuous-core-v3",
        "rulesetFingerprint": sha(rules_bytes),
        "canonicalLevelsFingerprint": sha(canonical),
        "approvalMode": "candidate-solution-trace-and-manual-playtest-required",
        "certificationTraceRequired": True,
        "levelCount": 34,
        "levels": candidate_levels,
    }
    OUTPUT.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("output", OUTPUT)
    print("fingerprint", catalog["canonicalLevelsFingerprint"])
    for entry in candidate_levels[30:]:
        occupied = sum(len(h["shape"]) for h in entry["habitats"]) + len(entry["targets"]) + len(entry["obstacles"])
        print(f'L{entry["id"]}: density={occupied}/64={occupied/64:.1%}, habitats={len(entry["habitats"])}, '
              f'initial={len(entry["targets"])}, queued={sum(len(x["queue"]) for x in entry["elevators"])}')


if __name__ == "__main__":
    main()
