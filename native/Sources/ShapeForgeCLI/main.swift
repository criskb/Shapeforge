import Foundation
import ShapeForgeCore

func printHelp() {
    print("ShapeForge Native CLI")
    print("  version                     Show version")
    print("  operators                   List available operators")
}

let args = CommandLine.arguments.dropFirst()
guard let command = args.first else {
    printHelp()
    exit(0)
}

switch command {
case "version", "--version", "-v":
    print("ShapeForge Native CLI v0.1.0")
case "operators", "list-operators":
    let ops: [any ShapeOperator] = [RepairFixOperator()]
    for op in ops {
        print("\(op.id) :: \(op.displayName)")
    }
default:
    fputs("Unknown command: \(command)\n", stderr)
    printHelp()
    exit(2)
}
