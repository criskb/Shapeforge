// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "ShapeForgeNative",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .library(name: "ShapeForgeCore", targets: ["ShapeForgeCore"]),
        .executable(name: "shapeforge-native", targets: ["ShapeForgeCLI"])
    ],
    targets: [
        .target(
            name: "ShapeForgeCore"
        ),
        .executableTarget(
            name: "ShapeForgeCLI",
            dependencies: ["ShapeForgeCore"]
        ),
        .testTarget(
            name: "ShapeForgeCoreTests",
            dependencies: ["ShapeForgeCore"]
        )
    ]
)
