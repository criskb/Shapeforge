using System.Text.Json;
using ShapeForge.Core.Diagnostics;

namespace ShapeForge.Tests;

internal sealed class FixtureRegistryEntry
{
    public required string FixtureId { get; init; }
    public required string MeshPath { get; init; }
    public bool BaselineHasWarningsOrErrors { get; init; }
    public IReadOnlyList<ExpectedIssue> RequiredIssues { get; init; } = [];
    public PostFixExpectation? PostFix { get; init; }
}

internal sealed class ExpectedIssue
{
    public required string Code { get; init; }
    public IssueSeverity? Severity { get; init; }
    public int? Count { get; init; }
}

internal sealed class PostFixExpectation
{
    public int? TrianglesBefore { get; init; }
    public int? TrianglesAfter { get; init; }
    public IReadOnlyDictionary<string, int> Metrics { get; init; } = new Dictionary<string, int>();
}

internal static class FixtureRegistry
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string FixturesDir = Path.Combine(RepoRoot, "tests", "ShapeForge.Tests", "Fixtures");

    public static FixtureRegistryEntry Load(string fixtureId)
    {
        var manifestPath = Directory
            .EnumerateFiles(FixturesDir, "expected.json", SearchOption.AllDirectories)
            .FirstOrDefault(path => IsFixtureManifest(path, fixtureId));

        if (manifestPath is null)
        {
            throw new InvalidOperationException($"Could not locate expected.json for fixture '{fixtureId}'.");
        }

        using var stream = File.OpenRead(manifestPath);
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;

        var meshFile = root.GetProperty("meshFile").GetString()
            ?? throw new InvalidOperationException($"Fixture '{fixtureId}' has no meshFile.");

        var baseline = root.GetProperty("baselineDiagnostics");
        var requiredIssues = baseline.GetProperty("requiredIssues")
            .EnumerateArray()
            .Select(ParseIssue)
            .ToArray();

        PostFixExpectation? postFix = null;
        if (root.TryGetProperty("postFix", out var postFixNode))
        {
            var metrics = new Dictionary<string, int>(StringComparer.Ordinal);
            if (postFixNode.TryGetProperty("metrics", out var metricsNode))
            {
                foreach (var prop in metricsNode.EnumerateObject())
                {
                    metrics[prop.Name] = prop.Value.GetInt32();
                }
            }

            postFix = new PostFixExpectation
            {
                TrianglesBefore = postFixNode.TryGetProperty("trianglesBefore", out var beforeNode) ? beforeNode.GetInt32() : null,
                TrianglesAfter = postFixNode.TryGetProperty("trianglesAfter", out var afterNode) ? afterNode.GetInt32() : null,
                Metrics = metrics
            };
        }

        return new FixtureRegistryEntry
        {
            FixtureId = fixtureId,
            MeshPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, meshFile),
            BaselineHasWarningsOrErrors = baseline.GetProperty("hasWarningsOrErrors").GetBoolean(),
            RequiredIssues = requiredIssues,
            PostFix = postFix
        };
    }

    private static ExpectedIssue ParseIssue(JsonElement node)
    {
        IssueSeverity? severity = null;
        if (node.TryGetProperty("severity", out var severityNode))
        {
            severity = Enum.Parse<IssueSeverity>(severityNode.GetString()!, ignoreCase: true);
        }

        return new ExpectedIssue
        {
            Code = node.GetProperty("code").GetString()!,
            Severity = severity,
            Count = node.TryGetProperty("count", out var countNode) ? countNode.GetInt32() : null
        };
    }

    private static bool IsFixtureManifest(string path, string fixtureId)
    {
        using var stream = File.OpenRead(path);
        using var json = JsonDocument.Parse(stream);
        if (!json.RootElement.TryGetProperty("fixtureId", out var idNode))
        {
            return false;
        }

        return string.Equals(idNode.GetString(), fixtureId, StringComparison.Ordinal);
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
