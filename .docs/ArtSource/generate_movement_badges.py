"""Bake the small enamel direction badges used on constrained habitat rims."""
from pathlib import Path

from PIL import Image, ImageDraw


OUT = Path(__file__).resolve().parents[2] / "Assets/Resources/HabitatShift/Board"


def horizontal_badge() -> Image.Image:
    image = Image.new("RGBA", (512, 224))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((8, 20, 503, 213), radius=88, fill=(43, 58, 48, 220))
    draw.rounded_rectangle((16, 24, 495, 200), radius=82, fill=(157, 123, 67))
    draw.rounded_rectangle((26, 33, 485, 190), radius=73, fill=(247, 234, 190))
    draw.rounded_rectangle((35, 42, 476, 181), radius=64, outline=(106, 125, 85, 160), width=4)
    draw.line((138, 111, 374, 111), fill=(64, 86, 66), width=19)
    draw.line((138, 106, 374, 106), fill=(100, 132, 89), width=9)
    draw.polygon(((113, 111), (176, 60), (176, 162)), fill=(59, 83, 65))
    draw.polygon(((399, 111), (336, 60), (336, 162)), fill=(59, 83, 65))
    draw.polygon(((124, 107), (169, 71), (169, 143)), fill=(122, 151, 104))
    draw.polygon(((388, 107), (343, 71), (343, 143)), fill=(122, 151, 104))
    return image


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    horizontal = horizontal_badge()
    horizontal.save(OUT / "board_move_horizontal.png", optimize=True)
    horizontal.rotate(90, expand=True).save(OUT / "board_move_vertical.png", optimize=True)


if __name__ == "__main__":
    main()
