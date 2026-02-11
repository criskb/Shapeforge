using ShapeForge.Core.Geometry;

namespace ShapeForge.Core.Backends;

public sealed class NullVolumeBackend : IVolumeBackend
{
    private static VolumeOperationResult NotAvailable(MeshModel input, string operation)
        => VolumeOperationResult.NotApplied(
            input,
            $"Volume backend unavailable for '{operation}'. Configure an adapter from a separate ShapeForge.Backends.* project to enable voxel/SDF operations.");

    public VolumeOperationResult RebuildSolid(MeshModel input, float voxelSizeMm) => NotAvailable(input, "rebuild");

    public VolumeOperationResult Offset(MeshModel input, float distanceMm, float voxelSizeMm) => NotAvailable(input, "offset");

    public VolumeOperationResult Hollow(MeshModel input, float wallThicknessMm, float drainHoleMm, float voxelSizeMm) => NotAvailable(input, "hollow");

    public VolumeOperationResult Smooth(MeshModel input, float strength, int iterations, float voxelSizeMm) => NotAvailable(input, "smooth");

    public VolumeOperationResult Remesh(MeshModel input, float targetEdgeLengthMm) => NotAvailable(input, "remesh");
}
