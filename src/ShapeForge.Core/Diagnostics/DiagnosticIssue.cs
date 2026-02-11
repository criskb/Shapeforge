namespace ShapeForge.Core.Diagnostics;

public enum IssueSeverity
{
    Info,
    Warning,
    Error
}

public record DiagnosticIssue(
    IssueSeverity Severity,
    string Code,
    string Message,
    int Count = 1,
    Dictionary<string, string>? Details = null);
