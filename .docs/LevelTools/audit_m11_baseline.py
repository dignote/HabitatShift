"""Read-only L1-L30 composition and byte/semantic preservation audit for M11."""
from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def raw_levels(source: str):
    decoder = json.JSONDecoder()
    start = source.index('"levels"')
    cursor = source.index("[", start) + 1
    while True:
        while source[cursor].isspace() or source[cursor] == ",":
            cursor += 1
        if source[cursor] == "]":
            return
        _, end = decoder.raw_decode(source, cursor)
        yield source[cursor:end]
        cursor = end


def audit(path: Path) -> dict:
    data = path.read_bytes()
    source = data.decode("utf-8")
    catalog = json.loads(source)
    spans = list(raw_levels(source))
    if len(spans) != 30 or len(catalog["levels"]) != 30:
        raise ValueError("M11 production baseline must have 30 levels")
    rows = []
    for level, raw in zip(catalog["levels"], spans):
        colors = Counter(h["color"] for h in level["habitats"])
        needs = Counter()
        supply = Counter(t["color"] for t in level["targets"])
        for habitat in level["habitats"]:
            needs[habitat["color"]] += habitat["need"]
        for elevator in level["elevators"]:
            supply.update(q["color"] for q in elevator["queue"][elevator.get("nextIndex", 0):])
        mask_cells = len(level.get("playableMask") or []) or level["cols"] * level["rows"]
        footprint = sum(len(h["shape"]) for h in level["habitats"])
        density = (footprint + len(level["targets"]) + len(level["obstacles"])) / mask_cells
        rows.append({
            "id": level["id"], "title": level["title"], "rawSha256": sha(raw.encode()),
            "semanticSha256": sha(json.dumps(level, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()),
            "cols": level["cols"], "rows": level["rows"], "playableCells": mask_cells,
            "habitats": len(level["habitats"]), "habitatFootprintCells": footprint,
            "colors": len(colors), "duplicateColorHabitats": sum(n - 1 for n in colors.values() if n > 1),
            "initialSproutlings": len(level["targets"]), "elevators": len(level["elevators"]),
            "queuedSproutlings": sum(len(e["queue"]) - e.get("nextIndex", 0) for e in level["elevators"]),
            "blockers": len(level["obstacles"]), "density": round(density, 6),
            "supplyBalanced": needs == supply,
        })
    return {
        "auditVersion": 1, "source": str(path), "catalogSha256": sha(data),
        "catalogFingerprint": catalog["canonicalLevelsFingerprint"], "levels": rows,
        "chapters": {
            "L1-L18": {"meanDensity": round(sum(x["density"] for x in rows[:18]) / 18, 6)},
            "L19-L30": {"meanDensity": round(sum(x["density"] for x in rows[18:]) / 12, 6),
                        "meanHabitats": round(sum(x["habitats"] for x in rows[18:]) / 12, 3)},
        },
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("catalog", nargs="?", type=Path,
                        default=ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    result = audit(args.catalog)
    content = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(content, encoding="utf-8")
    else:
        print(content)
