namespace ShapeForge.Core.Diagnostics;

public enum ReadinessTrafficLight
{
    Green,
    Yellow,
    Red
}

public enum ReadinessGrade
{
    Ready,
    NeedsAttention,
    Blocked
}

public record ReadinessIssue(
    string Code,
    IssueSeverity Severity,
    string Message,
    string RemediationHint,
    int Priority,
    string Rule,
    double Confidence = 0.8);

public record ReadinessResult(
    ReadinessGrade Grade,
    ReadinessTrafficLight Status,
    IReadOnlyList<ReadinessIssue> Issues,
    IReadOnlyList<ReadinessIssue> TopBlockers,
    string ConfidenceNote,
    double ConfidenceScore);
