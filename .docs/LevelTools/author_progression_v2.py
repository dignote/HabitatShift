"""Author the L1-L18 progression v2 candidate without touching L19-L30.

Default output is a verification draft. --apply writes byte-identical production
and mirror catalogs only after all static checks pass.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path

from audit_initial_mobility import audit_level as mobility
from audit_levels_1_18_density import audit, occupied
from convert_r9_levels import fingerprint
from refresh_levels_1_18 import complete_mask, object_spans, replace_root


ROOT = Path(__file__).resolve().parents[2]
PRODUCTION = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"
MIRROR = ROOT / ".docs/HabitatShift_CurrentGame_UnityLevels_v1/data/approved_levels_v4.json"
VERIFY = ROOT / ".docs/Verification/Levels01_18_ProgressionV2"
REVISION = "m8-20261001-levels-01-18-progression-v2"
APPROVAL = "candidate-2026-10-01-levels-01-18-progression-v2-manual-playtest-required"
SHAPES = {
    "single": [[0, 0]],
    "h2": [[0, 0], [1, 0]],
    "v2": [[0, 0], [0, 1]],
    "h3": [[0, 0], [1, 0], [2, 0]],
    "v3": [[0, 0], [0, 1], [0, 2]],
    "l3": [[0, 0], [0, 1], [1, 1]],
    "corner3": [[0, 0], [1, 0], [0, 1]],
    "square4": [[0, 0], [1, 0], [0, 1], [1, 1]],
}


def h(name: str, color: str, anchor: tuple[int, int], shape: str, need: int,
      axis: str = "FREE") -> dict:
    return dict(id=name, color=color, anchor=list(anchor), shape=SHAPES[shape],
                need=need, movementConstraint=axis)


def t(name: str, color: str, cell: tuple[int, int]) -> dict:
    return dict(id=name, color=color, position=[cell[0] + .5, cell[1] + .5])


def q(name: str, color: str) -> dict:
    return dict(id=name, color=color)


def mask(cols: int, rows: int, missing: set[tuple[int, int]]) -> list[list[int]]:
    return [[x, y] for y in range(rows) for x in range(cols) if (x, y) not in missing]


def set_level(level: dict, cols: int, rows: int, habitats: list[dict],
              targets: list[dict], obstacles: list[tuple[int, int]] = None,
              missing: set[tuple[int, int]] = None, challenge: str = None) -> None:
    level.update(cols=cols, rows=rows, habitats=habitats, targets=targets,
                 obstacles=[list(cell) for cell in (obstacles or [])],
                 playableMask=None if missing is None else mask(cols, rows, missing))
    if challenge:
        level["challenge"] = challenge


def design(levels: list[dict]) -> list[int]:
    l = levels
    elevator_masks = {level_id: copy.deepcopy(l[level_id - 1]["playableMask"])
                      for level_id in range(7, 11)}
    set_level(l[0], 4, 4,
              [h("A", "orange", (0, 1), "single", 2), h("B", "green", (3, 1), "v2", 1)],
              [t("L01_S01", "orange", (1, 1)), t("L01_S02", "orange", (2, 1)),
               t("L01_S03", "green", (3, 3))],
              challenge="First drag: collect two orange Sproutlings, then move the green tray up.")
    set_level(l[1], 5, 5,
              [h("A", "yellow", (0, 0), "l3", 2), h("B", "blue", (4, 2), "v2", 2)],
              [t("L02_S01", "yellow", (2, 1)), t("L02_S02", "yellow", (3, 2)),
               t("L02_S03", "blue", (2, 3)), t("L02_S04", "blue", (4, 4))],
              challenge="Bent footprints need clearance; two short routes share the middle.")
    set_level(l[2], 6, 6,
              [h("A", "pink", (0, 0), "square4", 3), h("B", "blue", (3, 0), "v2", 2),
               h("C", "yellow", (4, 3), "corner3", 3)],
              [t("L03_S01", "pink", (2, 0)), t("L03_S02", "pink", (2, 2)),
               t("L03_S03", "pink", (1, 4)), t("L03_S04", "blue", (3, 3)),
               t("L03_S05", "blue", (3, 5)), t("L03_S06", "yellow", (4, 2)),
               t("L03_S07", "yellow", (4, 5)), t("L03_S08", "yellow", (3, 4))],
              challenge="Three color clusters must be collected together on a larger board.")
    set_level(l[3], 6, 6,
              [h("A", "orange", (0, 2), "h2", 2), h("B", "green", (2, 1), "square4", 2),
               h("C", "blue", (4, 3), "corner3", 2), h("D", "pink", (1, 4), "single", 2)],
              [t("L04_S01", "orange", (0, 0)), t("L04_S02", "orange", (3, 3)),
               t("L04_S03", "green", (4, 1)), t("L04_S04", "green", (1, 3)),
               t("L04_S05", "blue", (5, 4)), t("L04_S06", "blue", (4, 5)),
               t("L04_S07", "pink", (1, 5)), t("L04_S08", "pink", (2, 4))],
              challenge="Four different trays meet around a busy central crossing.")
    set_level(l[4], 7, 6,
              [h("A", "purple", (1, 2), "h3", 2), h("B", "purple", (5, 3), "v2", 2),
               h("C", "orange", (0, 4), "square4", 2), h("D", "green", (3, 0), "corner3", 2),
               h("E", "blue", (5, 1), "h2", 2)],
              [t("L05_S01", "purple", (0, 0)), t("L05_S02", "purple", (6, 0)),
               t("L05_S03", "purple", (6, 5)), t("L05_S04", "purple", (2, 5)),
               t("L05_S05", "orange", (2, 4)), t("L05_S06", "orange", (3, 5)),
               t("L05_S07", "green", (4, 2)), t("L05_S08", "green", (4, 3)),
               t("L05_S09", "blue", (6, 3)), t("L05_S10", "blue", (5, 5))],
              challenge="Two purple trays with different footprints divide the outside loop.")
    set_level(l[5], 8, 7,
              [h("A", "red", (0, 2), "h3", 2), h("B", "red", (7, 4), "single", 2),
               h("C", "cyan", (5, 2), "h3", 4), h("D", "yellow", (2, 0), "l3", 3),
               h("E", "green", (3, 5), "corner3", 3)],
              l[5]["targets"],
              challenge="Two differently shaped red trays choose crossing routes through traffic.")
    l[6]["habitats"] = [
        h("L07_H_ORANGE", "orange", (1, 1), "h2", 1),
        h("L07_H_ORANGE_SMALL", "orange", (4, 1), "single", 1),
        h("L07_H_BLUE", "blue", (5, 4), "single", 1),
        h("L07_H_GREEN", "green", (2, 4), "v2", 1)]
    set_level(l[6], 7, 6, l[6]["habitats"],
              [t("L07_S_ORANGE_01", "orange", (3, 1)),
               t("L07_S_BLUE_01", "blue", (4, 4)),
               t("L07_S_GREEN_01", "green", (3, 5))],
              [(2, 3)], {(0, 0), (6, 0), (6, 5), (0, 5)},
              "Four trays share a broad funnel; the single lift remains the first arrival lesson.")
    set_level(l[7], 7, 6,
              [h("L08_H_YELLOW", "yellow", (0, 0), "l3", 1),
               h("L08_H_YELLOW_SMALL", "yellow", (3, 1), "single", 1),
               h("L08_H_PURPLE", "purple", (5, 4), "h2", 1),
               h("L08_H_PURPLE_TALL", "purple", (1, 3), "v2", 1)],
              [t("L08_S_YELLOW_01", "yellow", (4, 1)),
               t("L08_S_PURPLE_01", "purple", (3, 4))],
              [(3, 3)], {(6, 0), (5, 0), (6, 5), (3, 5)},
              "Two regions hold paired yellow and purple trays; assists stay optional.")
    set_level(l[8], 8, 7,
              [h("L09_H_BLUE", "blue", (1, 1), "h2", 1),
               h("L09_H_BLUE_SMALL", "blue", (4, 1), "single", 1),
               h("L09_H_BLUE_LONG", "blue", (2, 5), "h3", 1),
               h("L09_H_GREEN", "green", (5, 4), "v2", 2),
               h("L09_H_GREEN_SMALL", "green", (6, 2), "single", 1)],
              [t("L09_S_BLUE_01", "blue", (3, 1)),
               t("L09_S_GREEN_01", "green", (5, 2))],
              [(3, 3), (4, 2), (2, 4)],
              {(0, 0), (7, 0), (7, 6), (0, 6), (3, 6)},
              "Five trays choose how to use two arrivals and the shared middle.")
    set_level(l[9], 8, 7,
              [h("L10_H_RED", "red", (0, 1), "h2", 1),
               h("L10_H_RED_SMALL", "red", (3, 1), "single", 1),
               h("L10_H_CYAN", "cyan", (3, 4), "corner3", 2),
               h("L10_H_CYAN_TALL", "cyan", (6, 3), "v2", 1),
               h("L10_H_ORANGE", "orange", (6, 1), "single", 2)],
              [t("L10_S_RED_01", "red", (2, 1)),
               t("L10_S_CYAN_01", "cyan", (5, 4)),
               t("L10_S_ORANGE_01", "orange", (4, 5))],
              [(2, 5), (5, 3), (1, 2), (5, 2)],
              {(0, 0), (7, 0), (0, 6), (7, 6)},
              "Paired red and cyan trays negotiate crossed arrival queues.")

    # L11-L14 keep their refreshed composition, and L17 keeps its current mastery lead-in.
    set_level(l[14], 7, 7,
              [h("L15_H_CYAN_WIDE", "cyan", (0, 2), "h2", 2, "HORIZONTAL_ONLY"),
               h("L15_H_CYAN_SMALL", "cyan", (4, 5), "single", 1, "HORIZONTAL_ONLY"),
               h("L15_H_PINK_TALL", "pink", (6, 2), "v2", 2, "VERTICAL_ONLY"),
               h("L15_H_PINK_BENT", "pink", (2, 0), "l3", 1, "VERTICAL_ONLY")],
              [t("L15_S_CYAN_01", "cyan", (4, 2)), t("L15_S_CYAN_02", "cyan", (1, 5)),
               t("L15_S_CYAN_03", "cyan", (5, 2)),
               t("L15_S_PINK_01", "pink", (2, 4)), t("L15_S_PINK_02", "pink", (6, 5)),
               t("L15_S_PINK_03", "pink", (6, 1))],
              [(1, 3), (5, 3), (3, 6)], {(0, 0), (6, 0), (0, 6), (6, 6)},
              "Two horizontal and two vertical trays cross around a forgiving central pocket.")
    set_level(l[15], 8, 7,
              [h("L16_H_YELLOW_WIDE", "yellow", (0, 5), "h2", 1, "HORIZONTAL_ONLY"),
               h("L16_H_YELLOW_FREE", "yellow", (2, 3), "single", 2),
               h("L16_H_CYAN_LONG", "cyan", (1, 1), "h3", 2, "HORIZONTAL_ONLY"),
               h("L16_H_BLUE_TALL", "blue", (6, 3), "v2", 2, "VERTICAL_ONLY"),
               h("L16_H_PINK_TALL", "pink", (4, 3), "v2", 2, "VERTICAL_ONLY")],
              [t("L16_S_YELLOW_01", "yellow", (3, 5)),
               t("L16_S_CYAN_01", "cyan", (4, 1)),
               t("L16_S_BLUE_01", "blue", (6, 0)),
               t("L16_S_PINK_01", "pink", (4, 0)),
               t("L16_S_PINK_02", "pink", (4, 5))],
              [(2, 2), (5, 2), (2, 4), (5, 4)],
              {(0, 0), (7, 0), (0, 6), (7, 6)},
              "Four guided lanes and one free yellow tray coordinate two arrival queues.")
    l[15]["elevators"] = [
        dict(id="L16_E_BOTTOM", entry=[6.5, 6.5], direction="UP",
             queue=[q("L16_Q_BLUE_01", "blue"), q("L16_Q_YELLOW_01", "yellow")], nextIndex=0, mandatory=True),
        dict(id="L16_E_RIGHT", entry=[7.5, 1.5], direction="LEFT",
             queue=[q("L16_Q_CYAN_01", "cyan"), q("L16_Q_YELLOW_02", "yellow")], nextIndex=0, mandatory=True)]

    # The x=8 green lane is one cell wide at its only connection (x=7,y=0).
    # Yellow is a two-column bent tray on the left, so it cannot enter or seal that lane.
    barrier = {(7, y) for y in range(1, 8)} | {(0, 0), (9, 7)}
    set_level(l[17], 10, 8,
              [h("L18_H_RED", "red", (0, 1), "h2", 2, "HORIZONTAL_ONLY"),
               h("L18_H_RED_FREE", "red", (4, 3), "single", 1),
               h("L18_H_GREEN", "green", (8, 2), "v2", 3, "VERTICAL_ONLY"),
               h("L18_H_PURPLE", "purple", (2, 5), "corner3", 3),
               h("L18_H_YELLOW", "yellow", (4, 5), "l3", 3)],
              [t("L18_S_RED_01", "red", (3, 1)), t("L18_S_RED_02", "red", (5, 3)),
               t("L18_S_GREEN_01", "green", (8, 1)), t("L18_S_GREEN_02", "green", (8, 6)),
               t("L18_S_PURPLE_01", "purple", (1, 7)), t("L18_S_PURPLE_02", "purple", (3, 4)),
               t("L18_S_YELLOW_01", "yellow", (5, 2)), t("L18_S_YELLOW_02", "yellow", (4, 7))],
              [(0, 3), (1, 4), (6, 4), (6, 6), (4, 0), (0, 6)], barrier,
              "Green has a dedicated vertical lift lane; five trays time four deliveries through shared pockets.")
    l[17]["elevators"] = [
        dict(id="L18_E_TOP_PURPLE", entry=[2.5, .5], direction="DOWN",
             queue=[q("L18_Q_PURPLE_01", "purple")], nextIndex=0, mandatory=True),
        dict(id="L18_E_TOP_GREEN", entry=[8.5, .5], direction="DOWN",
             queue=[q("L18_Q_GREEN_01", "green")], nextIndex=0, mandatory=True),
        dict(id="L18_E_BOTTOM_YELLOW", entry=[5.5, 7.5], direction="UP",
             queue=[q("L18_Q_YELLOW_01", "yellow")], nextIndex=0, mandatory=True),
        dict(id="L18_E_RIGHT_RED", entry=[6.5, 1.5], direction="LEFT",
             queue=[q("L18_Q_RED_01", "red")], nextIndex=0, mandatory=True)]
    for level_id, original in elevator_masks.items():
        level = l[level_id - 1]
        habitat_cells, target_cells, elevator_cells = occupied(level)
        cells = ({tuple(cell) for cell in original} | set(habitat_cells) |
                 set(target_cells) | set(elevator_cells) |
                 {tuple(cell) for cell in level["obstacles"]})
        level["playableMask"] = [list(cell) for cell in sorted(cells, key=lambda p:(p[1],p[0]))]
        complete_mask(level)
    return [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 16, 18]


def validate(levels: list[dict], changed: list[int]) -> None:
    for level_id in changed:
        level = levels[level_id - 1]
        result = audit(level)
        if result["errors"]:
            raise ValueError(f"L{level_id}: {result['errors']}")
        rows = mobility(level)
        bad = [row for row in rows if not row["initial_legal"] or row["status"] != "PASS"]
        if bad:
            raise ValueError(f"L{level_id}: zero initial mobility: {bad}")
        for habitat in level["habitats"]:
            cells = habitat["shape"]
            x0, y0 = min(p[0] for p in cells), min(p[1] for p in cells)
            name = "-".join(f"{x-x0}_{y-y0}" for x, y in sorted(cells, key=lambda p:(p[0],p[1])))
            family = {"orange":"coral","green":"sage","blue":"cobalt","yellow":"saffron",
                      "pink":"peach","purple":"lavender","red":"rose","cyan":"teal"}[habitat["color"]]
            if not (ROOT / f"Assets/Resources/HabitatShift/Board/tray_{family}_{name}.png").is_file():
                raise ValueError(f"L{level_id} missing production tray {family}_{name}")
        print(f"L{level_id:02} {level['cols']}x{level['rows']} H={len(level['habitats'])} "
              f"target={len(level['targets'])} queued={result['queued']} "
              f"blockers={len(level['obstacles'])} mobility=PASS")
    green = next(row for row in mobility(levels[17]) if row["habitat"] == "L18_H_GREEN")
    if green["reachable_anchor_count"] <= 1:
        raise ValueError("L18 green constrained lane has fewer than two legal anchors")
    # Geometric proof independent of move ordering: every mask-legal yellow
    # placement stays left of the one-cell neck. It cannot enter x=8 or cover
    # any cell in green's dedicated lane, even after other trays are moved.
    l18 = levels[17]
    yellow = next(h for h in l18["habitats"] if h["id"] == "L18_H_YELLOW")
    mask_cells = {tuple(c) for c in l18["playableMask"]}
    def mask_legal(anchor: tuple[int, int]) -> bool:
        return {(anchor[0] + dx, anchor[1] + dy) for dx, dy in yellow["shape"]} <= mask_cells
    start = tuple(yellow["anchor"])
    placements = {start}
    pending = [start]
    while pending:
        x, y = pending.pop()
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            neighbor = (x + dx, y + dy)
            if neighbor not in placements and mask_legal(neighbor):
                placements.add(neighbor)
                pending.append(neighbor)
    if not mask_legal(start) or any(x + dx >= 8 for x, _ in placements for dx, _ in yellow["shape"]):
        raise ValueError("L18 yellow can reach green lane")
    print(f"L18 yellow mask-reachable anchors={len(placements)}, "
          f"max occupied x={max(x + dx for x, _ in placements for dx, _ in yellow['shape'])}; "
          f"green static span={green['reachable_anchor_count']}")


def write_candidate(original: str, levels: list[dict], changed: list[int]) -> bytes:
    spans = object_spans(original)
    if len(spans) != 30:
        raise ValueError("expected 30 original level objects")
    result = original
    for level_id in sorted(changed, reverse=True):
        start, end = spans[level_id - 1]
        block = json.dumps(levels[level_id - 1], ensure_ascii=False, indent=2)
        result = result[:start] + block.replace("\n", "\n    ") + result[end:]
    result = replace_root(result, "catalogRevision", REVISION)
    result = replace_root(result, "approvalMode", APPROVAL)
    result = replace_root(result, "canonicalLevelsFingerprint", fingerprint(levels))
    return result.replace("\n", "\r\n").encode("utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    source = PRODUCTION.read_bytes()
    if source != MIRROR.read_bytes():
        raise ValueError("production/mirror differ before authoring")
    original = source.decode("utf-8").replace("\r\n", "\n")
    catalog = json.loads(original)
    protected = catalog["levels"][18:]
    changed = design(catalog["levels"])
    if catalog["levels"][18:] != protected:
        raise ValueError("L19-L30 changed")
    validate(catalog["levels"], changed)
    candidate = write_candidate(original, catalog["levels"], changed)
    VERIFY.mkdir(parents=True, exist_ok=True)
    (VERIFY / "catalog_candidate.json").write_bytes(candidate)
    print("candidate SHA-256:", hashlib.sha256(candidate).hexdigest())
    if args.apply:
        PRODUCTION.write_bytes(candidate)
        MIRROR.write_bytes(candidate)


if __name__ == "__main__":
    main()
