import Foundation

struct QuantizedVec3: Hashable {
    let x: Int64
    let y: Int64
    let z: Int64

    init(_ v: Vec3, epsilon: Double) {
        x = Int64((v.x / epsilon).rounded())
        y = Int64((v.y / epsilon).rounded())
        z = Int64((v.z / epsilon).rounded())
    }
}
