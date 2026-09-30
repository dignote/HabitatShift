"""Cut the top row of .docs/sprite block.png into the runtime blocked-cell sprites.

Run: python .docs/ArtSource/cut_board_blocks.py

Nguyen tac:
- Chi doc file sheet, khong sua nguon.
- Tach 3 o hang TREN (da khac hoa, da reu, gach xep) bang gutter alpha;
  hang duoi (chau hoa, thung go, bui thuong xuan) khong dung.
- Crop theo alpha bbox, dua len canvas vuong 512x512, canh dai content = 96% canvas
  (dong bo voi board_tile: content ~95.7% o) => giu khe cream giua cac o.
- Ghi de board_blocker_stone.png (art dome cu) + them board_blocker_moss/brick.png.
"""
from __future__ import annotations

import json
import pathlib

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
SHEET = ROOT / ".docs" / "sprite block.png"
OUT = ROOT / "Assets" / "Resources" / "HabitatShift" / "Board"
REPORT = ROOT / ".docs" / "ArtSource" / "cut_board_blocks_report.txt"
CANVAS = 512
CONTENT = 0.96
ALPHA_MIN = 8
NAMES = ("board_blocker_stone", "board_blocker_moss", "board_blocker_brick")
LABELS = ("da khac hoa (stone)", "da reu (moss)", "gach xep (brick)")


def spans(profile: np.ndarray, threshold: int) -> list[tuple[int, int]]:
    found: list[tuple[int, int]] = []
    start = None
    for index, value in enumerate(profile):
        if value > threshold and start is None:
            start = index
        elif value <= threshold and start is not None:
            found.append((start, index - 1))
            start = None
    if start is not None:
        found.append((start, len(profile) - 1))
    return found


def main() -> None:
    sheet = Image.open(SHEET).convert("RGBA")
    alpha = np.asarray(sheet.getchannel("A"))
    opaque = alpha > ALPHA_MIN
    row_spans = spans(opaque.sum(axis=1), 2)
    column_spans = spans(opaque.sum(axis=0), 2)
    if len(row_spans) < 2 or len(column_spans) < 3:
        raise SystemExit(f"Khong tach duoc luoi 2x3: rows={row_spans} cols={column_spans}")
    top = row_spans[0]
    lines = [
        "cut_board_blocks report",
        f"sheet      : {SHEET.name} {sheet.size[0]}x{sheet.size[1]}",
        f"row spans  : {row_spans}",
        f"col spans  : {column_spans}",
        f"dung hang  : {top} (hang tren), bo hang duoi {row_spans[1]}",
        "",
    ]
    OUT.mkdir(parents=True, exist_ok=True)
    for index, (left, right) in enumerate(column_spans[:3]):
        region = alpha[top[0]:top[1] + 1, left:right + 1]
        rows = np.nonzero(region > ALPHA_MIN)[0]
        cols = np.nonzero(region > ALPHA_MIN)[0]
        y0, y1 = int(rows.min()), int(rows.max())
        x0, x1 = int(cols.min()), int(cols.max())
        content = sheet.crop((left + x0, top[0] + y0, left + x1 + 1, top[0] + y1 + 1))
        longest = max(content.width, content.height)
        target = int(round(CANVAS * CONTENT))
        scale = target / longest
        size = (max(1, int(round(content.width * scale))), max(1, int(round(content.height * scale))))
        resized = content.resize(size, Image.LANCZOS)
        canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
        canvas.alpha_composite(resized, ((CANVAS - size[0]) // 2, (CANVAS - size[1]) // 2))
        output = OUT / (NAMES[index] + ".png")
        canvas.save(output, optimize=True)
        out_alpha = np.asarray(canvas.getchannel("A"))
        filled = (out_alpha > ALPHA_MIN).mean()
        lines += [
            f"[{index}] {LABELS[index]}",
            f"    crop      : x {left + x0}..{left + x1} y {top[0] + y0}..{top[0] + y1} "
            f"({content.width}x{content.height})",
            f"    canvas    : {CANVAS}x{CANVAS} content {size[0]}x{size[1]} "
            f"({max(size) / CANVAS:.3f} canh dai)",
            f"    alpha     : {out_alpha.min()}..{out_alpha.max()} fill={filled:.3f}",
            f"    output    : {output.relative_to(ROOT)} ({output.stat().st_size} bytes)",
        ]
    text = "\n".join(lines) + "\n"
    REPORT.write_text(text, encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
