"""Validate board_mask_* silhouettes against the production playableMask.

Run: python .docs/ArtSource/validate_board_masks.py

Kiem tra:
1. moi level co playableMask bat quy tac deu co board_mask_L<id>.png;
2. level full rectangle KHONG duoc co mask/frame asset;
3. kich thuoc = (cols*192+56) x (rows*192+56);
4. tam o playable -> alpha >= 250;
5. tam o inactive -> alpha <= 4;
6. PNG co alpha;
7/8/9. ban do alpha theo o (co doi chieu Y) khop tuyet doi voi playableMask;
10. bao cao hash cac asset cu de doi chieu (--snapshot).

Frame (board_frame_L<id>.png) - HOP DONG MOI (co vai ceramic lo ra ngoai):
11. ton tai, cung kich thuoc voi board_mask;
12. vien nam NGOAI o playable (khong con dinh vao mep o), chua lai khoang tho;
13. long o inactive van trong suot; o inactive khong bi lap qua nua dien tich;
14. topology alpha theo o cua playableMask khong doi sau khi chong frame;
15. vien co canh ngoai toi + diem sang phia trong;
16. phan lo ra ngoai silhouette phai CO that va bi chan trong [MIN_OUT, MAX_OUT];
17. dai ceramic giua vien va o playable phai sang hon mep vien (khoang tho nhin ro).

Quy uoc toa do: catalog y tang XUONG, PIL cung y tang xuong, nen pixel_row = pad + board_y*192
(khong lat). Validator tu kiem chung dieu nay bang cach so ban do alpha voi mask that.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import sys

import numpy as np
import cv2
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
CATALOG = ROOT / "Assets" / "StreamingAssets" / "HabitatShift" / "approved_levels_v4.json"
BOARD = ROOT / "Assets" / "Resources" / "HabitatShift" / "Board"
UNIT, PAD = 192, 28
SHOULDER, FRAME_BAND = 24, 10     # phai khop generate_board_art.py
MIN_OUT, MAX_OUT = 14, 26         # bien do lo ra ngoai cho phep (px)


def main() -> None:
    parser = argparse.ArgumentParser(description="Validate generated board masks.")
    parser.add_argument("--snapshot", default=None, help="File hash snapshot truoc khi sinh (name  sha256).")
    args = parser.parse_args()

    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    problems: list[str] = []
    checked = 0
    for level in catalog["levels"]:
        level_id, cols, rows = level["id"], level["cols"], level["rows"]
        mask = level.get("playableMask")
        irregular = bool(mask) and len(mask) < cols * rows
        path = BOARD / f"board_mask_L{level_id}.png"
        if not irregular:
            if path.is_file():
                problems.append(f"L{level_id}: full rectangle khong duoc co mask asset")
            if (BOARD / f"board_frame_L{level_id}.png").is_file():
                problems.append(f"L{level_id}: full rectangle khong duoc co frame asset")
            continue
        checked += 1
        if not path.is_file():
            problems.append(f"L{level_id}: thieu {path.name}")
            continue
        frame_path = BOARD / f"board_frame_L{level_id}.png"
        if not frame_path.is_file():
            problems.append(f"L{level_id}: thieu {frame_path.name}")
            continue
        playable = {(c[0], c[1]) for c in mask}
        with Image.open(path) as image:
            if image.mode != "RGBA":
                problems.append(f"L{level_id}: {path.name} khong co alpha (mode {image.mode})")
                continue
            expected = (cols * UNIT + 2 * PAD, rows * UNIT + 2 * PAD)
            if image.size != expected:
                problems.append(f"L{level_id}: kich thuoc {image.size} != {expected}")
            alpha = image.getchannel("A")
            rows_map, png_map = [], []
            for y in range(rows):
                line = []
                for x in range(cols):
                    value = alpha.getpixel((PAD + x * UNIT + UNIT // 2, PAD + y * UNIT + UNIT // 2))
                    cell = "P" if (x, y) in playable else "."
                    png_map.append(cell if value >= 250 else ("_" if value > 4 else "o"))
                    line.append(cell if value >= 250 else ("_" if value > 4 else "o"))
                rows_map.append("".join(line))
            # 4/5/7/8/9: alpha theo o phai khop mask, o inactive phai trong suot
            for index, symbol in enumerate(png_map):
                x, y = index % cols, index // cols
                if symbol == "P" and (x, y) not in playable:
                    problems.append(f"L{level_id}: o ({x},{y}) inactive nhung lai opaque")
                if symbol != "P" and (x, y) in playable:
                    problems.append(f"L{level_id}: o ({x},{y}) playable nhung alpha khong du (ky hieu {symbol})")
            # 6: alpha phai co ca trong suot lan duc
            low, high = alpha.getextrema()
            if low != 0 or high < 200:
                problems.append(f"L{level_id}: alpha extrema {low}..{high} khong hop le")
            expected_map = ["".join("P" if (x, y) in playable else "o" for x in range(cols)) for y in range(rows)]
            print(f"L{level_id} ({cols}x{rows}) playableMap | alphaMap")
            for expected_line, actual_line in zip(expected_map, rows_map):
                flag = "" if expected_line == actual_line else "   <-- KHAC"
                print(f"   {expected_line} | {actual_line}{flag}")

            with Image.open(frame_path) as frame_image:
                if frame_image.mode != "RGBA":
                    problems.append(f"L{level_id}: {frame_path.name} khong co alpha (mode {frame_image.mode})")
                    continue
                if frame_image.size != image.size:
                    problems.append(f"L{level_id}: frame {frame_image.size} != mask {image.size}")
                frame = frame_image.convert("RGBA")
                frame_alpha = np.asarray(frame.getchannel("A"))

                cell = np.zeros(frame_alpha.shape, np.uint8)
                for x, y in playable:
                    cell[PAD + y * UNIT:PAD + (y + 1) * UNIT, PAD + x * UNIT:PAD + (x + 1) * UNIT] = 255

                visible = int((frame_alpha >= 32).sum())
                if visible < 1000:
                    problems.append(f"L{level_id}: frame khong co vien ro (chi {visible} px alpha>=32)")

                # Do luong phai dung CUNG hinh hoc voi generator: raster + MORPH_OPEN.
                solid = cv2.morphologyEx(cell, cv2.MORPH_OPEN,
                                         cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (25, 25)))
                # Khoang cach tu moi pixel ra ngoai silhouette playable (0 = con nam trong silhouette).
                dist_out = cv2.distanceTransform(255 - solid, cv2.DIST_L2, 5)
                outside = solid == 0

                # 12: vien khong duoc dinh vao o playable.
                near_cells = outside & (dist_out <= SHOULDER - FRAME_BAND - 1)
                if near_cells.any() and int(frame_alpha[near_cells].max()) >= 32:
                    problems.append(f"L{level_id}: vien con dinh vao o playable "
                                    f"(alpha {int(frame_alpha[near_cells].max())})")

                composite = Image.alpha_composite(image.convert("RGBA"), frame)
                comp_alpha = np.asarray(composite.getchannel("A"))
                comp_lum = np.asarray(composite.convert("RGB"), dtype=np.float32).mean(axis=2)

                # 16: phan lo ra ngoai phai co that va bi chan.
                solid_out = dist_out[comp_alpha >= 200]
                if solid_out.size == 0:
                    problems.append(f"L{level_id}: khong co vai/khung ceramic lo ra ngoai o playable")
                else:
                    reach = float(solid_out.max())
                    if reach < MIN_OUT:
                        problems.append(f"L{level_id}: vai ceramic qua mong ({reach:.1f}px < {MIN_OUT})")
                    if reach > MAX_OUT:
                        problems.append(f"L{level_id}: lo ra ngoai qua nhieu ({reach:.1f}px > {MAX_OUT})")

                # 15: canh ngoai toi + diem sang phia trong cua dai vien.
                edge = outside & (frame_alpha >= 200) & (dist_out >= SHOULDER - 2)
                gloss = outside & (frame_alpha >= 200) & (dist_out >= SHOULDER - 6.5) & (dist_out <= SHOULDER - 3.5)
                if edge.sum() < 200 or gloss.sum() < 200:
                    problems.append(f"L{level_id}: khong do duoc canh vien "
                                    f"(edge {int(edge.sum())}, gloss {int(gloss.sum())})")
                elif comp_lum[edge].mean() >= comp_lum[gloss].mean():
                    problems.append(f"L{level_id}: vien thieu canh ngoai toi/diem sang")

                # 17: dai ceramic (khoang tho) giua vien va o playable phai sang hon mep vien.
                gutter = outside & (comp_alpha >= 200) & (dist_out >= 2) & (dist_out <= SHOULDER - FRAME_BAND - 1)
                rim = outside & (comp_alpha >= 200) & (dist_out >= SHOULDER - 2)
                if gutter.sum() < 200 or rim.sum() < 200:
                    problems.append(f"L{level_id}: khong do duoc khoang tho giua vien va o playable")
                elif comp_lum[gutter].mean() <= comp_lum[rim].mean() + 25:
                    problems.append(f"L{level_id}: khoang tho chua sang hon vien "
                                    f"(gutter {comp_lum[gutter].mean():.0f} <= rim {comp_lum[rim].mean():.0f})")

                # 13: long o inactive van thoang, khong bi lap qua nua dien tich.
                for y in range(rows):
                    for x in range(cols):
                        if (x, y) in playable:
                            continue
                        y0, x0 = PAD + y * UNIT, PAD + x * UNIT
                        box = comp_alpha[y0 + 48:y0 + UNIT - 48, x0 + 48:x0 + UNIT - 48]
                        if box.size and int(box.max()) >= 32:
                            problems.append(f"L{level_id}: long o inactive ({x},{y}) bi lam dac "
                                            f"(alpha {int(box.max())})")
                        frac = float((comp_alpha[y0:y0 + UNIT, x0:x0 + UNIT] >= 32).mean())
                        if frac > 0.55:
                            problems.append(f"L{level_id}: o inactive ({x},{y}) bi lap "
                                            f"{frac*100:.0f}% (qua nhieu)")

                # 14: topology alpha theo o khong doi.
                comp_map = ["P" if comp_alpha[PAD + y * UNIT + UNIT // 2,
                                              PAD + x * UNIT + UNIT // 2] >= 250 else "o"
                            for y in range(rows) for x in range(cols)]
                mask_map = ["P" if (x, y) in playable else "o" for y in range(rows) for x in range(cols)]
                if comp_map != mask_map:
                    problems.append(f"L{level_id}: chong frame lam doi topology alpha theo o")

    if args.snapshot and pathlib.Path(args.snapshot).is_file():
        old = {}
        for line in pathlib.Path(args.snapshot).read_text(encoding="utf-8").splitlines():
            if line.strip():
                digest, name = line.split("  ", 1)
                old[name.strip()] = digest.strip()
        changed = []
        for name, digest in old.items():
            current = BOARD / name
            if current.is_file() and hashlib.sha256(current.read_bytes()).hexdigest() != digest.lower():
                changed.append(name)
        regenerated = [n for n in changed if n.startswith(("board_mask_L", "board_frame_L"))]
        unexpected = [n for n in changed if n not in regenerated]
        print(f"board asset sinh lai (du kien): {len(regenerated)} {sorted(regenerated)[:8]}")
        print(f"asset khac bi doi (phai = 0): {len(unexpected)} {unexpected[:5]}")
        if unexpected:
            problems.append(f"{len(unexpected)} asset ngoai board bi thay doi")

    print(f"irregular mask da kiem: {checked}")
    print("PASS: mask khop playableMask" if not problems else "FAIL")
    for problem in problems:
        print("  " + problem)
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()
