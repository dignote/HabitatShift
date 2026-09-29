"""Tao sproutling_ivory_v1.png bang cach recolor THAN cua mot sprite co san.

Run:
    python .docs/LevelTools/recolor_sproutling.py

Nguyen tac (theo yeu cau):
- chi doi vung THAN (hue am) sang ivory, giu do bong (value) tung pixel;
- giu nguyen kich thuoc, alpha (byte-identical) va moi pixel ngoai vung than;
- bao ve chi tiet nho (mat, long may, mieng, blush) bang connected-component:
  blob chi tiet nho nam trong vung am duoc giu nguyen mau goc;
- la (hue xanh) khong nam trong cua so hue am nen duoc giu nguyen.
"""
from __future__ import annotations

import argparse
import hashlib
import pathlib

import cv2
import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_SOURCE = ROOT / "Assets" / "Resources" / "HabitatShift" / "sproutling_coral_v1.png"
DEFAULT_OUTPUT = ROOT / "Assets" / "Resources" / "HabitatShift" / "sproutling_ivory_v1.png"
REPORT = ROOT / ".docs" / "LevelTools" / "recolor_sproutling_report.txt"


def main() -> None:
    parser = argparse.ArgumentParser(description="Recolor than sproutling sang ivory.")
    parser.add_argument("--source", default=str(DEFAULT_SOURCE))
    parser.add_argument("--out", default=str(DEFAULT_OUTPUT))
    parser.add_argument("--hue-deg", type=float, default=40.0, help="Hue dich cho than (do).")
    parser.add_argument("--sat-scale", type=float, default=0.25)
    parser.add_argument("--value-scale", type=float, default=0.90)
    parser.add_argument("--warm-low", type=float, default=330.0)
    parser.add_argument("--warm-high", type=float, default=30.0)
    parser.add_argument("--detail-value", type=float, default=0.50, help="Nguong toi de coi la chi tiet.")
    parser.add_argument("--detail-sat", type=float, default=0.62, help="Nguong bao hoa de coi la chi tiet.")
    parser.add_argument("--detail-area", type=float, default=0.015, help="Ty le dien tich toi da cua blob chi tiet.")
    parser.add_argument("--detail-min-area", type=int, default=300, help="Dien tich toi thieu (px) cua blob chi tiet.")
    parser.add_argument("--face-x", type=float, default=0.28, help="Bien trai/phai cua vung mat (ty le bbox).")
    parser.add_argument("--face-y", type=float, default=0.35, help="Bien tren cua vung mat (ty le bbox).")
    args = parser.parse_args()

    source = pathlib.Path(args.source)
    output = pathlib.Path(args.out)
    image = Image.open(source).convert("RGBA")
    alpha = image.getchannel("A")
    rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    hsv = np.asarray(image.convert("HSV"), dtype=np.int16)
    hue = hsv[..., 0].astype(np.float32) * (360.0 / 255.0)
    sat = hsv[..., 1].astype(np.float32) / 255.0
    value = hsv[..., 2].astype(np.float32) / 255.0
    subject = np.asarray(alpha) > 0

    warm = ((hue >= args.warm_low) | (hue <= args.warm_high)) & subject
    detail_seed = subject & ((value < args.detail_value) | (sat > args.detail_sat))
    count, labels, stats, _ = cv2.connectedComponentsWithStats(detail_seed.astype(np.uint8), connectivity=8)
    subject_area = int(np.count_nonzero(subject))
    rows = np.any(subject, axis=1)
    cols = np.any(subject, axis=0)
    top, bottom = int(np.argmax(rows)), int(len(rows) - np.argmax(rows[::-1]))
    left, right = int(np.argmax(cols)), int(len(cols) - np.argmax(cols[::-1]))
    box_w, box_h = max(1, right - left), max(1, bottom - top)
    protected = np.zeros_like(subject)
    protected_blobs = []
    for label in range(1, count):
        area = int(stats[label, cv2.CC_STAT_AREA])
        center_x = stats[label, cv2.CC_STAT_LEFT] + stats[label, cv2.CC_STAT_WIDTH] / 2.0
        center_y = stats[label, cv2.CC_STAT_TOP] + stats[label, cv2.CC_STAT_HEIGHT] / 2.0
        rel_x = (center_x - left) / box_w
        rel_y = (center_y - top) / box_h
        in_face = args.face_x <= rel_x <= 1.0 - args.face_x and args.face_y <= rel_y <= 0.90
        if in_face and args.detail_min_area <= area <= args.detail_area * subject_area:
            protected |= labels == label
            protected_blobs.append(area)
    recolorable = warm & ~protected

    target_hue = int(round(args.hue_deg / 360.0 * 255.0)) % 256
    hsv_out = hsv.copy()
    hsv_out[..., 0][recolorable] = target_hue
    hsv_out[..., 1][recolorable] = np.clip(hsv[..., 1][recolorable] * args.sat_scale, 0, 255).astype(np.int16)
    hsv_out[..., 2][recolorable] = np.clip(hsv[..., 2][recolorable] * args.value_scale, 0, 255).astype(np.int16)
    recolored_rgb = np.asarray(Image.fromarray(hsv_out.astype(np.uint8), "HSV").convert("RGB"), dtype=np.uint8)
    merged = np.where(recolorable[..., None], recolored_rgb, rgb)
    result = Image.fromarray(merged, "RGB").convert("RGBA")
    result.putalpha(alpha)
    output.parent.mkdir(parents=True, exist_ok=True)
    result.save(output, optimize=True)

    after = np.asarray(result, dtype=np.uint8)
    changed = np.any(np.asarray(image, dtype=np.uint8)[..., :3] != after[..., :3], axis=2)
    alpha_same = np.array_equal(np.asarray(alpha), after[..., 3])
    outside_changed = int(np.count_nonzero(changed & ~recolorable))
    body_mean = after[..., :3][recolorable].mean(axis=0) if np.any(recolorable) else np.zeros(3)
    tile = np.array([249, 241, 221], dtype=np.float64)
    delta = float(np.abs(body_mean - tile).mean())
    lines = [
        "recolor_sproutling report",
        f"source        : {source}",
        f"output        : {output}",
        f"size          : {image.size[0]}x{image.size[1]} -> {result.size[0]}x{result.size[1]}",
        f"alpha giong   : {alpha_same}",
        f"pixel than    : {int(np.count_nonzero(recolorable))}",
        f"blob chi tiet : {len(protected_blobs)} (dien tich {sorted(protected_blobs, reverse=True)[:6]})",
        f"ngoai than doi: {outside_changed} (phai = 0)",
        f"mau than TB   : R{body_mean[0]:.0f} G{body_mean[1]:.0f} B{body_mean[2]:.0f}",
        f"lech tile kem : {delta:.1f} (can >= 20)",
        "sha256 out    : " + hashlib.sha256(output.read_bytes()).hexdigest(),
        "",
        "QA: " + ("PASS" if (alpha_same and outside_changed == 0 and image.size == result.size and delta >= 20)
                  else "FAIL"),
    ]
    text = "\n".join(lines) + "\n"
    print(text)
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(text, encoding="utf-8")


if __name__ == "__main__":
    main()
