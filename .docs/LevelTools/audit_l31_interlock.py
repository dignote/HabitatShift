"""Frozen-neighbor integer-anchor checks for the L31 lock/key claims.

This is a structural audit. ContinuousSession traces remain authoritative.
"""
from collections import deque
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEVEL = json.loads((ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v5.json").read_text())["levels"][30]


def reachable(habitat_id, anchors=None, removed=(), stage_color=None, cleared_colors=(), cleared_cells=()):
    habitat = next(h for h in LEVEL["habitats"] if h["id"] == habitat_id)
    anchors = anchors or {}
    footprint = {tuple(c) for c in habitat["shape"]}
    start = tuple(anchors.get(habitat_id, habitat["anchor"]))
    color = stage_color or habitat.get("color") or habitat["stages"][0]["color"]
    others = {tuple((h["anchor"][0] + dx, h["anchor"][1] + dy))
              for h in LEVEL["habitats"] if h["id"] != habitat_id and h["id"] not in removed
              for dx, dy in h["shape"]}
    if anchors:
        others = {cell for cell in others if all(cell not in
                  {(h["anchor"][0] + dx, h["anchor"][1] + dy) for dx, dy in h["shape"]}
                  for h in LEVEL["habitats"] if h["id"] in anchors and h["id"] != habitat_id)}
        for hid, anchor in anchors.items():
            if hid == habitat_id or hid in removed: continue
            h = next(h for h in LEVEL["habitats"] if h["id"] == hid)
            others.update((anchor[0] + dx, anchor[1] + dy) for dx, dy in h["shape"])
    foreign = {(int(t["position"][0]), int(t["position"][1]))
               for t in LEVEL["targets"] if t["color"] != color and t["color"] not in cleared_colors
               and tuple(map(int, t["position"])) not in cleared_cells}
    obstacles = {tuple(c) for c in LEVEL["obstacles"]}

    def legal(a):
        cells = {(a[0] + dx, a[1] + dy) for dx, dy in footprint}
        return all(0 <= x < 8 and 0 <= y < 8 for x, y in cells) and not cells & (others | foreign | obstacles)

    if not legal(start): return set()
    seen = {start}; pending = deque([start])
    while pending:
        x, y = pending.popleft()
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nxt = (x + d[0], y + d[1])
            if nxt not in seen and legal(nxt): seen.add(nxt); pending.append(nxt)
    return seen


def covers(hid, anchors, cell):
    h = next(h for h in LEVEL["habitats"] if h["id"] == hid)
    return any((a[0] + dx, a[1] + dy) == cell for a in anchors for dx, dy in h["shape"])


def main():
    gate, key, blue, purple, pink = ("L31_H_" + n for n in
        ("GATE", "YELLOW_KEY", "BLUE_SQUARE", "PURPLE_SQUARE", "PINK_RING"))
    blue_targets = [(3, 1), (4, 1), (3, 2), (4, 2)]
    cases = [
        ("initial", {}),
        ("gate moved", {gate: (3, 4)}),
        ("gate and key moved", {gate: (3, 4), key: (4, 4)}),
    ]
    for label, moved in cases:
        anchors = reachable(blue, moved)
        print(label, "blue legal anchors", len(anchors),
              "blue targets coverable", [p for p in blue_targets if covers(blue, anchors, p)])
    before = reachable(pink)
    after = reachable(pink, {purple: (0, 2)}, cleared_cells=((0, 6),))
    print("pink initially, purple present", len(before), "left pink target reachable", covers(pink, before, (1, 7)))
    print("pink after purple reposition", len(after), "left pink target reachable", covers(pink, after, (1, 7)))
    white = reachable(pink, {purple: (0, 2), pink: (1, 5)}, stage_color="white",
                      cleared_colors=("pink",), cleared_cells=((0, 6),))
    print("pink white stage after left route", len(white), "white entry reachable", covers(pink, white, (0, 7)))


if __name__ == "__main__":
    main()
