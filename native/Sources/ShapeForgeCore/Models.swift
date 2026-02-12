import Foundation

public struct Vec3: Hashable, Sendable, Codable {
    public var x: Double
    public var y: Double
    public var z: Double

    public init(_ x: Double, _ y: Double, _ z: Double) {
        self.x = x
        self.y = y
        self.z = z
    }

    public static func + (lhs: Vec3, rhs: Vec3) -> Vec3 { Vec3(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z) }
    public static func - (lhs: Vec3, rhs: Vec3) -> Vec3 { Vec3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z) }
    public static func * (lhs: Vec3, rhs: Double) -> Vec3 { Vec3(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs) }

    public func dot(_ other: Vec3) -> Double { x * other.x + y * other.y + z * other.z }
    public func cross(_ other: Vec3) -> Vec3 {
        Vec3(y * other.z - z * other.y, z * other.x - x * other.z, x * other.y - y * other.x)
    }
    public func length() -> Double { sqrt(dot(self)) }
}

public struct Triangle: Hashable, Sendable, Codable {
    public var a: Int
    public var b: Int
    public var c: Int

    public init(_ a: Int, _ b: Int, _ c: Int) {
        self.a = a
        self.b = b
        self.c = c
    }

    public var indices: [Int] { [a, b, c] }
}

public struct Bounds3D: Codable, Equatable, Sendable {
    public var min: Vec3
    public var max: Vec3

    public init(min: Vec3, max: Vec3) {
        self.min = min
        self.max = max
    }

    public var size: Vec3 { max - min }
}

public struct MeshModel: Equatable, Sendable, Codable {
    public var vertices: [Vec3]
    public var triangles: [Triangle]
    public var units: String

    public init(vertices: [Vec3], triangles: [Triangle], units: String = "mm") {
        self.vertices = vertices
        self.triangles = triangles
        self.units = units
    }

    public var vertexCount: Int { vertices.count }
    public var triangleCount: Int { triangles.count }

    public var bounds: Bounds3D {
        guard var mn = vertices.first else {
            return Bounds3D(min: Vec3(0, 0, 0), max: Vec3(0, 0, 0))
        }
        var mx = mn
        for v in vertices.dropFirst() {
            mn = Vec3(min(mn.x, v.x), min(mn.y, v.y), min(mn.z, v.z))
            mx = Vec3(max(mx.x, v.x), max(mx.y, v.y), max(mx.z, v.z))
        }
        return Bounds3D(min: mn, max: mx)
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
    public var schemaVersion: String
    public var vertexCount: Int
    public var triangleCount: Int
    public var bounds: Bounds3D
    public var surfaceArea: Double
    public var volume: Double
    public var isWatertight: Bool
    public var isManifold: Bool
    public var shellCount: Int
    public var degenerateTriangleCount: Int
    public var duplicateFaceCount: Int
    public var nonManifoldEdgeCount: Int
    public var issues: [DiagnosticIssue]

    public init(
        schemaVersion: String = "1.0",
        vertexCount: Int,
        triangleCount: Int,
        bounds: Bounds3D,
        surfaceArea: Double,
        volume: Double,
        isWatertight: Bool,
        isManifold: Bool,
        shellCount: Int,
        degenerateTriangleCount: Int,
        duplicateFaceCount: Int,
        nonManifoldEdgeCount: Int,
        issues: [DiagnosticIssue]
    ) {
        self.schemaVersion = schemaVersion
        self.vertexCount = vertexCount
        self.triangleCount = triangleCount
        self.bounds = bounds
        self.surfaceArea = surfaceArea
        self.volume = volume
        self.isWatertight = isWatertight
        self.isManifold = isManifold
        self.shellCount = shellCount
        self.degenerateTriangleCount = degenerateTriangleCount
        self.duplicateFaceCount = duplicateFaceCount
        self.nonManifoldEdgeCount = nonManifoldEdgeCount
        self.issues = issues
    }
}
