# ShapeForge Native (Swift/Xcode) Rewrite Plan

This is the active execution plan for ShapeForge as a **native macOS Swift/Xcode project**.

## Current status

- ✅ Native Swift package scaffold created under `native/`
- ✅ Initial `ShapeForgeCore` + `ShapeForgeCLI` targets in place
- ✅ Baseline deterministic pipeline test in place
- ✅ Legacy .NET solution, Avalonia app, and related build scripts removed

## Execution principles

1. **Native-only delivery**: all new development targets Swift + Xcode.
2. **Parity first**: keep command behavior and operator IDs stable while rebuilding.
3. **Determinism**: same input + same params must produce same outputs.
4. **Schema continuity**: keep diagnostics/recipe artifacts stable as features land.

## Module map

- `ShapeForgeCore`
  - geometry models
  - diagnostics models
  - operator protocol + registry
  - pipeline runtime
- `ShapeForgeCLI`
  - command parsing
  - command handlers
  - console/report output
- `ShapeForgeCoreTests`
  - deterministic contract tests
  - fixture-based regression tests (next)

## Phase plan

### Phase 1 — Core/CLI parity baseline (in progress)

- [x] Package and targets initialized
- [x] `version` and `operators` commands scaffolded
- [x] Add STL load/save in native core
- [x] Add `fix --in/--out` end-to-end path
- [ ] Add run manifest output model

**Exit criteria**
- Native CLI can process baseline fixtures with stable operator output.

### Phase 2 — Diagnostics parity

- [x] Implement `MeshDiagnostics` computation
- [x] Add `DiagnosticIssue` severity pipeline
- [x] Implement `diagnose --json`
- [ ] Match exit-code policy for automation

**Exit criteria**
- Stable diagnostics JSON and deterministic exit codes on fixture suite.

### Phase 3 — Native app shell (SwiftUI/AppKit)

- [ ] Build workflow stages: Import → Diagnose → Stack → Compare → Export
- [ ] Bind stage data to `ShapeForgeCore` contracts
- [ ] Add per-step logs and run summary

**Exit criteria**
- App can execute baseline repair workflow from import through export.

### Phase 4 — Print-prep feature expansion

- [ ] FDM thickness + overhang metrics
- [ ] Resin hollow + drain + trap checks
- [ ] Split-to-bed and connector workflows

**Exit criteria**
- Native stack reaches planned print-readiness and prep feature parity.

## Immediate next actions

1. Implement native STL reader/writer in `ShapeForgeCore`.
2. Add CLI `fix` command wiring with deterministic operator reporting.
3. Add fixture files and regression tests in `native/Tests`.
4. Start diagnostics JSON schema snapshots for forward compatibility.
