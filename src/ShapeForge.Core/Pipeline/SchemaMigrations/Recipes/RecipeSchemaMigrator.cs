using System.Text.Json;

namespace ShapeForge.Core.Pipeline.SchemaMigrations.Recipes;

public static class RecipeSchemaMigrator
{
    public const int CurrentVersion = (int)RecipeVersion.V2;

    public static RecipeDocument NormalizeToCurrent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return NormalizeToCurrent(doc.RootElement);
    }

    public static RecipeDocument NormalizeToCurrent(JsonElement root)
    {
        var version = ResolveVersion(root);
        return version switch
        {
            (int)RecipeVersion.V1 => MigrateFromV1(root),
            (int)RecipeVersion.V2 => JsonSerializer.Deserialize<RecipeDocument>(root.GetRawText(), SchemaMigrations.SchemaJson.Options)
                ?? throw new InvalidOperationException("Invalid recipe JSON for version 2."),
            _ => throw new InvalidOperationException($"Unsupported recipe version '{version}'. Supported versions: 1, 2.")
        };
    }

    private static int ResolveVersion(JsonElement root)
    {
        if (root.TryGetProperty("recipeVersion", out var recipeVersionNode) && recipeVersionNode.ValueKind == JsonValueKind.Number)
        {
            return recipeVersionNode.GetInt32();
        }

        if (root.TryGetProperty("version", out var versionNode) && versionNode.ValueKind == JsonValueKind.Number)
        {
            return versionNode.GetInt32();
        }

        return (int)RecipeVersion.V1;
    }

    private static RecipeDocument MigrateFromV1(JsonElement root)
    {
        var units = root.TryGetProperty("units", out var unitsNode) ? unitsNode.GetString() : null;
        var steps = root.TryGetProperty("steps", out var stepsNode)
            ? JsonSerializer.Deserialize<List<RecipeStep>>(stepsNode.GetRawText(), SchemaMigrations.SchemaJson.Options) ?? []
            : [];

        return new RecipeDocument(
            Version: CurrentVersion,
            Profile: new ProfileDocument(Units: units),
            Recipe: new RecipeDefinition(steps),
            Pem: null);
    }
}
