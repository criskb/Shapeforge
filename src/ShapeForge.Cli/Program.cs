using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.IO;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;
using System.Text.Json;

var registry = new OperatorRegistry();
registry.Register(new RepairFixOperator());
registry.Register(new ThicknessEnforceOperator(1.2f, ThicknessMode.Inflate));

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    PrintHelp();
    return;
}

switch (args[0])
{
    case "version":
    case "--version":
    case "-v":
        Console.WriteLine("ShapeForge CLI v0.1.0");
        break;

    case "operators":
    case "list-operators":
        foreach (var op in registry.List())
        {
            Console.WriteLine($"{op.Id} :: {op.DisplayName}");
        }

        break;

    case "fix":
        await RunFixAsync(args.Skip(1).ToArray(), registry);
        break;

    case "diagnose":
        await RunDiagnoseAsync(args.Skip(1).ToArray(), registry);
        break;

    default:
        Console.Error.WriteLine($"Unknown command: {args[0]}");
        PrintHelp();
        Environment.ExitCode = 2;
        break;
}

static async Task RunFixAsync(string[] args, OperatorRegistry registry)
{
    string? input = null;
    string? output = null;
    var preset = PrintPreset.Fdm;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--in":
                if (!TryReadArgumentValue(args, ref i, out input))
                {
                    Console.Error.WriteLine("Missing value for --in.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls]");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--out":
                if (!TryReadArgumentValue(args, ref i, out output))
                {
                    Console.Error.WriteLine("Missing value for --out.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls]");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--preset":
                if (!TryReadArgumentValue(args, ref i, out var presetRaw))
                {
                    Console.Error.WriteLine("Missing value for --preset.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Enum.TryParse<PrintPreset>(presetRaw, ignoreCase: true, out preset))
                {
                    Console.Error.WriteLine($"Unsupported preset '{presetRaw}'. Use Fdm, Sla, or Sls.");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
        }
    }

    if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
    {
        Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls]");
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        var parameters = Presets.Resolve(preset);
        var io = new StlMeshIO();
        var mesh = await io.LoadStlAsync(input);

        var ctx = new OperatorContext(
            parameters.VoxelSizeMm,
            new Progress<float>(_ => { }),
            Console.WriteLine,
            new Dictionary<string, object>());

        var runner = new PipelineRunner();
        var steps = ResolvePresetPipeline(preset, parameters, registry);

        var preDiagnostics = ReportCard.Build(mesh);
        var (fixedMesh, reports) = await runner.RunAsync(mesh, steps, ctx, CancellationToken.None);
        var postDiagnostics = ReportCard.Build(fixedMesh, reports);

        await io.SaveStlAsync(output, fixedMesh);

        Console.WriteLine($"Saved improved mesh to {output}");
        PrintDiagnosticsSummary("Pre-fix diagnostics", preDiagnostics);
        PrintDiagnosticsSummary("Post-fix diagnostics", postDiagnostics);

        foreach (var report in reports)
        {
            Console.WriteLine($"[{report.Name}]");
            foreach (var metric in report.Metrics)
            {
                Console.WriteLine($"{metric.Key}: {metric.Value}");
            }

            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"WARNING: {warning}");
            }

            foreach (var note in report.Notes)
            {
                Console.WriteLine($"note: {note}");
            }
        }

        foreach (var issue in postDiagnostics.Issues.Where(i => i.Severity >= IssueSeverity.Warning))
        {
            Console.WriteLine($"WARNING: {issue.Code} - {issue.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Fix failed: {ex.Message}");
        Environment.ExitCode = 1;
    }
}

static async Task RunDiagnoseAsync(string[] args, OperatorRegistry registry)
{
    string? input = null;
    string? jsonOutput = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--in":
                if (!TryReadArgumentValue(args, ref i, out input))
                {
                    Console.Error.WriteLine("Missing value for --in.");
                    Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--json [report.json]]");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--json":
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    jsonOutput = args[++i];
                }
                else
                {
                    jsonOutput = string.Empty;
                }

                break;
        }
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--json [report.json]]");
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        var io = new StlMeshIO();
        var mesh = await io.LoadStlAsync(input);

        var diagnostics = ReportCard.Build(mesh);
        if (!registry.TryGet("repair.fix", out _))
        {
            diagnostics.Issues.Add(new DiagnosticIssue(IssueSeverity.Error, "operator.missing", "repair.fix operator is not registered."));
        }

        PrintDiagnosticsSummary($"Diagnostics for {input}", diagnostics);

        if (jsonOutput is not null)
        {
            var path = string.IsNullOrWhiteSpace(jsonOutput)
                ? Path.ChangeExtension(input, ".diagnostics.json")
                : jsonOutput;

            var payload = new
            {
                SchemaVersion = diagnostics.SchemaVersion,
                Input = input,
                GeneratedAtUtc = DateTime.UtcNow,
                Topology = diagnostics.Topology,
                Quality = diagnostics.Quality,
                Printability = diagnostics.Printability,
                Issues = diagnostics.Issues.Select(i => new
                {
                    Severity = i.Severity.ToString(),
                    i.Code,
                    i.Message,
                    i.Count,
                    i.Details
                })
            };

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            Console.WriteLine($"Wrote diagnostics JSON: {path}");
        }

        var maxSeverity = diagnostics.Issues.MaxBy(d => d.Severity)?.Severity ?? IssueSeverity.Info;
        Environment.ExitCode = maxSeverity >= IssueSeverity.Warning ? 2 : 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Diagnose failed: {ex.Message}");
        Environment.ExitCode = 1;
    }
}

static IReadOnlyList<IOperator> ResolvePresetPipeline(PrintPreset preset, PresetParameters parameters, OperatorRegistry registry)
{
    var steps = new List<IOperator>();

    if (!registry.TryGet("repair.fix", out var repair) || repair is null)
    {
        throw new InvalidOperationException("repair.fix is not registered.");
    }

    steps.Add(repair);

    var thicknessMode = preset == PrintPreset.Sls ? ThicknessMode.Reshell : ThicknessMode.Inflate;
    steps.Add(new ThicknessEnforceOperator(parameters.MinWallMm, thicknessMode));
    return steps;
}

static void PrintDiagnosticsSummary(string title, MeshDiagnostics diagnostics)
{
    Console.WriteLine(title);
    Console.WriteLine($"Schema: {diagnostics.SchemaVersion}");
    Console.WriteLine($"Topology: triangles={diagnostics.Topology.GetValueOrDefault("triangles.count"):0.###}, vertices={diagnostics.Topology.GetValueOrDefault("vertices.count"):0.###}");

    foreach (var finding in diagnostics.Issues)
    {
        var prefix = finding.Severity switch
        {
            IssueSeverity.Error => "ERROR",
            IssueSeverity.Warning => "WARN ",
            _ => "INFO "
        };

        Console.WriteLine($"[{prefix}] {finding.Code}: {finding.Message}" +
                          (finding.Count > 1 ? $" (count={finding.Count})" : string.Empty));
    }
}

static bool TryReadArgumentValue(string[] args, ref int index, out string? value)
{
    value = null;
    if (index + 1 >= args.Length)
    {
        return false;
    }

    var candidate = args[index + 1];
    if (candidate.StartsWith("--", StringComparison.Ordinal))
    {
        return false;
    }

    index++;
    value = candidate;
    return true;
}

static void PrintHelp()
{
    Console.WriteLine("ShapeForge CLI");
    Console.WriteLine("  version                 Show version");
    Console.WriteLine("  operators               List available operators");
    Console.WriteLine("  fix --in --out [--preset Fdm|Sla|Sls]   Run repair preset on STL");
    Console.WriteLine("  diagnose --in [--json [path]]            Analyze mesh and optionally write JSON");
}
