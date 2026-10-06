"""Apply the two-cell L8 mask bridge while preserving every other level object."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from convert_r9_levels import fingerprint
from refresh_levels_1_18 import object_spans, replace_root

ROOT = Path(__file__).resolve().parents[2]
VERIFY = ROOT / ".docs/Verification/L8SameColorFix"
PRODUCTION = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"
MIRROR = ROOT / ".docs/HabitatShift_CurrentGame_UnityLevels_v1/data/approved_levels_v4.json"


def main():
    source = (VERIFY / "production_before.json").read_bytes()
    assert source == (VERIFY / "mirror_before.json").read_bytes()
    assert PRODUCTION.read_bytes() == MIRROR.read_bytes() == source
    raw = source.decode("utf-8").replace("\r\n", "\n")
    catalog = json.loads(raw)
    assert catalog["catalogRevision"] == "m8-20261001-levels-01-18-progression-v2"
    level = catalog["levels"][7]
    before = {tuple(cell) for cell in level["playableMask"]}
    added = {(4, 0), (4, 2)}
    assert not before & added
    level["playableMask"] = [list(cell) for cell in sorted(before | added, key=lambda p:(p[1], p[0]))]
    spans = object_spans(raw)
    start, end = spans[7]
    block = json.dumps(level, ensure_ascii=False, indent=2).replace("\n", "\n    ")
    result = raw[:start] + block + raw[end:]
    result = replace_root(result, "canonicalLevelsFingerprint", fingerprint(catalog["levels"]))
    data = result.replace("\n", "\r\n").encode("utf-8")
    PRODUCTION.write_bytes(data)
    MIRROR.write_bytes(data)
    (VERIFY / "production_after.json").write_bytes(data)
    print("mask delta", sorted(added))
    print("catalog SHA256", hashlib.sha256(data).hexdigest())
    print("fingerprint", fingerprint(catalog["levels"]))


if __name__ == "__main__":
    main()
