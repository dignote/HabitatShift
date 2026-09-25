# Unity Runtime Parity Checklist

## Catalog

- [ ] L1–L18 load in authored order with matching titles, dimensions, difficulty, footprints, targets, constraints, Elevators, and queues.
- [ ] Production fingerprints and package hashes match before play.
- [ ] Invalid schema/version/ID/bounds/shape fails closed with an exact error.

## Motion

- [ ] Direct drag preserves grab offset and never jumps to center.
- [ ] Horizontal, vertical, diagonal, reversal, and multi-corner drag work in one gesture.
- [ ] Fast pointer motion does not tunnel through blockers.
- [ ] Direction changes after collision respond on the next pointer/frame update without pull-back debt.
- [ ] Holding contact does not grow traces/effects or become progressively slower.
- [ ] Exact-clearance 1×1, 2×1, 2×2, and L-shape passages work without releasing.
- [ ] Gaps short by 0.01, 0.04, or 0.08 cell remain blocked.
- [ ] Release from soft contact always ends at a strict-legal integer anchor.

## Mechanics

- [ ] L1: collection, counter, completion, Undo, Restart.
- [ ] L7: Elevator releases only after committed gestures and preserves queue order.
- [ ] L8: Nest Bloom valid/invalid/cancel and charge behavior.
- [ ] L12: Garden Shift two-stage selection; Habitat geometry does not move.
- [ ] L15: authored movement axes permit one axis and reject the other.
- [ ] L16: Root Trim and Restart-authored-footprint restoration.
- [ ] L18: dense-board motion, combined mechanics, stable frame pacing.

## Feedback and lifecycle

- [ ] Blocked contact feedback fires once per contact start, not every frame.
- [ ] VFX/audio/haptics respect independent settings and do not replay after navigation/recomposition equivalents.
- [ ] Victory celebration lasts 2900 ms, holds 700 ms, and opens Result once.
- [ ] Reduced-motion mode skips the cinematic wait.
- [ ] Restart, Next, Back, and level change clear transient interaction/VFX/victory state.

## Acceptance

- [ ] Stable target 60 FPS on the selected target device.
- [ ] No geometry, ruleset, progression, assist, or queue drift from Android.
- [ ] No raw internal IDs or debug-only unlimited charges in release UI.
