using ShapeForge.Core.Geometry;

namespace ShapeForge.Core.Operators;

public sealed class RepairFixOperator(float closeRadiusMm = 0.6f, float smoothStrength = 0.2f) : IOperator
{
    public string Id => "repair.fix";
    public string DisplayName => "3D Print Fix";

    public Task<(MeshModel mesh, OpReport report)> RunAsync(MeshModel input, OperatorContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ctx.Progress.Report(0.2f);
        ctx.Log($"Voxelizing at {ctx.VoxelSizeMm:0.###} mm...");

        // Placeholder for PicoGK-backed repair pipeline.
        var output = input with { };

        ctx.Progress.Report(1.0f);
        var report = new OpReport(
            Name: DisplayName,
            Metrics: new Dictionary<string, double>
            {
                ["triangles.before"] = input.Indices.Length / 3.0,
                ["triangles.after"] = output.Indices.Length / 3.0,
                ["volume.delta.estimate"] = 0
            },
            Warnings: [],
            Notes:
            [
                $"closeRadiusMm={closeRadiusMm}",
                $"smooth={smoothStrength}",
                "Stub implementation: integrate PicoGK level-set close/open/remesh in M2."
            ]);

        return Task.FromResult((output, report));
    }
}
