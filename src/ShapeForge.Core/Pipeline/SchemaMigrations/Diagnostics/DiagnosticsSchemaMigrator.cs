using System.Text.Json;
using ShapeForge.Core.Diagnostics;

namespace ShapeForge.Core.Pipeline.SchemaMigrations.Diagnostics;

public static class DiagnosticsSchemaMigrator
{
    public const string CurrentVersion = MeshDiagnostics.CurrentSchemaVersion;

    public static MeshDiagnostics NormalizeToCurrent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return NormalizeToCurrent(doc.RootElement);
    }

    public static MeshDiagnostics NormalizeToCurrent(JsonElement payload)
    {
        var version = ResolveVersion(payload);
        var major = ParseMajor(version);
        if (major != 1)
        {
            throw new InvalidOperationException($"Unsupported diagnostics schema version '{version}'. Supported major versions: 1.x.");
        }

        var topology = ReadDoubleMap(payload, "topology");
        var quality = ReadDoubleMap(payload, "quality");
        var printability = ReadDoubleMap(payload, "printability");
        var issues = ReadIssues(payload);
        var counts = ReadLongMap(payload, "counts");
        var booleans = ReadBoolMap(payload, "booleans");

        return new MeshDiagnostics(CurrentVersion, topology, quality, printability, issues, counts, booleans);
    }

    private static string ResolveVersion(JsonElement payload)
    {
        if (payload.TryGetProperty("schemaVersion", out var schemaVersion))
        {
            return schemaVersion.GetString() ?? CurrentVersion;
        }

        if (payload.TryGetProperty("diagnosticsVersion", out var diagnosticsVersion))
        {
            return diagnosticsVersion.GetString() ?? CurrentVersion;
        }

        return CurrentVersion;
    }

    private static int ParseMajor(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return 1;
        }

        var majorRaw = version.Split('.', 2, StringSplitOptions.TrimEntries)[0];
        return int.TryParse(majorRaw, out var major) ? major : 1;
    }

    private static Dictionary<string, double> ReadDoubleMap(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, double>(StringComparer.Ordinal);
        }

        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var prop in node.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var value))
            {
                map[prop.Name] = value;
            }
        }

        return map;
    }

    private static Dictionary<string, long> ReadLongMap(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var prop in node.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var value))
            {
                map[prop.Name] = value;
            }
        }

        return map;
    }

    private static Dictionary<string, bool> ReadBoolMap(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, bool>(StringComparer.Ordinal);
        }

        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var prop in node.EnumerateObject())
        {
            if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                map[prop.Name] = prop.Value.GetBoolean();
            }
        }

        return map;
    }

    private static List<DiagnosticIssue> ReadIssues(JsonElement payload)
    {
        if (!payload.TryGetProperty("issues", out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<DiagnosticIssue>>(node.GetRawText(), SchemaMigrations.SchemaJson.Options) ?? [];
    }
}
