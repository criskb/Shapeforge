using ShapeForge.Core.Backends;
using ShapeForge.Core.Geometry;

namespace ShapeForge.Backends.OptionalVolume;

/// <summary>
/// Placeholder for a third-party volume implementation that can live outside ShapeForge.Core.
/// </summary>
public sealed class ExampleVolumeBackend : IVolumeBackend
{
    private static VolumeOperationResult NotImplemented(MeshModel input, string op)
        => VolumeOperationResult.NotApplied(input, $"Optional backend operation '{op}' is not wired yet.");

    public VolumeOperationResult RebuildSolid(MeshModel input, float voxelSizeMm) => NotImplemented(input, "rebuild");

    public VolumeOperationResult Offset(MeshModel input, float distanceMm, float voxelSizeMm) => NotImplemented(input, "offset");

    public VolumeOperationResult Hollow(MeshModel input, float wallThicknessMm, float drainHoleMm, float voxelSizeMm) => NotImplemented(input, "hollow");

    public VolumeOperationResult Smooth(MeshModel input, float strength, int iterations, float voxelSizeMm) => NotImplemented(input, "smooth");

    public VolumeOperationResult Remesh(MeshModel input, float targetEdgeLengthMm) => NotImplemented(input, "remesh");
}
