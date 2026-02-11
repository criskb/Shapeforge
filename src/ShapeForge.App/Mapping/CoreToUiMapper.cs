using ShapeForge.App.ViewModels;
using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.App.Mapping;

public static class CoreToUiMapper
{
    public static void MapDiagnostics(DiagnosticsPanelViewModel target, MeshDiagnostics diagnostics)
    {
        target.TriangleCount = diagnostics.CountMetrics.TryGetValue("triangles.count", out var triangles)
            ? triangles.ToString("N0")
            : "-";

        target.VolumeDelta = diagnostics.Topology.TryGetValue("bounds.volume", out var boundsVolume)
            ? $"{boundsVolume:F2}"
            : "-";

        target.MinThickness = diagnostics.NumericPrintability
            .Where(m => m.Key.Contains("min", StringComparison.OrdinalIgnoreCase) && m.Key.Contains("wall", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Value)
            .DefaultIfEmpty(0d)
            .Min()
            .ToString("F2");

        target.TrappedVolumes = diagnostics.NumericPrintability.TryGetValue("print.trapped-volumes.count", out var trapped)
            ? trapped.ToString("N0")
            : "0";

        target.Issues.Clear();
        foreach (var issue in diagnostics.Issues)
        {
            target.Issues.Add(new DiagnosticIssueItemViewModel(issue.Severity.ToString(), issue.Code, issue.Message, issue.Count));
        }
    }

    public static void MapReadiness(ReadinessSummaryViewModel target, ReadinessResult readiness)
    {
        target.Status = readiness.Status.ToString();
        target.Grade = readiness.Grade.ToString();
        target.Confidence = $"{readiness.ConfidenceScore:P0} — {readiness.ConfidenceNote}";

        target.TopBlockers.Clear();
        foreach (var blocker in readiness.TopBlockers)
        {
            target.TopBlockers.Add(new ReadinessIssueItemViewModel(blocker.Code, blocker.Message, blocker.Severity.ToString()));
        }
    }

    public static void MapOperatorStack(OperatorStackViewModel target, IEnumerable<IOperator> operators)
    {
        target.Operators.Clear();
        foreach (var op in operators)
        {
            target.Operators.Add(new OperatorItemViewModel(op.Id, op.DisplayName, op.Schema.Category, op.Schema.Version));
        }

        target.RaiseSummaryChanged();
    }

    public static void MapPipelineRun(PipelineRunViewModel target, PipelineRunResult run)
    {
        target.Elapsed = run.Elapsed.ToString(@"mm\:ss\.fff");
        target.Steps.Clear();

        foreach (var report in run.StepReports)
        {
            var warningCount = report.Warnings.Count + report.StructuredIssues.Count(i => i.Severity >= IssueSeverity.Warning);
            var duration = run.StepElapsed is not null && run.StepElapsed.TryGetValue(report.Name, out var elapsed)
                ? elapsed.ToString(@"ss\.fff") + "s"
                : "-";
            target.Steps.Add(new PipelineStepViewModel(report.Name, duration, warningCount));
        }
    }
}
