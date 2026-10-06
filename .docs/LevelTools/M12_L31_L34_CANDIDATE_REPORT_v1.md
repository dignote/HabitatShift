# M12 L31–L34 candidate, v5/v3

**Historical report:** L31 was re-authored and promoted in the [M12 L31 interlock report](M12_L31_INTERLOCK_REPORT_v1.md). The L31 data and catalog hashes below describe the earlier candidate and are superseded.

These four levels are an **isolated authoring candidate**. The production loader still opens the approved v4/v2 catalog with 30 levels and the existing SaveV2 path. No production cutover is included here.

## Authored composition

| Level | Orientation | Habitats | Active colors | Dual-stage | Elevator queues | Blockers | Occupancy |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| L31 | Horizontal | 8 | 7 | 2 | 2 × 2 | 0 | 52/64 = 81.25% |
| L32 | Rotated clockwise | 8 | 7 | 2 | 2 × 2 | 1 | 53/64 = 82.81% |
| L33 | Horizontal, different lane directions | 8 | 7 | 2 | 2 × 2 | 2 | 54/64 = 84.38% |
| L34 | Rotated counterclockwise | 8 | 7 | 2 | 2 × 2 | 3 | 55/64 = 85.94% |

The footprint mix is a 2×2 square, an L, triominoes, and dominoes. Orange appears on two physical Habitats. Cyan→red and pink→white stage transitions draw their second-stage Sproutlings from separate two-item Elevators. The square and L each have a neighboring Habitat that must clear before the larger footprint can sweep. This slice follows the M12 70/30 authoring contract but still needs human difficulty and composition review before approval.

## Machine checks

- Production catalog: 30 levels; its first 30 level objects match the candidate prefix semantically. Production catalog SHA-256: `2763f5fe0ae7e6bb5091a85ef466cda38cbb3e9673799f9bfe47387e72739745`.
- Candidate catalog: 34 levels; SHA-256: `1279d673b2c0b37f0adf8a1c7dc22cbb6a355cea846fec005619b3bc73f7f79f`.
- Canonical levels fingerprint: `45ecef03837600fb05088f81f0f4ae8be3da482cbf2ef058d053994d755a6605` (the existing sorted-key, compact JSON method).
- `CatalogLoader.DeserializeValidatedPayloads` accepted the candidate v5/v3 pair. `LevelV5Audit.Check` returned 0 findings for each level.
- `HabitatPresentation.Render` resolved the current board and Habitat sprites for all four initial states in Unity batch mode.
- `ContinuousSession` reached `won` for each level with 10 committed gestures, 0 Habitats, 0 Sproutlings, and both queues at 2/2. Each trace replayed from a fresh session with matching checkpoints.
- Existing `GameRulesTests` and `LevelSystemV5Tests`: 63 passed, 0 failed. `Assembly-CSharp` and `Assembly-CSharp-Editor`: 0 errors. The old third-party image-effects scripts produced 15 obsolete API warnings in the runtime build.

The Unity runner source is `M12AuthoringRunner.cs`. It was executed in an isolated project copy under `.docs/Verification/M11_LevelSystemV5/android_project/Assets/Editor/`; it loads the candidate from this repository. To repeat, copy the runner into an isolated Unity project's `Assets/Editor`, open that project with Unity 6000.6.2f1, and execute `M12AuthoringRunner.Run` in batch mode. It writes the four trace JSON files in `fixtures/`. The authoring generator is `author_m12_l31_l34.py`.

## Approval gates still open

The traces prove one winning route per level under the current game rules. They do not measure whether the route is discoverable, fun, or free from every alternate-order deadlock. Play L31–L34 in the Editor after the v5/v3 cutover or in an isolated candidate-loading harness, inspect the rotated Elevator presentation, test alternate orderings and assists, and compare difficulty against L19–L30 before promotion. A production v5/v3 cutover also needs its own catalog hash, save activation, UI level-count change, and release build verification.
