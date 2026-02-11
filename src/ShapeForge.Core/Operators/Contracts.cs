using ShapeForge.Core.Geometry;

namespace ShapeForge.Core.Operators;

public record OpReport(
    string Name,
    Dictionary<string, double> Metrics,
    List<string> Warnings,
    List<string> Notes);

public interface IOperator
{
    string Id { get; }
    string DisplayName { get; }

    Task<(MeshModel mesh, OpReport report)> RunAsync(
        MeshModel input,
        OperatorContext ctx,
        CancellationToken ct);
}

public record OperatorContext(
    float VoxelSizeMm,
    IProgress<float> Progress,
    Action<string> Log,
    Dictionary<string, object> Scratch);
