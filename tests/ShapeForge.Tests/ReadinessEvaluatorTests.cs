using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.IO;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Tests;

public class ReadinessEvaluatorTests
{
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
        var watertightFixture = FixtureRegistry.Load("cube_ok");
        var leakyFixture = FixtureRegistry.Load("nonmanifold_edge");
        var watertight = await io.LoadStlAsync(watertightFixture.MeshPath);
        var leaky = await io.LoadStlAsync(leakyFixture.MeshPath);

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
}