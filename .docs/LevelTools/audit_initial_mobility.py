"""Conservative integer-anchor mobility audit for Habitat Shift level starts.

This holds other habitats and foreign sproutlings at their initial positions.
It is a local diagnostic, not a puzzle solver or a playtest certificate.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"
CARDINAL = ((-1, 0), (1, 0), (0, -1), (0, 1))


def footprint(habitat: dict, anchor: tuple[int, int]) -> set[tuple[int, int]]:
    return {(anchor[0] + x, anchor[1] + y) for x, y in habitat["shape"]}


def allowed_steps(habitat: dict) -> tuple[tuple[int, int], ...]:
    constraint = habitat["movementConstraint"]
    if constraint == "HORIZONTAL_ONLY":
        return CARDINAL[:2]
    if constraint == "VERTICAL_ONLY":
        return CARDINAL[2:]
    if constraint == "FREE":
        return CARDINAL
    raise ValueError(f"unknown movement constraint: {constraint}")


def legal(level: dict, habitat: dict, anchor: tuple[int, int]) -> bool:
    mask = ({(x, y) for y in range(level["rows"]) for x in range(level["cols"])}
            if level.get("playableMask") is None else {tuple(cell) for cell in level["playableMask"]})
    cells = footprint(habitat, anchor)
    if not cells or not cells <= mask or cells & {tuple(cell) for cell in level["obstacles"]}:
        return False
    if any(cells & footprint(other, tuple(other["anchor"]))
           for other in level["habitats"] if other is not habitat):
        return False
    # Targets are cell centres. At integer anchors, the runtime's 0.18-cell
    # foreign-sprout radius excludes a target exactly inside a footprint cell.
    if any(target["color"] != habitat["color"] and
           (int(target["position"][0]), int(target["position"][1])) in cells
           for target in level["targets"]):
        return False
    return True


def audit_level(level: dict) -> list[dict]:
    result = []
    for habitat in level["habitats"]:
        if habitat["need"] <= 0:
            continue
        start = tuple(habitat["anchor"])
        steps = allowed_steps(habitat)
        neighbors = [(start[0] + dx, start[1] + dy) for dx, dy in steps]
        open_neighbors = [list(anchor) for anchor in neighbors if legal(level, habitat, anchor)]
        reachable = {start} if legal(level, habitat, start) else set()
        pending = list(reachable)
        while pending:
            x, y = pending.pop()
            for dx, dy in steps:
                candidate = x + dx, y + dy
                if candidate not in reachable and legal(level, habitat, candidate):
                    reachable.add(candidate)
                    pending.append(candidate)
        result.append({
            "level": level["id"], "habitat": habitat["id"], "color": habitat["color"],
            "constraint": habitat["movementConstraint"], "anchor": list(start),
            "initial_legal": start in reachable, "legal_neighbors": open_neighbors,
            "reachable_anchor_count": len(reachable),
            "status": "PASS" if open_neighbors else "ZERO_INITIAL_MOBILITY",
        })
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("catalog", nargs="?", type=Path, default=DEFAULT)
    parser.add_argument("--json", type=Path)
    args = parser.parse_args()
    levels = json.loads(args.catalog.read_text(encoding="utf-8-sig"))["levels"][:18]
    rows = [row for level in levels for row in audit_level(level)]
    for row in rows:
        print(f"L{row['level']:02} {row['habitat']} {row['constraint']} "
              f"{row['status']} neighbors={row['legal_neighbors']} "
              f"static_span={row['reachable_anchor_count']}")
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8")
    if any(not row["initial_legal"] or row["status"] != "PASS" for row in rows):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
