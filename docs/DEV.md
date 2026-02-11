# Development Guide

## Solution layout

- `src/ShapeForge.Core`: domain model + operators + IO
- `src/ShapeForge.Cli`: scripting entrypoint
- `src/ShapeForge.App`: desktop UX shell
- `tests/ShapeForge.Tests`: xUnit tests

## Milestone notes

- M0 done: solution/project bootstrap, operator contracts, progress/cancellation patterns.
- M1 partial: STL import/export implemented, desktop shell includes placeholder diagnostics.
- M2 next: integrate PicoGK/ShapeKernel voxel pipeline in `RepairFixOperator`.

## Build

```bash
dotnet build ShapeForge.sln
dotnet test ShapeForge.sln
```

## macOS/Xcode workflow

- Use `scripts/macos/build-app.sh` and `scripts/macos/run-app.sh`.
- `build-app.sh universal` creates `artifacts/macos/universal/ShapeForge.App.app` with both `arm64` and `x86_64` payloads under `Contents/Resources/*`, selected by a launcher script.
- Profiles: `dev` (default), `release`, `full`.
- `scripts/macos/build-full-app.sh` builds `universal full` and creates `artifacts/macos/ShapeForge.App-macos-universal.zip`.
- Full setup is documented in `docs/MACOS_XCODE.md`.
