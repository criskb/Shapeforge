# Native Development Guide

## Requirements

- macOS 13+
- Xcode 15+ (or Xcode command line tools)
- Swift 5.9+

## Project layout

- `native/Sources/ShapeForgeCore`: core contracts, diagnostics models, pipeline runtime
- `native/Sources/ShapeForgeCLI`: CLI entrypoint and command handling
- `native/Tests/ShapeForgeCoreTests`: deterministic unit tests

## Build and test

```bash
cd native
swift build
swift test
```

## Run CLI

```bash
cd native
swift run shapeforge-native version
swift run shapeforge-native operators
```

## Development notes

- Keep operator IDs stable for recipe compatibility.
- Prefer deterministic behavior for all pipeline operations.
- Add fixture-based tests before expanding operator behavior.
