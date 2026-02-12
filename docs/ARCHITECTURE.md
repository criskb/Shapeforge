# ShapeForge Architecture Ownership

This document defines project boundaries so features can be implemented with clear responsibilities, stable contracts, and predictable dependency flow.

## Project ownership

### `ShapeForge.Core`

`ShapeForge.Core` owns:

- Domain models and domain-level data contracts.
- Diagnostics definitions, structures, and evaluation plumbing.
- Operators and operator contract abstractions.
- Pipeline runtime orchestration and execution primitives.
- Backend interfaces used by higher-level hosts.

`ShapeForge.Core` must remain host-agnostic and reusable by both CLI and app entry points.

### `ShapeForge.Cli`

`ShapeForge.Cli` owns:

- Argument parsing and command option validation.
- Command orchestration that maps CLI requests into Core workflows.
- Human-readable and machine-friendly report rendering.
- Process exit code mapping and command failure semantics.

`ShapeForge.Cli` should not duplicate domain rules that belong in Core.

### `ShapeForge.App`

`ShapeForge.App` owns:

- Workflow UI composition and interaction behavior.
- State management for session/workflow state.
- Visualization of geometry processing and diagnostics.
- Profile and recipe editing experiences.

`ShapeForge.App` should treat Core contracts as the source of truth for pipeline behavior.

### `ShapeForge.Tests`

`ShapeForge.Tests` owns:

- Unit tests for isolated components.
- Fixture-driven tests for domain and pipeline scenarios.
- CLI behavior tests, including command contracts and exit code behavior.

Tests should verify contracts at project boundaries and prevent ownership drift.


## Ownership boundary summary

- **Core**: canonical contracts and schema/version authority (`MeshModel`, `MeshDiagnostics`, `DiagnosticIssue`, `PipelineRunResult`, `OperatorSchema`, and recipe/PEM payloads).
- **Cli**: argument/report adapters over Core contracts only.
- **App**: UI state/bindings over Core contracts only.
- **Tests**: contract compatibility and fixture-driven regression coverage.

The dependency-direction rule is strict: host projects can depend on Core, while Core cannot depend on hosts.

## Dependency direction rule

Dependency flow is one-way:

- `ShapeForge.App` -> `ShapeForge.Core`
- `ShapeForge.Cli` -> `ShapeForge.Core`

`ShapeForge.Core` must never depend on `ShapeForge.App` or `ShapeForge.Cli`.

When introducing new abstractions, place shared contracts in Core and keep host-specific concerns in their respective host projects.

## New feature contract review checklist

Before implementation begins, every new feature must pass this checklist:

- [ ] Identify the primary owning project (`Core`, `Cli`, `App`, or `Tests`).
- [ ] Confirm domain contracts (models, diagnostics, operator/pipeline interfaces) are defined in `ShapeForge.Core` when shared.
- [ ] Verify dependency direction is preserved (no reverse dependency into Core).
- [ ] Define required CLI contract changes (arguments, reports, exit codes) if feature is command-facing.
- [ ] Define required App contract changes (state shape, view model bindings, visualization hooks) if feature is UI-facing.
- [ ] Specify test coverage additions in `ShapeForge.Tests` (unit, fixture, CLI behavior) before coding.
- [ ] Record any contract decisions and non-goals in the feature design note or issue.

No feature implementation should start until this review is complete and approved.
