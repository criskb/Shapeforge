import Foundation

public struct OperatorReport: Equatable, Sendable {
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

public protocol ShapeOperator: Sendable {
    var id: String { get }
    var displayName: String { get }
    func run(on input: MeshModel) throws -> (mesh: MeshModel, report: OperatorReport)
}

public struct PipelineRunner: Sendable {
    public init() {}

    public func run(input: MeshModel, steps: [any ShapeOperator]) throws -> (mesh: MeshModel, reports: [OperatorReport]) {
        var current = input
        var reports: [OperatorReport] = []

        for step in steps {
            let result = try step.run(on: current)
            current = result.mesh
            reports.append(result.report)
        }

        return (current, reports)
    }
}

public struct RepairFixOperator: ShapeOperator {
    public let id = "repair.fix"
    public let displayName = "3D Print Fix"

    public init() {}

    public func run(on input: MeshModel) throws -> (mesh: MeshModel, report: OperatorReport) {
        let report = OperatorReport(
            id: id,
            name: displayName,
            metrics: [
                "triangles.before": Double(input.triangleCount),
                "triangles.after": Double(input.triangleCount)
            ],
            notes: ["Native parity scaffold: implementation pending."]
        )

        return (input, report)
    }
}
