using System.Buffers.Binary;
using System.Globalization;
using ShapeForge.Core.Geometry;

namespace ShapeForge.Core.IO;

public sealed class StlMeshIO : IMeshIO
{
    public async Task<MeshModel> LoadStlAsync(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        if (IsLikelyAscii(stream))
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync(ct);
            return ParseAscii(text);
        }

        stream.Position = 0;
        return await ParseBinaryAsync(stream, ct);
    }

    public async Task SaveStlAsync(string path, MeshModel mesh, CancellationToken ct = default)
    {
        await using var stream = File.Create(path);
        await using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("ShapeForge STL Export"), header, 20);
        writer.Write(header);

        var triangleCount = mesh.Indices.Length / 3;
        writer.Write(triangleCount);

        for (var i = 0; i < mesh.Indices.Length; i += 3)
        {
            ct.ThrowIfCancellationRequested();
            WriteVector(writer, 0f, 0f, 0f);
            for (var j = 0; j < 3; j++)
            {
                var idx = mesh.Indices[i + j] * 3;
                WriteVector(writer, mesh.Vertices[idx], mesh.Vertices[idx + 1], mesh.Vertices[idx + 2]);
            }

            writer.Write((ushort)0);
        }

        await writer.FlushAsync();
    }

    private static bool IsLikelyAscii(Stream stream)
    {
        var probe = new byte[Math.Min(512, (int)stream.Length)];
        stream.ReadExactly(probe);
        var text = System.Text.Encoding.ASCII.GetString(probe);
        return text.StartsWith("solid", StringComparison.OrdinalIgnoreCase)
               && text.Contains("facet", StringComparison.OrdinalIgnoreCase);
    }

    private static MeshModel ParseAscii(string ascii)
    {
        var verts = new List<float>();
        var inds = new List<int>();
        var vertexMap = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var rawLine in ascii.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            var x = float.Parse(parts[1], CultureInfo.InvariantCulture);
            var y = float.Parse(parts[2], CultureInfo.InvariantCulture);
            var z = float.Parse(parts[3], CultureInfo.InvariantCulture);
            var key = $"{x:R}|{y:R}|{z:R}";
            if (!vertexMap.TryGetValue(key, out var idx))
            {
                idx = verts.Count / 3;
                vertexMap[key] = idx;
                verts.Add(x);
                verts.Add(y);
                verts.Add(z);
            }

            inds.Add(idx);
        }

        return new MeshModel(verts.ToArray(), inds.ToArray(), null);
    }

    private static async Task<MeshModel> ParseBinaryAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[80];
        await stream.ReadExactlyAsync(header, ct);
        var countBytes = new byte[4];
        await stream.ReadExactlyAsync(countBytes, ct);
        var triCount = BinaryPrimitives.ReadUInt32LittleEndian(countBytes);

        var verts = new List<float>((int)triCount * 9);
        var inds = new List<int>((int)triCount * 3);
        var map = new Dictionary<(float, float, float), int>();
        var triBuffer = new byte[50];

        for (var i = 0; i < triCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            await stream.ReadExactlyAsync(triBuffer, ct);
            for (var j = 0; j < 3; j++)
            {
                var offset = 12 + (j * 12);
                var x = BitConverter.ToSingle(triBuffer, offset);
                var y = BitConverter.ToSingle(triBuffer, offset + 4);
                var z = BitConverter.ToSingle(triBuffer, offset + 8);
                var key = (x, y, z);
                if (!map.TryGetValue(key, out var idx))
                {
                    idx = verts.Count / 3;
                    map[key] = idx;
                    verts.Add(x);
                    verts.Add(y);
                    verts.Add(z);
                }

                inds.Add(idx);
            }
        }

        return new MeshModel(verts.ToArray(), inds.ToArray(), null);
    }

    private static void WriteVector(BinaryWriter writer, float x, float y, float z)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
    }
}
