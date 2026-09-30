"""Read-only dump of candidate levels 24-30 from the R9.2.4 editor project."""
from __future__ import annotations

import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
PROJECT = (ROOT / ".docs" / "HabitatShift_CurrentGame_UnityLevels_v1" / "data"
           / "Habitat_Shift_R9_2_4_level_editor_project.json")


def main() -> None:
    project = json.loads(PROJECT.read_text(encoding="utf-8"))
    print("architecture:", project["architecture"])
    for level in project["candidateLevels"]:
        if not 24 <= level["id"] <= 30:
            continue
        print(f"\n=== L{level['id']} {level.get('title')} {level['cols']}x{level['rows']} ===")
        for key in level:
            if key in ("habitats", "targets", "ports", "mask", "blocked", "changeLog"):
                continue
            print(f"  {key}: {json.dumps(level[key], ensure_ascii=False)[:160]}")
        mask = level.get("mask") or []
        blocked = level.get("blocked") or []
        full = level["cols"] * level["rows"]
        print(f"  mask cells={len(mask)} (full={full}) irregular={len(mask) != full}")
        print(f"  blocked={json.dumps(blocked)}")
        print(f"  habitats={len(level['habitats'])} targets={len(level['targets'])} ports={len(level.get('ports') or [])}")
        for habitat in level["habitats"]:
            print("   H", json.dumps({k: habitat[k] for k in habitat if k in
                                      ("id", "color", "anchor", "shape", "need", "constraint")}, ensure_ascii=False))
        for target in level["targets"]:
            print("   T", json.dumps({k: target[k] for k in target if k in ("id", "color", "position")}, ensure_ascii=False))
        for port in level.get("ports") or []:
            print("   P", json.dumps({k: port[k] for k in port if k in
                                      ("id", "edge", "entryCell", "queue", "unknown", "releaseRule", "blockedBehavior")},
                                     ensure_ascii=False))
        colors = {}
        for habitat in level["habitats"]:
            colors[habitat["color"]] = colors.get(habitat["color"], 0) + habitat["need"]
        print("   need by color:", json.dumps(colors))


if __name__ == "__main__":
    main()
