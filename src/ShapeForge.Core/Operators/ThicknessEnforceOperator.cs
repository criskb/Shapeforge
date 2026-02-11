using ShapeForge.Core.Backends;
using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using System.Numerics;

namespace ShapeForge.Core.Operators;

public enum ThicknessMode
{
    Inflate,
    Reshell
}

public sealed class ThicknessEnforceOperator : IOperator
{
    private readonly float _minimumMm;
    private readonly ThicknessMode _mode;
    private readonly IVolumeBackend _volumeBackend;

    public ThicknessEnforceOperator(float minimumMm, ThicknessMode mode, IVolumeBackend? volumeBackend = null)
    {
        _minimumMm = minimumMm;
        _mode = mode;
        _volumeBackend = volumeBackend ?? new NullVolumeBackend();
    }

    public string Id => "thickness.enforce";
    public string DisplayName => "Minimum Wall Thickness";

    public OperatorSchema Schema => new(
        Id,
        DisplayName,
        "1.0",
        "Detects and optionally inflates regions that violate a minimum wall thickness target.",
        [
            new OperatorParameterSchema("minimumMm", OperatorParameterType.Number, "Minimum target wall thickness in millimeters.", true, _minimumMm, Min: 0),
            new OperatorParameterSchema("mode", OperatorParameterType.Enum, "How enforcement is applied.", true, _mode.ToString(), AllowedValues: Enum.GetNames<ThicknessMode>())
        ]);

    public Task<(MeshModel mesh, OpReport report)> RunAsync(MeshModel input, OperatorContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var vertices = ToVectors(input.Vertices);
        var triangleCount = input.Indices.Length / 3;

        ctx.Progress.Report(0.35f);
        var nearestNeighborDistances = ComputeNearestNeighborDistances(vertices);
        var thinBefore = nearestNeighborDistances.Count(d => d > 0f && d < _minimumMm);
        var observedMin = nearestNeighborDistances.Count == 0 ? 0f : nearestNeighborDistances.Where(d => d > 0f).DefaultIfEmpty(0f).Min();

        var warnings = new List<string>();
        var notes = new List<string> { $"mode={_mode}" };
        var output = input with { };

        var attemptedEnforcement = _mode == ThicknessMode.Inflate;
        var appliedVertices = vertices;
        var geometryEdited = false;

        if (attemptedEnforcement && thinBefore > 0)
        {
            var backendResult = _volumeBackend.Offset(input, _minimumMm * 0.5f, ctx.VoxelSizeMm);
            warnings.AddRange(backendResult.Warnings);

            if (backendResult.Applied)
            {
                output = backendResult.Mesh;
                appliedVertices = ToVectors(output.Vertices);
                geometryEdited = true;
                notes.Add("inflation performed by volume backend offset operation");
            }
            else
            {
                var (inflated, movedCount) = InflateThinRegions(vertices, nearestNeighborDistances, _minimumMm);
                if (movedCount > 0)
                {
                    appliedVertices = inflated;
                    geometryEdited = true;
                    output = new MeshModel(Flatten(appliedVertices), input.Indices.ToArray(), input.Normals?.ToArray(), input.Units);
                    notes.Add($"inflate.adjusted.vertices={movedCount}");
                    notes.Add("used managed fallback because volume backend did not apply");
                }
            }
        }

        var structuredIssues = new List<DiagnosticIssue>();
        if (!geometryEdited)
        {
            warnings.Add("severity=warning: thickness enforcement not applied; detection metrics only and no geometry edit was applied.");
            notes.Add("no geometry edit was applied");
            structuredIssues.Add(new DiagnosticIssue(
                IssueSeverity.Warning,
                "thickness.enforcement.skipped",
                "Thickness enforcement did not modify the mesh; detection metrics only.",
                1,
                new Dictionary<string, string> { ["mode"] = _mode.ToString() }));
        }

        var afterDistances = ComputeNearestNeighborDistances(appliedVertices);
        var thinAfter = afterDistances.Count(d => d > 0f && d < _minimumMm);
        var observedMinAfter = afterDistances.Count == 0 ? 0f : afterDistances.Where(d => d > 0f).DefaultIfEmpty(0f).Min();

        ctx.Progress.Report(1);
        var report = new OpReport(
            DisplayName,
            new Dictionary<string, double>
            {
                ["min.thickness.target.mm"] = _minimumMm,
                ["triangles.sampled"] = triangleCount,
                ["thin.vertices.before"] = thinBefore,
                ["thin.vertices.after"] = thinAfter,
                ["observed.min.vertex-spacing.before.mm"] = observedMin,
                ["observed.min.vertex-spacing.after.mm"] = observedMinAfter,
                ["enforcement.applied"] = geometryEdited ? 1 : 0
            },
            warnings,
            notes,
            structuredIssues);

        return Task.FromResult((output, report));
    }

    private static List<Vector3> ToVectors(float[] raw)
    {
        var result = new List<Vector3>(raw.Length / 3);
        for (var i = 0; i + 2 < raw.Length; i += 3)
        {
            result.Add(new Vector3(raw[i], raw[i + 1], raw[i + 2]));
        }

        return result;
    }

    private static List<float> ComputeNearestNeighborDistances(List<Vector3> vertices)
    {
        var result = new List<float>(vertices.Count);
        for (var i = 0; i < vertices.Count; i++)
        {
            var best = float.MaxValue;
            for (var j = 0; j < vertices.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var d = Vector3.Distance(vertices[i], vertices[j]);
                if (d < best)
                {
                    best = d;
                }
            }

            result.Add(best == float.MaxValue ? 0f : best);
        }

        return result;
    }

    private static (List<Vector3> Vertices, int Moved) InflateThinRegions(List<Vector3> vertices, List<float> nearestDistances, float minimumMm)
    {
        if (vertices.Count == 0)
        {
            return (vertices, 0);
        }

        var centroid = Vector3.Zero;
        foreach (var v in vertices)
        {
            centroid += v;
        }

        centroid /= vertices.Count;
        var output = new List<Vector3>(vertices.Count);
        var moved = 0;

        for (var i = 0; i < vertices.Count; i++)
        {
            var current = vertices[i];
            var nearest = nearestDistances[i];
            if (nearest <= 0f || nearest >= minimumMm)
            {
                output.Add(current);
                continue;
            }

            var direction = current - centroid;
            if (direction.LengthSquared() < 1e-10f)
            {
                direction = Vector3.UnitZ;
            }
            else
            {
                direction = Vector3.Normalize(direction);
            }

            var shift = (minimumMm - nearest) * 0.5f;
            output.Add(current + (direction * shift));
            moved++;
        }

        return (output, moved);
    }

    private static float[] Flatten(List<Vector3> vertices)
    {
        var result = new float[vertices.Count * 3];
        for (var i = 0; i < vertices.Count; i++)
        {
            var offset = i * 3;
            result[offset] = vertices[i].X;
            result[offset + 1] = vertices[i].Y;
            result[offset + 2] = vertices[i].Z;
        }

        return result;
    }
}
