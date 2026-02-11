using ShapeForge.Core.Backends;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Core.Operators;

public sealed class RepairFixOperator : IOperator
{
    private readonly float _closeRadiusMm;
    private readonly float _smoothStrength;
    private readonly IGeometryBackend _geometryBackend;

    public RepairFixOperator(float closeRadiusMm = 0.6f, float smoothStrength = 0.2f, IGeometryBackend? geometryBackend = null)
    {
        _closeRadiusMm = closeRadiusMm;
        _smoothStrength = smoothStrength;
        _geometryBackend = geometryBackend ?? new DefaultMeshBackend();
    }

    public const string CanonicalId = "repair.fix";
    public string Id => CanonicalId;
    public string DisplayName => "3D Print Fix";

    public OperatorSchema Schema => new(
        Id,
        DisplayName,
        "1.0",
        "Repairs mesh topology by welding vertices, removing invalid faces, and optionally filling small holes.",
        [
            new OperatorParameterSchema("closeRadiusMm", OperatorParameterType.Number, "Hole closure radius in millimeters.", false, _closeRadiusMm, Min: 0),
            new OperatorParameterSchema("smoothStrength", OperatorParameterType.Number, "Optional smoothing strength hint.", false, _smoothStrength, Min: 0)
        ],
        Category: OperatorCategories.RepairMesh,
        Deterministic: true,
        EstimatedCost: 4.0);

    public Task<(MeshModel mesh, OpReport report)> RunAsync(MeshModel input, OperatorContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var triangleCountBefore = input.Indices.Length / 3;
        var vertexCountBefore = input.Vertices.Length / 3;
        var qualityScale = ctx.Quality == PresetQuality.Preview ? 1.25f : 1.0f;
        var weldEpsilon = MathF.Max(1e-6f, ctx.VoxelSizeMm * 0.25f * qualityScale);

        ctx.Progress.Report(0.1f);
        var welded = _geometryBackend.WeldVertices(input, weldEpsilon, out var weldedMerged);

        ctx.Progress.Report(0.35f);
        var noDegenerates = _geometryBackend.RemoveDegenerateFaces(welded, weldEpsilon, out var degenerateRemoved);

        ctx.Progress.Report(0.55f);
        var noDuplicates = _geometryBackend.RemoveDuplicateFaces(noDegenerates, out var duplicateRemoved);

        ctx.Progress.Report(0.75f);
        var windingFixed = _geometryBackend.FixNormalsAndOrientation(noDuplicates);

        ctx.Progress.Report(0.9f);
        var repairRadiusScale = ctx.RepairMode switch
        {
            RepairMode.Conservative => 0.75f,
            RepairMode.Balanced => 1.0f,
            RepairMode.Aggressive => 1.35f,
            _ => 1.0f
        };

        var effectiveCloseRadiusMm = _closeRadiusMm * repairRadiusScale;
        var addedHoles = 0;
        var closed = windingFixed;
        if (effectiveCloseRadiusMm > 0f)
        {
            closed = _geometryBackend.FillSmallHoles(windingFixed, effectiveCloseRadiusMm, out addedHoles);
        }

        var tinyShellThresholdMm = effectiveCloseRadiusMm * 2f;
        var removedTinyShells = 0;
        var noTinyShells = closed;
        if (tinyShellThresholdMm > 0f)
        {
            noTinyShells = _geometryBackend.RemoveTinyShells(closed, tinyShellThresholdMm, out removedTinyShells);
        }

        ctx.Progress.Report(1.0f);
        var report = new OpReport(
            Name: DisplayName,
            Metrics: new Dictionary<string, double>
            {
                ["vertices.before"] = vertexCountBefore,
                ["vertices.after"] = noTinyShells.Vertices.Length / 3.0,
                ["triangles.before"] = triangleCountBefore,
                ["triangles.after"] = noTinyShells.Indices.Length / 3.0,
                ["vertex.weld.epsilon.mm"] = weldEpsilon,
                ["vertex.weld.merged"] = weldedMerged,
                ["triangles.removed.degenerate"] = degenerateRemoved,
                ["triangles.removed.duplicate"] = duplicateRemoved,
                ["triangles.added.hole-closure"] = addedHoles,
                ["triangles.removed.tiny-shells"] = removedTinyShells
            },
            Warnings: [],
            Notes:
            [
                $"closeRadiusMm={effectiveCloseRadiusMm:0.###}",
                $"repairMode={ctx.RepairMode}",
                $"quality={ctx.Quality}",
                $"mode={ctx.Mode}",
                $"overhangThresholdDeg={ctx.OverhangThresholdDeg:0.###}",
                $"tinyShellThresholdMm={tinyShellThresholdMm:0.###}",
                $"smooth={_smoothStrength}",
                "Deterministic backend-driven mesh repair steps applied (weld/clean/orient/optional-hole-fill/tiny-shell-filter)."
            ]);

        return Task.FromResult((noTinyShells, report));
    }
}
