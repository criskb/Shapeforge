using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;
using System.Diagnostics;

namespace ShapeForge.Tests;

public class PerformanceGuardrailsTests
{
    [Fact]
    public void AdaptiveSamplingCap_Decreases_ForLargeMeshes()
    {
        Assert.Equal(1f, PerformanceGuardrails.AdaptiveSamplingCap(10_000));
        Assert.True(PerformanceGuardrails.AdaptiveSamplingCap(200_000) < 1f);
        Assert.True(PerformanceGuardrails.AdaptiveSamplingCap(2_000_000) >= 0.2f);
    }

    [Fact]
    public async Task PreviewMode_SkipsThicknessEnforce_WhenTriangleLimitExceeded()
    {
        var input = BuildFlatMesh(triangleCount: PerformanceGuardrails.PreviewMaxTriangles + 10_000);
        var ctx = BuildContext(ExecutionMode.Preview);
        var runner = new PipelineRunner();

        var run = await runner.RunDetailedAsync(input, [new ThicknessEnforceOperator(1.2f, ThicknessMode.Inflate)], ctx, CancellationToken.None);

        var report = Assert.Single(run.StepReports);
        Assert.Contains(report.Warnings, w => w.Contains("operator skipped", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(PerformanceGuardrails.PreviewMaxTriangles, report.Metrics["perf.preview.triangle.limit"]);
    }

    [Fact]
    public async Task RepairFixOperator_PerformanceBudget_RelaxedForCi()
    {
        var input = BuildFlatMesh(triangleCount: 60_000);
        var ctx = BuildContext(ExecutionMode.Standard);
        var op = new RepairFixOperator();

        var sw = Stopwatch.StartNew();
        await op.RunAsync(input, ctx, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8), $"Expected repair pass under relaxed CI budget, got {sw.Elapsed}.");
    }

    private static OperatorContext BuildContext(ExecutionMode mode)
    {
        return new OperatorContext(
            0.2f,
            new Progress<float>(_ => { }),
            _ => { },
            new Dictionary<string, object>(),
            "mm",
            ProcessMode.Fdm,
            mode == ExecutionMode.Preview ? PresetQuality.Preview : PresetQuality.Final,
            MinimumWallPolicy.Strict,
            1.2f,
            45f,
            0f,
            RepairMode.Balanced,
            mode,
            QualityScalingPolicy.ForMode(mode),
            1337);
    }

    private static MeshModel BuildFlatMesh(int triangleCount)
    {
        var vertices = new float[triangleCount * 9];
        var indices = new int[triangleCount * 3];

        for (var i = 0; i < triangleCount; i++)
        {
            var vOffset = i * 9;
            var iOffset = i * 3;
            var x = (i % 1000) * 0.01f;
            var y = (i / 1000) * 0.01f;

            vertices[vOffset] = x;
            vertices[vOffset + 1] = y;
            vertices[vOffset + 2] = 0;

            vertices[vOffset + 3] = x + 0.005f;
            vertices[vOffset + 4] = y;
            vertices[vOffset + 5] = 0;

            vertices[vOffset + 6] = x;
            vertices[vOffset + 7] = y + 0.005f;
            vertices[vOffset + 8] = 0;

            indices[iOffset] = iOffset;
            indices[iOffset + 1] = iOffset + 1;
            indices[iOffset + 2] = iOffset + 2;
        }

        return new MeshModel(vertices, indices, null);
    }
}
