import XCTest
@testable import ShapeForgeCore

final class PipelineTests: XCTestCase {
    func testRepairFixOperatorProducesStableMetrics() throws {
        let input = MeshModel(
            vertices: [0, 0, 0, 1, 0, 0, 0, 1, 0],
            indices: [0, 1, 2]
        )

        let runner = PipelineRunner()
        let output = try runner.run(input: input, steps: [RepairFixOperator()])

        XCTAssertEqual(output.mesh.triangleCount, input.triangleCount)
        XCTAssertEqual(output.reports.count, 1)
        XCTAssertEqual(output.reports[0].metrics["triangles.before"], 1)
        XCTAssertEqual(output.reports[0].metrics["triangles.after"], 1)
    }
}
