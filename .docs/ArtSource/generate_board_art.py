"""Bake Habitat Shift board sprites. Run with: python .docs/ArtSource/generate_board_art.py"""
from __future__ import annotations

import argparse
import itertools
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Resources" / "HabitatShift" / "Board"
CATALOG = ROOT / "Assets" / "StreamingAssets" / "HabitatShift" / "approved_levels_v4.json"
SOURCE = Path(__file__).resolve().parent / "habitat_tray_material_source_v1.png"
OUT.mkdir(parents=True, exist_ok=True)

FAMILIES = {
    "orange": ("coral", (194, 94, 78)),
    "blue": ("cobalt", (75, 115, 181)),
    "green": ("sage", (101, 146, 93)),
    "yellow": ("saffron", (190, 144, 51)),
    "purple": ("lavender", (143, 112, 170)),
    "cyan": ("teal", (64, 150, 151)),
    "pink": ("peach", (202, 130, 131)),
    "red": ("rose", (177, 76, 98)),
    "white": ("ivory", (172, 168, 158)),
}

SKIP_EXISTING = False


def save(im: Image.Image, name: str) -> None:
    path = OUT / (name + ".png")
    if SKIP_EXISTING and path.exists():
        return
    im.save(path, optimize=True)


def rgba_panel(name: str, base: tuple[int, int, int], border: tuple[int, int, int], radius: int,
               shadow: bool = True) -> None:
    n = 256
    im = Image.new("RGBA", (n, n))
    if shadow:
        sh = Image.new("RGBA", (n, n))
        d = ImageDraw.Draw(sh)
        d.rounded_rectangle((16, 23, 240, 245), radius=radius, fill=(76, 58, 36, 100))
        im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(7)))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((12, 10, 244, 238), radius=radius, fill=border)
    d.rounded_rectangle((18, 15, 238, 230), radius=max(1, radius - 5), fill=base)
    d.rounded_rectangle((22, 19, 234, 225), radius=max(1, radius - 9), outline=(255, 255, 243, 125), width=3)
    rng = np.random.default_rng(1729)
    for x, y in rng.integers(27, 226, size=(90, 2)):
        d.point((int(x), int(y)), fill=(120, 94, 59, 28))
    save(im, name)


def stone() -> None:
    im = Image.new("RGBA", (256, 256))
    d = ImageDraw.Draw(im)
    d.ellipse((32, 66, 232, 236), fill=(55, 54, 48, 55))
    d.rounded_rectangle((30, 28, 225, 214), radius=55, fill=(115, 116, 99))
    d.rounded_rectangle((39, 33, 217, 196), radius=51, fill=(164, 164, 139))
    d.ellipse((68, 48, 188, 103), fill=(204, 201, 173, 84))
    for x, y, r in ((63, 154, 8), (184, 142, 6), (151, 87, 4), (95, 179, 3)):
        d.ellipse((x-r, y-r, x+r, y+r), fill=(105, 110, 91, 72))
    d.arc((43, 41, 215, 196), 193, 305, fill=(225, 221, 188, 165), width=4)
    save(im, "board_blocker_stone")


def elevator(direction: str) -> None:
    im = Image.new("RGBA", (256, 256))
    d = ImageDraw.Draw(im)
    d.ellipse((24, 34, 232, 240), fill=(52, 49, 38, 56))
    d.rounded_rectangle((23, 20, 233, 225), radius=42, fill=(85, 111, 85))
    d.rounded_rectangle((32, 29, 224, 214), radius=36, fill=(164, 164, 124))
    d.rounded_rectangle((47, 43, 209, 198), radius=28, fill=(75, 99, 85))
    d.rounded_rectangle((55, 51, 201, 190), radius=24, fill=(111, 134, 116))
    d.arc((36, 30, 220, 207), 190, 312, fill=(234, 226, 179, 205), width=6)
    d.polygon(((128, 68), (166, 124), (141, 124), (141, 165), (115, 165), (115, 124), (90, 124)),
              fill=(246, 230, 174))
    d.line(((113, 146), (113, 174), (143, 174), (143, 146)), fill=(72, 91, 73), width=4)
    if direction == "right": im = im.rotate(-90)
    elif direction == "down": im = im.rotate(180)
    elif direction == "left": im = im.rotate(90)
    save(im, "board_elevator_" + direction)


def preview(name: str, rgb: tuple[int, int, int]) -> None:
    im = Image.new("RGBA", (256, 256))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((9, 9, 246, 246), radius=31, fill=(*rgb, 32), outline=(*rgb, 235), width=13)
    d.rounded_rectangle((26, 26, 229, 229), radius=20, outline=(255, 251, 229, 205), width=4)
    save(im, name)


def intake() -> None:
    im = Image.new("RGBA", (256, 256))
    d = ImageDraw.Draw(im)
    d.ellipse((23, 23, 233, 233), fill=(67, 92, 73, 82))
    d.ellipse((37, 32, 219, 214), fill=(247, 236, 198), outline=(115, 132, 96), width=10)
    d.polygon(((128, 67), (186, 137), (146, 130), (146, 179), (110, 179), (110, 130), (70, 137)),
              fill=(92, 143, 98))
    d.line(((126, 87), (126, 170)), fill=(240, 233, 188), width=8)
    save(im, "board_intake")


def normalized(cells: list[tuple[int, int]]) -> list[tuple[int, int]]:
    x0 = min(x for x, _ in cells)
    y0 = min(y for _, y in cells)
    return sorted((x-x0, y-y0) for x, y in cells)


def key(cells: list[tuple[int, int]]) -> str:
    return "-".join(f"{x}_{y}" for x, y in normalized(cells))


def connected(cells: list[tuple[int, int]]) -> bool:
    todo = [cells[0]]
    seen = {cells[0]}
    cellset = set(cells)
    while todo:
        x, y = todo.pop()
        for other in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            if other in cellset and other not in seen:
                seen.add(other)
                todo.append(other)
    return len(seen) == len(cells)


def tray(cells: list[tuple[int, int]], family: str, pigment: tuple[int, int, int]) -> None:
    cells = normalized(cells)
    cols = max(x for x, _ in cells) + 1
    rows = max(y for _, y in cells) + 1
    unit, pad = 192, 28
    width, height = cols*unit+2*pad, rows*unit+2*pad
    mask = np.zeros((height, width), np.uint8)
    for x, y in cells:
        cv2.rectangle(mask, (pad+x*unit, pad+y*unit),
                      (pad+(x+1)*unit-1, pad+(y+1)*unit-1), 255, -1)
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (25, 25))
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
    dist = cv2.distanceTransform(mask, cv2.DIST_L2, 5)
    yy, xx = np.indices(mask.shape)
    grain = np.random.default_rng(7601).normal(0, 2.2, mask.shape)
    grain += 1.7*np.sin(xx*.19)*np.sin(yy*.13)
    # Transfer the fine ceramic/leaf grain from the original material study;
    # geometry and palette are baked independently for every footprint.
    source = np.asarray(Image.open(SOURCE).convert("RGB"), dtype=np.float32)
    rim_sample = source[93:200, 290:990].mean(axis=2)
    basin_sample = source[310:900, 290:990].mean(axis=2)
    rim_sample = cv2.resize(rim_sample, (width, 107), interpolation=cv2.INTER_LINEAR)
    rim_sample = np.tile(rim_sample, (height//107+1, 1))[:height]
    basin_sample = cv2.resize(basin_sample, (width, height), interpolation=cv2.INTER_LINEAR)
    grain += (rim_sample-rim_sample.mean())*.13*(dist < 29)
    grain += (basin_sample-basin_sample.mean())*.14*(dist >= 29)
    rim = np.clip(dist/28, 0, 1)
    basin = np.clip((dist-22)/8, 0, 1)
    light = np.clip((height-yy)/height*.6 + (width-xx)/width*.35, 0, 1)
    base = np.array(pigment, np.float32)
    dark = base*.63
    bright = base*.74 + np.array((77, 74, 61), np.float32)
    rim_rgb = dark[None, None, :]*(1-rim[:, :, None]) + bright[None, None, :]*rim[:, :, None]
    rim_rgb += (light[:, :, None]-.4)*19
    cream = np.array((244, 230, 201), np.float32)
    cream_rgb = cream[None, None, :] + (light[:, :, None]-.5)*12
    rgb = rim_rgb*(1-basin[:, :, None]) + cream_rgb*basin[:, :, None]
    rgb += grain[:, :, None]
    ridge = (dist > 20) & (dist < 24)
    rgb[ridge] *= .77
    rgb[(dist > 25) & (dist < 29)] += 14
    alpha = cv2.GaussianBlur(mask, (0, 0), 1.0)
    pixels = np.dstack((np.clip(rgb, 0, 255).astype(np.uint8), alpha))
    body = Image.fromarray(pixels, "RGBA")
    shadow_mask = Image.fromarray(mask, "L").filter(ImageFilter.GaussianBlur(8))
    shadow = Image.new("RGBA", (width, height), (66, 50, 37, 0))
    shadow.putalpha(shadow_mask.point(lambda a: int(a*.24)))
    shadow = shadow.transform((width, height), Image.Transform.AFFINE, (1, 0, -3, 0, 1, -7))
    result = Image.new("RGBA", (width, height))
    result.alpha_composite(shadow)
    result.alpha_composite(body)
    # Tiny botanical enamel detail in the topmost exposed segment.
    top = min(cells, key=lambda q: (q[1], q[0]))
    cx = pad + top[0]*unit + unit//2
    cy = pad + top[1]*unit + 12
    d = ImageDraw.Draw(result)
    leaf = tuple(int(v*.55+95) for v in pigment) + (235,)
    for dx, dy, a in ((-12, 1, -25), (0, -3, 0), (12, 1, 25)):
        d.ellipse((cx+dx-6, cy+dy-8, cx+dx+6, cy+dy+6), fill=leaf)
    save(result, f"tray_{family}_{key(cells)}")


def main() -> None:
    global SKIP_EXISTING
    parser = argparse.ArgumentParser(description="Bake Habitat Shift board sprites.")
    parser.add_argument("--missing-only", action="store_true",
                        help="Chi ghi sprite chua ton tai, khong dung file cu.")
    args, _ = parser.parse_known_args()
    SKIP_EXISTING = args.missing_only
    rgba_panel("board_card", (239, 226, 195), (136, 120, 88), 25)
    rgba_panel("board_tile", (249, 241, 221), (176, 153, 113), 22)
    stone()
    for direction in ("up", "right", "down", "left"):
        elevator(direction)
    preview("board_preview_valid", (98, 155, 96))
    preview("board_preview_invalid", (197, 89, 92))
    preview("board_preview_selected", (216, 174, 84))
    intake()
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    shapes: dict[str, set[str]] = {color: set() for color in FAMILIES}
    shape_cells: dict[str, list[tuple[int, int]]] = {}
    for level in catalog["levels"]:
        for habitat in level["habitats"]:
            raw = [tuple(map(int, cell)) for cell in habitat["shape"]]
            for size in range(1, len(raw)+1):
                for subset in itertools.combinations(raw, size):
                    if connected(list(subset)):
                        k = key(list(subset))
                        shapes[habitat["color"]].add(k)
                        shape_cells[k] = normalized(list(subset))
    for color, (family, pigment) in FAMILIES.items():
        for shape in sorted(shapes[color]):
            tray(shape_cells[shape], family, pigment)
    print(f"Wrote {len(list(OUT.glob('*.png')))} board sprites to {OUT}")


if __name__ == "__main__":
    main()
