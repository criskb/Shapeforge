import Foundation

public struct CleanupOperator: ShapeOperator {
    public let id = "repair.cleanup"
    public let displayName = "Cleanup"

    public init() {}

    public func run(on input: MeshModel, profile _: PrintProfile) throws -> (mesh: MeshModel, report: OperatorReport) {
        let beforeTriangles = input.triangleCount
        let beforeVertices = input.vertexCount

        var uniqueVertices: [Vec3] = []
        var vertexMap: [QuantizedVec3: Int] = [:]
        var remap: [Int] = Array(repeating: 0, count: input.vertices.count)

        for (i, v) in input.vertices.enumerated() {
            let key = QuantizedVec3(v, epsilon: 1e-6)
            if let idx = vertexMap[key] {
                remap[i] = idx
            } else {
                let idx = uniqueVertices.count
                uniqueVertices.append(v)
                vertexMap[key] = idx
                remap[i] = idx
            }
        }

        var cleaned: [Triangle] = []
        var faceSet: Set<FaceKey> = []
        var removedDegenerate = 0
        var removedDuplicate = 0

        for tri in input.triangles {
            guard tri.a < remap.count, tri.b < remap.count, tri.c < remap.count else { continue }
            let mapped = Triangle(remap[tri.a], remap[tri.b], remap[tri.c])
            if mapped.a == mapped.b || mapped.b == mapped.c || mapped.a == mapped.c {
                removedDegenerate += 1
                continue
            }
            let v0 = uniqueVertices[mapped.a]
            let v1 = uniqueVertices[mapped.b]
            let v2 = uniqueVertices[mapped.c]
            let area = 0.5 * (v1 - v0).cross(v2 - v0).length()
            if area < 1e-12 {
                removedDegenerate += 1
                continue
            }
            let key = FaceKey(mapped)
            if faceSet.contains(key) {
                removedDuplicate += 1
                continue
            }
            faceSet.insert(key)
            cleaned.append(mapped)
        }

        let out = MeshModel(vertices: uniqueVertices, triangles: cleaned, units: input.units)
        let report = OperatorReport(
            id: id,
            name: displayName,
            metrics: [
                "vertices.before": Double(beforeVertices),
                "vertices.after": Double(out.vertexCount),
                "triangles.before": Double(beforeTriangles),
                "triangles.after": Double(out.triangleCount),
                "removed.degenerate": Double(removedDegenerate),
                "removed.duplicate": Double(removedDuplicate)
            ],
            notes: []
        )
        return (out, report)
    }
}

public struct NormalsOperator: ShapeOperator {
    public let id = "repair.normals"
    public let displayName = "Normals & Winding"

    public init() {}

    public func run(on input: MeshModel, profile _: PrintProfile) throws -> (mesh: MeshModel, report: OperatorReport) {
        guard !input.vertices.isEmpty else {
            return (input, OperatorReport(id: id, name: displayName, notes: ["Mesh has no vertices"]))
        }

        var centroid = Vec3(0, 0, 0)
        for v in input.vertices { centroid = centroid + v }
        centroid = centroid * (1.0 / Double(input.vertices.count))

        var flipped = 0
        var outTriangles: [Triangle] = []
        outTriangles.reserveCapacity(input.triangleCount)

        for t in input.triangles {
            let v0 = input.vertices[t.a]
            let v1 = input.vertices[t.b]
            let v2 = input.vertices[t.c]
            let n = (v1 - v0).cross(v2 - v0)
            let triCentroid = Vec3((v0.x + v1.x + v2.x) / 3.0, (v0.y + v1.y + v2.y) / 3.0, (v0.z + v1.z + v2.z) / 3.0)
            let outward = triCentroid - centroid
            if n.dot(outward) < 0 {
                outTriangles.append(Triangle(t.a, t.c, t.b))
                flipped += 1
            } else {
                outTriangles.append(t)
            }
        }

        let out = MeshModel(vertices: input.vertices, triangles: outTriangles, units: input.units)
        let report = OperatorReport(id: id, name: displayName, metrics: ["triangles.flipped": Double(flipped)], notes: [])
        return (out, report)
    }
}

public struct RemoveTinyShellsOperator: ShapeOperator {
    public let id = "repair.remove_tiny_shells"
    public let displayName = "Remove Tiny Shells"
    public var minTriangles: Int

    public init(minTriangles: Int = 8) {
        self.minTriangles = minTriangles
    }

    public func run(on input: MeshModel, profile _: PrintProfile) throws -> (mesh: MeshModel, report: OperatorReport) {
        guard !input.triangles.isEmpty else { return (input, OperatorReport(id: id, name: displayName)) }

        var vertexToFaces: [Int: [Int]] = [:]
        for (i, tri) in input.triangles.enumerated() {
            for v in tri.indices { vertexToFaces[v, default: []].append(i) }
        }

        var adj: [[Int]] = Array(repeating: [], count: input.triangleCount)
        for faces in vertexToFaces.values {
            for i in 0..<faces.count {
                for j in (i + 1)..<faces.count {
                    adj[faces[i]].append(faces[j])
                    adj[faces[j]].append(faces[i])
                }
            }
        }

        var visited = Array(repeating: false, count: input.triangleCount)
        var keep = Array(repeating: false, count: input.triangleCount)
        var removedShells = 0
        var removedTriangles = 0

        for i in 0..<input.triangleCount where !visited[i] {
            var shell: [Int] = []
            var stack = [i]
            visited[i] = true
            while let cur = stack.popLast() {
                shell.append(cur)
                for n in adj[cur] where !visited[n] {
                    visited[n] = true
                    stack.append(n)
                }
            }

            if shell.count >= minTriangles {
                for face in shell { keep[face] = true }
            } else {
                removedShells += 1
                removedTriangles += shell.count
            }
        }

        let filteredTriangles = input.triangles.enumerated().compactMap { keep[$0.offset] ? $0.element : nil }
        let out = MeshModel(vertices: input.vertices, triangles: filteredTriangles, units: input.units)
        let report = OperatorReport(
            id: id,
            name: displayName,
            metrics: [
                "shells.removed": Double(removedShells),
                "triangles.removed": Double(removedTriangles),
                "triangles.after": Double(out.triangleCount)
            ],
            notes: []
        )
        return (out, report)
    }
}

public struct RepairFixOperator: ShapeOperator {
    public let id = "repair.fix"
    public let displayName = "3D Print Fix"

    public init() {}

    public func run(on input: MeshModel, profile: PrintProfile) throws -> (mesh: MeshModel, report: OperatorReport) {
        let chain: [any ShapeOperator] = [CleanupOperator(), NormalsOperator(), RemoveTinyShellsOperator()]
        let runner = PipelineRunner()
        let result = try runner.run(input: input, steps: chain, profile: profile)

        var metrics: [String: Double] = [
            "triangles.before": Double(input.triangleCount),
            "triangles.after": Double(result.outputMesh.triangleCount),
            "issues.before": Double(result.inputDiagnostics.issues.count),
            "issues.after": Double(result.outputDiagnostics.issues.count)
        ]
        for step in result.reports {
            for (k, v) in step.metrics {
                metrics["\(step.id).\(k)"] = v
            }
        }

        return (
            result.outputMesh,
            OperatorReport(id: id, name: displayName, metrics: metrics, notes: ["Executed cleanup/normals/tiny-shell chain"])
        )
    }
}
