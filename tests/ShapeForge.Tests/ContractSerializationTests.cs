using System.Text.Json;
using System.Text.Json.Serialization;
using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Tests;

public class ContractSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public void MeshModel_SerializesWithMetadata_AndRoundTrips()
    {
        var model = new MeshModel(
            Vertices: [0, 0, 0, 1, 0, 0, 0, 1, 0],
            Indices: [0, 1, 2],
            Normals: [0, 0, 1, 0, 0, 1, 0, 0, 1],
            Units: "mm",
            SourceInfo: new MeshSourceInfo("/tmp/source.stl", "stl", DateTimeOffset.Parse("2025-01-01T00:00:00Z"), "abc123"),
            Tags: new Dictionary<string, string> { ["part"] = "sample" });

        var json = JsonSerializer.Serialize(model, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<MeshModel>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Contains("\"units\":\"mm\"", json);
        Assert.Contains("\"sourceInfo\"", json);
        Assert.Equal("stl", roundTrip!.SourceInfo!.FileFormat);
        Assert.Equal("sample", roundTrip.Tags!["part"]);
    }

    [Fact]
    public void MeshDiagnostics_SerializesCountsBooleansAndPrintability()
    {
        var diagnostics = new MeshDiagnostics(
            "1.0",
            Topology: new Dictionary<string, double> { ["triangles.count"] = 12 },
            Quality: new Dictionary<string, double> { ["normals.missing"] = 0 },
            Printability: new Dictionary<string, double> { ["thin.vertices.after"] = 2 },
            Issues: [new DiagnosticIssue(IssueSeverity.Warning, "thin-wall", "Thin wall detected", 2)],
            Counts: new Dictionary<string, long> { ["triangles.count"] = 12 },
            Booleans: new Dictionary<string, bool> { ["mesh.has-warnings-or-errors"] = true });

        var json = JsonSerializer.Serialize(diagnostics, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<MeshDiagnostics>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Contains("\"counts\"", json);
        Assert.Contains("\"booleans\"", json);
        Assert.Equal(12, roundTrip!.CountMetrics["triangles.count"]);
        Assert.True(roundTrip.BooleanFlags["mesh.has-warnings-or-errors"]);
    }
    [Fact]
    public void MeshDiagnostics_FromLegacyV1Json_NormalizesToCurrentSchema()
    {
        const string v1Json = """
        {
          "diagnosticsVersion": "1.0",
          "topology": { "triangles.count": 12 },
          "quality": { "normals.missing": 0 },
          "printability": { "thin.vertices.after": 2 },
          "issues": [
            {
              "severity": "warning",
              "code": "thin-wall",
              "message": "Thin wall detected",
              "count": 2
            }
          ],
          "counts": { "triangles.count": 12 },
          "booleans": { "mesh.has-warnings-or-errors": true }
        }
        """;

        var normalized = MeshDiagnostics.FromJson(v1Json);
        var canonical = normalized.ToJson();

        Assert.Equal(MeshDiagnostics.CurrentSchemaVersion, normalized.SchemaVersion);
        Assert.Contains("\"schemaVersion\": \"1.0\"", canonical);
        Assert.DoesNotContain("diagnosticsVersion", canonical, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12, normalized.CountMetrics["triangles.count"]);
    }

    [Fact]
    public void MeshDiagnostics_FromUnsupportedMajorVersion_Throws()
    {
        const string invalidJson = """
        {
          "schemaVersion": "2.0",
          "topology": {},
          "quality": {},
          "printability": {},
          "issues": []
        }
        """;

        var ex = Assert.Throws<InvalidOperationException>(() => MeshDiagnostics.FromJson(invalidJson));
        Assert.Contains("Unsupported diagnostics schema version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void OperatorAndPipelineContracts_SerializeAsStableV1Shape()
    {
        var report = new OpReport(
            Name: "thickness.enforce",
            Metrics: new Dictionary<string, double> { ["thin.vertices.after"] = 1 },
            Warnings: ["thin wall warning"],
            Notes: ["details"],
            Issues: [new DiagnosticIssue(IssueSeverity.Warning, "thin", "thin wall", 1)],
            ModeAdjustedParams: new Dictionary<string, double> { ["sampling.density.scale"] = 0.7 },
            Elapsed: TimeSpan.FromMilliseconds(10));

        var schema = new OperatorSchema(
            "thickness.enforce",
            "Minimum Wall Thickness",
            "1.0",
            "Detects thin regions.",
            [new OperatorParameterSchema("minimumMm", OperatorParameterType.Number, "Minimum wall", true, 1.2, Min: 0)]);

        var run = new PipelineRunResult(
            new MeshModel([0, 0, 0], [0, 0, 0], null),
            null,
            null,
            [report],
            TimeSpan.FromMilliseconds(123),
            new Dictionary<string, TimeSpan> { ["thickness.enforce"] = TimeSpan.FromMilliseconds(10) });

        var reportJson = JsonSerializer.Serialize(report, JsonOptions);
        var schemaJson = JsonSerializer.Serialize(schema, JsonOptions);
        var pipelineJson = JsonSerializer.Serialize(run, JsonOptions);

        Assert.Contains("\"issues\"", reportJson);
        Assert.Contains("\"modeAdjustedParams\"", reportJson);
        Assert.Contains("\"parameters\"", schemaJson);
        Assert.Contains("\"elapsed\"", pipelineJson);
        Assert.Contains("\"stepElapsed\"", pipelineJson);
        Assert.NotNull(JsonSerializer.Deserialize<OpReport>(reportJson, JsonOptions));
        Assert.NotNull(JsonSerializer.Deserialize<OperatorSchema>(schemaJson, JsonOptions));
        Assert.NotNull(JsonSerializer.Deserialize<PipelineRunResult>(pipelineJson, JsonOptions));
    }


    [Fact]
    public void RunManifest_SerializesAndIncludesReproducibilityFields()
    {
        var inputMesh = new MeshModel(
            Vertices: [0, 0, 0, 1, 0, 0, 0, 1, 0],
            Indices: [0, 1, 2],
            Normals: null,
            Units: "mm");

        var report = new OpReport(
            Name: "repair.fix",
            Metrics: new Dictionary<string, double> { ["components.before"] = 2 },
            Warnings: ["auto-bridge may alter tiny features"],
            Notes: ["seed=123"],
            ModeAdjustedParams: new Dictionary<string, double> { ["seed"] = 123 },
            Elapsed: TimeSpan.FromMilliseconds(42));

        var run = new PipelineRunResult(
            FinalMesh: inputMesh,
            PreDiagnostics: null,
            PostDiagnostics: new MeshDiagnostics(
                "1.0",
                Topology: new Dictionary<string, double>(),
                Quality: new Dictionary<string, double>(),
                Printability: new Dictionary<string, double>(),
                Issues: [],
                Counts: new Dictionary<string, long>(),
                Booleans: new Dictionary<string, bool>()),
            StepReports: [report],
            Elapsed: TimeSpan.FromMilliseconds(99),
            StepElapsed: new Dictionary<string, TimeSpan> { ["repair.fix"] = TimeSpan.FromMilliseconds(42) });

        var context = new OperatorContext(
            0.2f,
            new Progress<float>(_ => { }),
            _ => { },
            new Dictionary<string, object>(),
            "mm",
            ProcessMode.Fdm,
            PresetQuality.Final,
            MinimumWallPolicy.Enforce,
            1.2f,
            45f,
            2f,
            RepairMode.Balanced,
            ExecutionMode.Final,
            QualityScalingPolicy.ForMode(ExecutionMode.Final),
            Seed: 1337);

        var manifest = RunManifestBuilder.Build(
            inputMesh,
            run,
            context,
            preset: "Fdm",
            profile: "Fdm-Final-Balanced",
            fileHash: "deadbeef",
            readinessStatus: "Green");

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<RunManifest>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Contains("\"inputs\"", json);
        Assert.Contains("\"runtime\"", json);
        Assert.Contains("\"steps\"", json);
        Assert.Contains("\"outputs\"", json);
        Assert.Equal("deadbeef", roundTrip!.Inputs.FileHash);
        Assert.Equal("Green", roundTrip.Outputs.ReadinessStatus);
        Assert.Single(roundTrip.Steps);
    }

}
