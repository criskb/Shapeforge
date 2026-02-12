using System.Text.Json;

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
    public const string CurrentSchemaVersion = "1.0";

    public IReadOnlyDictionary<string, long> CountMetrics { get; init; } = Counts ?? new Dictionary<string, long>();
    public IReadOnlyDictionary<string, bool> BooleanFlags { get; init; } = Booleans ?? new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, double> NumericPrintability => Printability;
    public bool HasWarningsOrErrors => Issues.Any(i => i.Severity >= IssueSeverity.Warning);

    public static MeshDiagnostics FromJson(string json)
        => SchemaMigrations.Diagnostics.DiagnosticsSchemaMigrator.NormalizeToCurrent(json);

    public string ToJson()
        => JsonSerializer.Serialize(this with { SchemaVersion = CurrentSchemaVersion }, SchemaMigrations.SchemaJson.Options);
}
