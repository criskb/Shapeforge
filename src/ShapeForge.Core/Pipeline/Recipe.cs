using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline.SchemaMigrations;
using ShapeForge.Core.Pipeline.SchemaMigrations.Recipes;
using System.Globalization;
using System.Text.Json;

namespace ShapeForge.Core.Pipeline;

public enum RecipeVersion
{
    V1 = 1,
    V2 = 2
}

public sealed record RecipeStep(string Op, Dictionary<string, JsonElement> Params);

public sealed record ProfileDocument(
    string? Name = null,
    string? Units = null,
    ProcessMode? Mode = null,
    PresetQuality? Quality = null,
    float? VoxelSizeMm = null,
    float? MinWallMm = null,
    MinimumWallPolicy? MinWallPolicy = null,
    float? OverhangThresholdDeg = null,
    float? MinimumDrainHoleMm = null,
    ThicknessMode? ThicknessMode = null,
    RepairMode? RepairMode = null);

public sealed record RecipeDefinition(
    List<RecipeStep> Steps,
    Dictionary<string, Dictionary<string, JsonElement>>? OperatorOverrides = null);

public sealed record ValidationRuleSet(
    bool FailOnUnknownOperator = true,
    bool FailOnUnknownParams = true,
    bool FailOnIncompatibleUnits = true,
    IReadOnlyList<string>? AllowedUnits = null);

public sealed record PemDocument(
    string Name,
    ProfileDocument? Defaults,
    RecipeDefinition Recipe,
    ValidationRuleSet? Validation = null);

public sealed class RecipeValidationException : InvalidOperationException
{
    public RecipeValidationException(IReadOnlyList<string> errors)
        : base($"Recipe validation failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", errors)}")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed record RecipeDocument(
    int Version,
    ProfileDocument? Profile,
    RecipeDefinition Recipe,
    PemDocument? Pem = null)
{
    public static JsonSerializerOptions JsonOptions => SchemaJson.Options;

    public const int CurrentVersion = (int)RecipeVersion.V2;

    public RecipeVersion RecipeVersion => (RecipeVersion)Version;

    public static RecipeDocument CreateV2(ProfileDocument? profile, RecipeDefinition recipe, PemDocument? pem = null)
        => new(CurrentVersion, profile, recipe, pem);

    public static RecipeDocument FromJson(string json)
    {
        var normalized = RecipeSchemaMigrator.NormalizeToCurrent(json);
        return normalized with { Version = CurrentVersion };
    }

    public string ToJson() => JsonSerializer.Serialize(this with { Version = CurrentVersion }, JsonOptions);

    public RecipeDocument ToLatestVersion() => this with { Version = CurrentVersion };

    public PresetParameters ResolveEffectiveProfile(PresetParameters baseProfile, ProfileDocument? runtimeOverrides = null)
    {
        var resolved = ApplyProfileOverrides(baseProfile, Profile);
        resolved = ApplyProfileOverrides(resolved, Pem?.Defaults);
        resolved = ApplyProfileOverrides(resolved, runtimeOverrides);
        return resolved;
    }

    public void ValidateOrThrow(OperatorRegistry registry, ProfileDocument? runtimeOverrides = null)
    {
        var errors = Validate(registry, runtimeOverrides);
        if (errors.Count > 0)
        {
            throw new RecipeValidationException(errors);
        }
    }

    public IReadOnlyList<string> Validate(OperatorRegistry registry, ProfileDocument? runtimeOverrides = null)
    {
        var errors = new List<string>();
        var validation = Pem?.Validation ?? new ValidationRuleSet();

        var units = runtimeOverrides?.Units
            ?? Pem?.Defaults?.Units
            ?? Profile?.Units
            ?? "mm";

        if (validation.AllowedUnits is { Count: > 0 } && !validation.AllowedUnits.Contains(units, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Units '{units}' are not allowed by PEM validation policy. Allowed: {string.Join(", ", validation.AllowedUnits)}.");
        }

        for (var i = 0; i < Recipe.Steps.Count; i++)
        {
            var step = Recipe.Steps[i];
            if (!registry.TryGet(step.Op, out var op) || op is null)
            {
                if (validation.FailOnUnknownOperator)
                {
                    errors.Add($"Step #{i + 1}: unknown operator id '{step.Op}'. Registered operators: {string.Join(", ", registry.List().Select(o => o.Id))}.");
                }

                continue;
            }

            var schema = op.Schema;
            var paramsByName = schema.Parameters.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var suppliedParam in step.Params)
            {
                if (!paramsByName.TryGetValue(suppliedParam.Key, out var parameterSchema))
                {
                    if (validation.FailOnUnknownParams)
                    {
                        var known = string.Join(", ", schema.Parameters.Select(p => p.Name));
                        errors.Add($"Step #{i + 1} ({step.Op}): unknown parameter '{suppliedParam.Key}'. Known parameters: {known}.");
                    }

                    continue;
                }

                ValidateParameter(errors, i, step.Op, suppliedParam.Key, suppliedParam.Value, parameterSchema);

                if (validation.FailOnIncompatibleUnits && units.Equals("in", StringComparison.OrdinalIgnoreCase) && suppliedParam.Key.EndsWith("Mm", StringComparison.Ordinal))
                {
                    errors.Add($"Step #{i + 1} ({step.Op}): parameter '{suppliedParam.Key}' is expressed in millimeters but the resolved units are '{units}'. Use a non-mm parameter or switch units to 'mm'.");
                }
            }

            foreach (var requiredParameter in schema.Parameters.Where(p => p.Required))
            {
                if (!step.Params.Keys.Contains(requiredParameter.Name, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Step #{i + 1} ({step.Op}): missing required parameter '{requiredParameter.Name}'.");
                }
            }
        }

        return errors;
    }
    private static PresetParameters ApplyProfileOverrides(PresetParameters baseline, ProfileDocument? profile)
    {
        if (profile is null)
        {
            return baseline;
        }

        return baseline with
        {
            Mode = profile.Mode ?? baseline.Mode,
            Quality = profile.Quality ?? baseline.Quality,
            Units = string.IsNullOrWhiteSpace(profile.Units) ? baseline.Units : profile.Units.Trim(),
            VoxelSizeMm = profile.VoxelSizeMm ?? baseline.VoxelSizeMm,
            MinWallMm = profile.MinWallMm ?? baseline.MinWallMm,
            MinWallPolicy = profile.MinWallPolicy ?? baseline.MinWallPolicy,
            OverhangThresholdDeg = profile.OverhangThresholdDeg ?? baseline.OverhangThresholdDeg,
            MinimumDrainHoleMm = profile.MinimumDrainHoleMm ?? baseline.MinimumDrainHoleMm,
            ThicknessMode = profile.ThicknessMode ?? baseline.ThicknessMode,
            RepairMode = profile.RepairMode ?? baseline.RepairMode
        };
    }

    private static void ValidateParameter(
        List<string> errors,
        int stepIndex,
        string opId,
        string name,
        JsonElement value,
        OperatorParameterSchema parameterSchema)
    {
        var path = $"Step #{stepIndex + 1} ({opId}) parameter '{name}'";

        switch (parameterSchema.Type)
        {
            case OperatorParameterType.Number:
                if (!TryReadDouble(value, out var number))
                {
                    errors.Add($"{path} must be numeric.");
                    return;
                }

                ValidateNumberRange(errors, path, number, parameterSchema);
                break;

            case OperatorParameterType.Integer:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var integer))
                {
                    errors.Add($"{path} must be an integer.");
                    return;
                }

                ValidateNumberRange(errors, path, integer, parameterSchema);
                break;

            case OperatorParameterType.Boolean:
                if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                {
                    errors.Add($"{path} must be a boolean.");
                }

                break;

            case OperatorParameterType.String:
            case OperatorParameterType.Enum:
                if (value.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"{path} must be a string.");
                    return;
                }

                var stringValue = value.GetString() ?? string.Empty;
                if (parameterSchema.Type == OperatorParameterType.Enum && parameterSchema.AllowedValues is { Count: > 0 })
                {
                    if (!parameterSchema.AllowedValues.Contains(stringValue, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"{path} has invalid value '{stringValue}'. Allowed values: {string.Join(", ", parameterSchema.AllowedValues)}.");
                    }
                }

                break;

            case OperatorParameterType.Object:
                if (value.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"{path} must be a JSON object.");
                }

                break;
        }
    }

    private static bool TryReadDouble(JsonElement value, out double number)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetDouble(out number);
        }

        if (value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        number = 0;
        return false;
    }

    private static void ValidateNumberRange(List<string> errors, string path, double value, OperatorParameterSchema schema)
    {
        if (schema.Min.HasValue && value < schema.Min.Value)
        {
            errors.Add($"{path} value {value:0.###} is below minimum {schema.Min.Value:0.###}.");
        }

        if (schema.Max.HasValue && value > schema.Max.Value)
        {
            errors.Add($"{path} value {value:0.###} is above maximum {schema.Max.Value:0.###}.");
        }
    }
}
