"""Verify the L8 mask-only catalog delta and protected catalog identity."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from audit_levels_1_18_density import audit
from convert_r9_levels import fingerprint
from refresh_levels_1_18 import object_spans

ROOT = Path(__file__).resolve().parents[2]
VERIFY = ROOT / ".docs/Verification/L8SameColorFix"
PRODUCTION = ROOT / "Assets/StreamingAssets/HabitatShift/approved_levels_v4.json"
MIRROR = ROOT / ".docs/HabitatShift_CurrentGame_UnityLevels_v1/data/approved_levels_v4.json"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main():
    before_bytes = (VERIFY / "production_before.json").read_bytes()
    after_bytes = PRODUCTION.read_bytes()
    assert after_bytes == MIRROR.read_bytes()
    before_text, after_text = before_bytes.decode("utf-8"), after_bytes.decode("utf-8")
    before, after = json.loads(before_text), json.loads(after_text)
    assert before["catalogRevision"] == after["catalogRevision"] == "m8-20261001-levels-01-18-progression-v2"
    assert before["rulesetVersion"] == after["rulesetVersion"] == "continuous-core-v2"
    assert fingerprint(after["levels"]) == after["canonicalLevelsFingerprint"]
    old, new = before["levels"][7], after["levels"][7]
    old_mask, new_mask = {tuple(c) for c in old["playableMask"]}, {tuple(c) for c in new["playableMask"]}
    assert new_mask - old_mask == {(4, 0), (4, 2)} and not old_mask - new_mask
    assert {k:v for k,v in old.items() if k != "playableMask"} == {k:v for k,v in new.items() if k != "playableMask"}
    old_spans, new_spans = object_spans(before_text), object_spans(after_text)
    for i in range(30):
        if i == 7:
            continue
        a, b = old_spans[i], new_spans[i]
        assert before_text[a[0]:a[1]] == after_text[b[0]:b[1]], f"L{i+1} raw object changed"
    for level in after["levels"][:18]:
        assert not audit(level)["errors"], f"L{level['id']} static audit failed"
    print("L8 mask delta: +(4,0), +(4,2); all other L8 fields unchanged")
    print("L1-L7,L9-L30 raw level objects unchanged: PASS")
    print("L1-L18 supply/placement/connectivity/mask routes: PASS")
    print("production/mirror identical; catalog SHA256", sha(after_bytes))
    print("fingerprint", after["canonicalLevelsFingerprint"])


if __name__ == "__main__":
    main()
