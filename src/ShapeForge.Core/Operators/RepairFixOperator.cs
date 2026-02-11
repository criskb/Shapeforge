using ShapeForge.Core.Geometry;
using System.Numerics;

namespace ShapeForge.Core.Operators;

public sealed class RepairFixOperator(float closeRadiusMm = 0.6f, float smoothStrength = 0.2f) : IOperator
{
    public string Id => "repair.fix";
    public string DisplayName => "3D Print Fix";

    public Task<(MeshModel mesh, OpReport report)> RunAsync(MeshModel input, OperatorContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var triangleCountBefore = input.Indices.Length / 3;
        var weldEpsilon = MathF.Max(1e-6f, ctx.VoxelSizeMm * 0.25f);

        ctx.Progress.Report(0.1f);
        var vertices = ToVectorList(input.Vertices);
        var welded = WeldVertices(vertices, input.Indices, weldEpsilon);

        ctx.Progress.Report(0.35f);
        var (withoutDegenerates, degenerateRemoved) = RemoveDegenerateTriangles(welded.Vertices, welded.Indices, weldEpsilon);

        ctx.Progress.Report(0.55f);
        var (withoutDuplicates, duplicateRemoved) = RemoveDuplicateTriangles(withoutDegenerates);

        ctx.Progress.Report(0.75f);
        var windingFixed = FixWindingConsistency(withoutDuplicates, welded.Vertices);

        ctx.Progress.Report(0.9f);
        var (closed, holeFillAdded) = closeRadiusMm > 0f
            ? CloseSmallHoles(windingFixed, welded.Vertices, closeRadiusMm)
            : (windingFixed, 0);

        var output = BuildMesh(welded.Vertices, closed, input.Units);

        ctx.Progress.Report(1.0f);
        var report = new OpReport(
            Name: DisplayName,
            Metrics: new Dictionary<string, double>
            {
                ["vertices.before"] = vertices.Count,
                ["vertices.after"] = welded.Vertices.Count,
                ["triangles.before"] = triangleCountBefore,
                ["triangles.after"] = output.Indices.Length / 3.0,
                ["vertex.weld.epsilon.mm"] = weldEpsilon,
                ["vertex.weld.merged"] = vertices.Count - welded.Vertices.Count,
                ["triangles.removed.degenerate"] = degenerateRemoved,
                ["triangles.removed.duplicate"] = duplicateRemoved,
                ["triangles.added.hole-closure"] = holeFillAdded
            },
            Warnings: [],
            Notes:
            [
                $"closeRadiusMm={closeRadiusMm}",
                $"smooth={smoothStrength}",
                "Deterministic in-memory repair steps applied (weld/clean/orient/optional-hole-fill)."
            ]);

        return Task.FromResult((output, report));
    }

    private static List<Vector3> ToVectorList(float[] raw)
    {
        var result = new List<Vector3>(raw.Length / 3);
        for (var i = 0; i + 2 < raw.Length; i += 3)
        {
            result.Add(new Vector3(raw[i], raw[i + 1], raw[i + 2]));
        }

        return result;
    }

    private static (List<Vector3> Vertices, int[] Indices) WeldVertices(List<Vector3> inputVertices, int[] inputIndices, float epsilon)
    {
        var map = new Dictionary<(int, int, int), int>();
        var remap = new int[inputVertices.Count];
        var vertices = new List<Vector3>(inputVertices.Count);

        for (var i = 0; i < inputVertices.Count; i++)
        {
            var v = inputVertices[i];
            var key = ((int)MathF.Round(v.X / epsilon), (int)MathF.Round(v.Y / epsilon), (int)MathF.Round(v.Z / epsilon));
            if (!map.TryGetValue(key, out var idx))
            {
                idx = vertices.Count;
                map[key] = idx;
                vertices.Add(v);
            }

            remap[i] = idx;
        }

        var indices = new int[inputIndices.Length];
        for (var i = 0; i < inputIndices.Length; i++)
        {
            indices[i] = remap[inputIndices[i]];
        }

        return (vertices, indices);
    }

    private static (List<(int A, int B, int C)> Triangles, int Removed) RemoveDegenerateTriangles(
        List<Vector3> vertices,
        int[] indices,
        float epsilon)
    {
        var triangles = new List<(int A, int B, int C)>(indices.Length / 3);
        var threshold = epsilon * epsilon;

        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            var a = indices[i];
            var b = indices[i + 1];
            var c = indices[i + 2];
            if (a == b || b == c || c == a)
            {
                continue;
            }

            var ab = vertices[b] - vertices[a];
            var ac = vertices[c] - vertices[a];
            var areaSq = Vector3.Cross(ab, ac).LengthSquared();
            if (areaSq <= threshold)
            {
                continue;
            }

            triangles.Add((a, b, c));
        }

        return (triangles, (indices.Length / 3) - triangles.Count);
    }

    private static (List<(int A, int B, int C)> Triangles, int Removed) RemoveDuplicateTriangles(List<(int A, int B, int C)> triangles)
    {
        var seen = new HashSet<(int, int, int)>();
        var result = new List<(int A, int B, int C)>(triangles.Count);

        foreach (var t in triangles)
        {
            var canonical = Sort3(t.A, t.B, t.C);
            if (!seen.Add(canonical))
            {
                continue;
            }

            result.Add(t);
        }

        return (result, triangles.Count - result.Count);
    }

    private static List<(int A, int B, int C)> FixWindingConsistency(List<(int A, int B, int C)> triangles, List<Vector3> vertices)
    {
        var oriented = triangles.ToArray();
        var edgeToTriangles = new Dictionary<(int, int), List<int>>();

        for (var i = 0; i < oriented.Length; i++)
        {
            var (a, b, c) = oriented[i];
            AddEdge(edgeToTriangles, a, b, i);
            AddEdge(edgeToTriangles, b, c, i);
            AddEdge(edgeToTriangles, c, a, i);
        }

        var visited = new bool[oriented.Length];
        for (var start = 0; start < oriented.Length; start++)
        {
            if (visited[start])
            {
                continue;
            }

            var component = new List<int>();
            var queue = new Queue<int>();
            visited[start] = true;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var (u, v) in EnumerateEdges(oriented[current]))
                {
                    var key = u < v ? (u, v) : (v, u);
                    if (!edgeToTriangles.TryGetValue(key, out var neighbors))
                    {
                        continue;
                    }

                    foreach (var next in neighbors)
                    {
                        if (next == current)
                        {
                            continue;
                        }

                        if (EdgeHasSameDirection(oriented[current], oriented[next], key.Item1, key.Item2))
                        {
                            oriented[next] = Flip(oriented[next]);
                        }

                        if (!visited[next])
                        {
                            visited[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            var signedVolume = 0.0;
            foreach (var idx in component)
            {
                var t = oriented[idx];
                signedVolume += SignedTetraVolume(vertices[t.A], vertices[t.B], vertices[t.C]);
            }

            if (signedVolume < 0)
            {
                foreach (var idx in component)
                {
                    oriented[idx] = Flip(oriented[idx]);
                }
            }
        }

        return oriented.ToList();
    }

    private static (List<(int A, int B, int C)> Triangles, int Added) CloseSmallHoles(
        List<(int A, int B, int C)> triangles,
        List<Vector3> vertices,
        float closeRadiusMm)
    {
        var directedEdgeCount = new Dictionary<(int, int), int>();
        foreach (var t in triangles)
        {
            IncrementEdge(directedEdgeCount, t.A, t.B);
            IncrementEdge(directedEdgeCount, t.B, t.C);
            IncrementEdge(directedEdgeCount, t.C, t.A);
        }

        var boundaryEdges = new HashSet<(int, int)>();
        foreach (var edge in directedEdgeCount.Keys)
        {
            var reverse = (edge.Item2, edge.Item1);
            if (!directedEdgeCount.ContainsKey(reverse))
            {
                boundaryEdges.Add(edge);
            }
        }

        var consumed = new HashSet<(int, int)>();
        var additions = new List<(int A, int B, int C)>();
        var nextByStart = boundaryEdges.GroupBy(e => e.Item1).ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToList());

        foreach (var edge in boundaryEdges.OrderBy(e => e.Item1).ThenBy(e => e.Item2))
        {
            if (consumed.Contains(edge))
            {
                continue;
            }

            var loop = new List<int> { edge.Item1, edge.Item2 };
            consumed.Add(edge);
            var current = edge.Item2;
            var guard = 0;
            var closed = false;

            while (guard++ < boundaryEdges.Count)
            {
                if (!nextByStart.TryGetValue(current, out var candidates))
                {
                    break;
                }

                int? chosen = null;
                foreach (var candidate in candidates.OrderBy(v => v))
                {
                    if (consumed.Contains((current, candidate)))
                    {
                        continue;
                    }

                    chosen = candidate;
                    break;
                }

                if (!chosen.HasValue)
                {
                    break;
                }

                consumed.Add((current, chosen.Value));
                if (chosen.Value == loop[0])
                {
                    closed = true;
                    break;
                }

                loop.Add(chosen.Value);
                current = chosen.Value;
            }

            if (loop.Count < 3 || !closed)
            {
                continue;
            }
            if (LoopMaxRadius(loop, vertices) > closeRadiusMm)
            {
                continue;
            }

            for (var i = 1; i + 1 < loop.Count; i++)
            {
                additions.Add((loop[0], loop[i], loop[i + 1]));
            }
        }

        if (additions.Count == 0)
        {
            return (triangles, 0);
        }

        var merged = new List<(int A, int B, int C)>(triangles.Count + additions.Count);
        merged.AddRange(triangles);
        merged.AddRange(additions);
        return (merged, additions.Count);
    }

    private static MeshModel BuildMesh(List<Vector3> vertices, List<(int A, int B, int C)> triangles, string units)
    {
        var vertexRaw = new float[vertices.Count * 3];
        for (var i = 0; i < vertices.Count; i++)
        {
            var offset = i * 3;
            vertexRaw[offset] = vertices[i].X;
            vertexRaw[offset + 1] = vertices[i].Y;
            vertexRaw[offset + 2] = vertices[i].Z;
        }

        var indices = new int[triangles.Count * 3];
        for (var i = 0; i < triangles.Count; i++)
        {
            var offset = i * 3;
            indices[offset] = triangles[i].A;
            indices[offset + 1] = triangles[i].B;
            indices[offset + 2] = triangles[i].C;
        }

        return new MeshModel(vertexRaw, indices, Normals: null, units);
    }

    private static void AddEdge(Dictionary<(int, int), List<int>> map, int a, int b, int tri)
    {
        var key = a < b ? (a, b) : (b, a);
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(tri);
    }

    private static IEnumerable<(int, int)> EnumerateEdges((int A, int B, int C) t)
    {
        yield return (t.A, t.B);
        yield return (t.B, t.C);
        yield return (t.C, t.A);
    }

    private static bool EdgeHasSameDirection((int A, int B, int C) left, (int A, int B, int C) right, int u, int v)
    {
        var leftForward = HasDirectedEdge(left, u, v);
        var rightForward = HasDirectedEdge(right, u, v);
        return leftForward == rightForward;
    }

    private static bool HasDirectedEdge((int A, int B, int C) t, int u, int v)
        => (t.A == u && t.B == v) || (t.B == u && t.C == v) || (t.C == u && t.A == v);

    private static (int A, int B, int C) Flip((int A, int B, int C) t) => (t.A, t.C, t.B);

    private static (int, int, int) Sort3(int a, int b, int c)
    {
        if (a > b)
        {
            (a, b) = (b, a);
        }

        if (b > c)
        {
            (b, c) = (c, b);
        }

        if (a > b)
        {
            (a, b) = (b, a);
        }

        return (a, b, c);
    }

    private static void IncrementEdge(Dictionary<(int, int), int> map, int a, int b)
    {
        var key = (a, b);
        map.TryGetValue(key, out var current);
        map[key] = current + 1;
    }

    private static float LoopMaxRadius(List<int> loop, List<Vector3> vertices)
    {
        var center = Vector3.Zero;
        foreach (var idx in loop)
        {
            center += vertices[idx];
        }

        center /= loop.Count;
        var max = 0f;
        foreach (var idx in loop)
        {
            max = MathF.Max(max, (vertices[idx] - center).Length());
        }

        return max;
    }

    private static double SignedTetraVolume(Vector3 a, Vector3 b, Vector3 c)
        => Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
}
