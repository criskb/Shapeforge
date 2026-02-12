namespace ShapeForge.Core.Pipeline;

public enum MeshSizeTier
{
    Small,
    Medium,
    Large
}

public static class PerformanceGuardrails
{
    public const int SmallMeshMaxTriangles = 100_000;
    public const int MediumMeshMaxTriangles = 500_000;
    public const int PreviewMaxTriangles = 350_000;

    public static MeshSizeTier ResolveTier(int triangleCount) => triangleCount switch
    {
        <= SmallMeshMaxTriangles => MeshSizeTier.Small,
        <= MediumMeshMaxTriangles => MeshSizeTier.Medium,
        _ => MeshSizeTier.Large
    };

    public static int MaxSampleVerticesFor(MeshSizeTier tier) => tier switch
    {
        MeshSizeTier.Small => 20_000,
        MeshSizeTier.Medium => 15_000,
        _ => 8_000
    };

    public static float AdaptiveSamplingCap(int triangleCount)
    {
        if (triangleCount <= SmallMeshMaxTriangles)
        {
            return 1f;
        }

        var ratio = (float)SmallMeshMaxTriangles / triangleCount;
        return Math.Clamp(MathF.Sqrt(ratio), 0.2f, 1f);
    }

    public static bool ExceedsPreviewTriangleLimit(int triangleCount)
        => triangleCount > PreviewMaxTriangles;
}
