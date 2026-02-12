using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Core.Operators;

public enum OperatorParameterType
{
    String,
    Number,
    Integer,
    Boolean,
    Enum,
    Object
}

public record OperatorParameterSchema(
    string Name,
    OperatorParameterType Type,
    string Description,
    bool Required = false,
    object? DefaultValue = null,
    IReadOnlyList<string>? AllowedValues = null,
    double? Min = null,
    double? Max = null);

public record OperatorSchema(
    string Id,
    string DisplayName,
    string Version,
    string Description,
    IReadOnlyList<OperatorParameterSchema> Parameters,
    string Category = "analysis.generic",
    bool Deterministic = true,
    double EstimatedCost = 1.0,
    BackendCapabilityFlags RequiredBackendCapabilities = BackendCapabilityFlags.None,
    IReadOnlyList<ProcessMode>? SupportedModes = null,
    IReadOnlyList<PresetQuality>? SupportedQualities = null)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string DefaultCategory = "analysis.generic";

    public static OperatorSchema Empty(string id, string displayName)
        => new(id, displayName, CurrentSchemaVersion, string.Empty, Array.Empty<OperatorParameterSchema>(), DefaultCategory);
}

[Flags]
public enum BackendCapabilityFlags
{
    None = 0,
    FastMesh = 1 << 0,
    Voxel = 1 << 1
}

public enum OperatorSupportLevel
{
    Supported,
    Limited,
    NotSupported
}

public sealed record OperatorSupportResult(
    OperatorSupportLevel Level,
    string Reason);

public static class OperatorSupportEvaluator
{
    public static OperatorSupportResult Evaluate(
        OperatorSchema schema,
        ProcessMode mode,
        PresetQuality quality,
        BackendCapabilityFlags availableBackends)
    {
        if (schema.SupportedModes is { Count: > 0 } && !schema.SupportedModes.Contains(mode))
        {
            return new OperatorSupportResult(
                OperatorSupportLevel.NotSupported,
                $"mode '{mode}' is unsupported; supported modes: {string.Join(", ", schema.SupportedModes)}");
        }

        if (schema.SupportedQualities is { Count: > 0 } && !schema.SupportedQualities.Contains(quality))
        {
            return new OperatorSupportResult(
                OperatorSupportLevel.NotSupported,
                $"quality '{quality}' is unsupported; supported qualities: {string.Join(", ", schema.SupportedQualities)}");
        }

        var missing = schema.RequiredBackendCapabilities & ~availableBackends;
        if (missing != BackendCapabilityFlags.None)
        {
            return new OperatorSupportResult(
                OperatorSupportLevel.Limited,
                $"missing backend capability '{missing}', fallback behavior may be used");
        }

        return new OperatorSupportResult(OperatorSupportLevel.Supported, "supported");
    }
}

public record OpReport(
    string Name,
    Dictionary<string, double> Metrics,
    List<string> Warnings,
    List<string> Notes,
    List<DiagnosticIssue>? Issues = null,
    Dictionary<string, double>? ModeAdjustedParams = null,
    TimeSpan? Elapsed = null)
{
    public IReadOnlyList<DiagnosticIssue> StructuredIssues { get; init; } = Issues ?? new List<DiagnosticIssue>();
    public IReadOnlyDictionary<string, double> ModeAdjustedParameters { get; init; } = ModeAdjustedParams ?? new Dictionary<string, double>();
}

public interface IOperator
{
    string Id { get; }
    string DisplayName { get; }
    OperatorSchema Schema => OperatorSchema.Empty(Id, DisplayName);

    Task<(MeshModel mesh, OpReport report)> RunAsync(
        MeshModel input,
        OperatorContext ctx,
        CancellationToken ct);
}

public record OperatorContext(
    float VoxelSizeMm,
    IProgress<float> Progress,
    Action<string> Log,
    Dictionary<string, object> Scratch,
    string Units,
    ProcessMode Mode,
    PresetQuality Quality,
    MinimumWallPolicy MinWallPolicy,
    float MinWallMm,
    float OverhangThresholdDeg,
    float MinimumDrainHoleMm,
    RepairMode RepairMode,
    ExecutionMode ExecutionMode,
    QualityScalingPolicy ScalingPolicy,
    int Seed)
{
    public int DeterministicSeedFor(string operatorId)
        => HashCode.Combine(Seed, operatorId, ExecutionMode, Mode, Quality, Units);
}
