# Habitat Motion and Interaction Contract

## Coordinate and input model

- Board space is continuous `float` 2D, measured in cells, with authored origin at top-left, positive X right, positive Y down.
- Rendering and pointer conversion use one shared fit transform. Never scale or offset input separately from board visuals.
- Press captures the exact local grab offset. A Habitat must not jump its center to the pointer.
- Each pointer update uses incremental delta from the previous pointer/contact position, not an absolute target accumulated behind a blocker.
- Support horizontal, vertical, diagonal, reversal, and multi-corner motion in one uninterrupted gesture.

## Swept contact solver

- Subdivide requested motion so no sweep step exceeds `0.08` board unit.
- Move to earliest legal contact, gather simultaneous contact normals, remove only vector components pushing into those normals, and retain tangent motion.
- Iterate the contact manifold up to four times per sweep step for corners and multiple blockers.
- Rebase blocked pointer components at contact so changing direction responds on the next pointer update without motion debt.
- Retain contact normals with `0.20` release hysteresis to avoid edge chatter.
- A blocked-contact presentation event fires once when contact begins, not once per sub-step or held frame.

## Collision policy

- Foreign-color Sproutlings use collision radius `0.18` cell.
- Bounds, playable mask, obstacles, and collection geometry are always strict.
- During active drag only, shrink the moving Habitat collision core by `0.04` cell against other Habitats. Rendering, footprint, center, hitbox identity, and resting geometry do not shrink.
- A soft candidate is valid only when a strict-legal projection exists within `0.12` cell, reachable along contact normals with the same orientation and footprint.
- Correct toward strict projection by at most `0.02` cell per sweep step.
- Reject soft candidates without strict projection. This permits naturally aligned exact-clearance passages but never smaller gaps or crossing through a blocking Habitat.
- Corridor assistance may use the authored `0.10` active-drag inset but never changes resting legality.

## Collection, constraints, and release

- Same-color collection occurs live during drag with tolerance `0.12` cell; counters update from state immediately.
- Foreign-color Sproutlings block motion. Do not collect, swap, push, or pass through them.
- `HORIZONTAL_ONLY` and `VERTICAL_ONLY` filter motion to the authored axis while retaining continuous grab behavior.
- Track `lastStrictPose` and a bounded strict-safe trail; do not retain unbounded motion traces or allocate effects each sub-step.
- Release settles to the nearest safe integer anchor. A soft pose must project to strict legality or fall back through the strict-safe trail.
- Never commit soft overlap to board state, Undo, Restart, serialization, or persistence.
- Undo snapshots committed gameplay only. Restart restores the authored level including footprints, targets, queues, constraints, and transient-assist effects.

## Assist interaction

- Nest Bloom: assist → eligible Habitat → apply compatible collection.
- Garden Shift: assist → Sproutling → compatible Habitat; Habitat geometry does not move.
- Root Trim: assist → eligible Habitat → trim; Restart restores the authored footprint.
- Invalid targets remain cancelable and must not consume charges or emit success VFX.
- Debug unlimited mode bypasses charge accounting only; it never bypasses eligibility or writes DataStore.

## Feedback and victory

- Selection and compatible targets must be readable without changing hitboxes.
- Sound Effects, Haptics, and VFX are independent settings/channels.
- Presentation events are transient, uniquely identified, exactly once, and cleared by Restart/level change as appropriate.
- Victory locks gameplay input, keeps the completed board visible, runs Level Complete VFX for 2900 ms, holds 700 ms, then opens Result exactly once.
- Recomposition, Pause/Settings navigation, and configuration change must not restart consumed effects or victory timing.
- At animation scale zero, use only a brief confirmation and do not impose the 3600 ms wait.

## Performance acceptance

- Target stable 60 FPS on the target device.
- Direction change after contact must affect the next pointer/frame update.
- Holding against a blocker must not grow trace, trail, effect count, or allocation without bound.
- Valid tight gaps must be traversable without releasing the pointer; invalid gaps remain blocked.
- Dense L18 must show no progressively worsening frame time during repeated contact, collection, and reversal.
