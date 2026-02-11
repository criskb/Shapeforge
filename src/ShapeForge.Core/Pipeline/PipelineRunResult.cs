using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;

namespace ShapeForge.Core.Pipeline;

public record PipelineRunResult(
    MeshModel FinalMesh,
    MeshDiagnostics? PreDiagnostics,
    MeshDiagnostics? PostDiagnostics,
    IReadOnlyList<OpReport> StepReports,
    TimeSpan Elapsed,
    IReadOnlyDictionary<string, TimeSpan>? StepElapsed = null);
