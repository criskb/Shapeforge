import XCTest
@testable import ShapeForgeCore

final class PipelineTests: XCTestCase {
    func testCleanupRemovesDegenerateAndDuplicateFaces() throws {
        let mesh = MeshModel(
            vertices: [
                Vec3(0, 0, 0), Vec3(1, 0, 0), Vec3(0, 1, 0),
                Vec3(0, 0, 0.0000000001)
            ],
            triangles: [
                Triangle(0, 1, 2),
                Triangle(0, 2, 1),
                Triangle(0, 0, 1)
            ]
        )

        let out = try CleanupOperator().run(on: mesh, profile: .init(name: "FDM", minWallMm: 0.8, overhangThresholdDeg: 50))
        XCTAssertEqual(out.mesh.triangleCount, 1)
        XCTAssertEqual(out.report.metrics["removed.duplicate"], 1)
        XCTAssertEqual(out.report.metrics["removed.degenerate"], 1)
    }

    func testDiagnosticsFlagsOpenMesh() {
        let mesh = MeshModel(
            vertices: [Vec3(0, 0, 0), Vec3(1, 0, 0), Vec3(0, 1, 0)],
            triangles: [Triangle(0, 1, 2)]
        )
        let diag = MeshDiagnosticsAnalyzer.analyze(mesh, profile: .init(name: "FDM", minWallMm: 0.8, overhangThresholdDeg: 50))
        XCTAssertFalse(diag.isWatertight)
        XCTAssertTrue(diag.issues.contains(where: { $0.code == "topology.open_boundaries" }))
    }

    func testStlBinaryRoundTripPreservesTriangleCount() throws {
        let mesh = MeshModel(
            vertices: [Vec3(0, 0, 0), Vec3(1, 0, 0), Vec3(0, 1, 0)],
            triangles: [Triangle(0, 1, 2)]
        )

        let tmp = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("shapeforge-test-\(UUID().uuidString).stl")
        defer { try? FileManager.default.removeItem(at: tmp) }

        try StlMeshIO.saveBinary(mesh, to: tmp.path)
        let loaded = try StlMeshIO.load(from: tmp.path)

        XCTAssertEqual(loaded.triangleCount, mesh.triangleCount)
        XCTAssertEqual(loaded.vertexCount, mesh.vertexCount)
    }

    func testPipelineFixChainRuns() throws {
        let mesh = MeshModel(
            vertices: [
                Vec3(0, 0, 0), Vec3(1, 0, 0), Vec3(0, 1, 0),
                Vec3(5, 5, 5), Vec3(5.1, 5, 5), Vec3(5, 5.1, 5)
            ],
            triangles: [Triangle(0, 1, 2), Triangle(3, 4, 5)]
        )

        let result = try PipelineRunner().run(
            input: mesh,
            steps: OperatorRegistry.defaultFixChain(for: .fdm),
            profile: Preset.fdm.profile
        )

        XCTAssertEqual(result.reports.count, 3)
        XCTAssertLessThan(result.outputMesh.triangleCount, mesh.triangleCount)
    }
}
