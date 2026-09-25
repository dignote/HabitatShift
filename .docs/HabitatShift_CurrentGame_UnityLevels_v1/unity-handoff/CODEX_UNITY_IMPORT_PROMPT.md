# Codex Prompt — Import Habitat Shift into Unity

Implement the supplied Habitat Shift catalog and ruleset in the target Unity project.

Read completely, in this order:

1. `README.md`
2. both PRDs
3. `prd/HABITAT_MOTION_AND_INTERACTION_CONTRACT.md`
4. JSON schemas and field reference
5. Unity import guide and runtime parity checklist

Requirements:

- Treat `data/approved_levels_v4.json` and `data/ruleset_manifest_v1.json` as immutable production truth.
- Create serializable DTOs, a validator, immutable catalog access, and separate mutable runtime state.
- Keep simulation coordinates top-left/Y-down unless a single explicit adapter performs conversion.
- Implement continuous float direct manipulation and the complete contact/strict-projection contract; do not replace it with grid snapping or Rigidbody approximation.
- Preserve every level ID, shape, anchor, target, queue, constraint, and fingerprint.
- Implement progression/assists/persistence outside level DTOs.
- Add automated catalog validation and solver/motion regression tests before UI integration.
- Execute every item in `UNITY_RUNTIME_PARITY_CHECKLIST.md` and report PASS/FAIL with evidence.
- If protected parity requires changing source JSON, stop and report the mismatch; never silently repair data.

This package intentionally contains no C# importer or assets. Adapt implementation to the target Unity version and project architecture while preserving the contracts above.
