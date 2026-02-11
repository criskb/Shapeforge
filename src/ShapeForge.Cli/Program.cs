using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.IO;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;
using System.Text.Json;

var registry = new OperatorRegistry();
registry.Register(new RepairFixOperator());
registry.Register(new ThicknessEnforceOperator(1.2f, ThicknessMode.Inflate));
registry.RegisterCompatibilityMap(DeprecatedOperatorIds.Map);

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
        RunOperatorsCommand(args.Skip(1).ToArray(), registry);
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
    string? unitsOverride = null;
    ProcessMode? modeOverride = null;
    PresetQuality? qualityOverride = null;
    RepairMode? repairModeOverride = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--in":
                if (!TryReadArgumentValue(args, ref i, out input))
                {
                    Console.Error.WriteLine("Missing value for --in.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--out":
                if (!TryReadArgumentValue(args, ref i, out output))
                {
                    Console.Error.WriteLine("Missing value for --out.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--preset":
                if (!TryReadArgumentValue(args, ref i, out var presetRaw))
                {
                    Console.Error.WriteLine("Missing value for --preset.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Presets.TryParsePreset(presetRaw, out preset))
                {
                    Console.Error.WriteLine($"Unsupported preset '{presetRaw}'. Use Fdm, Sla/Resin, or Sls.");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--mode":
                if (!TryReadArgumentValue(args, ref i, out var modeRaw))
                {
                    Console.Error.WriteLine("Missing value for --mode.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Presets.TryParseMode(modeRaw, out var parsedMode))
                {
                    Console.Error.WriteLine($"Unsupported mode '{modeRaw}'. Use Fdm, Resin, or Sls.");
                    Environment.ExitCode = 2;
                    return;
                }

                modeOverride = parsedMode;
                break;
            case "--quality":
                if (!TryReadArgumentValue(args, ref i, out var qualityRaw))
                {
                    Console.Error.WriteLine("Missing value for --quality.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Presets.TryParseQuality(qualityRaw, out var parsedQuality))
                {
                    Console.Error.WriteLine($"Unsupported quality '{qualityRaw}'. Use Preview or Final.");
                    Environment.ExitCode = 2;
                    return;
                }

                qualityOverride = parsedQuality;
                break;
            case "--units":
                if (!TryReadArgumentValue(args, ref i, out unitsOverride))
                {
                    Console.Error.WriteLine("Missing value for --units.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--repair-mode":
                if (!TryReadArgumentValue(args, ref i, out var repairModeRaw))
                {
                    Console.Error.WriteLine("Missing value for --repair-mode.");
                    Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Presets.TryParseRepairMode(repairModeRaw, out var parsedRepairMode))
                {
                    Console.Error.WriteLine($"Unsupported repair mode '{repairModeRaw}'. Use Conservative, Balanced, or Aggressive.");
                    Environment.ExitCode = 2;
                    return;
                }

                repairModeOverride = parsedRepairMode;
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
    {
        Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        var profile = Presets.Resolve(preset, unitsOverride, modeOverride, qualityOverride, repairModeOverride);
        var io = new StlMeshIO();
        var mesh = await io.LoadStlAsync(input);

        if (!string.IsNullOrWhiteSpace(profile.Units))
        {
            mesh = mesh with { Units = profile.Units };
        }


        var executionMode = profile.Quality switch
        {
            PresetQuality.Preview => ExecutionMode.Preview,
            PresetQuality.Final => ExecutionMode.Final,
            _ => ExecutionMode.Standard
        };
        var scalingPolicy = QualityScalingPolicy.ForMode(executionMode);

        var ctx = new OperatorContext(
            profile.VoxelSizeMm,
            new Progress<float>(_ => { }),
            Console.WriteLine,
            new Dictionary<string, object>
            {
                ["profile.preset"] = preset.ToString(),
                ["profile.mode"] = profile.Mode.ToString(),
                ["profile.quality"] = profile.Quality.ToString()
            },
            profile.Units,
            profile.Mode,
            profile.Quality,
            profile.MinWallPolicy,
            profile.MinWallMm,
            profile.OverhangThresholdDeg,
            profile.MinimumDrainHoleMm,
            profile.RepairMode,
            executionMode,
            scalingPolicy,
            Seed: 1337);

        var runner = new PipelineRunner();
        var steps = ResolvePresetPipeline(profile, registry);
        PrintOperatorSupportWarnings(steps, profile, BackendCapabilityFlags.FastMesh);

        var preDiagnostics = ReportCard.Build(mesh);
        var (fixedMesh, reports) = await runner.RunAsync(mesh, steps, ctx, CancellationToken.None);
        var postDiagnostics = ReportCard.Build(fixedMesh, reports);
        var evaluator = new ReadinessEvaluator();
        var preReadiness = evaluator.Evaluate(preDiagnostics, profile);
        var postReadiness = evaluator.Evaluate(postDiagnostics, profile);

        await io.SaveStlAsync(output, fixedMesh);

        Console.WriteLine($"Saved improved mesh to {output}");
        PrintDiagnosticsSummary("Pre-fix diagnostics", preDiagnostics);
        PrintReadinessSummary("Pre-fix readiness summary", preReadiness);
        PrintDiagnosticsSummary("Post-fix diagnostics", postDiagnostics);
        PrintReadinessSummary("Post-fix readiness summary", postReadiness);

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

static void PrintOperatorSupportWarnings(
    IEnumerable<IOperator> steps,
    PresetParameters profile,
    BackendCapabilityFlags availableBackends)
{
    foreach (var step in steps)
    {
        var support = OperatorSupportEvaluator.Evaluate(step.Schema, profile.Mode, profile.Quality, availableBackends);
        if (support.Level == OperatorSupportLevel.Supported)
        {
            continue;
        }

        Console.WriteLine($"WARNING: operator '{step.Id}' is {support.Level.ToString().ToLowerInvariant()} for mode={profile.Mode}, quality={profile.Quality}: {support.Reason}");
    }
}

static async Task RunDiagnoseAsync(string[] args, OperatorRegistry registry)
{
    string? input = null;
    string? jsonOutput = null;
    var preset = PrintPreset.Fdm;
    string? unitsOverride = null;
    ProcessMode? modeOverride = null;
    PresetQuality? qualityOverride = null;
    RepairMode? repairModeOverride = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--in":
                if (!TryReadArgumentValue(args, ref i, out input))
                {
                    Console.Error.WriteLine("Missing value for --in.");
                    Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive] [--json [report.json]]");
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
            case "--preset":
                if (!TryReadArgumentValue(args, ref i, out var presetRaw))
                {
                    Console.Error.WriteLine("Missing value for --preset.");
                    Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive] [--json [report.json]]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Presets.TryParsePreset(presetRaw, out preset))
                {
                    Console.Error.WriteLine($"Unsupported preset '{presetRaw}'. Use Fdm, Sla/Resin, or Sls.");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--mode":
                if (!TryReadArgumentValue(args, ref i, out var modeRaw))
                {
                    Console.Error.WriteLine("Missing value for --mode.");
                    Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive] [--json [report.json]]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Presets.TryParseMode(modeRaw, out var parsedMode))
                {
                    Console.Error.WriteLine($"Unsupported mode '{modeRaw}'. Use Fdm, Resin, or Sls.");
                    Environment.ExitCode = 2;
                    return;
                }

                modeOverride = parsedMode;
                break;
            case "--quality":
                if (!TryReadArgumentValue(args, ref i, out var qualityRaw))
                {
                    Console.Error.WriteLine("Missing value for --quality.");
                    Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive] [--json [report.json]]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Presets.TryParseQuality(qualityRaw, out var parsedQuality))
                {
                    Console.Error.WriteLine($"Unsupported quality '{qualityRaw}'. Use Preview or Final.");
                    Environment.ExitCode = 2;
                    return;
                }

                qualityOverride = parsedQuality;
                break;
            case "--units":
                if (!TryReadArgumentValue(args, ref i, out unitsOverride))
                {
                    Console.Error.WriteLine("Missing value for --units.");
                    Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive] [--json [report.json]]");
                    Environment.ExitCode = 2;
                    return;
                }

                break;
            case "--repair-mode":
                if (!TryReadArgumentValue(args, ref i, out var repairModeRaw))
                {
                    Console.Error.WriteLine("Missing value for --repair-mode.");
                    Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive] [--json [report.json]]");
                    Environment.ExitCode = 2;
                    return;
                }

                if (!Presets.TryParseRepairMode(repairModeRaw, out var parsedRepairMode))
                {
                    Console.Error.WriteLine($"Unsupported repair mode '{repairModeRaw}'. Use Conservative, Balanced, or Aggressive.");
                    Environment.ExitCode = 2;
                    return;
                }

                repairModeOverride = parsedRepairMode;
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.Error.WriteLine("Usage: shapeforge diagnose --in input.stl [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive] [--json [report.json]]");
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        var io = new StlMeshIO();
        var mesh = await io.LoadStlAsync(input);
        var profile = Presets.Resolve(preset, unitsOverride, modeOverride, qualityOverride, repairModeOverride);
        var evaluator = new ReadinessEvaluator();

        var diagnostics = ReportCard.Build(mesh);
        if (!registry.TryGet(RepairFixOperator.CanonicalId, out _))
        {
            diagnostics.Issues.Add(new DiagnosticIssue(IssueSeverity.Error, "operator.missing", $"{RepairFixOperator.CanonicalId} operator is not registered."));
        }

        PrintDiagnosticsSummary($"Diagnostics for {input}", diagnostics);
        var readiness = evaluator.Evaluate(diagnostics, profile);
        PrintReadinessSummary("Readiness summary", readiness);

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
                Readiness = new
                {
                    Status = readiness.Status.ToString(),
                    Grade = readiness.Grade.ToString(),
                    TopBlockers = readiness.TopBlockers.Select(b => new { b.Code, b.Message, b.RemediationHint }),
                    readiness.ConfidenceNote,
                    readiness.ConfidenceScore
                },
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

static IReadOnlyList<IOperator> ResolvePresetPipeline(PresetParameters profile, OperatorRegistry registry)
{
    var steps = new List<IOperator>();

    if (!registry.TryGet(RepairFixOperator.CanonicalId, out var repair) || repair is null)
    {
        throw new InvalidOperationException($"{RepairFixOperator.CanonicalId} is not registered.");
    }

    steps.Add(repair);

    steps.Add(new ThicknessEnforceOperator(profile.MinWallMm, profile.ThicknessMode));
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


static void RunOperatorsCommand(string[] args, OperatorRegistry registry)
{
    var format = "table";
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--format")
        {
            if (!TryReadArgumentValue(args, ref i, out var rawFormat))
            {
                Console.Error.WriteLine("Missing value for --format. Use table or json.");
                Environment.ExitCode = 2;
                return;
            }

            format = rawFormat ?? "table";
            continue;
        }

        Console.Error.WriteLine($"Unknown option for operators command: {args[i]}");
        Environment.ExitCode = 2;
        return;
    }

    var operators = registry.List()
        .Select(op =>
        {
            var schema = op.Schema;
            return new
            {
                id = op.Id,
                displayName = op.DisplayName,
                category = schema.Category,
                deterministic = schema.Deterministic,
                estimatedCost = schema.EstimatedCost,
                requiredBackendCapabilities = schema.RequiredBackendCapabilities.ToString(),
                supportedModes = schema.SupportedModes,
                supportedQualities = schema.SupportedQualities,
                version = schema.Version,
                description = schema.Description,
                parameters = schema.Parameters
            };
        })
        .ToArray();

    var compatibility = registry.CompatibilityMap
        .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
        .Select(kvp => new { oldId = kvp.Key, newId = kvp.Value })
        .ToArray();

    if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
    {
        var payload = new
        {
            operators,
            compatibility
        };

        Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));

        return;
    }

    if (!format.Equals("table", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Unknown format '{format}'. Use table or json.");
        Environment.ExitCode = 2;
        return;
    }

    foreach (var op in operators)
    {
        Console.WriteLine($"{op.id} :: {op.displayName}");
        Console.WriteLine($"  category: {op.category}");
        Console.WriteLine($"  deterministic: {op.deterministic}");
        Console.WriteLine($"  estimated-cost: {op.estimatedCost:0.###}");
        Console.WriteLine($"  required-backends: {op.requiredBackendCapabilities}");
        if (op.supportedModes is not null)
        {
            Console.WriteLine($"  supported-modes: {string.Join(", ", op.supportedModes)}");
        }

        if (op.supportedQualities is not null)
        {
            Console.WriteLine($"  supported-qualities: {string.Join(", ", op.supportedQualities)}");
        }

        Console.WriteLine($"  params: {op.parameters.Length}");
    }

    if (compatibility.Length > 0)
    {
        Console.WriteLine("compatibility:");
        foreach (var entry in compatibility)
        {
            Console.WriteLine($"  {entry.oldId} -> {entry.newId}");
        }
    }
}

static void PrintReadinessSummary(string title, ReadinessResult readiness)
{
    Console.WriteLine(title);
    var status = readiness.Status switch
    {
        ReadinessTrafficLight.Green => "🟢",
        ReadinessTrafficLight.Yellow => "🟡",
        _ => "🔴"
    };

    Console.WriteLine($"{status} Status: {readiness.Status} ({readiness.Grade})");
    if (readiness.TopBlockers.Count == 0)
    {
        Console.WriteLine("Top blockers: none");
    }
    else
    {
        Console.WriteLine("Top blockers:");
        foreach (var blocker in readiness.TopBlockers)
        {
            Console.WriteLine($" - {blocker.Code}: {blocker.Message}");
            Console.WriteLine($"   fix: {blocker.RemediationHint}");
        }
    }

    Console.WriteLine($"Confidence note: {readiness.ConfidenceNote}");
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
    Console.WriteLine("  operators [--format table|json]  List available operators");
    Console.WriteLine("  fix --in --out [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive]");
    Console.WriteLine("  diagnose --in [--preset Fdm|Sla|Sls] [--mode Fdm|Resin|Sls] [--quality Preview|Final] [--units mm|in] [--repair-mode Conservative|Balanced|Aggressive] [--json [path]]");
}
