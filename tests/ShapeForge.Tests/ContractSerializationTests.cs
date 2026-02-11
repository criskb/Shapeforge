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
}
