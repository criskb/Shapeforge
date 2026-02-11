using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Tests;

public class ThicknessEnforceOperatorTests
{
    [Fact]
    public async Task ThicknessEnforceOperator_ReportsDetectionAndWarning_WhenNoEditApplied()
    {
        var input = new MeshModel(
            Vertices: [0, 0, 0, 0.1f, 0, 0, 0, 1, 0],
            Indices: [0, 1, 2],
            Normals: null);

        var op = new ThicknessEnforceOperator(0.5f, ThicknessMode.Reshell);
        var ctx = new OperatorContext(0.2f, new Progress<float>(_ => { }), _ => { }, new Dictionary<string, object>(), "mm", ProcessMode.Fdm, PresetQuality.Final, MinimumWallPolicy.Strict, 1.2f, 45f, 0f, RepairMode.Balanced);

        var (mesh, report) = await op.RunAsync(input, ctx, CancellationToken.None);

        Assert.Equal(input.Vertices, mesh.Vertices);
        Assert.Equal(0, report.Metrics["enforcement.applied"]);
        Assert.True(report.Warnings.Count > 0);
        Assert.Contains("no geometry edit was applied", string.Join(' ', report.Notes), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ThicknessEnforceOperator_IsDeterministic_ForSameInputAndParameters()
    {
        var input = new MeshModel(
            Vertices: [0, 0, 0, 0.1f, 0, 0, 0.2f, 0, 0, 0, 1, 0],
            Indices: [0, 1, 3, 1, 2, 3],
            Normals: null);

        var op = new ThicknessEnforceOperator(0.5f, ThicknessMode.Inflate);
        var ctx = new OperatorContext(0.2f, new Progress<float>(_ => { }), _ => { }, new Dictionary<string, object>(), "mm", ProcessMode.Fdm, PresetQuality.Final, MinimumWallPolicy.Strict, 1.2f, 45f, 0f, RepairMode.Balanced);

        var (meshA, reportA) = await op.RunAsync(input, ctx, CancellationToken.None);
        var (meshB, reportB) = await op.RunAsync(input, ctx, CancellationToken.None);

        Assert.Equal(meshA.Vertices, meshB.Vertices);
        Assert.Equal(meshA.Indices, meshB.Indices);
        Assert.Equal(reportA.Metrics.OrderBy(k => k.Key).Select(k => k.Value), reportB.Metrics.OrderBy(k => k.Key).Select(k => k.Value));
        Assert.Equal(reportA.Warnings, reportB.Warnings);
    }
}
