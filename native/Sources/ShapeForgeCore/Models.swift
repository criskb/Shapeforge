import Foundation

public struct MeshModel: Equatable, Sendable {
    public var vertices: [Float]
    public var indices: [Int]
    public var units: String

    public init(vertices: [Float], indices: [Int], units: String = "mm") {
        self.vertices = vertices
        self.indices = indices
        self.units = units
    }

    public var triangleCount: Int {
        indices.count / 3
    }
}

public enum DiagnosticSeverity: String, Codable, Sendable {
    case info
    case warning
    case error
}

public struct DiagnosticIssue: Codable, Equatable, Sendable {
    public var severity: DiagnosticSeverity
    public var code: String
    public var message: String
    public var count: Int

    public init(severity: DiagnosticSeverity, code: String, message: String, count: Int = 1) {
        self.severity = severity
        self.code = code
        self.message = message
        self.count = count
    }
}

public struct MeshDiagnostics: Codable, Equatable, Sendable {
    public var vertexCount: Int
    public var triangleCount: Int
    public var isWatertight: Bool
    public var isManifold: Bool
    public var shellCount: Int
    public var issues: [DiagnosticIssue]

    public init(
        vertexCount: Int,
        triangleCount: Int,
        isWatertight: Bool,
        isManifold: Bool,
        shellCount: Int,
        issues: [DiagnosticIssue]
    ) {
        self.vertexCount = vertexCount
        self.triangleCount = triangleCount
        self.isWatertight = isWatertight
        self.isManifold = isManifold
        self.shellCount = shellCount
        self.issues = issues
    }
}
