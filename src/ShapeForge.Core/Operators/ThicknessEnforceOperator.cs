using ShapeForge.Core.Geometry;

namespace ShapeForge.Core.Operators;

public enum ThicknessMode
{
    Inflate,
    Reshell
}

public sealed class ThicknessEnforceOperator(float minimumMm, ThicknessMode mode) : IOperator
{
    public string Id => "thickness.enforce";
    public string DisplayName => "Minimum Wall Thickness";

    public Task<(MeshModel mesh, OpReport report)> RunAsync(MeshModel input, OperatorContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ctx.Progress.Report(1);
        var report = new OpReport(
            DisplayName,
            new Dictionary<string, double>
            {
                ["min.thickness.target.mm"] = minimumMm,
                ["thin.vertices.before"] = 0,
                ["thin.vertices.after"] = 0
            },
            [],
            [$"mode={mode}", "Stub implementation pending voxel thickness field."]);

        return Task.FromResult((input with { }, report));
    }
}
