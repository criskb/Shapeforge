# ShapeForge Docs (Native macOS Track)

ShapeForge is now on a **native Swift/Xcode-only** development path.

## Start here

- Rewrite strategy and phased execution: `docs/SWIFT_REWRITE_PLAN.md`
- Local development workflow: `docs/DEV.md`
- macOS + Xcode build/run guide: `docs/MACOS_XCODE.md`

## Repository layout

- `native/Package.swift` — Swift package manifest
- `native/Sources/ShapeForgeCore` — shared geometry/pipeline core contracts
- `native/Sources/ShapeForgeCLI` — native command line app
- `native/Tests/ShapeForgeCoreTests` — baseline deterministic tests

Legacy .NET/Avalonia projects and scripts were removed as part of the native cutover.
