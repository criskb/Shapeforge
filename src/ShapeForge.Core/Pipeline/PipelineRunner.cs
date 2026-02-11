using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;

namespace ShapeForge.Core.Pipeline;

public sealed class PipelineRunner
{
    public async Task<(MeshModel mesh, IReadOnlyList<OpReport> reports)> RunAsync(
        MeshModel input,
        IEnumerable<IOperator> steps,
        OperatorContext context,
        CancellationToken ct)
    {
        var current = input;
        var reports = new List<OpReport>();

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            context.Log($"Running {step.Id}...");
            var result = await step.RunAsync(current, context, ct);
            current = result.mesh;
            reports.Add(result.report);
        }

        return (current, reports);
    }
}
