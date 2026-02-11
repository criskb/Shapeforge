using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;

namespace ShapeForge.Tests;

public class ReportCardTests
{
    [Fact]
    public void Build_ComputesTopologyAndQualityMetrics()
    {
        var mesh = new MeshModel(
            Vertices: [0, 0, 0, 1, 0, 0, 0, 1, 0],
            Indices: [0, 1, 2],
            Normals: null);

        var diagnostics = ReportCard.Build(mesh);

        Assert.Equal("1.0", diagnostics.SchemaVersion);
        Assert.Equal(1, diagnostics.Topology["triangles.count"]);
        Assert.Equal(3, diagnostics.Topology["vertices.count"]);
        Assert.True(diagnostics.Quality["triangles.area.avg"] > 0);
        Assert.Contains(diagnostics.Issues, i => i.Code == "mesh.low-triangle-count");
        Assert.Contains(diagnostics.Issues, i => i.Code == "mesh.normals.missing");
    }

    [Fact]
    public void Build_AddsPrintabilityAndOperatorWarningIssues()
    {
        var mesh = new MeshModel(
            Vertices: [0, 0, 0, 1, 0, 0, 0, 1, 0],
            Indices: [0, 1, 2],
            Normals: [0, 0, 1, 0, 0, 1, 0, 0, 1]);

        var report = new OpReport(
            "thickness.enforce",
            new Dictionary<string, double> { ["thin.vertices.after"] = 5 },
            ["Adaptive inflation reached movement cap."],
            []);

        var diagnostics = ReportCard.Build(mesh, [report]);

        Assert.Equal(5, diagnostics.Printability["thickness.enforce.thin.vertices.after"]);
        Assert.Contains(diagnostics.Issues, i => i.Code == "printability.thin-vertices" && i.Count == 5);
        Assert.Contains(diagnostics.Issues, i => i.Code == "operator.thickness.enforce.warning");
    }
}
