"""Audit integer-anchor access to same-color initial and queued Sproutlings.

Foreign habitats and Sproutlings stay at initial positions. Other habitats of
the audited color may move or complete, so they are omitted. This is a static
ordering-risk check, not a full continuous-motion puzzle solver.
"""
from __future__ import annotations

import argparse
import json
from collections import Counter, deque
from pathlib import Path

from audit_initial_mobility import allowed_steps, footprint

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"


def legal(level: dict, habitat: dict, anchor: tuple[int, int]) -> bool:
    mask = ({(x, y) for y in range(level["rows"]) for x in range(level["cols"])}
            if level.get("playableMask") is None else {tuple(c) for c in level["playableMask"]})
    cells = footprint(habitat, anchor)
    if not cells or not cells <= mask or cells & {tuple(c) for c in level["obstacles"]}:
        return False
    if any(cells & footprint(other, tuple(other["anchor"]))
           for other in level["habitats"] if other["color"] != habitat["color"]):
        return False
    if any(tuple(map(int, target["position"])) in cells
           for target in level["targets"] if target["color"] != habitat["color"]):
        return False
    return True


def anchors(level: dict, habitat: dict) -> set[tuple[int, int]]:
    start = tuple(habitat["anchor"])
    if not legal(level, habitat, start):
        return set()
    seen = {start}
    pending = deque([start])
    while pending:
        x, y = pending.popleft()
        for dx, dy in allowed_steps(habitat):
            neighbor = (x + dx, y + dy)
            if neighbor not in seen and legal(level, habitat, neighbor):
                seen.add(neighbor)
                pending.append(neighbor)
    return seen


def components(level: dict, habitat: dict) -> list[list[list[int]]]:
    remaining = {(x, y) for y in range(level["rows"]) for x in range(level["cols"])
                 if legal(level, habitat, (x, y))}
    found = []
    while remaining:
        start = min(remaining, key=lambda p: (p[1], p[0]))
        group = {start}
        pending = [start]
        remaining.remove(start)
        while pending:
            x, y = pending.pop()
            for dx, dy in allowed_steps(habitat):
                neighbor = (x + dx, y + dy)
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    group.add(neighbor)
                    pending.append(neighbor)
        found.append([list(p) for p in sorted(group, key=lambda p: (p[1], p[0]))])
    return found


def matrix(level: dict) -> list[dict]:
    counts = Counter(h["color"] for h in level["habitats"])
    rows = []
    for habitat in level["habitats"]:
        if counts[habitat["color"]] < 2:
            continue
        reachable = anchors(level, habitat)
        targets = [(t["id"], tuple(map(int, t["position"])), "initial")
                   for t in level["targets"] if t["color"] == habitat["color"]]
        targets += [(q["id"], tuple(map(int, elevator["entry"])), "queued")
                    for elevator in level["elevators"] for q in elevator["queue"][elevator["nextIndex"]:]
                    if q["color"] == habitat["color"]]
        access = {}
        for target_id, cell, kind in targets:
            hits = [list(anchor) for anchor in sorted(reachable, key=lambda p:(p[1],p[0]))
                    if cell in footprint(habitat, anchor)]
            access[target_id] = {"kind": kind, "cell": list(cell), "reachable": bool(hits), "anchors": hits}
        rows.append({"level": level["id"], "habitat": habitat["id"],
                     "color": habitat["color"], "reachable_anchor_count": len(reachable),
                     "targets": access})
    return rows


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("catalog", nargs="?", type=Path, default=CATALOG)
    parser.add_argument("--json", type=Path)
    parser.add_argument("--require-l8", action="store_true")
    args = parser.parse_args()
    levels = json.loads(args.catalog.read_text(encoding="utf-8-sig"))["levels"][:18]
    rows = [row for level in levels for row in matrix(level)]
    for row in rows:
        cells = " ".join(f"{name}:{'Y' if value['reachable'] else 'N'}"
                         for name, value in row["targets"].items())
        print(f"L{row['level']:02} {row['habitat']} anchors={row['reachable_anchor_count']} {cells}")
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8")
    if args.require_l8:
        expected_habitats = {"L08_H_YELLOW", "L08_H_YELLOW_SMALL"}
        expected_targets = {"L08_S_YELLOW_01", "L08_Q_YELLOW_01"}
        selected = [row for row in rows if row["level"] == 8 and row["habitat"] in expected_habitats]
        if {row["habitat"] for row in selected} != expected_habitats or any(
            not expected_targets <= row["targets"].keys() or
            not all(row["targets"][target]["reachable"] for target in expected_targets)
            for row in selected
        ):
            raise SystemExit("FAIL: L8 yellow ordering reachability")
        print("PASS: L8 yellow same-color ordering reachability")


if __name__ == "__main__":
    main()
