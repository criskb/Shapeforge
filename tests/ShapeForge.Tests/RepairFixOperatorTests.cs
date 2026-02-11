using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;

namespace ShapeForge.Tests;

public class RepairFixOperatorTests
{
    [Fact]
    public async Task RepairFixOperator_ReturnsMetricsWithoutTriangleExplosion()
    {
        var input = new MeshModel(
            Vertices: [0, 0, 0, 1, 0, 0, 0, 1, 0],
            Indices: [0, 1, 2],
            Normals: null);

        var op = new RepairFixOperator();
        var ctx = new OperatorContext(
            0.2f,
            new Progress<float>(_ => { }),
            _ => { },
            new Dictionary<string, object>());

        var (mesh, report) = await op.RunAsync(input, ctx, CancellationToken.None);

        Assert.True(mesh.Indices.Length <= input.Indices.Length * 2);
        Assert.Contains("triangles.before", report.Metrics.Keys);
        Assert.Contains("triangles.after", report.Metrics.Keys);
    }
}
