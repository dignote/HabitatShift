"""Verify protected files and print concise progression v2 evidence."""
from __future__ import annotations

import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path

from convert_r9_levels import fingerprint
from refresh_levels_1_18 import object_spans

ROOT = Path(__file__).resolve().parents[2]
VERIFY = ROOT / ".docs/Verification/Levels01_18_ProgressionV2"
PRODUCTION = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"
MIRROR = ROOT / ".docs/HabitatShift_CurrentGame_UnityLevels_v1/data/approved_levels_v4.json"
CHANGED_ART = {f"board_{kind}_L{level}.png{suffix}" for kind in ("mask", "frame")
               for level in (7, 8, 15, 16, 18) for suffix in ("", ".meta")}
INTENTIONAL = {
    str(PRODUCTION.relative_to(ROOT)).replace("\\", "/"),
    str(MIRROR.relative_to(ROOT)).replace("\\", "/"),
    "Assets/HabitatShift/Scripts/Core/HabitatGameCore.cs",
    "Assets/HabitatShift/Tests/EditMode/GameRulesTests.cs",
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main():
    baseline = json.loads((VERIFY / "baseline.json").read_text(encoding="utf-8"))
    old_bytes = (VERIFY / "production_before.json").read_bytes()
    new_bytes = PRODUCTION.read_bytes()
    assert new_bytes == MIRROR.read_bytes(), "production/mirror mismatch"
    before, after = json.loads(old_bytes)["levels"], json.loads(new_bytes)["levels"]
    assert fingerprint(after) == json.loads(new_bytes)["canonicalLevelsFingerprint"]
    assert sha(old_bytes) == baseline["production_sha256"]
    old_text = old_bytes.decode("utf-8")
    new_text = new_bytes.decode("utf-8")
    old_spans, new_spans = object_spans(old_text), object_spans(new_text)
    assert len(old_spans) == len(new_spans) == 30
    for i in range(18, 30):
        a, b = old_spans[i], new_spans[i]
        assert old_text[a[0]:a[1]] == new_text[b[0]:b[1]], f"L{i+1} raw object changed"
    protected_semantic = sha(json.dumps(after[18:], ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode())
    print("production/mirror SHA256", sha(new_bytes))
    print("canonicalLevelsFingerprint", fingerprint(after))
    print("L19-L30 raw objects identical: PASS")
    print("L19-L30 semantic SHA256", protected_semantic)
    assert before[10:14] == after[10:14] and before[16] == after[16]
    print("L11-L14 and L17 semantic preservation: PASS")
    for entry in baseline["dirty_files"]:
        path = entry["path"]
        if path in INTENTIONAL or Path(path).name in CHANGED_ART:
            continue
        assert sha((ROOT / path).read_bytes()) == entry["sha256"], f"pre-existing file changed: {path}"
    for entry in baseline["board_assets"]:
        path = entry["path"]
        if Path(path).name in CHANGED_ART:
            continue
        assert sha((ROOT / path).read_bytes()) == entry["sha256"], f"protected board art changed: {path}"
    print("all unrelated pre-existing dirty/art bytes preserved: PASS")
    print("level | size before->after | habitats before->after | initial/queued supply | duplicate colors")
    for old, new in zip(before[:18], after[:18]):
        count = Counter(h["color"] for h in new["habitats"])
        duplicates = ",".join(color for color, n in count.items() if n > 1) or "none"
        supply = defaultdict(lambda: [0, 0, 0])
        for h in new["habitats"]: supply[h["color"]][0] += h["need"]
        for t in new["targets"]: supply[t["color"]][1] += 1
        for e in new["elevators"]:
            for q in e["queue"][e["nextIndex"]:]: supply[q["color"]][2] += 1
        assert all(a == b + c for a, b, c in supply.values())
        supply_text = " ".join(f"{c}:{a}={b}+{q}" for c, (a, b, q) in supply.items())
        print(f"L{new['id']:02} | {old['cols']}x{old['rows']}->{new['cols']}x{new['rows']} | "
              f"{len(old['habitats'])}->{len(new['habitats'])} | {supply_text} | {duplicates}")


if __name__ == "__main__":
    main()
