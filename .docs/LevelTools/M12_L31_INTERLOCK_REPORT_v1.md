# M12 L31 interlock rework and in-app test build

Historical Round 1 report. The current L31 is documented in `M12_L31_ROUND2_REPORT_v1.md`.

## Authored change

The former L31 used eight mostly independent horizontal lanes and 31 initial Sproutlings. Revised L31, **Interlock Garden**, retains eight physical Habitats and two Dual-Ring Habitats, but uses a central lock, a left turning pocket, and a right stage/queue route. It has seven active Habitat colors, nine colors across both stages, two Elevators with two mandatory items each, one obstacle, 29 initial Sproutlings, and density **54/64 = 84.375%**. L31's full rectangular board needs no new board mask or frame.

Only the orange trio is on the outer perimeter with an immediately independent complete route (1/8 = 12.5%). Purple touches the perimeter but needs the blue square to clear its lower pocket. No four same-color initial Sproutlings form a straight contiguous line; the authoring script asserts this.

## Dependency map

1. `L31_H_GATE` starts at `(3,3)`. Its first move to `(3,4)` collects nothing and frees passage for the 2×2 `L31_H_BLUE_SQUARE`. In a frozen-neighbor integer-anchor audit, blue starts with one legal anchor and no target it can cover. After the gate moves, four legal anchors cover all four blue targets.
2. The blue square's completion frees `(1,3)`, allowing `L31_H_PURPLE_SQUARE` to reach its last lower target. Purple's completion vacates the left pocket and clears purple from the white Elevator entry `(0.5,7.5)`.
3. `L31_H_PINK_RING` collects pink in the upper pocket, changes ActiveColor to white, then returns through the cleared left pocket to collect the white queue. The white Sproutling is a foreign blocker before the stage transition and a collectible afterward.
4. `L31_H_GATE` later clears orange from `(6,4)` and `(7,4)`, opening the right turning pocket. `L31_H_CYAN_RING` collects one cyan Sproutling, yields space to `L31_H_YELLOW_KEY`, then completes cyan after yellow clears `(6,7)`. Its red stage travels down the green route to the red Elevator at `(7.5,0.5)`.

## Verification

- `LevelV5Audit.Check`: 0 findings on L31 and on L32–L34. This includes stage supply, placement, initial mobility, same-color static reachability, and bounded queue-stage checks.
- Winning `ContinuousSession` trace from fresh L31: 15 gestures, `won`, both queues 2/2. `SolutionTraceRunner.Replay` passed against the production v5 catalog.
- Five alternate first-order paths also won and replayed: yellow key first, perimeter orange trio first, pink Dual-Ring first, purple square first, and cyan Dual-Ring first. They took 15 or 16 gestures. This is bounded evidence, not a full solver.
- Existing GameRules/LevelSystem tests: **66 passed, 0 failed**. `Assembly-CSharp` and `Assembly-CSharp-Editor`: **0 errors**. Runtime build had 15 pre-existing obsolete API warnings from third-party image-effects code.
- Editor Level Select probe: **34 cards**, L31 unlocked after legacy L30 completion, and the L31 card opened gameplay.
- The previous v5 catalog's per-level semantic hashes changed only at L31. L1–L30 and L32–L34 are preserved. Production/mirror v5 catalog bytes match. Production catalog SHA-256: `5322a0f6b6fcc5d19e8bcae40becb877476b4bae86b5b421de525e16a3bb5c7e`; canonical levels fingerprint: `b342b1df75ccf759188dc939131bce83655a2f9b434a8e6275076cdde36b42d7`.
- SaveV3 tags new snapshots with the catalog fingerprint. An in-progress snapshot from the former L31 layout is cleared on load; old snapshots for unchanged levels are preserved. Completion records and other save progress remain.
- Android Development APK: `Builds/HabitatShift-M12-L31-interlock-dev.apk`; Unity result **Succeeded**, 0 errors, 2 unrelated warnings. The APK contains the v5 catalog and v3 ruleset with the expected hashes. `adb devices` returned no device or emulator, so install/launch and Android interaction are **UNVERIFIED**.
- Initial-state Unity capture: `.docs/Verification/M12_L31_L34/L31_interlock_initial.png`.

## Release observation

The 50/50 authoring contract now targets 76–85% density for L31–L35. L31 satisfies that range. Existing L34 is 85.9%, slightly above the new target; this task explicitly preserves L32–L34. Human L31 playtest remains required to judge discoverability, feel, and any alternate routes beyond the five simulated paths. No manual gameplay PASS is claimed.
