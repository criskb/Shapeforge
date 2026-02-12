# ShapeForge

ShapeForge is a .NET 8 desktop + CLI toolkit for improving 3D-printable meshes.

## Current status

This repository is bootstrapped with:

- `ShapeForge.Core`: mesh models, STL IO, pipeline contracts, and initial operators.
- `ShapeForge.Cli`: commands for version, operator listing, and `fix` on STL files.
- `ShapeForge.App`: Avalonia shell UI for presets, operator stack, and diagnostics.
- `ShapeForge.Tests`: baseline unit/integration tests for STL IO and repair operator contract.

## CLI quick start

```bash
shapeforge operators
shapeforge fix --in input.stl --out improved.stl --preset Fdm
```

## macOS + Xcode

For macOS build/run instructions using Xcode tooling, see `docs/MACOS_XCODE.md`.
The default macOS workflow now builds a universal app bundle supporting both Apple Silicon and Intel Macs via per-architecture payloads in one `.app`.
To build the full distributable app, run `./scripts/macos/build-full-app.sh`.
## Roadmap

See `docs/ROADMAP.md` for planned epics, fixture-driven validation scope, and release milestone gates.

## Native Swift/Xcode rewrite track

A phased native rewrite plan and bootstrap Swift package scaffold are available under:

- `docs/SWIFT_REWRITE_PLAN.md`
- `native/`

This allows an Apple-first migration track while the .NET implementation remains the production baseline.

