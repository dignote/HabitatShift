"""Regenerate only L8 board mask and frame using the established generator."""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
source = ROOT / ".docs/ArtSource/generate_board_art.py"
spec = importlib.util.spec_from_file_location("generate_board_art", source)
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)
level = json.loads((ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json").read_text(encoding="utf-8"))["levels"][7]
cells = [tuple(cell) for cell in level["playableMask"]]
generator.board_mask(cells, level["cols"], level["rows"], "board_mask_L8")
generator.board_frame(cells, level["cols"], level["rows"], "board_frame_L8")
print("Regenerated L8 mask/frame only")
