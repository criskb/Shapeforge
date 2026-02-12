import Foundation
import ShapeForgeCore

@main
struct ShapeForgeCliMain {
    static func main() {
        do {
            let cli = Cli()
            let code = try cli.run(args: Array(CommandLine.arguments.dropFirst()))
            exit(Int32(code))
        } catch {
            fputs("error: \(error.localizedDescription)\n", stderr)
            exit(1)
        }
    }
}

struct Cli {
    func run(args: [String]) throws -> Int {
        guard let command = args.first else {
            printHelp()
            return 0
        }

        switch command {
        case "version", "--version", "-v":
            print("ShapeForge Native CLI v0.2.0")
            return 0
        case "operators", "list-operators":
            for op in OperatorRegistry.all() {
                print("\(op.id) :: \(op.displayName)")
            }
            return 0
        case "diagnose":
            return try runDiagnose(Array(args.dropFirst()))
        case "fix":
            return try runFix(Array(args.dropFirst()))
        default:
            fputs("Unknown command: \(command)\n", stderr)
            printHelp()
            return 2
        }
    }

    private func runDiagnose(_ args: [String]) throws -> Int {
        let input = try requireValue("--in", in: args)
        let jsonPath = value("--json", in: args)
        let preset = try parsePreset(args)

        let mesh = try StlMeshIO.load(from: input)
        let diag = MeshDiagnosticsAnalyzer.analyze(mesh, profile: preset.profile)

        print("Triangles: \(diag.triangleCount)")
        print("Vertices: \(diag.vertexCount)")
        print("Watertight: \(diag.isWatertight)")
        print("Manifold: \(diag.isManifold)")
        print("Shells: \(diag.shellCount)")
        print("Issues: \(diag.issues.count)")
        for i in diag.issues {
            print("- [\(i.severity.rawValue.uppercased())] \(i.code): \(i.message) (\(i.count))")
        }

        if let jsonPath {
            let encoder = JSONEncoder()
            encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
            let payload = try encoder.encode(diag)
            try payload.write(to: URL(fileURLWithPath: jsonPath))
        }

        return diag.issues.contains(where: { $0.severity == .error }) ? 2 : 0
    }

    private func runFix(_ args: [String]) throws -> Int {
        let input = try requireValue("--in", in: args)
        let output = try requireValue("--out", in: args)
        let preset = try parsePreset(args)

        let mesh = try StlMeshIO.load(from: input)
        let chain = OperatorRegistry.defaultFixChain(for: preset)
        let runner = PipelineRunner()
        let result = try runner.run(input: mesh, steps: chain, profile: preset.profile)

        try StlMeshIO.saveBinary(result.outputMesh, to: output)

        print("Preset: \(preset.rawValue)")
        print("Pre issues: \(result.inputDiagnostics.issues.count)")
        print("Post issues: \(result.outputDiagnostics.issues.count)")
        print("Triangles: \(result.inputDiagnostics.triangleCount) -> \(result.outputDiagnostics.triangleCount)")
        for report in result.reports {
            print("* \(report.id) :: \(report.name)")
            for (k, v) in report.metrics.sorted(by: { $0.key < $1.key }) {
                print("  - \(k): \(v)")
            }
            for note in report.notes {
                print("  - note: \(note)")
            }
        }

        return result.outputDiagnostics.issues.contains(where: { $0.severity == .error }) ? 2 : 0
    }

    private func parsePreset(_ args: [String]) throws -> Preset {
        guard let raw = value("--preset", in: args) else { return .fdm }
        guard let preset = Preset.allCases.first(where: { $0.rawValue.caseInsensitiveCompare(raw) == .orderedSame }) else {
            throw ShapeForgeError.invalidArgument("Unknown preset '\(raw)'. Use Fdm or Resin")
        }
        return preset
    }

    private func value(_ flag: String, in args: [String]) -> String? {
        guard let idx = args.firstIndex(of: flag), idx + 1 < args.count else { return nil }
        return args[idx + 1]
    }

    private func requireValue(_ flag: String, in args: [String]) throws -> String {
        if let v = value(flag, in: args) { return v }
        throw ShapeForgeError.invalidArgument("Missing required argument \(flag)")
    }

    private func printHelp() {
        print("ShapeForge Native CLI")
        print("  version")
        print("  operators")
        print("  diagnose --in model.stl [--json report.json] [--preset Fdm|Resin]")
        print("  fix --in model.stl --out fixed.stl [--preset Fdm|Resin]")
    }
}
