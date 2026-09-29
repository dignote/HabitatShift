# Validation Report

Date: 2026-09-28
Revision: `m7-20260928-levels-19-23` (levels 19-23 imported)

## Result

`PASS` (data + art gates). Unity EditMode unit tests could not run in this pass (Test Framework discovery is still not wired);
static preflight plus build/APK verification were used. Device smoke test pending device reconnection.

## Data identity

- Catalog SHA-256: `c886b91977b48a98bb73ce0bd0da48d8a183b0a73aca77f7a8ff2ef4e1b3df99`
- Catalog bytes: 111326
- Ruleset SHA-256: `3e0a9ef1d6b3f0fde22c5814198946ba53744973d851fcf7e2cc8035e0e4ef95` (unchanged)
- Catalog schema: `habitat-shift-levels-v4`
- Ruleset version: `continuous-core-v2` (unchanged)
- Levels: 23
- Canonical `levels[]` fingerprint: `178a5a082af30a7503cf06a903a816f2d2d7a633323edf236fde55cacd3c0ea3`
- Previous revision `m6-20260916-c-full-footprint-fix`: 18 levels, fingerprint `95665e0fecf1097f7add31bb8a0567bbca4b6f607bc182b5a5a5b810aa7e89d6`

## Gates

- Converter self-test: L1-L18 semantic equal AND per-level raw text identical (no reformatting).
- Geometry preservation: absolute cells (`anchor + shape`) for every L19-L23 habitat are identical before/after conversion.
- Determinism: converter output is byte-identical across runs.
- Fingerprint reproducibility: the documented formula reproduces the 18-level fingerprint and the new 23-level fingerprint.
- Preflight for L19-L23: ids 19..23, board bounds within 10x10, playableMask valid, habitats inside playable cells, no habitat overlap, shapes connected, need >= 1, sproutlings inside playable cells, elevator entryCell inside board, queues non-empty, unique ids, per-colour supply balance (need == board targets + queue supply).
- Board art: `validate_board_art.py` -> `PASS: 97 required sprites, alpha, Android dimensions and L2 seams` (35 new tray PNGs generated with `--missing-only`; 0 existing tray files changed hash).
- Unity build: Development APK built with 0 errors (2m45s); APK contains `assets/HabitatShift/approved_levels_v4.json`
  with SHA-256 equal to the source catalog, levelCount 23 and revision `m7-20260928-levels-19-23`.
- New asset import: `sproutling_ivory_v1.png` imported as Sprite (Single, PPU 100); new trays imported as Sprite
  (Single, PPU 192 per HabitatAssetImporter) with Android platform overrides written by Unity.
- Sproutling ivory: recolor QA PASS (dimensions and alpha identical, 0 pixels changed outside the recoloured body region, body mean R209 G200 B181 vs cream tile Delta 40.2).

## Editor provenance (documented, not gates)

- Source of truth: `Habitat_Shift_R9_2_2_level_editor_project.json` -> `candidateLevels` ids 19-23, user-approved by manual playtest 2026-09-28.
- Colour audit source vs candidate: 4 entities kept identical geometry but were recoloured in the editor
  (L20 habitat `20_B`, L20 sproutlings `20t1`/`20t3`, L21 habitat `21_B`; all brown -> green).
  Three brown source entities have no counterpart in the approved candidate layer (`19_B`, `19t5`, `21t9`).
  Decision: the approved map contains no brown, so no walnut family was added; the recolour is recorded here only.
- Ports: 11 ACTIVE_RUNTIME_PORT imported, 1 EDITOR_PLACEHOLDER excluded
  (L22 `E22_R8_1`: empty queue and duplicate edge/entryCell of `P22_TOP`).
- Editor-only elevator fields (`releaseRule: INITIAL_AND_ON_ENTRY_AVAILABLE`, `blockedBehavior: WAIT_AUTORETRY`,
  `maxActivePerPort`, `previewSlots`) are not represented by the production catalog/runtime; L19-L23 use the
  current runtime behaviour (release after a committed gesture when the entry cell is free, nextIndex 0).
- Elevator `direction` is presentation data only (sprite selection); collection is omnidirectional.

## Runtime follow-up (2026-09-29)

- Root Trim (assist) previously assumed the Habitat anchor cell `(0,0)` is occupied. Seven rotated-L Habitats in
  L20-L23 (L20 `20_O`; L21 `21_G`, `21_Y`, `H21_R81_1`; L22 `22_G3`; L23 `23_Y`, `23_R`) have an empty
  bounding-box corner, so the assist reported `ORIGIN CELL BLOCKED` (or shifted the Habitat by one cell).
- Root Trim now shrinks the Habitat to its topmost, then leftmost occupied cell and moves the anchor with it, so the
  surviving cell keeps its absolute position. Behaviour is unchanged for every Habitat whose `(0,0)` is occupied
  (all of L1-L18).
- Catalog data is unchanged by this fix; only `HabitatGameCore.cs` and the tests were updated.

## Known limitations

- `sproutling_ivory_v1.png` is a recolor of `sproutling_coral_v1.png`, not a new render.
- `GameplayFxController` prewarms 8 capture ghosts; levels with more than 8 sproutlings may show fewer ghost animations.
- Auto-generated metadata (`title`, `profile`, `difficulty`, `challenge`) because the editor project has no such fields.
- Level Select shows 23 cards (8 rows) and therefore scrolls.
