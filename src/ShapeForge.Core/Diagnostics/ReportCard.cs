using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;

namespace ShapeForge.Core.Diagnostics;

public static class ReportCard
{
    public const string SchemaVersion = MeshDiagnostics.CurrentSchemaVersion;

    public static MeshDiagnostics Build(MeshModel mesh, IEnumerable<OpReport>? reports = null)
    {
        var topology = BuildTopologyMetrics(mesh);
        var quality = BuildQualityMetrics(mesh);
        var printability = BuildPrintabilityMetrics(reports);
        var issues = BuildIssues(mesh, topology, printability, reports);

        var countMetrics = BuildCountMetrics(topology, issues);
        var booleanFlags = BuildBooleanFlags(topology, quality, issues);

        return new MeshDiagnostics(SchemaVersion, topology, quality, printability, issues, countMetrics, booleanFlags);
    }


    private static Dictionary<string, long> BuildCountMetrics(
        Dictionary<string, double> topology,
        List<DiagnosticIssue> issues)
    {
        var counts = new Dictionary<string, long>();
        foreach (var metric in topology)
        {
            if (metric.Key.EndsWith(".count", StringComparison.OrdinalIgnoreCase))
            {
                counts[metric.Key] = (long)Math.Round(metric.Value);
            }
        }

        counts["issues.total"] = issues.Count;
        counts["issues.warning"] = issues.Count(i => i.Severity == IssueSeverity.Warning);
        counts["issues.error"] = issues.Count(i => i.Severity == IssueSeverity.Error);
        return counts;
    }

    private static Dictionary<string, bool> BuildBooleanFlags(
        Dictionary<string, double> topology,
        Dictionary<string, double> quality,
        List<DiagnosticIssue> issues)
    {
        return new Dictionary<string, bool>
        {
            ["mesh.has-invalid-indices"] = topology.TryGetValue("indices.invalid.count", out var invalid) && invalid > 0,
            ["mesh.has-degenerate-triangles"] = topology.TryGetValue("triangles.degenerate.count", out var degenerate) && degenerate > 0,
            ["mesh.has-duplicate-triangles"] = topology.TryGetValue("triangles.duplicate.count", out var duplicate) && duplicate > 0,
            ["mesh.is-watertight"] = topology.TryGetValue("mesh.is-watertight", out var watertight) && watertight > 0,
            ["mesh.is-manifold"] = topology.TryGetValue("mesh.is-manifold", out var manifold) && manifold > 0,
            ["mesh.normals.missing"] = quality.TryGetValue("normals.missing", out var missingNormals) && missingNormals > 0,
            ["mesh.has-warnings-or-errors"] = issues.Any(i => i.Severity >= IssueSeverity.Warning)
        };
    }

    private static Dictionary<string, double> BuildTopologyMetrics(MeshModel mesh)
    {
        var vertexCount = mesh.Vertices.Length / 3;
        var triangleCount = MeshMetrics.TriangleCount(mesh);
        var (minX, minY, minZ, maxX, maxY, maxZ) = MeshMetrics.Bounds(mesh);
        var degenerateCount = 0;
        var duplicateCount = 0;
        var invalidIndexCount = 0;
        var edgeUseCounts = new Dictionary<(int a, int b), int>();
        var trianglesByVertex = new Dictionary<int, List<int>>();
        var validTriangles = new List<(int A, int B, int C)>();

        var faces = new HashSet<(int a, int b, int c)>();
        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
        {
            var ia = mesh.Indices[i];
            var ib = mesh.Indices[i + 1];
            var ic = mesh.Indices[i + 2];

            if (ia < 0 || ib < 0 || ic < 0 || ia >= vertexCount || ib >= vertexCount || ic >= vertexCount)
            {
                invalidIndexCount++;
                continue;
            }

            if (ia == ib || ib == ic || ia == ic)
            {
                degenerateCount++;
                continue;
            }

            var ordered = OrderIndices(ia, ib, ic);
            if (!faces.Add(ordered))
            {
                duplicateCount++;
            }

            CountEdge(edgeUseCounts, ia, ib);
            CountEdge(edgeUseCounts, ib, ic);
            CountEdge(edgeUseCounts, ic, ia);

            var triIndex = validTriangles.Count;
            validTriangles.Add((ia, ib, ic));
            AddTriangleReference(trianglesByVertex, ia, triIndex);
            AddTriangleReference(trianglesByVertex, ib, triIndex);
            AddTriangleReference(trianglesByVertex, ic, triIndex);
        }

        var boundaryEdges = edgeUseCounts.Count(e => e.Value == 1);
        var nonManifoldEdges = edgeUseCounts.Count(e => e.Value > 2);
        var shellCount = CountShells(validTriangles, trianglesByVertex);
        var eulerCharacteristic = (double)vertexCount - edgeUseCounts.Count + validTriangles.Count;
        var (surfaceArea, signedVolume) = ComputeGeometricStats(mesh, validTriangles);
        var isWatertight = triangleCount > 0 && boundaryEdges == 0 && invalidIndexCount == 0;
        var isManifold = triangleCount > 0 && nonManifoldEdges == 0 && invalidIndexCount == 0;

        return new Dictionary<string, double>
        {
            ["vertices.count"] = vertexCount,
            ["triangles.count"] = triangleCount,
            ["indices.invalid.count"] = invalidIndexCount,
            ["triangles.degenerate.count"] = degenerateCount,
            ["triangles.duplicate.count"] = duplicateCount,
            ["edges.boundary.count"] = boundaryEdges,
            ["edges.nonmanifold.count"] = nonManifoldEdges,
            ["mesh.shells.count"] = shellCount,
            ["mesh.euler.characteristic"] = eulerCharacteristic,
            ["mesh.surface.area"] = surfaceArea,
            ["mesh.volume.signed"] = signedVolume,
            ["mesh.volume.abs"] = Math.Abs(signedVolume),
            ["mesh.is-watertight"] = isWatertight ? 1 : 0,
            ["mesh.is-manifold"] = isManifold ? 1 : 0,
            ["bounds.min.x"] = minX,
            ["bounds.min.y"] = minY,
            ["bounds.min.z"] = minZ,
            ["bounds.max.x"] = maxX,
            ["bounds.max.y"] = maxY,
            ["bounds.max.z"] = maxZ,
            ["bounds.size.x"] = maxX - minX,
            ["bounds.size.y"] = maxY - minY,
            ["bounds.size.z"] = maxZ - minZ,
            ["bounds.volume"] = (maxX - minX) * (maxY - minY) * (maxZ - minZ),
            ["bounds.diagonal"] = Distance((minX, minY, minZ), (maxX, maxY, maxZ))
        };
    }

    private static Dictionary<string, double> BuildQualityMetrics(MeshModel mesh)
    {
        if (mesh.Indices.Length < 3)
        {
            return new Dictionary<string, double>
            {
                ["triangles.area.min"] = 0,
                ["triangles.area.max"] = 0,
                ["triangles.area.avg"] = 0,
                ["triangles.aspect.max"] = 0,
                ["triangles.aspect.avg"] = 0,
                ["normals.missing"] = mesh.Normals is null || mesh.Normals.Length == 0 ? 1 : 0
            };
        }

        var minArea = double.MaxValue;
        var maxArea = 0.0;
        var sumArea = 0.0;
        var maxAspect = 0.0;
        var sumAspect = 0.0;
        var measuredTriangles = 0;

        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
        {
            var ia = mesh.Indices[i] * 3;
            var ib = mesh.Indices[i + 1] * 3;
            var ic = mesh.Indices[i + 2] * 3;
            if (ia < 0 || ib < 0 || ic < 0 || ia + 2 >= mesh.Vertices.Length || ib + 2 >= mesh.Vertices.Length || ic + 2 >= mesh.Vertices.Length)
            {
                continue;
            }

            var a = (x: mesh.Vertices[ia], y: mesh.Vertices[ia + 1], z: mesh.Vertices[ia + 2]);
            var b = (x: mesh.Vertices[ib], y: mesh.Vertices[ib + 1], z: mesh.Vertices[ib + 2]);
            var c = (x: mesh.Vertices[ic], y: mesh.Vertices[ic + 1], z: mesh.Vertices[ic + 2]);

            var ab = Distance(a, b);
            var bc = Distance(b, c);
            var ca = Distance(c, a);
            var longest = Math.Max(ab, Math.Max(bc, ca));
            var shortest = Math.Min(ab, Math.Min(bc, ca));
            var aspect = shortest <= 0 ? 0 : longest / shortest;

            var area = TriangleArea(a, b, c);
            minArea = Math.Min(minArea, area);
            maxArea = Math.Max(maxArea, area);
            sumArea += area;
            maxAspect = Math.Max(maxAspect, aspect);
            sumAspect += aspect;
            measuredTriangles++;
        }

        if (measuredTriangles == 0)
        {
            minArea = 0;
        }

        return new Dictionary<string, double>
        {
            ["triangles.area.min"] = minArea,
            ["triangles.area.max"] = maxArea,
            ["triangles.area.avg"] = measuredTriangles == 0 ? 0 : sumArea / measuredTriangles,
            ["triangles.aspect.max"] = maxAspect,
            ["triangles.aspect.avg"] = measuredTriangles == 0 ? 0 : sumAspect / measuredTriangles,
            ["normals.missing"] = mesh.Normals is null || mesh.Normals.Length == 0 ? 1 : 0
        };
    }

    private static Dictionary<string, double> BuildPrintabilityMetrics(IEnumerable<OpReport>? reports)
    {
        var printability = new Dictionary<string, double>();

        if (reports is null)
        {
            return printability;
        }

        foreach (var report in reports)
        {
            foreach (var metric in report.Metrics)
            {
                if (metric.Key.StartsWith("thin.", StringComparison.OrdinalIgnoreCase) ||
                    metric.Key.StartsWith("wall.", StringComparison.OrdinalIgnoreCase) ||
                    metric.Key.StartsWith("print.", StringComparison.OrdinalIgnoreCase))
                {
                    printability[$"{report.Name}.{metric.Key}"] = metric.Value;
                }
            }
        }

        return printability;
    }

    private static List<DiagnosticIssue> BuildIssues(
        MeshModel mesh,
        Dictionary<string, double> topology,
        Dictionary<string, double> printability,
        IEnumerable<OpReport>? reports)
    {
        var issues = new List<DiagnosticIssue>();
        var triangleCount = (int)topology["triangles.count"];
        var invalidIndices = (int)topology["indices.invalid.count"];
        var degenerateCount = (int)topology["triangles.degenerate.count"];
        var duplicateCount = (int)topology["triangles.duplicate.count"];

        if (triangleCount == 0)
        {
            issues.Add(new DiagnosticIssue(IssueSeverity.Error, "mesh.empty", "Mesh has zero triangles.", 1));
        }
        else if (triangleCount < 100)
        {
            issues.Add(new DiagnosticIssue(IssueSeverity.Warning, "mesh.low-triangle-count", "Mesh triangle count is very low.", triangleCount));
        }

        if (invalidIndices > 0)
        {
            issues.Add(new DiagnosticIssue(IssueSeverity.Error, "mesh.invalid-indices", "Mesh has invalid triangle indices.", invalidIndices));
        }

        if (degenerateCount > 0)
        {
            issues.Add(new DiagnosticIssue(IssueSeverity.Warning, "mesh.degenerate-triangles", "Mesh contains degenerate triangles.", degenerateCount));
        }

        if (duplicateCount > 0)
        {
            issues.Add(new DiagnosticIssue(IssueSeverity.Warning, "mesh.duplicate-triangles", "Mesh contains duplicate triangles.", duplicateCount));
        }

        if (topology.TryGetValue("mesh.is-watertight", out var watertight) && watertight < 0.5)
        {
            var boundaryEdges = topology.GetValueOrDefault("edges.boundary.count");
            issues.Add(new DiagnosticIssue(IssueSeverity.Error, "mesh.not-watertight", "Mesh is not watertight and may leak volume assumptions.", Math.Max(1, (int)Math.Round(boundaryEdges))));
        }

        if (topology.TryGetValue("mesh.is-manifold", out var manifold) && manifold < 0.5)
        {
            var nonManifoldEdges = topology.GetValueOrDefault("edges.nonmanifold.count");
            issues.Add(new DiagnosticIssue(IssueSeverity.Error, "mesh.non-manifold", "Mesh contains non-manifold edges.", Math.Max(1, (int)Math.Round(nonManifoldEdges))));
        }

        if (topology.TryGetValue("mesh.shells.count", out var shellCount) && shellCount > 1)
        {
            issues.Add(new DiagnosticIssue(IssueSeverity.Warning, "mesh.shells.multiple", "Mesh contains multiple disconnected shells.", (int)Math.Round(shellCount)));
        }

        if (mesh.Normals is null || mesh.Normals.Length == 0)
        {
            issues.Add(new DiagnosticIssue(IssueSeverity.Info, "mesh.normals.missing", "Mesh normals are missing and may be regenerated."));
        }

        foreach (var metric in printability.Where(m => m.Key.Contains("thin.vertices", StringComparison.OrdinalIgnoreCase) && m.Value > 0))
        {
            issues.Add(new DiagnosticIssue(
                IssueSeverity.Warning,
                "printability.thin-vertices",
                "Thin-wall risks detected; minimum thickness may be below preset.",
                (int)Math.Round(metric.Value),
                new Dictionary<string, string> { ["metric"] = metric.Key }));
        }

        if (reports is not null)
        {
            foreach (var report in reports)
            {
                foreach (var warning in report.Warnings)
                {
                    issues.Add(new DiagnosticIssue(
                        IssueSeverity.Warning,
                        $"operator.{report.Name}.warning",
                        warning,
                        1,
                        new Dictionary<string, string> { ["operator"] = report.Name }));
                }

                issues.AddRange(report.StructuredIssues);
            }
        }

        return issues;
    }

    private static (int a, int b, int c) OrderIndices(int ia, int ib, int ic)
    {
        if (ia > ib)
        {
            (ia, ib) = (ib, ia);
        }

        if (ib > ic)
        {
            (ib, ic) = (ic, ib);
        }

        if (ia > ib)
        {
            (ia, ib) = (ib, ia);
        }

        return (ia, ib, ic);
    }

    private static void CountEdge(Dictionary<(int a, int b), int> edgeUseCounts, int ia, int ib)
    {
        var edge = ia <= ib ? (ia, ib) : (ib, ia);
        edgeUseCounts.TryGetValue(edge, out var count);
        edgeUseCounts[edge] = count + 1;
    }

    private static int CountShells(List<(int A, int B, int C)> triangles, Dictionary<int, List<int>> trianglesByVertex)
    {
        if (triangles.Count == 0)
        {
            return 0;
        }

        var visited = new bool[triangles.Count];
        var shells = 0;

        for (var i = 0; i < triangles.Count; i++)
        {
            if (visited[i])
            {
                continue;
            }

            shells++;
            var queue = new Queue<int>();
            visited[i] = true;
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var tri = triangles[current];

                VisitConnectedTriangles(trianglesByVertex, tri.A, visited, queue);
                VisitConnectedTriangles(trianglesByVertex, tri.B, visited, queue);
                VisitConnectedTriangles(trianglesByVertex, tri.C, visited, queue);
            }
        }

        return shells;
    }

    private static void VisitConnectedTriangles(Dictionary<int, List<int>> trianglesByVertex, int vertex, bool[] visited, Queue<int> queue)
    {
        if (!trianglesByVertex.TryGetValue(vertex, out var neighbors))
        {
            return;
        }

        foreach (var next in neighbors)
        {
            if (visited[next])
            {
                continue;
            }

            visited[next] = true;
            queue.Enqueue(next);
        }
    }

    private static void AddTriangleReference(Dictionary<int, List<int>> map, int vertex, int triIndex)
    {
        if (!map.TryGetValue(vertex, out var list))
        {
            list = [];
            map[vertex] = list;
        }

        list.Add(triIndex);
    }


    private static (double surfaceArea, double signedVolume) ComputeGeometricStats(MeshModel mesh, List<(int A, int B, int C)> triangles)
    {
        var surfaceArea = 0.0;
        var signedVolume = 0.0;

        foreach (var tri in triangles)
        {
            var aIdx = tri.A * 3;
            var bIdx = tri.B * 3;
            var cIdx = tri.C * 3;

            var a = (x: mesh.Vertices[aIdx], y: mesh.Vertices[aIdx + 1], z: mesh.Vertices[aIdx + 2]);
            var b = (x: mesh.Vertices[bIdx], y: mesh.Vertices[bIdx + 1], z: mesh.Vertices[bIdx + 2]);
            var c = (x: mesh.Vertices[cIdx], y: mesh.Vertices[cIdx + 1], z: mesh.Vertices[cIdx + 2]);

            surfaceArea += TriangleArea(a, b, c);
            signedVolume += SignedTetraVolume(a, b, c);
        }

        return (surfaceArea, signedVolume);
    }

    private static double Distance((float x, float y, float z) a, (float x, float y, float z) b)
    {
        var dx = a.x - b.x;
        var dy = a.y - b.y;
        var dz = a.z - b.z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static double SignedTetraVolume((float x, float y, float z) a, (float x, float y, float z) b, (float x, float y, float z) c)
        => ((a.x * ((b.y * c.z) - (b.z * c.y))) -
            (a.y * ((b.x * c.z) - (b.z * c.x))) +
            (a.z * ((b.x * c.y) - (b.y * c.x)))) / 6.0;

    private static double TriangleArea((float x, float y, float z) a, (float x, float y, float z) b, (float x, float y, float z) c)
    {
        var abx = b.x - a.x;
        var aby = b.y - a.y;
        var abz = b.z - a.z;
        var acx = c.x - a.x;
        var acy = c.y - a.y;
        var acz = c.z - a.z;

        var cx = (aby * acz) - (abz * acy);
        var cy = (abz * acx) - (abx * acz);
        var cz = (abx * acy) - (aby * acx);
        return 0.5 * Math.Sqrt((cx * cx) + (cy * cy) + (cz * cz));
    }
}
