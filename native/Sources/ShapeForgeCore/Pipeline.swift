import Foundation

public struct OperatorReport: Codable, Equatable, Sendable {
    public var id: String
    public var name: String
    public var metrics: [String: Double]
    public var notes: [String]

    public init(id: String, name: String, metrics: [String: Double] = [:], notes: [String] = []) {
        self.id = id
        self.name = name
        self.metrics = metrics
        self.notes = notes
    }
}

public struct PipelineRunResult: Codable, Sendable {
    public var inputDiagnostics: MeshDiagnostics
    public var outputDiagnostics: MeshDiagnostics
    public var outputMesh: MeshModel
    public var reports: [OperatorReport]
}

public protocol ShapeOperator: Sendable {
    var id: String { get }
    var displayName: String { get }
    func run(on input: MeshModel, profile: PrintProfile) throws -> (mesh: MeshModel, report: OperatorReport)
}

public struct PipelineRunner: Sendable {
    public init() {}

    public func run(input: MeshModel, steps: [any ShapeOperator], profile: PrintProfile) throws -> PipelineRunResult {
        var current = input
        let before = MeshDiagnosticsAnalyzer.analyze(input, profile: profile)
        var reports: [OperatorReport] = []

        for step in steps {
            let result = try step.run(on: current, profile: profile)
            current = result.mesh
            reports.append(result.report)
        }

        let after = MeshDiagnosticsAnalyzer.analyze(current, profile: profile)

        return PipelineRunResult(
            inputDiagnostics: before,
            outputDiagnostics: after,
            outputMesh: current,
            reports: reports
        )
    }
}

public enum Preset: String, Codable, CaseIterable, Sendable {
    case fdm = "Fdm"
    case resin = "Resin"

    public var profile: PrintProfile {
        switch self {
        case .fdm:
            return PrintProfile(name: "FDM", minWallMm: 0.8, overhangThresholdDeg: 50)
        case .resin:
            return PrintProfile(name: "Resin", minWallMm: 1.0, overhangThresholdDeg: 75)
        }
    }
}

public struct PrintProfile: Codable, Equatable, Sendable {
    public var name: String
    public var minWallMm: Double
    public var overhangThresholdDeg: Double

    public init(name: String, minWallMm: Double, overhangThresholdDeg: Double) {
        self.name = name
        self.minWallMm = minWallMm
        self.overhangThresholdDeg = overhangThresholdDeg
    }
}

public struct OperatorRegistry {
    public static func all() -> [any ShapeOperator] {
        [CleanupOperator(), NormalsOperator(), RemoveTinyShellsOperator(), RepairFixOperator()]
    }

    public static func defaultFixChain(for preset: Preset) -> [any ShapeOperator] {
        switch preset {
        case .fdm, .resin:
            return [CleanupOperator(), NormalsOperator(), RemoveTinyShellsOperator()]
        }
    }
}

public enum ShapeForgeError: Error, LocalizedError {
    case invalidArgument(String)
    case fileIO(String)
    case parse(String)

    public var errorDescription: String? {
        switch self {
        case .invalidArgument(let m), .fileIO(let m), .parse(let m): return m
        }
    }
}
