namespace ShapeForge.Core.Diagnostics;

public record MeshDiagnostics(
    string SchemaVersion,
    Dictionary<string, double> Topology,
    Dictionary<string, double> Quality,
    Dictionary<string, double> Printability,
    List<DiagnosticIssue> Issues,
    Dictionary<string, long>? Counts = null,
    Dictionary<string, bool>? Booleans = null)
{
    public IReadOnlyDictionary<string, long> CountMetrics { get; init; } = Counts ?? new Dictionary<string, long>();
    public IReadOnlyDictionary<string, bool> BooleanFlags { get; init; } = Booleans ?? new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, double> NumericPrintability => Printability;
    public bool HasWarningsOrErrors => Issues.Any(i => i.Severity >= IssueSeverity.Warning);
}
