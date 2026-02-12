# ShapeForge Native (Swift/Xcode) Rewrite Plan

This document executes the "Option 2" strategy: a phased migration path to a native Apple stack while protecting roadmap progress and preserving deterministic pipeline behavior.

## Why this exists

ShapeForge currently ships as a .NET 8 codebase with shared Core + CLI + Desktop layers. The rewrite track is for an Apple-first future where Xcode/Swift are the primary implementation stack.

## Principles

1. **Behavior parity before feature expansion**: match current CLI/Core behavior first.
2. **Schema continuity**: keep diagnostics/recipe/report artifacts compatible.
3. **Determinism first**: preserve reproducibility for maker workflows and farms.
4. **Phased cutover**: avoid all-at-once rewrites that freeze product progress.

## Target module map (Swift Package)

- `ShapeForgeCore`
  - Geometry models
  - Operator protocol + registry
  - Pipeline runtime
  - Diagnostics models and readiness evaluation
- `ShapeForgeCLI`
  - Command parsing and command handlers
  - Report rendering and exit code policy
- `ShapeForgeCoreTests`
  - Contract tests
  - Fixture-driven diagnostics/repair tests

## Parity matrix (v1)

| Capability | Current status (.NET) | Native target (Swift) | Phase |
|---|---|---|---|
| STL load/save | Present | Equivalent parser/writer in Core | 2 |
| Operator registry/listing | Present | Equivalent IDs and metadata | 2 |
| `fix` command orchestration | Present | Equivalent command + outputs | 2 |
| Diagnostics JSON output | Planned in .NET roadmap | Native schema implementation | 3 |
| Print readiness scoring | Planned | Native rule engine | 3 |
| App workflow shell | Present (Avalonia shell) | SwiftUI/AppKit workflow shell | 4 |
| Resin/FDM advanced prep | Planned | Native implementation after parity | 5 |

## Phase plan

### Phase 0 — Decision gate and scope lock (1–2 weeks)

- Lock parity scope and defer net-new advanced features.
- Freeze schema format requirements for diagnostics and recipes.
- Define success criteria for replacing .NET runtime dependency in Apple builds.

**Exit criteria**

- Approved parity matrix.
- Approved acceptance checklist and timeline.

### Phase 1 — Native architecture skeleton (1 week)

- Initialize `native/` Swift Package with target modules.
- Define protocol-level contracts only:
  - `MeshModel`
  - `DiagnosticIssue`
  - `MeshDiagnostics`
  - `ShapeOperator`
  - `PipelineRunner`
- Add contract tests for determinism and schema encoding.

**Exit criteria**

- `swift test` passes for foundational contracts.

### Phase 2 — Core/CLI parity MVP (3–6 weeks)

- Implement STL reader/writer.
- Implement operator registry and pipeline execution.
- Implement baseline cleanup/fix operator and `operators` + `fix` commands.
- Add deterministic run manifest model.

**Exit criteria**

- Native CLI can execute `operators` and `fix` on baseline fixture meshes.
- Fixture tests pass.

### Phase 3 — Diagnostics parity (2–4 weeks)

- Implement structured diagnostics model.
- Implement readiness evaluator with severity rules.
- Implement `diagnose --json` with exit code semantics.

**Exit criteria**

- Stable diagnostics JSON generated from native CLI.
- Exit codes match policy and tests.

### Phase 4 — Native app parity MVP (3–6 weeks)

- Build workflow shell:
  - Import → Diagnose → Stack → Compare → Export.
- Bind to shared native Core contracts.
- Add per-step logs and run summary panels.

**Exit criteria**

- Apple-native app can run baseline repair workflow end-to-end.

### Phase 5 — Feature catch-up and differentiation

- Resin hollow/drain/trap checks.
- FDM overhang/thickness workflows.
- Split-to-bed and connectors.

**Exit criteria**

- Native stack reaches roadmap equivalence for targeted release.

## Risks and mitigations

- **Risk:** rewrite slows roadmap delivery.
  - **Mitigation:** keep a parity-first backlog and freeze nonessential scope.
- **Risk:** schema drift breaks automation.
  - **Mitigation:** contract tests with canonical fixtures and JSON snapshots.
- **Risk:** deterministic behavior regressions.
  - **Mitigation:** fixed seeds + fixture baseline hash checks.

## Acceptance gates

1. Native contract tests green.
2. CLI parity commands green on fixtures.
3. Diagnostics schema compatibility validated.
4. Native app performs baseline repair flow.
5. Product decision: native-only cutover or dual-track support.
