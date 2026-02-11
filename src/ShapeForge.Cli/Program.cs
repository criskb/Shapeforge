using ShapeForge.Core.IO;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;

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
                input = args[++i];
                break;
            case "--out":
                output = args[++i];
                break;
            case "--preset":
                preset = Enum.Parse<PrintPreset>(args[++i], ignoreCase: true);
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
    {
        Console.Error.WriteLine("Usage: shapeforge fix --in input.stl --out output.stl [--preset Fdm|Sla|Sls]");
        Environment.ExitCode = 2;
        return;
    }

    var parameters = Presets.Resolve(preset);
    var io = new StlMeshIO();
    var mesh = await io.LoadStlAsync(input);

    var ctx = new OperatorContext(
        parameters.VoxelSizeMm,
        new Progress<float>(_ => { }),
        Console.WriteLine,
        new Dictionary<string, object>());

    if (!registry.TryGet("repair.fix", out var op) || op is null)
    {
        throw new InvalidOperationException("repair.fix is not registered.");
    }

    var (fixedMesh, report) = await op.RunAsync(mesh, ctx, CancellationToken.None);
    await io.SaveStlAsync(output, fixedMesh);

    Console.WriteLine($"Saved improved mesh to {output}");
    foreach (var metric in report.Metrics)
    {
        Console.WriteLine($"{metric.Key}: {metric.Value}");
    }
}

static void PrintHelp()
{
    Console.WriteLine("ShapeForge CLI");
    Console.WriteLine("  version                 Show version");
    Console.WriteLine("  operators               List available operators");
    Console.WriteLine("  fix --in --out [preset] Run repair preset on STL");
}
