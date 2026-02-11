using ShapeForge.Core.Backends;
using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace ShapeForge.Core.Pipeline;

public record RunManifest(
    DateTimeOffset CreatedAtUtc,
    RunManifestInputs Inputs,
    RunManifestRuntimeConfig Runtime,
    IReadOnlyList<RunManifestStepLog> Steps,
    RunManifestOutputs Outputs);

public record RunManifestInputs(
    string FileHash,
    RunManifestMeshStats MeshStats,
    string Units);

public record RunManifestMeshStats(
    int VertexCount,
    int TriangleCount,
    float[] BoundsMin,
    float[] BoundsMax);

public record RunManifestRuntimeConfig(
    string Preset,
    string Profile,
    ExecutionMode ExecutionMode,
    int Seed,
    IReadOnlyDictionary<string, string> BackendVersions);

public record RunManifestStepLog(
    string OperatorId,
    IReadOnlyDictionary<string, double> ResolvedParams,
    TimeSpan Timing,
    IReadOnlyDictionary<string, double> KeyMetrics,
    IReadOnlyList<string> Warnings);

public record RunManifestOutputs(
    string MeshHash,
    MeshDiagnostics? FinalDiagnostics,
    string ReadinessStatus);

public static class RunManifestBuilder
{
    public static RunManifest Build(
        MeshModel inputMesh,
        PipelineRunResult run,
        OperatorContext context,
        string preset,
        string profile,
        string? fileHash = null,
        string? readinessStatus = null,
        IReadOnlyDictionary<string, string>? backendVersions = null)
    {
        var bounds = MeshMetrics.Bounds(inputMesh);
        var inputs = new RunManifestInputs(
            fileHash ?? ComputeMeshHash(inputMesh),
            new RunManifestMeshStats(
                VertexCount: inputMesh.Vertices.Length / 3,
                TriangleCount: MeshMetrics.TriangleCount(inputMesh),
                BoundsMin: [bounds.minX, bounds.minY, bounds.minZ],
                BoundsMax: [bounds.maxX, bounds.maxY, bounds.maxZ]),
            inputMesh.Units);

        var runtime = new RunManifestRuntimeConfig(
            preset,
            profile,
            context.ExecutionMode,
            context.Seed,
            backendVersions ?? ResolveDefaultBackendVersions());

        var steps = run.StepReports
            .Select(report => new RunManifestStepLog(
                OperatorId: report.Name,
                ResolvedParams: new Dictionary<string, double>(report.ModeAdjustedParameters, StringComparer.OrdinalIgnoreCase),
                Timing: report.Elapsed ?? TimeSpan.Zero,
                KeyMetrics: new Dictionary<string, double>(report.Metrics, StringComparer.OrdinalIgnoreCase),
                Warnings: report.Warnings.AsReadOnly()))
            .ToList();

        var outputs = new RunManifestOutputs(
            ComputeMeshHash(run.FinalMesh),
            run.PostDiagnostics,
            readinessStatus ?? "Unknown");

        return new RunManifest(DateTimeOffset.UtcNow, inputs, runtime, steps, outputs);
    }

    public static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeMeshHash(MeshModel mesh)
    {
        using var sha = SHA256.Create();
        AppendFloats(sha, mesh.Vertices);
        AppendInts(sha, mesh.Indices);
        if (mesh.Normals is { Length: > 0 } normals)
        {
            AppendFloats(sha, normals);
        }

        var unitsBytes = System.Text.Encoding.UTF8.GetBytes(mesh.Units ?? string.Empty);
        sha.TransformBlock(unitsBytes, 0, unitsBytes.Length, null, 0);
        sha.TransformFinalBlock([], 0, 0);

        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    public static IReadOnlyDictionary<string, string> ResolveDefaultBackendVersions()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["geometry.default"] = FormatVersion(typeof(DefaultMeshBackend).Assembly.GetName().Version),
            ["volume.null"] = FormatVersion(typeof(NullVolumeBackend).Assembly.GetName().Version)
        };

    private static string FormatVersion(Version? version)
        => version?.ToString() ?? "unknown";

    private static void AppendFloats(HashAlgorithm hash, float[] values)
    {
        var chunk = new byte[sizeof(float)];
        foreach (var value in values)
        {
            BinaryPrimitives.WriteSingleLittleEndian(chunk, value);
            hash.TransformBlock(chunk, 0, chunk.Length, null, 0);
        }
    }

    private static void AppendInts(HashAlgorithm hash, int[] values)
    {
        var chunk = new byte[sizeof(int)];
        foreach (var value in values)
        {
            BinaryPrimitives.WriteInt32LittleEndian(chunk, value);
            hash.TransformBlock(chunk, 0, chunk.Length, null, 0);
        }
    }
}
