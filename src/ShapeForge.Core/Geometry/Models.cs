namespace ShapeForge.Core.Geometry;

public record MeshModel(float[] Vertices, int[] Indices, float[]? Normals, string Units = "mm");

public record VoxelModel(
    object GridHandle,
    float VoxelSizeMm,
    (float x, float y, float z) Min,
    (float x, float y, float z) Max);

public static class MeshMetrics
{
    public static int TriangleCount(MeshModel mesh) => mesh.Indices.Length / 3;

    public static (float minX, float minY, float minZ, float maxX, float maxY, float maxZ) Bounds(MeshModel mesh)
    {
        if (mesh.Vertices.Length < 3)
        {
            return (0, 0, 0, 0, 0, 0);
        }

        var minX = mesh.Vertices[0];
        var minY = mesh.Vertices[1];
        var minZ = mesh.Vertices[2];
        var maxX = minX;
        var maxY = minY;
        var maxZ = minZ;

        for (var i = 3; i < mesh.Vertices.Length; i += 3)
        {
            var x = mesh.Vertices[i];
            var y = mesh.Vertices[i + 1];
            var z = mesh.Vertices[i + 2];
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);
        }

        return (minX, minY, minZ, maxX, maxY, maxZ);
    }
}
