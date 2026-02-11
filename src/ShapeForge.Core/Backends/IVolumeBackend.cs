using ShapeForge.Core.Geometry;

namespace ShapeForge.Core.Backends;

public interface IVolumeBackend
{
    VolumeOperationResult RebuildSolid(MeshModel input, float voxelSizeMm);

    VolumeOperationResult Offset(MeshModel input, float distanceMm, float voxelSizeMm);

    VolumeOperationResult Hollow(MeshModel input, float wallThicknessMm, float drainHoleMm, float voxelSizeMm);

    VolumeOperationResult Smooth(MeshModel input, float strength, int iterations, float voxelSizeMm);

    VolumeOperationResult Remesh(MeshModel input, float targetEdgeLengthMm);
}

public sealed record VolumeOperationResult(
    MeshModel Mesh,
    bool Applied,
    IReadOnlyList<string> Warnings)
{
    public static VolumeOperationResult NotApplied(MeshModel mesh, params string[] warnings)
        => new(mesh, false, warnings);

    public static VolumeOperationResult AppliedResult(MeshModel mesh, params string[] warnings)
        => new(mesh, true, warnings);
}
