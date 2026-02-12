# Performance Budgets

This document defines practical runtime/memory budgets and fallback behavior for ShapeForge pipeline commands.

## Mesh size tiers

| Tier | Triangle count |
| --- | --- |
| Small | `<= 100,000` triangles |
| Medium | `100,001 - 500,000` triangles |
| Large | `> 500,000` triangles |

## Runtime budgets per command

Budgets are soft targets for typical local runs and CI validation.

| Command | Small | Medium | Large |
| --- | --- | --- | --- |
| `diagnose` | <= 2s | <= 6s | <= 15s |
| `fix fast` (preview-oriented fix profile) | <= 4s | <= 12s | <= 30s |
| `fix voxel` (full/final profile with volumetric enforcement when available) | <= 8s | <= 25s | <= 60s |

## Memory targets

| Command class | Small | Medium | Large |
| --- | --- | --- | --- |
| `diagnose` | <= 200 MB RSS | <= 450 MB RSS | <= 900 MB RSS |
| `fix fast` | <= 350 MB RSS | <= 800 MB RSS | <= 1.4 GB RSS |
| `fix voxel` | <= 600 MB RSS | <= 1.2 GB RSS | <= 2.0 GB RSS |

## Timeout and fallback policy

- **Per-step cooperative cancellation**: operators and long loops must check `CancellationToken` regularly and abort quickly when requested.
- **Soft timeout policy**:
  - `diagnose`: 20s soft timeout
  - `fix fast`: 45s soft timeout
  - `fix voxel`: 90s soft timeout
- **Fallback escalation**:
  1. Reduce sampling density with adaptive caps by mesh size.
  2. Clamp sampled-vertex counts (especially expensive nearest-neighbor style scans).
  3. In preview mode, skip high-cost thickness/voxel-ish steps over the preview triangle threshold and emit a warning report.
  4. Return partial diagnostics and reports instead of failing hard where possible.

## Pipeline safeguards implemented

- **Adaptive sampling caps** scale down with mesh triangle count, bounded to avoid pathological under-sampling.
- **Preview triangle threshold** avoids expensive preview operations over `350,000` triangles.
- **Cancellation-friendly loops** are enforced in computational hotspots (for example, nearest-neighbor scans) with periodic cancellation checks.
- **CI-stable performance tests** use relaxed timing assertions to detect regressions while minimizing flakes.
