# Habitat Shift — Current Game / Unity Level Handoff v1

This package is the authoritative documentation and data handoff for recreating the current Habitat Shift product and its approved L1–L18 catalog in Unity.

## Authority order

1. `data/approved_levels_v4.json` is authoritative for level geometry and authored content.
2. `data/ruleset_manifest_v1.json` is authoritative for numeric movement rules.
3. `prd/HABITAT_MOTION_AND_INTERACTION_CONTRACT.md` is authoritative for runtime behavior not fully represented in JSON.
4. The bilingual PRDs define product behavior, screens, progression, feedback, persistence, and visual direction.

Do not silently repair, normalize, rebalance, round, reorder, or reinterpret production data. Reject invalid content and report the exact field. This package contains no art, audio, Android source, or C# importer.

## Production identity

- Catalog schema: `habitat-shift-levels-v4`
- Catalog revision: `m6-20260916-c-full-footprint-fix`
- Ruleset: `continuous-core-v2`
- Levels: 18
- Canonical `levels[]` fingerprint: `95665e0fecf1097f7add31bb8a0567bbca4b6f607bc182b5a5a5b810aa7e89d6`
- Ruleset fingerprint: `e7d1faa0472a140f3204d479d8f95d4c8ddaeea769eef8329622032a503d8904`

Start with `unity-handoff/CODEX_UNITY_IMPORT_PROMPT.md`, then use the import guide and parity checklist.
