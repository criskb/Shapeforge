using ShapeForge.Core.Pipeline;

namespace ShapeForge.Core.Diagnostics;

public sealed class ReadinessEvaluator
{
    private readonly IReadOnlyList<IReadinessRule> _rules;

    public ReadinessEvaluator()
    {
        _rules = BuildDefaultRules();
    }

    public ReadinessResult Evaluate(MeshDiagnostics diagnostics, PresetParameters profile)
    {
        var readinessProfile = new ReadinessProfile(profile);
        var issues = new List<ReadinessIssue>();

        foreach (var rule in _rules)
        {
            var issue = rule.Evaluate(diagnostics, readinessProfile);
            if (issue is not null)
            {
                issues.Add(issue);
            }
        }

        foreach (var diagnosticIssue in diagnostics.Issues)
        {
            if (TryMapDiagnosticIssue(diagnosticIssue, out var mappedIssue))
            {
                issues.Add(mappedIssue);
            }
        }

        var deduped = issues
            .GroupBy(i => i.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(i => i.Priority).ThenByDescending(i => i.Severity).First())
            .OrderByDescending(i => i.Priority)
            .ThenByDescending(i => i.Severity)
            .ThenBy(i => i.Code, StringComparer.Ordinal)
            .ToList();

        var status = ResolveStatus(deduped);
        var grade = ResolveGrade(status);
        var topBlockers = deduped.Where(i => i.Severity >= IssueSeverity.Warning).Take(3).ToList();
        var confidenceScore = deduped.Count == 0 ? 0.95 : Math.Clamp(deduped.Average(i => i.Confidence), 0.0, 0.99);
        var confidenceNote = $"Confidence is {(int)Math.Round(confidenceScore * 100)}% based on available mesh metrics and operator reports.";

        return new ReadinessResult(grade, status, deduped, topBlockers, confidenceNote, confidenceScore);
    }

    private static ReadinessTrafficLight ResolveStatus(IReadOnlyList<ReadinessIssue> issues)
    {
        if (issues.Any(i => i.Severity == IssueSeverity.Error))
        {
            return ReadinessTrafficLight.Red;
        }

        if (issues.Any(i => i.Severity == IssueSeverity.Warning))
        {
            return ReadinessTrafficLight.Yellow;
        }

        return ReadinessTrafficLight.Green;
    }

    private static ReadinessGrade ResolveGrade(ReadinessTrafficLight status)
        => status switch
        {
            ReadinessTrafficLight.Green => ReadinessGrade.Ready,
            ReadinessTrafficLight.Yellow => ReadinessGrade.NeedsAttention,
            _ => ReadinessGrade.Blocked
        };

    private static bool TryMapDiagnosticIssue(DiagnosticIssue issue, out ReadinessIssue mapped)
    {
        var hint = issue.Code switch
        {
            "mesh.invalid-indices" => "Run repair.fix to rebuild invalid faces before slicing.",
            "mesh.degenerate-triangles" => "Run repair.fix and simplify tiny sliver triangles.",
            "mesh.duplicate-triangles" => "Deduplicate faces with repair.fix to avoid over-extrusion artifacts.",
            "mesh.normals.missing" => "Regenerate normals during export or in repair.fix for stable shading.",
            "mesh.low-triangle-count" => "Increase tessellation or export at higher resolution.",
            "mesh.non-manifold" => "Run repair.fix in aggressive mode to resolve non-manifold edges.",
            "mesh.not-watertight" => "Small hole fill recommended before hollowing and slicing.",
            "printability.thin-vertices" => "Use thickness.enforce or increase wall design thickness.",
            _ when issue.Code.StartsWith("operator.repair.fix", StringComparison.OrdinalIgnoreCase)
                => "Review repair.fix warnings and re-run with a stronger repair mode.",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(hint))
        {
            mapped = default!;
            return false;
        }

        mapped = new ReadinessIssue(
            issue.Code,
            issue.Severity,
            issue.Message,
            hint,
            PriorityFromSeverity(issue.Severity),
            "diagnostic-issue-map",
            Confidence: 0.75);
        return true;
    }

    private static int PriorityFromSeverity(IssueSeverity severity)
        => severity switch
        {
            IssueSeverity.Error => 100,
            IssueSeverity.Warning => 70,
            _ => 40
        };

    private static IReadOnlyList<IReadinessRule> BuildDefaultRules()
    {
        return
        [
            new ThresholdRule(
                name: "wall-threshold",
                metricKey: "thickness.enforce.wall.min.mm",
                threshold: p => p.MinWallMm,
                isFailure: static (wallMin, profileMin) => wallMin < profileMin,
                issueCode: "readiness.wall-below-profile-min",
                severity: IssueSeverity.Warning,
                message: "Minimum wall thickness is below the selected profile minimum.",
                priority: 90,
                hint: "Use thickness.enforce or increase local wall thickness in CAD."),
            new RatioRule(
                name: "overhang-ratio",
                numeratorMetric: "print.overhang.area",
                denominatorMetric: "print.surface.area",
                maxRatio: p => p.OverhangThresholdDeg <= 35 ? 0.2 : 0.35,
                issueCode: "readiness.high-overhang-ratio",
                severity: IssueSeverity.Warning,
                message: "Overhang area ratio exceeds the profile tolerance.",
                priority: 80,
                hint: "Rotate part orientation or add supports for overhang-heavy regions."),
            new TopologyRule(
                name: "topology-watertight",
                boolMetricKey: "mesh.is-watertight",
                expectedValue: true,
                issueCode: "mesh.not-watertight",
                severity: IssueSeverity.Error,
                message: "Mesh is not watertight.",
                priority: 110,
                hint: "Small hole fill recommended before generating toolpaths."),
            new TopologyRule(
                name: "topology-manifold",
                boolMetricKey: "mesh.is-manifold",
                expectedValue: true,
                issueCode: "mesh.non-manifold",
                severity: IssueSeverity.Error,
                message: "Mesh has non-manifold topology.",
                priority: 105,
                hint: "Run repair.fix in aggressive mode to remove non-manifold edges.")
        ];
    }

    private sealed record ReadinessProfile(PresetParameters Source) : IReadinessProfile
    {
        public float MinWallMm => Source.MinWallMm;
        public float OverhangThresholdDeg => Source.OverhangThresholdDeg;
    }
}
