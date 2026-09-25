# Unity Import Guide

## Import boundary

Deserialize the two files in `data/` into immutable catalog/ruleset DTOs, validate them, then construct separate mutable runtime state. Never mutate source DTOs to represent play state.

## Required pipeline

1. Load UTF-8 JSON without rounding numeric values.
2. Validate against the supplied Draft 2020-12 schemas before runtime construction.
3. Require exact schema, revision, ruleset version, ruleset fingerprint, level count, and catalog fingerprint.
4. Enforce semantic validation: unique IDs, board bounds, unique shape cells, positive needs, valid queue index, and in-bounds ports/targets.
5. Preserve list order for levels, Habitats, targets, Elevators, and queue items.
6. Build runtime copies and implement the motion contract separately from rendering.

## Coordinate conversion

Catalog board coordinates are top-left/Y-down. Recommended Unity choices:

- Keep simulation in catalog coordinates and convert only in the view layer; or
- Convert to Unity Y-up once at runtime construction and use the inverse conversion for serialization/debug comparison.

The first option is recommended because it makes parity tests and catalog comparisons exact. Rendering and input must share one board transform.

## Do not infer

- Do not infer shapes from images or bounding boxes.
- Do not recolor or merge enum values.
- Do not convert target centers to integer cells.
- Do not auto-fix overlaps or out-of-bounds content.
- Do not seed assist inventory from level JSON; progression/persistence owns assist state.
- Do not treat decorative Result stars as scoring.

## Compatibility failure

Reject the whole catalog and show a developer-facing exact error when validation fails. Do not partially load valid-looking levels, fall back to guessed rules, or regenerate fingerprints in production.
