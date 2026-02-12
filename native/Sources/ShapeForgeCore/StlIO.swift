import Foundation

public enum StlMeshIO {
    public static func load(from path: String) throws -> MeshModel {
        let url = URL(fileURLWithPath: path)
        let data = try Data(contentsOf: url)
        if isLikelyAsciiSTL(data) {
            return try parseAscii(data)
        }
        return try parseBinary(data)
    }

    public static func saveBinary(_ mesh: MeshModel, to path: String) throws {
        var out = Data(count: 80)
        var triCount = UInt32(mesh.triangleCount).littleEndian
        withUnsafeBytes(of: &triCount) { out.append(contentsOf: $0) }

        for tri in mesh.triangles {
            let v0 = mesh.vertices[tri.a]
            let v1 = mesh.vertices[tri.b]
            let v2 = mesh.vertices[tri.c]
            let n = (v1 - v0).cross(v2 - v0)
            appendFloat3(Vec3(n.x, n.y, n.z), to: &out)
            appendFloat3(v0, to: &out)
            appendFloat3(v1, to: &out)
            appendFloat3(v2, to: &out)
            var attr: UInt16 = 0
            withUnsafeBytes(of: &attr) { out.append(contentsOf: $0) }
        }

        try out.write(to: URL(fileURLWithPath: path))
    }

    private static func appendFloat3(_ v: Vec3, to data: inout Data) {
        var x = Float(v.x).bitPattern.littleEndian
        var y = Float(v.y).bitPattern.littleEndian
        var z = Float(v.z).bitPattern.littleEndian
        withUnsafeBytes(of: &x) { data.append(contentsOf: $0) }
        withUnsafeBytes(of: &y) { data.append(contentsOf: $0) }
        withUnsafeBytes(of: &z) { data.append(contentsOf: $0) }
    }

    private static func isLikelyAsciiSTL(_ data: Data) -> Bool {
        guard data.count > 5 else { return false }
        if let prefix = String(data: data.prefix(5), encoding: .ascii)?.lowercased(), prefix == "solid" {
            let textSample = data.prefix(min(512, data.count))
            if let str = String(data: textSample, encoding: .ascii), str.contains("facet") {
                return true
            }
        }
        return false
    }

    private static func parseAscii(_ data: Data) throws -> MeshModel {
        guard let text = String(data: data, encoding: .utf8) ?? String(data: data, encoding: .ascii) else {
            throw ShapeForgeError.parse("Unable to decode ASCII STL")
        }

        var vertices: [Vec3] = []
        var triangles: [Triangle] = []
        var vertexMap: [QuantizedVec3: Int] = [:]
        var facetVertices: [Int] = []

        for rawLine in text.split(whereSeparator: \ .isNewline) {
            let line = rawLine.trimmingCharacters(in: .whitespacesAndNewlines)
            if line.hasPrefix("vertex ") {
                let parts = line.split(separator: " ").compactMap { Double($0) }
                guard parts.count == 3 else { continue }
                let v = Vec3(parts[0], parts[1], parts[2])
                let key = QuantizedVec3(v, epsilon: 1e-7)
                let idx: Int
                if let existing = vertexMap[key] {
                    idx = existing
                } else {
                    idx = vertices.count
                    vertices.append(v)
                    vertexMap[key] = idx
                }
                facetVertices.append(idx)
                if facetVertices.count == 3 {
                    triangles.append(Triangle(facetVertices[0], facetVertices[1], facetVertices[2]))
                    facetVertices.removeAll(keepingCapacity: true)
                }
            }
        }

        return MeshModel(vertices: vertices, triangles: triangles)
    }

    private static func parseBinary(_ data: Data) throws -> MeshModel {
        guard data.count >= 84 else { throw ShapeForgeError.parse("Binary STL too short") }
        let triCount = Int(readUInt32LE(data, offset: 80))
        let expected = 84 + triCount * 50
        guard data.count >= expected else { throw ShapeForgeError.parse("Binary STL truncated") }

        var vertices: [Vec3] = []
        var triangles: [Triangle] = []
        var vertexMap: [QuantizedVec3: Int] = [:]
        vertices.reserveCapacity(triCount * 3)
        triangles.reserveCapacity(triCount)

        var offset = 84
        for _ in 0..<triCount {
            offset += 12 // skip normal
            var triIdx: [Int] = []
            for _ in 0..<3 {
                let x = Double(readFloatLE(data, offset: offset)); offset += 4
                let y = Double(readFloatLE(data, offset: offset)); offset += 4
                let z = Double(readFloatLE(data, offset: offset)); offset += 4
                let v = Vec3(x, y, z)
                let key = QuantizedVec3(v, epsilon: 1e-7)
                if let idx = vertexMap[key] {
                    triIdx.append(idx)
                } else {
                    let idx = vertices.count
                    vertices.append(v)
                    vertexMap[key] = idx
                    triIdx.append(idx)
                }
            }
            offset += 2 // attribute bytes
            triangles.append(Triangle(triIdx[0], triIdx[1], triIdx[2]))
        }

        return MeshModel(vertices: vertices, triangles: triangles)
    }

    private static func readUInt32LE(_ data: Data, offset: Int) -> UInt32 {
        data[offset..<(offset + 4)].withUnsafeBytes { raw in
            raw.load(as: UInt32.self).littleEndian
        }
    }

    private static func readFloatLE(_ data: Data, offset: Int) -> Float {
        let bits = data[offset..<(offset + 4)].withUnsafeBytes { raw in
            raw.load(as: UInt32.self).littleEndian
        }
        return Float(bitPattern: bits)
    }
}
