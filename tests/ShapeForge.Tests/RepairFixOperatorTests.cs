using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;

namespace ShapeForge.Tests;

public class RepairFixOperatorTests
{
    [Fact]
    public async Task RepairFixOperator_RemovesDegeneratesAndDuplicates_AndReportsMetrics()
    {
        var input = new MeshModel(
            Vertices:
            [
                0, 0, 0,
                1, 0, 0,
                1, 1, 0,
                0, 1, 0,
                1.00001f, 1.00001f, 0 // near-duplicate to weld
            ],
            Indices:
            [
                0, 1, 2,
                0, 2, 3,
                0, 1, 2, // exact duplicate face
                0, 4, 4 // degenerate
            ],
            Normals: null);

        var op = new RepairFixOperator(closeRadiusMm: 0f);
        var ctx = new OperatorContext(
            0.2f,
            new Progress<float>(_ => { }),
            _ => { },
            new Dictionary<string, object>());

        var (mesh, report) = await op.RunAsync(input, ctx, CancellationToken.None);

        Assert.Equal(2, mesh.Indices.Length / 3);
        Assert.True(report.Metrics["vertex.weld.merged"] >= 1);
        Assert.Equal(1, report.Metrics["triangles.removed.degenerate"]);
        Assert.Equal(1, report.Metrics["triangles.removed.duplicate"]);
    }

    [Fact]
    public async Task RepairFixOperator_IsDeterministic_ForSameInputAndParameters()
    {
        var input = new MeshModel(
            Vertices: [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1],
            Indices: [0, 1, 2, 0, 2, 3],
            Normals: null);

        var op = new RepairFixOperator(closeRadiusMm: 0.3f);
        var ctx = new OperatorContext(0.1f, new Progress<float>(_ => { }), _ => { }, new Dictionary<string, object>());

        var (meshA, reportA) = await op.RunAsync(input, ctx, CancellationToken.None);
        var (meshB, reportB) = await op.RunAsync(input, ctx, CancellationToken.None);

        Assert.Equal(meshA.Vertices, meshB.Vertices);
        Assert.Equal(meshA.Indices, meshB.Indices);
        Assert.Equal(reportA.Metrics.OrderBy(k => k.Key).Select(k => k.Value), reportB.Metrics.OrderBy(k => k.Key).Select(k => k.Value));
    }
}
