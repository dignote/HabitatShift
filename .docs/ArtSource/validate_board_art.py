"""Read-only QA for the baked board art. Run with: python .docs/ArtSource/validate_board_art.py"""
from pathlib import Path
import importlib.util
import itertools
import json

from PIL import Image

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("board_art", HERE / "generate_board_art.py")
art = importlib.util.module_from_spec(spec)
spec.loader.exec_module(art)

catalog = json.loads(art.CATALOG.read_text(encoding="utf-8"))
checked = set()
for level in catalog["levels"]:
    for habitat in level["habitats"]:
        family = art.FAMILIES[habitat["color"]][0]
        shape = [tuple(map(int, p)) for p in habitat["shape"]]
        for n in range(1, len(shape) + 1):
            for subset in itertools.combinations(shape, n):
                if art.connected(list(subset)):
                    checked.add(art.OUT / f"tray_{family}_{art.key(list(subset))}.png")
checked.update(art.OUT / (name + ".png") for name in (
    "board_card", "board_tile", "board_blocker_stone", "board_intake",
    "board_preview_valid", "board_preview_invalid", "board_preview_selected",
    "board_elevator_up", "board_elevator_right", "board_elevator_down", "board_elevator_left"))

for path in sorted(checked):
    assert path.is_file(), f"Missing art: {path}"
    with Image.open(path) as image:
        assert image.mode == "RGBA", f"No alpha: {path}"
        assert max(image.size) <= 1024, f"Oversized for Android: {path}"
        alpha = image.getchannel("A")
        assert alpha.getextrema()[0] == 0 and alpha.getextrema()[1] >= 200, f"Broken silhouette: {path}"

# L2's two 3-cell shapes have a single basin at their shared boundaries.
for name in ("tray_saffron_0_0-0_1-1_1", "tray_cobalt_0_0-1_0-1_1"):
    with Image.open(art.OUT / (name + ".png")) as image:
        assert image.width == 440 and image.height == 440
        pixel = image.load()
        if "saffron" in name:
            a, b = pixel[100, 219], pixel[100, 221]
        else:
            a, b = pixel[219, 100], pixel[221, 100]
        assert max(abs(a[i] - b[i]) for i in range(3)) < 18, f"Interior seam in {name}"

print(f"PASS: {len(checked)} required sprites, alpha, Android dimensions and L2 seams")
