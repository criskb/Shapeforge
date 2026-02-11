using System.Diagnostics;
using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.IO;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Tests;

public class FixtureBasedRepairAndDiagnoseTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string FixturesDir = Path.Combine(RepoRoot, "tests", "ShapeForge.Tests", "Fixtures");

    [Fact]
    public async Task Diagnostics_IssueCountsAndSeverity_AreDeterministicForFixtures()
    {
        var io = new StlMeshIO();
        var cube = await io.LoadStlAsync(Path.Combine(FixturesDir, "cube_ok.stl"));
        var nonmanifold = await io.LoadStlAsync(Path.Combine(FixturesDir, "nonmanifold_edge.stl"));

        var cubeDiagnosticsA = ReportCard.Build(cube);
        var cubeDiagnosticsB = ReportCard.Build(cube);
        var nonmanifoldDiagnostics = ReportCard.Build(nonmanifold);

        Assert.Equal(cubeDiagnosticsA.Issues, cubeDiagnosticsB.Issues);
        Assert.True(cubeDiagnosticsA.HasWarningsOrErrors);
        Assert.Contains(cubeDiagnosticsA.Issues, i => i.Code == "mesh.low-triangle-count" && i.Severity == IssueSeverity.Warning);
        Assert.Contains(cubeDiagnosticsA.Issues, i => i.Code == "mesh.normals.missing" && i.Severity == IssueSeverity.Info);

        Assert.True(nonmanifoldDiagnostics.HasWarningsOrErrors);
        Assert.Contains(nonmanifoldDiagnostics.Issues, i => i.Code == "mesh.low-triangle-count" && i.Count == 4);
        Assert.DoesNotContain(nonmanifoldDiagnostics.Issues, i => i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task RepairFixOperator_FillsSmallHole_WhenRepairModeIsAggressive()
    {
        var io = new StlMeshIO();
        var meshWithHole = await io.LoadStlAsync(Path.Combine(FixturesDir, "cube_hole.stl"));
        var op = new RepairFixOperator();
        var ctx = BuildContext(RepairMode.Aggressive);

        var (repaired, report) = await op.RunAsync(meshWithHole, ctx, CancellationToken.None);

        Assert.Equal(10, meshWithHole.Indices.Length / 3);
        Assert.Equal(12, repaired.Indices.Length / 3);
        Assert.Equal(2, report.Metrics["triangles.added.hole-closure"]);
    }

    [Fact]
    public async Task RepairFixOperator_ReducesDegeneratesAndDuplicates_AndKeepsResultStable()
    {
        var input = new MeshModel(
            Vertices:
            [
                0, 0, 0,
                1, 0, 0,
                1, 1, 0,
                0, 1, 0,
                1.00001f, 1.00001f, 0
            ],
            Indices:
            [
                0, 1, 2,
                0, 2, 3,
                0, 1, 2,
                0, 4, 4
            ],
            Normals: null);

        var op = new RepairFixOperator(closeRadiusMm: 0f);
        var ctx = BuildContext(RepairMode.Balanced);

        var (meshA, reportA) = await op.RunAsync(input, ctx, CancellationToken.None);
        var (meshB, reportB) = await op.RunAsync(input, ctx, CancellationToken.None);

        Assert.Equal(2, meshA.Indices.Length / 3);
        Assert.Equal(1, reportA.Metrics["triangles.removed.degenerate"]);
        Assert.Equal(1, reportA.Metrics["triangles.removed.duplicate"]);
        Assert.Equal(meshA.Indices, meshB.Indices);
        Assert.Equal(reportA.Metrics.OrderBy(x => x.Key), reportB.Metrics.OrderBy(x => x.Key));
    }

    [Fact]
    public async Task RepairFixOperator_RemovesTinyDetachedShells_BasedOnThreshold()
    {
        var io = new StlMeshIO();
        var input = await io.LoadStlAsync(Path.Combine(FixturesDir, "tiny_shells.stl"));

        var op = new RepairFixOperator(closeRadiusMm: 0.6f);
        var ctx = BuildContext(RepairMode.Balanced);

        var (repaired, report) = await op.RunAsync(input, ctx, CancellationToken.None);

        Assert.Equal(24, input.Indices.Length / 3);
        Assert.Equal(12, repaired.Indices.Length / 3);
        Assert.Equal(12, report.Metrics["triangles.removed.tiny-shells"]);
        Assert.Equal(0, report.Metrics["triangles.added.hole-closure"]);
    }

    [Fact]
    public async Task CliDiagnose_ExitCodes_FollowSeveritySemantics()
    {
        var io = new StlMeshIO();
        var healthyFile = Path.Combine(Path.GetTempPath(), $"sf-grid-{Guid.NewGuid():N}.stl");
        await io.SaveStlAsync(healthyFile, BuildGridMesh(11));

        try
        {
            var warningExit = await RunCliAsync($"diagnose --in \"{Path.Combine(FixturesDir, "cube_ok.stl")}\"");
            var okExit = await RunCliAsync($"diagnose --in \"{healthyFile}\"");
            var usageExit = await RunCliAsync("diagnose");

            Assert.Equal(2, warningExit);
            Assert.Equal(0, okExit);
            Assert.Equal(2, usageExit);
        }
        finally
        {
            if (File.Exists(healthyFile))
            {
                File.Delete(healthyFile);
            }
        }
    }

    private static OperatorContext BuildContext(RepairMode mode)
        => new(
            0.2f,
            new Progress<float>(_ => { }),
            _ => { },
            new Dictionary<string, object>(),
            "mm",
            ProcessMode.Fdm,
            PresetQuality.Final,
            MinimumWallPolicy.Strict,
            1.2f,
            45f,
            0f,
            mode);

    private static MeshModel BuildGridMesh(int cellsPerAxis)
    {
        var vertices = new List<float>();
        var indices = new List<int>();
        var indexByPosition = new Dictionary<(int X, int Y), int>();

        for (var y = 0; y <= cellsPerAxis; y++)
        {
            for (var x = 0; x <= cellsPerAxis; x++)
            {
                var idx = vertices.Count / 3;
                indexByPosition[(x, y)] = idx;
                vertices.Add(x);
                vertices.Add(y);
                vertices.Add(0);
            }
        }

        for (var y = 0; y < cellsPerAxis; y++)
        {
            for (var x = 0; x < cellsPerAxis; x++)
            {
                var v00 = indexByPosition[(x, y)];
                var v10 = indexByPosition[(x + 1, y)];
                var v11 = indexByPosition[(x + 1, y + 1)];
                var v01 = indexByPosition[(x, y + 1)];

                indices.Add(v00);
                indices.Add(v10);
                indices.Add(v11);

                indices.Add(v00);
                indices.Add(v11);
                indices.Add(v01);
            }
        }

        return new MeshModel(vertices.ToArray(), indices.ToArray(), Normals: null);
    }

    private static async Task<int> RunCliAsync(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(RepoRoot, "src", "ShapeForge.Cli", "ShapeForge.Cli.csproj")}\" -- {args}",
            WorkingDirectory = RepoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start CLI process.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ShapeForge.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
