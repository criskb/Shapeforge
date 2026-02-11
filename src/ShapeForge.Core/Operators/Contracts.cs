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
    IReadOnlyList<OperatorParameterSchema> Parameters)
{
    public static OperatorSchema Empty(string id, string displayName)
        => new(id, displayName, "1.0", string.Empty, Array.Empty<OperatorParameterSchema>());
}

public record OpReport(
    string Name,
    Dictionary<string, double> Metrics,
    List<string> Warnings,
    List<string> Notes,
    List<DiagnosticIssue>? Issues = null)
{
    public IReadOnlyList<DiagnosticIssue> StructuredIssues { get; init; } = Issues ?? new List<DiagnosticIssue>();
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
    RepairMode RepairMode);
