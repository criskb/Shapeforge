using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.IO;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Tests;

public class ReadinessEvaluatorTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string FixturesDir = Path.Combine(RepoRoot, "tests", "ShapeForge.Tests", "Fixtures");

    [Fact]
    public void RulePrimitives_TriggerExpectedIssues()
    {
        var diagnostics = new MeshDiagnostics(
            "1.0",
            new Dictionary<string, double>
            {
                ["print.surface.area"] = 100,
                ["mesh.is-watertight"] = 0,
                ["mesh.is-manifold"] = 1
            },
            new Dictionary<string, double>(),
            new Dictionary<string, double>
            {
                ["thickness.enforce.wall.min.mm"] = 0.6,
                ["print.overhang.area"] = 45
            },
            new List<DiagnosticIssue>(),
            Booleans: new Dictionary<string, bool>
            {
                ["mesh.is-watertight"] = false,
                ["mesh.is-manifold"] = true
            });

        var profile = Presets.Resolve(PrintPreset.Fdm);
        var evaluator = new ReadinessEvaluator();

        var readiness = evaluator.Evaluate(diagnostics, profile);

        Assert.Contains(readiness.Issues, i => i.Code == "readiness.wall-below-profile-min");
        Assert.Contains(readiness.Issues, i => i.Code == "readiness.high-overhang-ratio");
        Assert.Contains(readiness.Issues, i => i.Code == "mesh.not-watertight");
        Assert.Equal(ReadinessTrafficLight.Red, readiness.Status);
    }

    [Fact]
    public async Task Evaluator_ProducesDeterministicSeverity_ForFixtureMeshes()
    {
        var io = new StlMeshIO();
        var watertight = await io.LoadStlAsync(Path.Combine(FixturesDir, "cube_ok.stl"));
        var leaky = await io.LoadStlAsync(Path.Combine(FixturesDir, "nonmanifold_edge.stl"));

        var evaluator = new ReadinessEvaluator();
        var profile = Presets.Resolve(PrintPreset.Fdm);

        var watertightReadiness = evaluator.Evaluate(ReportCard.Build(watertight), profile);
        var leakyReadiness = evaluator.Evaluate(ReportCard.Build(leaky), profile);

        Assert.Equal(ReadinessTrafficLight.Yellow, watertightReadiness.Status);
        Assert.Equal(ReadinessGrade.NeedsAttention, watertightReadiness.Grade);
        Assert.Contains(watertightReadiness.TopBlockers, b => b.Code == "mesh.low-triangle-count");

        Assert.Equal(ReadinessTrafficLight.Red, leakyReadiness.Status);
        Assert.Equal(ReadinessGrade.Blocked, leakyReadiness.Grade);
        Assert.Contains(leakyReadiness.TopBlockers, b => b.Code == "mesh.not-watertight");
        Assert.Contains(leakyReadiness.TopBlockers, b => b.RemediationHint.Contains("hole fill", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ShapeForge.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
