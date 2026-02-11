namespace ShapeForge.Core.Diagnostics;

public record MeshDiagnostics(
    string SchemaVersion,
    Dictionary<string, double> Topology,
    Dictionary<string, double> Quality,
    Dictionary<string, double> Printability,
    List<DiagnosticIssue> Issues)
{
    public bool HasWarningsOrErrors => Issues.Any(i => i.Severity >= IssueSeverity.Warning);
}
