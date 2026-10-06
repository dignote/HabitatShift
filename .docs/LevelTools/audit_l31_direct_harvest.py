"""L31 authoring diagnostic; frozen neighbors are an approximation, not a solver.

Run against the candidate preview by default, or pass a catalog path. Cells are
integer board cells. Foreign targets and other starting Habitats block travel.
"""
from __future__ import annotations

import argparse
import json
import math
from collections import Counter, deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / ".docs/Verification/M12_L31_L34/l31_interlock_preview_v5.json"
DIRS = ((1, 0), (-1, 0), (0, 1), (0, -1))


def cells(h, anchor=None):
    x, y = anchor or h["anchor"]
    return {(x + dx, y + dy) for dx, dy in h["shape"]}


def classify(level):
    result = []
    starts = {h["id"]: cells(h) for h in level["habitats"]}
    targets = [(t["color"], tuple(map(int, t["position"]))) for t in level["targets"]]
    obstacle = set(map(tuple, level["obstacles"]))
    for h in level["habitats"]:
        color = h.get("color") or h["stages"][0]["color"]
        need = h.get("need") or h["stages"][0]["need"]
        start = tuple(h["anchor"])
        match = {p for c, p in targets if c == color}
        foreign = {p for c, p in targets if c != color}
        other = set().union(*(v for k, v in starts.items() if k != h["id"]))

        def legal(anchor, block_other=True):
            footprint = cells(h, anchor)
            return all(0 <= x < level["cols"] and 0 <= y < level["rows"] for x, y in footprint) and not footprint & (foreign | obstacle | (other if block_other else set()))

        def bfs(block_other=True):
            q = deque([start]); distances = {start: 0}; parents = {}
            while q:
                anchor = q.popleft()
                for dx, dy in DIRS:
                    nxt = (anchor[0] + dx, anchor[1] + dy)
                    if nxt not in distances and legal(nxt, block_other):
                        distances[nxt] = distances[anchor] + 1
                        parents[nxt] = anchor
                        q.append(nxt)
            return distances, parents

        frozen, _ = bfs()
        free, free_parents = bfs(False)
        immediate = {p for p in match if any(p in cells(h, a) for a, d in frozen.items() if d <= 1)}
        coverable = {p for p in match if any(p in cells(h, a) for a in frozen)}
        simple = set()
        for dx, dy in DIRS:
            a = start
            while True:
                nxt = (a[0] + dx, a[1] + dy)
                if not legal(nxt): break
                simple |= cells(h, nxt) & match
                a = nxt
        nearest = min((round(math.hypot(x-u, y-v), 3) for x, y in starts[h["id"]] for u, v in match), default=None)
        # Identify one useful shortest route that needs another Habitat's starting cells.
        crossed = False
        for p in match - coverable:
            anchors = [a for a in free if p in cells(h, a)]
            if not anchors: continue
            a = min(anchors, key=lambda v: free[v])
            while a != start:
                if cells(h, a) & other: crossed = True
                a = free_parents[a]
        simple_majority = len(simple) > need / 2
        if simple_majority:
            label = "EASY_DIRECT"
        elif len(coverable) < need or crossed:
            label = "INTERLOCKED"
        else:
            label = "ROUTED"
        sx, sy = start
        cross_region = any((x >= 4) != (sx >= 4) or (y >= 4) != (sy >= 4) for x, y in match)
        central = any(cells(h, a) & {(x, y) for x in range(2, 6) for y in range(2, 6)} for a in frozen)
        result.append(dict(id=h["id"], color=color, need=need, shape=h["shape"], nearest=nearest,
                           immediate=len(immediate), simple=len(simple), frozen=len(coverable),
                           supply=len(match), crossesInitialTerritory=crossed, crossRegion=cross_region,
                           centralReachable=central, classification=label))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("catalog", nargs="?", type=Path, default=DEFAULT)
    args = parser.parse_args()
    level = json.loads(args.catalog.read_text(encoding="utf8"))["levels"][30]
    rows = classify(level)
    for row in rows: print(json.dumps(row))
    counts = Counter(r["classification"] for r in rows)
    print("classes", dict(counts))
    print("crossRegion", sum(r["crossRegion"] for r in rows), "centralReachable", sum(r["centralReachable"] for r in rows))
    histogram = Counter(len(r["shape"]) for r in rows)
    print("footprintCells", dict(sorted(histogram.items())))
    assert len(rows) == 8 and histogram[4] <= 1
    assert counts["EASY_DIRECT"] <= 2 and counts["ROUTED"] + counts["INTERLOCKED"] >= 6
    assert counts["INTERLOCKED"] >= 3 and sum(r["crossRegion"] for r in rows) >= 5


if __name__ == "__main__": main()
