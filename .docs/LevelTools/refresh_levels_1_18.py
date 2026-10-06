"""Apply the authored L1-L18 composition refresh without rewriting L19-L30.

Run one wave at a time: --wave A, --wave B, --wave C.
"""
from __future__ import annotations

import argparse
import heapq
import json
import pathlib
import re

from audit_levels_1_18_density import audit, occupied, reachable_cells
from convert_r9_levels import fingerprint

ROOT = pathlib.Path(__file__).resolve().parents[2]
PRODUCTION = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"
MIRROR = ROOT / ".docs/HabitatShift_CurrentGame_UnityLevels_v1/data/approved_levels_v4.json"
REVISION = "m8-20261001-levels-01-18-density-refresh"
APPROVAL_MODE = "candidate-2026-10-01-levels-01-18-density-refresh-manual-playtest-required"


def r(first: int, last: int) -> list[int]:
    return list(range(first, last + 1))


# Rows are indexed from the bottom. Gaps outside these sets are deliberately
# removed from the visual board; obstacles are independently authored below.
MASKS = {
    7: {4:r(0,5),3:[0,1,3,4,5],2:r(1,5),1:r(0,5),0:r(2,4)},
    8: {5:r(0,1),4:r(1,6),3:r(1,6),2:[1,2,5,6],1:r(0,5),0:r(0,2)},
    9: {5:r(0,5),4:r(2,5),3:r(2,6),2:r(1,6),1:r(1,7),0:r(3,4)},
    10:{6:r(4,7),5:r(2,6),4:r(2,6),3:r(2,6),2:r(1,6),1:r(0,6),0:r(2,3)},
    11:{6:r(0,4),5:r(1,7),4:r(1,7),3:r(1,8),2:r(1,7),1:r(1,7),0:r(3,4)},
    12:{7:r(3,5),6:r(4,8),5:r(1,7),4:r(0,7),3:r(1,7),2:r(1,7),1:r(0,7),0:r(3,5)},
    13:{7:r(6,7),6:r(2,7),5:r(2,7),4:r(1,6),3:r(2,6),2:r(1,7),1:r(1,7),0:r(3,4)},
    14:{6:r(1,7),5:r(2,8),4:r(2,7),3:r(1,7),2:r(0,7),1:r(0,5),0:[0]},
    15:{6:r(4,6),5:r(4,6),4:r(4,6),3:r(4,6),2:r(1,6),1:r(0,7),0:r(4,6)},
    16:{6:r(4,7),5:r(3,7),4:r(3,7),3:r(3,7),2:r(3,8),1:r(0,8),0:r(5,6)},
    17:{7:[0,1,2,5,6],6:r(2,7),5:r(2,7),4:r(4,7),3:r(4,8),2:r(3,8),1:r(0,9),0:r(5,7)},
    18:{7:[1,2,3,5,6,9],6:r(2,9),5:r(2,8),4:r(3,8),3:r(4,8),2:r(4,8),1:r(0,9),0:[2,7,8]},
}

BLOCKERS = {
    7:[[3,3],[4,2],[0,1]],
    8:[[3,3],[5,2],[2,1]],
    9:[[2,4],[3,3],[4,2],[3,0]],
    10:[[2,5],[5,3],[3,3],[1,2]],
    11:[[3,5],[4,4],[4,3],[5,2],[4,1]],
    12:[[5,6],[5,5],[2,4],[6,4],[3,3],[2,2],[5,1]],
    13:[[6,6],[2,5],[3,4],[4,3]],
    14:[[6,6],[2,5],[3,4],[3,3],[4,1]],
    15:[[1,2],[4,3],[6,5]],
    16:[[5,5],[4,3],[3,2],[7,4]],
    17:[[7,6],[4,4],[8,3],[3,2],[7,2],[5,0],[5,7]],
    18:[[4,6],[5,5],[8,3],[8,0],[6,2],[8,2],[8,4],[3,7],[6,3],[6,4]],
}

TARGET_MOVES = {
    3:{"L03_S01":[2.5,.5],"L03_S02":[2.5,2.5],"L03_S04":[1.5,4.5],
       "L03_S05":[3.5,3.5],"L03_S06":[3.5,4.5],"L03_S07":[4.5,3.5],
       "L03_S10":[4.5,5.5]},
    4:{"L04_S01":[.5,1.5],"L04_S02":[3.5,3.5],"L04_S04":[.5,4.5],
       "L04_S05":[5.5,1.5],"L04_S06":[6.5,4.5],"L04_S07":[3.5,4.5],
       "L04_S09":[5.5,6.5],"L04_S11":[2.5,4.5]},
}


def complete_mask(level: dict) -> None:
    """Add the fewest corridor cells needed for each color's mask-only route."""
    cols, rows = level["cols"], level["rows"]
    mask = {tuple(cell) for cell in level["playableMask"]}
    goals = [(target["color"], (int(target["position"][0]), int(target["position"][1])))
             for target in level["targets"]]
    goals += [(item["color"], (int(port["entry"][0]), int(port["entry"][1])))
              for port in level["elevators"] for item in port["queue"]]
    for color, goal in goals:
        habitat = next(h for h in level["habitats"] if h["color"] == color)
        if goal in reachable_cells(habitat, mask, set()):
            continue
        shape = {tuple(cell) for cell in habitat["shape"]}
        start = tuple(habitat["anchor"])
        placements = {}
        for y in range(rows):
            for x in range(cols):
                cells = {(x+dx,y+dy) for dx,dy in shape}
                if all(0 <= cx < cols and 0 <= cy < rows for cx,cy in cells):
                    if habitat["movementConstraint"] == "HORIZONTAL_ONLY" and y != start[1]:
                        continue
                    if habitat["movementConstraint"] == "VERTICAL_ONLY" and x != start[0]:
                        continue
                    placements[x,y] = cells
        best = {start:(0,0)}
        previous = {}
        pending = [(0,0,start)]
        found = None
        while pending:
            cost, length, point = heapq.heappop(pending)
            if (cost,length) != best.get(point):
                continue
            if goal in placements[point]:
                found = point
                break
            x,y = point
            for neighbor in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if neighbor not in placements:
                    continue
                score = (cost + len(placements[neighbor] - mask), length + 1)
                if score < best.get(neighbor, (10**9,10**9)):
                    best[neighbor] = score
                    previous[neighbor] = point
                    heapq.heappush(pending, (*score,neighbor))
        if found is None:
            raise ValueError(f"L{level['id']} {color} cannot reach {goal} even on full board")
        while found != start:
            mask.update(placements[found])
            found = previous[found]
        mask.update(placements[start])
    level["playableMask"] = [list(cell) for cell in sorted(mask, key=lambda p:(p[1],p[0]))]


def place_blockers(level: dict, preferred: list[list[int]]) -> None:
    mask = {tuple(cell) for cell in level["playableMask"]}
    habitats, targets, elevators = occupied(level)
    occupied_cells = set(habitats) | set(targets) | set(elevators)
    originals = [tuple(cell) for cell in preferred]
    candidates = originals + sorted(mask - occupied_cells - set(originals),
                                    key=lambda cell:(min(abs(cell[0]-x)+abs(cell[1]-y)
                                                          for x,y in originals),cell[1],cell[0]))
    accepted = []
    for candidate in candidates:
        if candidate not in mask or candidate in occupied_cells or candidate in accepted:
            continue
        trial = accepted + [candidate]
        level["obstacles"] = [list(cell) for cell in trial]
        if not audit(level)["errors"]:
            accepted = trial
        if len(accepted) == len(preferred):
            break
    if len(accepted) != len(preferred):
        print(f"L{level['id']} only {len(accepted)}/{len(preferred)} blockers preserve all static routes")
    level["obstacles"] = [list(cell) for cell in accepted]


def object_spans(text: str) -> list[tuple[int, int]]:
    match = re.search(r'"levels"\s*:\s*\[', text)
    if not match:
        raise ValueError("levels array missing")
    spans = []
    depth = 0
    in_string = False
    escaped = False
    start = None
    for index in range(match.end(), len(text)):
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
        elif char == "{":
            if depth == 0:
                start = index
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0 and start is not None:
                spans.append((start, index + 1))
                start = None
        elif char == "]" and depth == 0:
            return spans
    raise ValueError("levels array unterminated")


def replace_root(text: str, key: str, value: str) -> str:
    pattern = r'("' + re.escape(key) + r'"\s*:\s*)"[^"]*"'
    updated, count = re.subn(pattern, lambda match: match.group(1) + json.dumps(value), text, count=1)
    if count != 1:
        raise ValueError(f"root field {key} missing")
    return updated


def apply_wave(text: str, wave: str) -> str:
    catalog = json.loads(text)
    before_protected = catalog["levels"][18:]
    old_spans = object_spans(text)
    assert len(old_spans) == 30
    changed = set()
    if wave == "A":
        for level_id, moves in TARGET_MOVES.items():
            level = catalog["levels"][level_id - 1]
            for target in level["targets"]:
                if target["id"] in moves:
                    target["position"] = moves[target["id"]]
            changed.add(level_id)
    else:
        ids = range(7, 13) if wave == "B" else range(13, 19)
        for level_id in ids:
            level = catalog["levels"][level_id - 1]
            mask = [[x, y] for y, xs in MASKS[level_id].items() for x in xs]
            level["playableMask"] = sorted(mask, key=lambda cell:(cell[1],cell[0]))
            level["obstacles"] = []
            complete_mask(level)
            place_blockers(level, BLOCKERS[level_id])
            changed.add(level_id)
    for level_id in changed:
        result = audit(catalog["levels"][level_id - 1])
        if result["errors"]:
            raise ValueError(f"L{level_id}: {result['errors']}")
        print(f"L{level_id:02} playable={result['playable']} blockers={result['blockers']} density={result['density']:.1%}")
        print(result["board"])
    assert catalog["levels"][18:] == before_protected
    replacements = []
    for level_id in changed:
        start, end = old_spans[level_id - 1]
        block = json.dumps(catalog["levels"][level_id - 1], ensure_ascii=False, indent=2)
        block = block.replace("\n", "\n    ")
        replacements.append((start, end, block))
    for start, end, block in sorted(replacements, reverse=True):
        text = text[:start] + block + text[end:]
    text = replace_root(text, "catalogRevision", REVISION)
    text = replace_root(text, "approvalMode", APPROVAL_MODE)
    text = replace_root(text, "canonicalLevelsFingerprint", fingerprint(catalog["levels"]))
    assert json.loads(text)["levels"][18:] == before_protected
    return text


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--wave", choices=("A", "B", "C"), required=True)
    args = parser.parse_args()
    source = PRODUCTION.read_bytes()
    assert source == MIRROR.read_bytes(), "production/mirror mismatch before edit"
    text = source.decode("utf-8").replace("\r\n", "\n")
    result = apply_wave(text, args.wave).replace("\n", "\r\n").encode("utf-8")
    PRODUCTION.write_bytes(result)
    MIRROR.write_bytes(result)


if __name__ == "__main__":
    main()
