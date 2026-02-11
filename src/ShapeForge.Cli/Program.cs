using ShapeForge.Core.IO;
using ShapeForge.Core.Diagnostics;
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
        var (fixedMesh, reports) = await runner.RunAsync(mesh, steps, ctx, CancellationToken.None);
        await io.SaveStlAsync(output, fixedMesh);

        Console.WriteLine($"Saved improved mesh to {output}");
        foreach (var report in reports)
        {
            Console.WriteLine($"[{report.Name}]");
            foreach (var metric in report.Metrics)
            {
                Console.WriteLine($"{metric.Key}: {metric.Value}");
            }
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

        var diagnostics = ComputeDiagnostics(mesh, registry);
        PrintDiagnosticsSummary(input, diagnostics);

        if (jsonOutput is not null)
        {
            var path = string.IsNullOrWhiteSpace(jsonOutput)
                ? Path.ChangeExtension(input, ".diagnostics.json")
                : jsonOutput;

            var payload = new
            {
                Input = input,
                GeneratedAtUtc = DateTime.UtcNow,
                Diagnostics = diagnostics.Select(d => new { d.Severity, d.Code, d.Message, d.Value })
            };

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            Console.WriteLine($"Wrote diagnostics JSON: {path}");
        }

        var maxSeverity = diagnostics.MaxBy(d => d.Severity)?.Severity ?? DiagnosticSeverity.Info;
        Environment.ExitCode = maxSeverity >= DiagnosticSeverity.Warning ? 2 : 0;
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

static IReadOnlyList<DiagnosticFinding> ComputeDiagnostics(ShapeForge.Core.Geometry.MeshModel mesh, OperatorRegistry registry)
{
    var findings = new List<DiagnosticFinding>();
    var triangleCount = mesh.Indices.Length / 3.0;
    if (triangleCount <= 0)
    {
        findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "mesh.empty", "Mesh has zero triangles.", triangleCount));
    }
    else if (triangleCount < 100)
    {
        findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "mesh.low-triangle-count", "Mesh triangle count is very low.", triangleCount));
    }
    else
    {
        findings.Add(new DiagnosticFinding(DiagnosticSeverity.Info, "mesh.triangle-count", "Mesh triangle count looks normal.", triangleCount));
    }

    if (!registry.TryGet("repair.fix", out _))
    {
        findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "operator.missing", "repair.fix operator is not registered."));
    }

    return findings;
}

static void PrintDiagnosticsSummary(string input, IReadOnlyList<DiagnosticFinding> diagnostics)
{
    Console.WriteLine($"Diagnostics for {input}");
    foreach (var finding in diagnostics)
    {
        var prefix = finding.Severity switch
        {
            DiagnosticSeverity.Error => "ERROR",
            DiagnosticSeverity.Warning => "WARN ",
            _ => "INFO "
        };

        Console.WriteLine($"[{prefix}] {finding.Code}: {finding.Message}" +
                          (finding.Value.HasValue ? $" ({finding.Value.Value:0.###})" : string.Empty));
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

enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

record DiagnosticFinding(DiagnosticSeverity Severity, string Code, string Message, double? Value = null);

static void PrintHelp()
{
    Console.WriteLine("ShapeForge CLI");
    Console.WriteLine("  version                 Show version");
    Console.WriteLine("  operators               List available operators");
    Console.WriteLine("  fix --in --out [--preset Fdm|Sla|Sls]   Run repair preset on STL");
    Console.WriteLine("  diagnose --in [--json [path]]            Analyze mesh and optionally write JSON");
}
