# ShapeForge Roadmap

This roadmap defines the next product epics and the release gates required before general availability.

## Epic 1: Split/Connectors

- **User story**: As a maker preparing large or fragile prints, I want to split a model and add connectors so parts fit on my build plate and can be reassembled reliably.
- **CLI entry points**:
  - `shapeforge split --in <input.stl> --out <dir> --plane <x,y,z,d>`
  - `shapeforge connectors --in <split-dir> --type <dowel|keyhole|dovetail> --clearance <mm>`
  - `shapeforge fix --preset Fdm --with-connectors`
- **UI entry points**:
  - New **Prepare > Split** wizard in `ShapeForge.App`.
  - Connector presets in the operator stack side panel.
  - 3D preview overlays for split plane and connector placement.
- **Diagnostics/readiness impact**:
  - Adds diagnostics for seam manifoldness, connector interference, and minimum wall thickness at connector interfaces.
  - Emits a readiness flag: `assembly_fit_risk` with severity levels.
- **Fixture-based test mapping (`tests/ShapeForge.Tests/Fixtures/`)**:
  - `cube_ok.stl`: baseline split + connector insertion does not regress watertightness.
  - `cube_hole.stl`: validate connector placement avoids existing cavities.
  - `nonmanifold_edge.stl`: ensure split pipeline reports and remediates edge anomalies before connector generation.
- **Acceptance criteria**:
  - Generated parts remain manifold and watertight after split/connect operations.
  - Connector clearances are honored within configured tolerance band.
  - CLI and UI produce equivalent connector topology for the same preset.
  - Diagnostics report includes seam + connector checks in one run.

## Epic 2: Tolerance Wizard

- **User story**: As a user dialing in printer/material fit, I want guided tolerance recommendations so snap-fits and mating parts work first print.
- **CLI entry points**:
  - `shapeforge tolerance scan --in <input.stl> --profile <printer-profile.json>`
  - `shapeforge tolerance apply --in <input.stl> --out <output.stl> --target <press-fit|slip-fit>`
  - `shapeforge fix --preset Fdm --tolerance auto`
- **UI entry points**:
  - New **Tolerance Wizard** launched from preset selection.
  - Fit target selector (`Press`, `Slip`, `Loose`) and material profile dropdown.
  - Before/after deviation heatmap in viewport.
- **Diagnostics/readiness impact**:
  - Adds dimensional drift diagnostics and fit-risk scoring.
  - Persists calibration metadata for replay in CI and support bundles.
- **Fixture-based test mapping (`tests/ShapeForge.Tests/Fixtures/`)**:
  - `cube_ok.stl`: verify neutral geometry remains unchanged when tolerance deltas are zero.
  - `tiny_shells.stl`: verify wizard flags under-minimum feature sizes and suggests safe offsets.
  - `cube_hole.stl`: validate hole compensation behavior for target fit classes.
- **Acceptance criteria**:
  - Wizard outputs deterministic offsets for identical profile + model inputs.
  - Applied tolerance operation stays within user-selected deviation bounds.
  - Diagnostics expose both recommended and applied offsets.
  - CLI and UI wizard outputs are schema-compatible.

## Epic 3: Batch

- **User story**: As a production-oriented user, I want to run ShapeForge over many files at once with predictable outputs and per-file reports.
- **CLI entry points**:
  - `shapeforge batch run --in <input-dir> --out <output-dir> --preset <name>`
  - `shapeforge batch plan --manifest <jobs.json>`
  - `shapeforge batch resume --run-id <id>`
- **UI entry points**:
  - Batch queue panel with drag/drop directory ingest.
  - Run monitor page for throughput, per-file status, and retries.
  - Export button for consolidated diagnostics bundle.
- **Diagnostics/readiness impact**:
  - Introduces run-level summaries (`success`, `warning`, `failed`) and retry telemetry.
  - Adds per-run reproducibility hash covering input, preset, and operator versions.
- **Fixture-based test mapping (`tests/ShapeForge.Tests/Fixtures/`)**:
  - Batch corpus includes `cube_ok.stl`, `cube_hole.stl`, `nonmanifold_edge.stl`, and `tiny_shells.stl` as a deterministic smoke set.
  - Regression suite verifies stable output filenames and diagnostics ordering.
- **Acceptance criteria**:
  - Batch execution is restartable from checkpoint without duplicating completed outputs.
  - Per-file failures do not halt unrelated jobs by default.
  - Final report includes deterministic run hash and file-level diagnostics.
  - Throughput metrics are emitted for baseline performance comparison.

## Epic 4: Farm Mode

- **User story**: As an operator managing multiple print nodes, I want distributed processing and centralized diagnostics to prepare jobs consistently across a print farm.
- **CLI entry points**:
  - `shapeforge farm agent --node <name> --capabilities <json>`
  - `shapeforge farm dispatch --manifest <jobs.json> --strategy <balanced|affinity>`
  - `shapeforge farm replay --run-id <id>`
- **UI entry points**:
  - Farm dashboard with node health, queue depth, and job assignment view.
  - Dispatch policy editor and node capability tagging.
  - Replay viewer for comparing deterministic run outputs across nodes.
- **Diagnostics/readiness impact**:
  - Adds node readiness checks (version skew, profile parity, storage pressure).
  - Captures cross-node determinism diagnostics and drift alerts.
  - Extends support bundle to include node-level execution traces.
- **Fixture-based test mapping (`tests/ShapeForge.Tests/Fixtures/`)**:
  - `cube_ok.stl` and `cube_hole.stl`: baseline parity checks across heterogeneous nodes.
  - `nonmanifold_edge.stl`: verifies remediation flow consistency across node architectures.
  - `tiny_shells.stl`: validates deterministic warning/report generation under constrained geometry.
- **Acceptance criteria**:
  - Same manifest processed on different eligible nodes yields byte-stable outputs (or documented equivalent hash policy).
  - Dispatch respects node capabilities and emits auditable assignment logs.
  - Farm replay reproduces diagnostics and output hashes from archived run metadata.
  - Health checks block dispatch when schema/version mismatch is detected.

## Milestone gates

All four epics must satisfy the following program-level gates before release:

1. **Schema stability gate**
   - CLI JSON/report schemas are versioned and backward-compatible across one minor release.
   - UI import/export artifacts validate against the published schema contract.
2. **Performance baseline gate**
   - Baseline throughput and latency are recorded on the fixture corpus (`cube_ok.stl`, `cube_hole.stl`, `nonmanifold_edge.stl`, `tiny_shells.stl`).
   - Regressions beyond agreed thresholds automatically fail CI.
3. **Deterministic replay gate**
   - Re-running the same inputs/presets/manifests reproduces equivalent output hashes and diagnostics ordering.
   - Replay evidence is captured in CI for local, batch, and farm execution modes.
