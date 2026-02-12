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
            var triangleCount = current.Indices.Length / 3;
            var meshTier = PerformanceGuardrails.ResolveTier(triangleCount);
            var adaptiveSamplingCap = PerformanceGuardrails.AdaptiveSamplingCap(triangleCount);
            var previewLimitExceeded = context.ExecutionMode == ExecutionMode.Preview && PerformanceGuardrails.ExceedsPreviewTriangleLimit(triangleCount);

            context.Scratch["perf.mesh.triangleCount"] = triangleCount;
            context.Scratch["perf.mesh.tier"] = meshTier.ToString();
            context.Scratch["perf.adaptive.sampling.cap"] = adaptiveSamplingCap;
            context.Scratch["perf.max.sample.vertices"] = PerformanceGuardrails.MaxSampleVerticesFor(meshTier);
            context.Scratch["perf.preview.triangle.limit"] = PerformanceGuardrails.PreviewMaxTriangles;
            context.Scratch["perf.preview.triangle.limit.exceeded"] = previewLimitExceeded;

            if (previewLimitExceeded && string.Equals(step.Id, ThicknessEnforceOperator.CanonicalId, StringComparison.OrdinalIgnoreCase))
            {
                context.Log($"Skipping {step.Id}: preview safeguard exceeded triangle threshold ({triangleCount:N0} > {PerformanceGuardrails.PreviewMaxTriangles:N0}).");
                reports.Add(new OpReport(
                    step.DisplayName,
                    new Dictionary<string, double>
                    {
                        ["perf.preview.triangle.limit"] = PerformanceGuardrails.PreviewMaxTriangles,
                        ["perf.triangles.current"] = triangleCount
                    },
                    new List<string> { "preview triangle threshold exceeded; operator skipped to keep responsive interaction." },
                    new List<string> { $"meshTier={meshTier}", "fallback=diagnostic-only" },
                    ModeAdjustedParams: BuildModeAdjustedParams(context, step.Id),
                    Elapsed: TimeSpan.Zero));
                continue;
            }

            context.Progress.Report(0f);
            context.Log($"Running {step.Id} ({context.ExecutionMode})...");

            var perOperatorPolicy = context.ScalingPolicy.ForOperator(step.Id);
            perOperatorPolicy = perOperatorPolicy with
            {
                SamplingDensityScale = MathF.Min(perOperatorPolicy.SamplingDensityScale, adaptiveSamplingCap)
            };
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
            ["sampling.adaptive.cap"] = context.Scratch.TryGetValue("perf.adaptive.sampling.cap", out var cap) && cap is float capValue ? capValue : 1.0,
            ["voxel.size.mm"] = context.VoxelSizeMm,
            ["smoothing.passes"] = context.ScalingPolicy.SmoothingPasses,
            ["perf.preview.triangle.limit"] = context.Scratch.TryGetValue("perf.preview.triangle.limit", out var limit) && limit is int threshold ? threshold : PerformanceGuardrails.PreviewMaxTriangles,
            ["perf.preview.limit.exceeded"] = context.Scratch.TryGetValue("perf.preview.triangle.limit.exceeded", out var exceeded) && exceeded is bool exceededBool && exceededBool ? 1 : 0,
            ["seed"] = context.DeterministicSeedFor(operatorId)
        };
    }
}
