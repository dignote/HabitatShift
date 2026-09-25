# Validation Report

Date: 2026-09-24

## Result

`PASS`

## Data identity

- Catalog source SHA-256: `d7c920d0130ee0d54860f71247e8a7cd2b8e9c1968337ca6382f168461108270`
- Exported catalog SHA-256: identical
- Ruleset source SHA-256: `3e0a9ef1d6b3f0fde22c5814198946ba53744973d851fcf7e2cc8035e0e4ef95`
- Exported ruleset SHA-256: identical
- Canonical levels fingerprint: `95665e0fecf1097f7add31bb8a0567bbca4b6f607bc182b5a5a5b810aa7e89d6`
- Canonical ruleset fingerprint: `e7d1faa0472a140f3204d479d8f95d4c8ddaeea769eef8329622032a503d8904`

## Gates

- Draft 2020-12 JSON schema validation: PASS for catalog and ruleset.
- Semantic validation: PASS for L1–L18 sequence, local IDs, shape uniqueness, board bounds, Elevator entries, queues, and indices.
- Gradle `:app:verifyCatalogContract`: PASS.
- Catalog contract output: `schema=habitat-shift-levels-v4 levels=18` with expected fingerprint.
- Targeted tests `ContinuousCatalogTest` and `RulesetAndV4ModelTest`: 8 tests, 0 failures, 0 errors, 0 skipped.
- Export/source byte identity: PASS.

## Non-blocking environment notes

Gradle reported existing Android SDK duplicate-location notices and known configuration-cache incompatibilities for build-script catalog tasks. The verification and tests completed successfully; these notices do not change the exported data or contract.
