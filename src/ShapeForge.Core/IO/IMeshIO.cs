using ShapeForge.Core.Geometry;

namespace ShapeForge.Core.IO;

public interface IMeshIO
{
    Task<MeshModel> LoadStlAsync(string path, CancellationToken ct = default);
    Task SaveStlAsync(string path, MeshModel mesh, CancellationToken ct = default);
}
