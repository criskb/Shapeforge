using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShapeForge.Core.Pipeline.SchemaMigrations;

public static class SchemaJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
