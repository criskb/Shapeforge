# Operator Capabilities Matrix

Capability status values:

- **supported**: expected to run with full intent in the listed mode/capability.
- **limited**: runs, but with degraded behavior or fallback implementation.
- **not-supported**: intentionally out of scope for that operator.

| Operator ID | FastMesh | Voxel | Fdm | Resin | Preview | Final |
| --- | --- | --- | --- | --- | --- | --- |
| `repair.fix` | supported — uses mesh backend repair steps (weld/clean/orient/hole-fill/tiny-shell filter). | not-supported — does not require voxel/SDF operations. | supported — generic mesh repair used in FDM workflows. | supported — generic mesh repair still applies to resin meshes. | supported — same algorithm with lighter scaling policy. | supported — same algorithm with full scaling policy. |
| `prep.fdm.thickness.enforce` | limited — managed fallback can adjust vertices, but lacks robust volumetric edits. | supported — volume offset backend enables intended thickness enforcement path. | supported — operator is authored for FDM minimum-wall workflows. | not-supported — operator metadata restricts this step to FDM mode. | limited — lower sampling + potential fallback can reduce enforcement quality. | limited — full sampling, but still limited without voxel backend in default builds. |

## Notes for implementers

- Core operator metadata now includes `RequiredBackendCapabilities`, `SupportedModes`, and `SupportedQualities`.
- CLI/App evaluate this metadata against the active profile and available backend capabilities.
- Unsupported or limited combinations are reported as warnings rather than hard failures.
