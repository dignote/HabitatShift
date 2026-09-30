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
    "brown": ("walnut", (128, 88, 54)),
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


def tray(cells: list[tuple[int, int]], family: str, pigment: tuple[int, int, int], name: str | None = None, with_leaf: bool = True, basin_rgb: tuple[int, int, int] = (244, 230, 201)) -> None:
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
    cream = np.array(basin_rgb, np.float32)
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
    if with_leaf:
        top = min(cells, key=lambda q: (q[1], q[0]))
        cx = pad + top[0]*unit + unit//2
        cy = pad + top[1]*unit + 12
        d = ImageDraw.Draw(result)
        leaf = tuple(int(v*.55+95) for v in pigment) + (235,)
        for dx, dy, a in ((-12, 1, -25), (0, -3, 0), (12, 1, 25)):
            d.ellipse((cx+dx-6, cy+dy-8, cx+dx+6, cy+dy+6), fill=leaf)
    save(result, name or f"tray_{family}_{key(cells)}")


BOARD_UNIT, BOARD_PAD = 192, 28
# Vai ceramic lo ra ngoai silhouette (px) + do day dai vien tinh tu MEP SLAB vao trong.
BOARD_SHOULDER, BOARD_FRAME_BAND = 24, 10


def board_solid(cells: list[tuple[int, int]], cols: int, rows: int) -> np.ndarray:
    """Raster hoa playableMask theo luoi 192px + lam muot goc (giong board cu)."""
    mask = np.zeros((rows*BOARD_UNIT + 2*BOARD_PAD, cols*BOARD_UNIT + 2*BOARD_PAD), np.uint8)
    for x, y in cells:
        cv2.rectangle(mask, (BOARD_PAD + x*BOARD_UNIT, BOARD_PAD + y*BOARD_UNIT),
                      (BOARD_PAD + (x+1)*BOARD_UNIT - 1, BOARD_PAD + (y+1)*BOARD_UNIT - 1), 255, -1)
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (25, 25))
    return cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)


def board_slab(solid: np.ndarray, shoulder: int = BOARD_SHOULDER) -> np.ndarray:
    """Slab = playable + vai lo ra ngoai <shoulder> px, goc tron deu (distance transform)."""
    outside = cv2.distanceTransform(255 - solid, cv2.DIST_L2, 5)
    return np.where((solid > 0) | (outside <= shoulder), 255, 0).astype(np.uint8)


def board_mask(cells: list[tuple[int, int]], cols: int, rows: int, name: str,
               shoulder: int = BOARD_SHOULDER,
               rim: tuple[int, int, int] = (136, 120, 88),
               basin: tuple[int, int, int] = (239, 226, 195)) -> None:
    """Bake board base = slab ceramic co vai lo ra ngoai playableMask.

    - long o playable + vai: ceramic (rim gradient + grain), ranh gioi playable giu nguyen pixel;
    - vai an vao o inactive toi da <shoulder> px tu mep, long o inactive van trong suot;
    - vien do board_frame ve rieng, nam tren MEP SLAB (khong con dinh vao o playable).
    Toa do catalog la tuyet doi (Y huong xuong), khong chuan hoa lai.
    """
    slab = board_slab(board_solid(cells, cols, rows), shoulder)
    width, height = slab.shape[1], slab.shape[0]
    dist = cv2.distanceTransform(slab, cv2.DIST_L2, 5)
    yy, xx = np.indices(slab.shape)
    grain = np.random.default_rng(7601).normal(0, 2.2, slab.shape)
    grain += 1.7*np.sin(xx*.19)*np.sin(yy*.13)
    source = np.asarray(Image.open(SOURCE).convert("RGB"), dtype=np.float32)
    rim_sample = source[93:200, 290:990].mean(axis=2)
    basin_sample = source[310:900, 290:990].mean(axis=2)
    rim_sample = cv2.resize(rim_sample, (width, 107), interpolation=cv2.INTER_LINEAR)
    rim_sample = np.tile(rim_sample, (height//107+1, 1))[:height]
    basin_sample = cv2.resize(basin_sample, (width, height), interpolation=cv2.INTER_LINEAR)
    grain += (rim_sample-rim_sample.mean())*.13*(dist < shoulder+29)
    grain += (basin_sample-basin_sample.mean())*.14*(dist >= shoulder+29)
    rimt = np.clip(dist/max(1.0, float(shoulder)), 0, 1)      # 0 o mep slab -> 1 tai ranh gioi playable
    basint = np.clip((dist-shoulder)/8, 0, 1)                 # cream dung tu ranh gioi playable tro vao
    light = np.clip((height-yy)/height*.6 + (width-xx)/width*.35, 0, 1)
    base = np.array(rim, np.float32)
    dark = base*.63
    bright = base*.74 + np.array((77, 74, 61), np.float32)
    rim_rgb = dark[None, None, :]*(1-rimt[:, :, None]) + bright[None, None, :]*rimt[:, :, None]
    rim_rgb += (light[:, :, None]-.4)*19
    cream = np.array(basin, np.float32)
    cream_rgb = cream[None, None, :] + (light[:, :, None]-.5)*12
    rgb = rim_rgb*(1-basint[:, :, None]) + cream_rgb*basint[:, :, None]
    rgb += grain[:, :, None]
    alpha = cv2.GaussianBlur(slab, (0, 0), 1.0)
    pixels = np.dstack((np.clip(rgb, 0, 255).astype(np.uint8), alpha))
    body = Image.fromarray(pixels, "RGBA")
    shadow_mask = Image.fromarray(slab, "L").filter(ImageFilter.GaussianBlur(8))
    shadow = Image.new("RGBA", (width, height), (66, 50, 37, 0))
    shadow.putalpha(shadow_mask.point(lambda a: int(a*.24)))
    shadow = shadow.transform((width, height), Image.Transform.AFFINE, (1, 0, -3, 0, 1, -7))
    result = Image.new("RGBA", (width, height))
    result.alpha_composite(shadow)
    result.alpha_composite(body)
    save(result, name)


def board_frame(cells: list[tuple[int, int]], cols: int, rows: int, name: str,
                shoulder: int = BOARD_SHOULDER, band: int = BOARD_FRAME_BAND) -> None:
    """Bake vien ceramic bao quanh MEP NGOAI cua slab (khong con nam tren o playable).

    Vien la dai <band> px tinh tu mep slab vao trong, nen giua vien va o playable
    con lai (shoulder - band) px ranh gioi ceramic -> co khoang tho cho board.
    Vien di theo moi goc loi/khuyet, khong sinh vien noi giua hai o playable ke nhau.
    """
    slab = board_slab(board_solid(cells, cols, rows), shoulder)
    dist = cv2.distanceTransform(slab, cv2.DIST_L2, 5)
    yy, xx = np.indices(slab.shape)
    # Mat cat vien: canh ngoai toi -> diem sang phia trong -> tan dan vao vai.
    stops = np.array([0.0, 1.5, 4.0, 6.5, float(band)], np.float32)
    colors = np.array([(58, 46, 28), (58, 46, 28), (219, 205, 173),
                       (196, 181, 148), (150, 132, 99)], np.float32)
    alphas = np.array([.95, 1.0, 1.0, 1.0, 0.0], np.float32)
    d = np.clip(dist, 0.0, float(band))
    rgb = np.stack([np.interp(d, stops, colors[:, c]) for c in range(3)], axis=-1)
    alpha = np.interp(d, stops, alphas)
    grain = np.random.default_rng(4477).normal(0, 1.6, slab.shape)
    grain += 1.3*np.sin(xx*.21)*np.sin(yy*.15)
    rgb += grain[:, :, None]
    alpha = np.where((slab > 0) & (dist <= band), alpha, 0.0)
    pixels = np.dstack((np.clip(rgb, 0, 255).astype(np.uint8),
                        np.clip(alpha*255, 0, 255).astype(np.uint8)))
    save(Image.fromarray(pixels, "RGBA"), name)


def main() -> None:
    global SKIP_EXISTING
    parser = argparse.ArgumentParser(description="Bake Habitat Shift board sprites.")
    parser.add_argument("--missing-only", action="store_true",
                        help="Chi ghi sprite chua ton tai, khong dung file cu.")
    parser.add_argument("--boards-only", action="store_true",
                        help="Chi sinh lai board_mask/board_frame cua cac man bat quy tac.")
    args, _ = parser.parse_known_args()
    SKIP_EXISTING = args.missing_only
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    if not args.boards_only:
        rgba_panel("board_card", (239, 226, 195), (136, 120, 88), 25)
        rgba_panel("board_tile", (249, 241, 221), (176, 153, 113), 22)
        # board_blocker_stone/moss/brick duoc cat tu .docs/sprite block.png
        # bang .docs/ArtSource/cut_board_blocks.py (khong con bake bang code).
        for direction in ("up", "right", "down", "left"):
            elevator(direction)
        preview("board_preview_valid", (98, 155, 96))
        preview("board_preview_invalid", (197, 89, 92))
        preview("board_preview_selected", (216, 174, 84))
        intake()
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
    for level in catalog["levels"]:
        cols, rows = level["cols"], level["rows"]
        mask = level.get("playableMask")
        if not mask or len(mask) == cols * rows:
            continue
        cells = [tuple(map(int, cell)) for cell in mask]
        board_mask(cells, cols, rows, f"board_mask_L{level['id']}")
        board_frame(cells, cols, rows, f"board_frame_L{level['id']}")
    print(f"Wrote {len(list(OUT.glob('*.png')))} board sprites to {OUT}")


if __name__ == "__main__":
    main()
