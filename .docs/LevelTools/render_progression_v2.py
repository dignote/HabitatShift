"""Render compact before/after schematic contact sheets for the v2 review."""
from __future__ import annotations

import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
VERIFY = ROOT / ".docs/Verification/Levels01_18_ProgressionV2"
COLORS = {
    "orange": "#e99553", "green": "#78ab75", "blue": "#659bd2",
    "yellow": "#e6c45c", "pink": "#db8fae", "purple": "#aa91c9",
    "red": "#cf7373", "cyan": "#72bbc4",
}
CELL = 30


def font(size: int):
    path = Path("C:/Windows/Fonts/segoeui.ttf")
    return ImageFont.truetype(str(path), size) if path.is_file() else ImageFont.load_default()


def draw_level(out: Image.Image, level: dict, left: int, top: int, label: str):
    d = ImageDraw.Draw(out)
    d.text((left, top), f"L{level['id']:02} {label}  {level['cols']}x{level['rows']}  H{len(level['habitats'])}",
           fill="#352b23", font=font(17))
    top += 27
    mask = ({(x, y) for y in range(level["rows"]) for x in range(level["cols"])}
            if level.get("playableMask") is None else {tuple(c) for c in level["playableMask"]})
    def rect(x, y, inset=0):
        sx, sy = left+x*CELL, top+(level["rows"]-1-y)*CELL
        return (sx+inset, sy+inset, sx+CELL-1-inset, sy+CELL-1-inset)
    for y in range(level["rows"]):
        for x in range(level["cols"]):
            d.rectangle(rect(x, y), fill="#f4ead1" if (x, y) in mask else "#bdc6ad", outline="#ded4bb")
    for x, y in level["obstacles"]:
        d.rectangle(rect(x, y, 4), fill="#62594f")
    for elevator in level["elevators"]:
        x, y = map(int, elevator["entry"])
        d.rectangle(rect(x, y, 5), fill="#6f5a43")
        d.text((left+x*CELL+8, top+(level["rows"]-1-y)*CELL+5), "E", fill="white", font=font(15))
    for habitat in level["habitats"]:
        ax, ay = habitat["anchor"]
        for dx, dy in habitat["shape"]:
            d.rounded_rectangle(rect(ax+dx, ay+dy, 2), radius=5,
                                fill=COLORS[habitat["color"]], outline="#473d34", width=2)
        if habitat["movementConstraint"] != "FREE":
            mark = "↔" if habitat["movementConstraint"] == "HORIZONTAL_ONLY" else "↕"
            d.text((left+ax*CELL+5, top+(level["rows"]-1-ay)*CELL+4), mark,
                   fill="#202020", font=font(17))
    for target in level["targets"]:
        x, y = map(int, target["position"])
        cx, cy = left+x*CELL+CELL//2, top+(level["rows"]-1-y)*CELL+CELL//2
        d.ellipse((cx-6, cy-6, cx+6, cy+6), fill=COLORS[target["color"]], outline="white", width=2)


def main():
    before = json.loads((VERIFY / "production_before.json").read_text(encoding="utf-8"))["levels"]
    after = json.loads((VERIFY / "catalog_candidate.json").read_text(encoding="utf-8"))["levels"]
    groups = {"A_L1-L4": range(1, 5), "B_L5-L10": range(5, 11),
              "C_L15-L16": range(15, 17), "D_L18": [18]}
    for name, ids in groups.items():
        ids = list(ids)
        out = Image.new("RGB", (690, len(ids)*290+42), "#e7e1d1")
        d = ImageDraw.Draw(out)
        d.text((18, 8), "Habitat Shift — progression v2 | before / after", fill="#352b23", font=font(22))
        for i, level_id in enumerate(ids):
            top = 45+i*290
            draw_level(out, before[level_id-1], 20, top, "BEFORE")
            draw_level(out, after[level_id-1], 355, top, "AFTER")
        path = VERIFY / f"contact_{name}.png"
        out.save(path)
        print(path)


if __name__ == "__main__":
    main()
