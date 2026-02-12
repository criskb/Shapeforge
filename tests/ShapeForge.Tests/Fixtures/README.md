# Fixture Taxonomy

This folder contains fixture families used by `ShapeForge.Tests`.

## Taxonomy

- `topology/`
  - `holes/`
  - `non-manifold/`
  - `self-intersection/`
- `stress/`
  - `dense-noisy-scans/`
- `scale-units/`
  - `mm-inch-ambiguity/`

Each leaf fixture directory should contain:

- `expected.json` (required outcome contract)
- one or more mesh files (`.stl`, optional for synthetic-only expected contracts)

## Outcome registry scaffold

Use `fixture-outcomes.registry.json` as the canonical index for fixture expectations.
It is intentionally lightweight so tests can progressively adopt it while preserving
existing per-fixture `expected.json` manifests.
