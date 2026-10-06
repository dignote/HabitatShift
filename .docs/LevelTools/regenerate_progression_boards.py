"""Regenerate only board masks/frames whose playableMask changed in v2."""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
VERIFY = ROOT / ".docs/Verification/Levels01_18_ProgressionV2"
BOARD = ROOT / "Assets/Resources/HabitatShift/Board"
GENERATOR = ROOT / ".docs/ArtSource/generate_board_art.py"


def main():
    before = json.loads((VERIFY / "production_before.json").read_text(encoding="utf-8"))["levels"]
    after = json.loads((ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json").read_text(encoding="utf-8"))["levels"]
    spec = importlib.util.spec_from_file_location("generate_board_art", GENERATOR)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    for old, new in zip(before, after):
        if old.get("playableMask") == new.get("playableMask"):
            continue
        if not new.get("playableMask") or len(new["playableMask"]) == new["cols"] * new["rows"]:
            raise ValueError(f"L{new['id']} changed to rectangle; review mask/frame removal manually")
        cells = [tuple(cell) for cell in new["playableMask"]]
        for kind, render in (("mask", module.board_mask), ("frame", module.board_frame)):
            name = f"board_{kind}_L{new['id']}"
            render(cells, new["cols"], new["rows"], name)
            meta = BOARD / f"{name}.png.meta"
            contents = meta.read_text(encoding="utf-8")
            width = new["cols"] * 192 + 56
            height = new["rows"] * 192 + 56
            import re
            contents, count = re.subn(r"(      rect:\s+serializedVersion: 2\s+x: 0\s+y: 0\s+width: )\d+(\s+height: )\d+",
                                      lambda m: f"{m.group(1)}{width}{m.group(2)}{height}", contents)
            if count != 1:
                raise ValueError(f"{meta}: expected exactly one sprite rect")
            meta.write_text(contents, encoding="utf-8", newline="\n")
        print(f"L{new['id']:02} {new['cols']}x{new['rows']}: mask/frame regenerated")


if __name__ == "__main__":
    main()
