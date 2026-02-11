using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;
using System.Diagnostics;

namespace ShapeForge.Core.Pipeline;

public sealed class PipelineRunner
{
    public async Task<(MeshModel mesh, IReadOnlyList<OpReport> reports)> RunAsync(
        MeshModel input,
        IEnumerable<IOperator> steps,
        OperatorContext context,
        CancellationToken ct)
    {
        var result = await RunDetailedAsync(input, steps, context, includeDiagnostics: false, ct);
        return (result.FinalMesh, result.StepReports);
    }

    public Task<PipelineRunResult> RunDetailedAsync(
        MeshModel input,
        IEnumerable<IOperator> steps,
        OperatorContext context,
        CancellationToken ct)
        => RunDetailedAsync(input, steps, context, includeDiagnostics: true, ct);

    private static async Task<PipelineRunResult> RunDetailedAsync(
        MeshModel input,
        IEnumerable<IOperator> steps,
        OperatorContext context,
        bool includeDiagnostics,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var current = input;
        var reports = new List<OpReport>();
        var preDiagnostics = includeDiagnostics ? ReportCard.Build(input) : null;

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            context.Log($"Running {step.Id}...");
            var result = await step.RunAsync(current, context, ct);
            current = result.mesh;
            reports.Add(result.report);
        }

        var postDiagnostics = includeDiagnostics ? ReportCard.Build(current, reports) : null;
        stopwatch.Stop();
        return new PipelineRunResult(current, preDiagnostics, postDiagnostics, reports, stopwatch.Elapsed);
    }
}
