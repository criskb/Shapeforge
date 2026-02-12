import Foundation

public enum MeshDiagnosticsAnalyzer {
    public static func analyze(_ mesh: MeshModel, profile: PrintProfile) -> MeshDiagnostics {
        var edgeUses: [Edge: Int] = [:]
        var triMap: [FaceKey: Int] = [:]
        var degenerate = 0
        var area = 0.0
        var signedVolume = 0.0

        for t in mesh.triangles {
            guard t.a < mesh.vertices.count, t.b < mesh.vertices.count, t.c < mesh.vertices.count else { continue }
            let v0 = mesh.vertices[t.a]
            let v1 = mesh.vertices[t.b]
            let v2 = mesh.vertices[t.c]
            let cross = (v1 - v0).cross(v2 - v0)
            let triArea = 0.5 * cross.length()
            if triArea < 1e-12 {
                degenerate += 1
            }
            area += triArea
            signedVolume += v0.dot(v1.cross(v2)) / 6.0

            for e in [Edge(t.a, t.b), Edge(t.b, t.c), Edge(t.c, t.a)] {
                edgeUses[e, default: 0] += 1
            }

            triMap[FaceKey(t), default: 0] += 1
        }

        let nonManifoldEdges = edgeUses.values.filter { $0 > 2 }.count
        let boundaryEdges = edgeUses.values.filter { $0 == 1 }.count
        let watertight = boundaryEdges == 0 && nonManifoldEdges == 0 && degenerate == 0
        let manifold = nonManifoldEdges == 0
        let duplicates = triMap.values.filter { $0 > 1 }.reduce(0) { $0 + ($1 - 1) }
        let shells = shellCount(mesh)

        var issues: [DiagnosticIssue] = []
        if degenerate > 0 {
            issues.append(DiagnosticIssue(severity: .error, code: "topology.degenerate", message: "Degenerate triangles detected", count: degenerate))
        }
        if duplicates > 0 {
            issues.append(DiagnosticIssue(severity: .warning, code: "topology.duplicate_faces", message: "Duplicate faces detected", count: duplicates))
        }
        if nonManifoldEdges > 0 {
            issues.append(DiagnosticIssue(severity: .error, code: "topology.non_manifold_edges", message: "Non-manifold edges detected", count: nonManifoldEdges))
        }
        if boundaryEdges > 0 {
            issues.append(DiagnosticIssue(severity: .error, code: "topology.open_boundaries", message: "Open boundary edges detected", count: boundaryEdges))
        }

        let b = mesh.bounds.size
        let maxDim = max(b.x, max(b.y, b.z))
        if maxDim < 1.0 {
            issues.append(DiagnosticIssue(severity: .warning, code: "scale.suspicious_small", message: "Model appears very small; verify units", count: 1))
        } else if maxDim > 1000 {
            issues.append(DiagnosticIssue(severity: .warning, code: "scale.suspicious_large", message: "Model appears very large; verify units", count: 1))
        }

        if profile.minWallMm > 0 && minDimension(b) < profile.minWallMm {
            issues.append(DiagnosticIssue(severity: .warning, code: "print.thin_features", message: "At least one axis is smaller than min wall setting", count: 1))
        }

        return MeshDiagnostics(
            vertexCount: mesh.vertexCount,
            triangleCount: mesh.triangleCount,
            bounds: mesh.bounds,
            surfaceArea: area,
            volume: watertight ? abs(signedVolume) : 0,
            isWatertight: watertight,
            isManifold: manifold,
            shellCount: shells,
            degenerateTriangleCount: degenerate,
            duplicateFaceCount: duplicates,
            nonManifoldEdgeCount: nonManifoldEdges,
            issues: issues
        )
    }

    private static func shellCount(_ mesh: MeshModel) -> Int {
        guard !mesh.triangles.isEmpty else { return 0 }
        var faceAdj: [[Int]] = Array(repeating: [], count: mesh.triangles.count)
        var vertToFaces: [Int: [Int]] = [:]
        for (idx, t) in mesh.triangles.enumerated() {
            for v in t.indices { vertToFaces[v, default: []].append(idx) }
        }
        for faces in vertToFaces.values {
            for i in 0..<faces.count {
                for j in (i + 1)..<faces.count {
                    faceAdj[faces[i]].append(faces[j])
                    faceAdj[faces[j]].append(faces[i])
                }
            }
        }

        var visited = Array(repeating: false, count: mesh.triangles.count)
        var components = 0
        for i in 0..<mesh.triangles.count where !visited[i] {
            components += 1
            var stack = [i]
            visited[i] = true
            while let cur = stack.popLast() {
                for n in faceAdj[cur] where !visited[n] {
                    visited[n] = true
                    stack.append(n)
                }
            }
        }
        return components
    }

    private static func minDimension(_ v: Vec3) -> Double {
        min(v.x, min(v.y, v.z))
    }
}

struct Edge: Hashable {
    let a: Int
    let b: Int

    init(_ i: Int, _ j: Int) {
        if i <= j {
            a = i
            b = j
        } else {
            a = j
            b = i
        }
    }
}

struct FaceKey: Hashable {
    let a: Int
    let b: Int
    let c: Int

    init(_ tri: Triangle) {
        let s = tri.indices.sorted()
        a = s[0]
        b = s[1]
        c = s[2]
    }
}
