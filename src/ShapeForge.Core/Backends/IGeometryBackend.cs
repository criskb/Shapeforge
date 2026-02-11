using ShapeForge.Core.Geometry;

namespace ShapeForge.Core.Backends;

public interface IGeometryBackend
{
    MeshModel WeldVertices(MeshModel input, float epsilonMm, out int mergedVertices);

    MeshModel RemoveDegenerateFaces(MeshModel input, float epsilonMm, out int removedFaces);

    MeshModel RemoveDuplicateFaces(MeshModel input, out int removedFaces);

    MeshModel FixNormalsAndOrientation(MeshModel input);

    MeshModel FillSmallHoles(MeshModel input, float closeRadiusMm, out int addedFaces);

    MeshModel RemoveTinyShells(MeshModel input, float tinyShellThresholdMm, out int removedFaces);
}
