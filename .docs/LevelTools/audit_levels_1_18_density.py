"""Audit production Habitat Shift level composition and basic supply integrity.

Usage: python .docs/LevelTools/audit_levels_1_18_density.py [catalog.json]
"""
from __future__ import annotations

import json
import pathlib
import sys
from collections import Counter

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"


def occupied(level: dict):
    habitats = {}
    for habitat in level["habitats"]:
        for dx, dy in habitat["shape"]:
            cell = habitat["anchor"][0] + dx, habitat["anchor"][1] + dy
            habitats[cell] = habitat["color"][0].upper()
    targets = {(int(target["position"][0]), int(target["position"][1])):
               target["color"][0].lower() for target in level["targets"]}
    elevators = {(int(port["entry"][0]), int(port["entry"][1])): "E"
                 for port in level["elevators"]}
    return habitats, targets, elevators


def flood(start, cells):
    seen, pending = {start}, [start]
    while pending:
        x, y = pending.pop()
        for next_cell in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if next_cell in cells and next_cell not in seen:
                seen.add(next_cell)
                pending.append(next_cell)
    return seen


def reachable_cells(habitat, mask, blockers):
    shape = {tuple(cell) for cell in habitat["shape"]}
    cols = max(x for x, _ in mask) + 1
    rows = max(y for _, y in mask) + 1
    placements = set()
    for y in range(rows):
        for x in range(cols):
            cells = {(x+dx,y+dy) for dx,dy in shape}
            if cells <= mask - blockers:
                placements.add((x,y))
    start = tuple(habitat["anchor"])
    if start not in placements:
        return set()
    constraint = habitat["movementConstraint"]
    if constraint == "HORIZONTAL_ONLY":
        placements = {point for point in placements if point[1] == start[1]}
    elif constraint == "VERTICAL_ONLY":
        placements = {point for point in placements if point[0] == start[0]}
    anchors = flood(start, placements)
    return {(x+dx,y+dy) for x,y in anchors for dx,dy in shape}


def audit(level: dict):
    cols, rows = level["cols"], level["rows"]
    full = {(x, y) for y in range(rows) for x in range(cols)}
    mask = full if level.get("playableMask") is None else {tuple(c) for c in level["playableMask"]}
    blockers = {tuple(c) for c in level["obstacles"]}
    habitats, targets, elevators = occupied(level)
    needs = Counter()
    supply = Counter()
    for habitat in level["habitats"]:
        needs[habitat["color"]] += habitat["need"]
    for target in level["targets"]:
        supply[target["color"]] += 1
    for port in level["elevators"]:
        for item in port["queue"][port.get("nextIndex", 0):]:
            supply[item["color"]] += 1
    errors = []
    if cols > 10 or rows > 10 or cols < 1 or rows < 1:
        errors.append("board dimensions outside 1..10")
    if level.get("playableMask") is not None and len(mask) != len(level["playableMask"]):
        errors.append("duplicate playable cells")
    if len(blockers) != len(level["obstacles"]):
        errors.append("duplicate blockers")
    if len(habitats) != sum(len(h["shape"]) for h in level["habitats"]):
        errors.append("habitat footprints overlap or repeat a cell")
    ids = ([h["id"] for h in level["habitats"]] + [t["id"] for t in level["targets"]] +
           [e["id"] for e in level["elevators"]] +
           [item["id"] for e in level["elevators"] for item in e["queue"]])
    if len(ids) != len(set(ids)):
        errors.append("duplicate entity id")
    for habitat in level["habitats"]:
        shape = {tuple(cell) for cell in habitat["shape"]}
        if not shape or len(shape) != len(habitat["shape"]) or len(flood(next(iter(shape)), shape)) != len(shape):
            errors.append(f"{habitat['id']} shape disconnected or repeated")
    for label, cells in (("mask", mask), ("blocker", blockers), ("habitat", set(habitats)),
                         ("target", set(targets)), ("elevator", set(elevators))):
        outside = cells - full if label == "mask" else cells - mask
        if outside:
            errors.append(f"{label} outside playable: {sorted(outside)}")
    for a, b, left, right in (("habitat", "target", habitats, targets),
                              ("habitat", "blocker", habitats, blockers),
                              ("target", "blocker", targets, blockers),
                              ("elevator", "blocker", elevators, blockers),
                              ("habitat", "elevator", habitats, elevators),
                              ("target", "elevator", targets, elevators)):
        overlap = set(left) & set(right)
        if overlap:
            errors.append(f"{a}/{b} overlap: {sorted(overlap)}")
    for color in sorted(set(needs) | set(supply)):
        if needs[color] != supply[color]:
            errors.append(f"{color} need={needs[color]} supply={supply[color]}")
    if len(targets) != len(level["targets"]):
        errors.append("duplicate initial target cells")
    walkable = mask - blockers
    if walkable and len(flood(next(iter(walkable)), walkable)) != len(walkable):
        errors.append("disconnected walkable cells")
    reach = {habitat["id"]: reachable_cells(habitat, mask, blockers)
             for habitat in level["habitats"]}
    for target in level["targets"]:
        cell = int(target["position"][0]), int(target["position"][1])
        if not any(cell in reach[habitat["id"]] for habitat in level["habitats"]
                   if habitat["color"] == target["color"]):
            errors.append(f"{target['id']} has no mask-only route for matching habitat")
    for elevator in level["elevators"]:
        cell = int(elevator["entry"][0]), int(elevator["entry"][1])
        for item in elevator["queue"][elevator.get("nextIndex", 0):]:
            if not any(cell in reach[habitat["id"]] for habitat in level["habitats"]
                       if habitat["color"] == item["color"]):
                errors.append(f"{item['id']} has no mask-only route for matching habitat")
    footprint = sum(len(h["shape"]) for h in level["habitats"])
    density = (footprint + len(level["targets"]) + len(blockers)) / len(mask)
    lines = []
    for y in reversed(range(rows)):
        row = ""
        for x in range(cols):
            cell = x, y
            row += (" " if cell not in mask else "#" if cell in blockers else
                    habitats[cell] if cell in habitats else targets[cell] if cell in targets else
                    elevators[cell] if cell in elevators else ".")
        lines.append(f"{y:2} {row}")
    return dict(id=level["id"], title=level["title"], dimensions=f"{cols}x{rows}",
                playable=len(mask), full=len(full), habitats=len(level["habitats"]),
                footprint=footprint, initial=len(level["targets"]), elevators=len(level["elevators"]),
                queued=sum(len(e["queue"]) - e.get("nextIndex", 0) for e in level["elevators"]),
                blockers=len(blockers), density=density, supply=dict(needs=needs, available=supply),
                errors=errors, board="\n".join(lines))


def main():
    path = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT
    levels = json.loads(path.read_text(encoding="utf-8-sig"))["levels"][:18]
    for level in levels:
        a = audit(level)
        print(f"L{a['id']:02} {a['title']}: {a['dimensions']} playable={a['playable']}/{a['full']} "
              f"({a['playable']/a['full']:.1%}) "
              f"habitats={a['habitats']} footprint={a['footprint']} initial={a['initial']} "
              f"elevators={a['elevators']} queued={a['queued']} blockers={a['blockers']} "
              f"density={a['density']:.1%}")
        print(a["board"])
        print("supply:", dict(a["supply"]["needs"]), dict(a["supply"]["available"]))
        for error in a["errors"]:
            print("ERROR:", error)
    if any(audit(level)["errors"] for level in levels):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
