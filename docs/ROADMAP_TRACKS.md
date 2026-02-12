# ShapeForge Roadmap Tracks

This document defines execution tracks for major product capabilities. Each track includes prerequisites, phased milestones (P0/P1/P2), objective acceptance checks, and cross-track dependencies.

---

## 1) Print Readiness Intelligence

**Scope:** Diagnostics, printability scoring, and actionable hints before export.

### Prerequisites
- Stable mesh validation primitives (watertightness, manifold checks, non-manifold edge detection).
- Baseline geometric metrics pipeline (wall thickness sampling, overhang analysis, island detection).
- Unified issue model (`severity`, `category`, `location`, `suggested_action`).
- Test corpus of known-printable and known-failing models.

### Milestones
- **P0 — Foundational Diagnostics**
  - Implement deterministic checks for watertightness, non-manifold geometry, and minimum wall thickness.
  - Emit structured diagnostic report (JSON + in-app representation).
  - Add hint templates tied to each diagnostic type.
- **P1 — Scoring + Prioritized Guidance**
  - Introduce weighted print-readiness score (0–100) with category subscores.
  - Add confidence indicators and severity-driven prioritization.
  - Provide contextual hints with parameterized recommendations (e.g., minimum wall delta needed).
- **P2 — Adaptive Intelligence**
  - Calibrate scoring weights with empirical print outcomes.
  - Add profile-aware diagnostics (FDM vs. resin presets).
  - Support "what changed" delta diagnostics after user edits.

### Objective Acceptance Checks
- Diagnostics are reproducible: same input mesh and profile produce identical issue list and score.
- At least 95% of regression corpus diagnostics match expected classifications.
- Every reported issue includes at least one actionable hint with measurable remediation guidance.
- Scoring API responds within target latency budget for reference model sizes.

### Cross-Track Dependencies
- **Depends on Track 2:** Repair outputs must round-trip into diagnostics for before/after comparison.
- **Depends on Track 3:** Resin-specific checks (hollow traps, suction risk) feed scoring categories.
- **Depends on Track 4:** Connector/tolerance checks contribute to assembly print-readiness.
- **Supports Track 5:** Batch CLI/manifests should consume diagnostics + score artifacts.

---

## 2) Repair Robustness

**Scope:** Mesh repair reliability through direct fixes and voxel rebuild fallback.

### Prerequisites
- Import pipeline that preserves transform/units and detects topology anomalies.
- Dual-path repair architecture (direct mesh operations + voxel remesh pipeline).
- Tolerance policy definitions for repair aggressiveness and feature preservation.
- Gold-standard fixtures for broken meshes across failure modes.

### Milestones
- **P0 — Deterministic Mesh Fix Core**
  - Implement hole closing, normal reorientation, duplicate/degenerate face cleanup.
  - Add repair report with operations applied and confidence markers.
  - Ensure non-destructive preview and reversible apply.
- **P1 — Voxel Rebuild Fallback**
  - Add automatic fallback to voxelization/reconstruction when direct repair confidence is low.
  - Expose resolution controls with estimated detail loss.
  - Preserve scale and orientation through repair transitions.
- **P2 — Robustness Hardening**
  - Introduce multi-strategy orchestration with best-result selection.
  - Add feature-preservation safeguards for sharp edges and thin walls.
  - Expand failure analytics and telemetry-backed heuristics.

### Objective Acceptance Checks
- Repair success rate meets target threshold on broken-mesh benchmark set.
- Repaired outputs pass baseline manifold/watertight checks when expected.
- Dimensional drift stays within tolerance budget for calibration fixtures.
- Fallback path invocation is logged and explainable in repair report.

### Cross-Track Dependencies
- **Feeds Track 1:** Repair confidence and residual defects must be visible to diagnostics/scoring.
- **Feeds Track 3:** Resin hollowing depends on reliable watertight repaired solids.
- **Feeds Track 4:** Split and connector operations require stable post-repair topology.
- **Supports Track 5:** Batch workflows need deterministic, reportable repair outcomes.

---

## 3) Resin Workflow

**Scope:** Hollowing, drain-hole planning, and resin-trap risk mitigation.

### Prerequisites
- Watertight solid enforcement (native or repaired).
- Internal cavity and shell generation kernel with thickness controls.
- Orientation analysis utilities (gravity-relative trapped volume detection).
- Printer/material profile schema for resin constraints.

### Milestones
- **P0 — Core Hollow + Drains**
  - Implement shell generation with minimum wall constraints.
  - Add drain-hole insertion (manual placement + configurable diameter/depth).
  - Validate that outputs remain manifold after operations.
- **P1 — Trap Risk Detection**
  - Detect enclosed resin pockets and suction-cup risk zones by orientation.
  - Recommend and optionally auto-place additional drains.
  - Integrate warnings into pre-export checks.
- **P2 — Resin-Aware Optimization**
  - Add profile-based presets (dental, miniatures, engineering resin).
  - Optimize hollow patterns for structural integrity vs. material savings.
  - Simulate resin escape paths for candidate orientations.

### Objective Acceptance Checks
- Hollowed models maintain target shell thickness within tolerance bounds.
- Drain operations do not introduce non-manifold artifacts on reference corpus.
- Trap-risk detector catches known problematic fixtures at agreed recall target.
- Orientation + drain recommendations reduce failed resin print incidence in pilot runs.

### Cross-Track Dependencies
- **Depends on Track 2:** Requires robust repair to create valid shells/cavities.
- **Feeds Track 1:** Trap and suction diagnostics must map into readiness scoring.
- **Interacts with Track 4:** Split strategies can reduce trap risk and alter drain placement.
- **Supports Track 5:** Batch resin preprocessing must emit reproducible hollow/drain manifests.

---

## 4) Part Preparation

**Scope:** Split-to-bed workflows, connector generation, and tolerance management.

### Prerequisites
- Bed volume constraints and orientation solver integration.
- Boolean/splitting operations stable on repaired meshes.
- Connector library (pins, dovetails, keyed joints) with parametric schema.
- Dimensional compensation model for process/material-specific tolerances.

### Milestones
- **P0 — Split-to-Bed Basics**
  - Add automated split suggestions for out-of-bounds models.
  - Preserve alignment metadata for reassembly.
  - Provide manual split plane editing with preview.
- **P1 — Connector Generation**
  - Implement auto-connector placement with collision checks.
  - Support connector profiles and orientation-aware keying.
  - Add configurable fit classes (press, slip, adhesive gap).
- **P2 — Tolerance Intelligence**
  - Introduce material/printer-specific tolerance presets.
  - Add compensation simulation for shrinkage/expansion.
  - Generate assembly validation report with predicted fit outcomes.

### Objective Acceptance Checks
- Generated parts fit within target bed dimensions with margin constraints.
- Reassembled calibration parts achieve target positional error tolerance.
- Connector placement passes collision/interference checks across test corpus.
- Tolerance presets produce fit outcomes within expected class bands.

### Cross-Track Dependencies
- **Depends on Track 2:** Clean topology is required for reliable splitting/booleans.
- **Feeds Track 1:** Assembly risks and tolerance violations must appear in readiness reports.
- **Interacts with Track 3:** Split boundaries affect resin drainage and hollow integrity.
- **Supports Track 5:** Manifests must encode split graphs, connectors, and tolerance settings.

---

## 5) UX/Automation

**Scope:** App stack editor, batch CLI, and manifest-driven reproducible workflows.

### Prerequisites
- Canonical operation graph/model representing repair, analysis, resin, and part-prep steps.
- Stable serialization format for workflow manifests.
- Shared validation layer between UI and CLI execution paths.
- Versioning strategy for manifests and operation compatibility.

### Milestones
- **P0 — Workflow Surface + CLI Backbone**
  - Deliver app stack editor to compose and reorder processing stages.
  - Implement batch CLI execution for single and multi-file jobs.
  - Produce machine-readable run reports and exit codes.
- **P1 — Manifest Contracts + Reproducibility**
  - Add manifest import/export with strict schema validation.
  - Guarantee deterministic execution under pinned versions/profiles.
  - Support dry-run mode with full planned-operation report.
- **P2 — Automation Ecosystem**
  - Add preset libraries and team-shareable workflow templates.
  - Integrate CI-friendly outputs (artifacts, summaries, pass/fail gates).
  - Add incremental execution/caching for large batches.

### Objective Acceptance Checks
- Equivalent workflow runs produce identical outputs/reports under fixed inputs and versions.
- CLI supports non-interactive operation with documented exit codes and failure diagnostics.
- Manifest validation rejects incompatible/unknown operations with actionable errors.
- UI stack editor and CLI engine remain behaviorally consistent on parity test suite.

### Cross-Track Dependencies
- **Depends on Tracks 1–4:** Automation layer orchestrates and exposes all domain capabilities.
- **Provides backpressure to Tracks 1–4:** Requires stable contracts, schema versioning, and deterministic behavior.
- **Enables system-wide acceptance:** End-to-end checks and batch governance rely on manifest + report standards.
