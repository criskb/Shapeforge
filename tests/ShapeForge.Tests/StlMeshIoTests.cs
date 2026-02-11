using ShapeForge.Core.Geometry;
using ShapeForge.Core.IO;

namespace ShapeForge.Tests;

public class StlMeshIoTests
{
    [Fact]
    public async Task SaveAndLoadBinaryStl_RoundTripsTriangleCount()
    {
        var io = new StlMeshIO();
        var mesh = new MeshModel(
            Vertices: [0, 0, 0, 1, 0, 0, 0, 1, 0],
            Indices: [0, 1, 2],
            Normals: null);

        var file = Path.Combine(Path.GetTempPath(), $"sf-{Guid.NewGuid():N}.stl");
        await io.SaveStlAsync(file, mesh);
        var loaded = await io.LoadStlAsync(file);

        Assert.Equal(mesh.Indices.Length / 3, loaded.Indices.Length / 3);
    }
}
