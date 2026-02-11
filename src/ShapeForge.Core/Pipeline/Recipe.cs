using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShapeForge.Core.Pipeline;

public sealed record RecipeStep(string Op, Dictionary<string, JsonElement> Params);

public sealed record RecipeDocument(int Version, string Units, List<RecipeStep> Steps)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static RecipeDocument FromJson(string json) =>
        JsonSerializer.Deserialize<RecipeDocument>(json, JsonOptions)
        ?? throw new InvalidOperationException("Invalid recipe JSON.");

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
