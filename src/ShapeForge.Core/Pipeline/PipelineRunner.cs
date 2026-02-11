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
        var stepElapsed = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        var preDiagnostics = includeDiagnostics ? ReportCard.Build(input) : null;

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            context.Progress.Report(0f);
            context.Log($"Running {step.Id} ({context.ExecutionMode})...");

            var perOperatorPolicy = context.ScalingPolicy.ForOperator(step.Id);
            var opContext = context with
            {
                ScalingPolicy = perOperatorPolicy,
                VoxelSizeMm = MathF.Max(0.0001f, context.VoxelSizeMm * perOperatorPolicy.VoxelSizeScale)
            };

            var stepStopwatch = Stopwatch.StartNew();
            var result = await step.RunAsync(current, opContext, ct);
            stepStopwatch.Stop();
            ct.ThrowIfCancellationRequested();

            current = result.mesh;
            var adjustedParams = BuildModeAdjustedParams(opContext, step.Id);
            reports.Add(result.report with { ModeAdjustedParams = adjustedParams, Elapsed = stepStopwatch.Elapsed });
            stepElapsed[step.Id] = stepStopwatch.Elapsed;

            context.Progress.Report(1f);
        }

        var postDiagnostics = includeDiagnostics ? ReportCard.Build(current, reports) : null;
        stopwatch.Stop();
        return new PipelineRunResult(current, preDiagnostics, postDiagnostics, reports, stopwatch.Elapsed, stepElapsed);
    }

    private static Dictionary<string, double> BuildModeAdjustedParams(OperatorContext context, string operatorId)
    {
        return new Dictionary<string, double>
        {
            ["execution.mode"] = (double)context.ExecutionMode,
            ["sampling.density.scale"] = context.ScalingPolicy.SamplingDensityScale,
            ["voxel.size.mm"] = context.VoxelSizeMm,
            ["smoothing.passes"] = context.ScalingPolicy.SmoothingPasses,
            ["seed"] = context.DeterministicSeedFor(operatorId)
        };
    }
}
