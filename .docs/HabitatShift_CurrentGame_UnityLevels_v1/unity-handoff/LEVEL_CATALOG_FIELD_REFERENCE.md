# Level Catalog Field Reference

## Coordinates

All pairs are `[x, y]`. Catalog origin is top-left; X increases right and Y increases down.

- `anchor`: integer top-left board position of a Habitat's local shape.
- `shape`: unique integer local occupied cells relative to anchor.
- `position`: continuous cell-space center for a Sproutling.
- `entry`: continuous cell-space center of an Elevator port.
- `obstacles` / `playableMask`: integer board cells.

Unity projects using Y-up must convert presentation/runtime coordinates explicitly. Do not rewrite source JSON or its fingerprint.

## Root

- `schema`, `catalogRevision`: catalog compatibility identity.
- `rulesetVersion`, `rulesetFingerprint`: required movement contract identity.
- `levelCount`: must equal `levels.length` and 18 in this package.
- `canonicalLevelsFingerprint`: SHA-256 of canonicalized `levels[]` using the Android build contract.

## Level

- `id`, `title`, `difficulty`, `profile`, `challenge`, `blockerIntent`: authored identity and player/design metadata.
- `cols`, `rows`: board bounds.
- `habitats`: movable footprint definitions.
- `targets`: initially present Sproutlings.
- `elevators`: authored queues released after committed gestures.
- `obstacles`: blocked cells; empty in current production levels but contractually supported.
- `playableMask`: `null` means full rectangular board; otherwise only listed cells are playable.
- `bonusPickups`: reserved and empty in approved v4.

## Habitat and completion

- `id`: level-local unique identifier.
- `color`: compatibility key.
- `need`: required compatible Sproutlings.
- `movementConstraint`: `FREE`, `HORIZONTAL_ONLY`, or `VERTICAL_ONLY`.
- A level is won only after all movable Habitats complete, mandatory queues are exhausted, and no Sproutlings remain.

## Elevator

- `direction` is the authored inward direction: `UP`, `RIGHT`, `DOWN`, `LEFT`.
- `queue` order is stable and mandatory when `mandatory=true`.
- `nextIndex` is authored initial state and is zero in production.
- Elevator evaluation occurs after a committed gesture, not per pointer sample.
