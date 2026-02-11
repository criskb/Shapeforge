# Building and Running ShapeForge on macOS with Xcode

ShapeForge is a .NET 8 solution. On macOS, Xcode is used to provide the native toolchain and command-line tools required by graphics/native dependencies.

## Prerequisites

1. Install **Xcode** from the App Store.
2. Install Xcode command line tools:

   ```bash
   xcode-select --install
   ```

3. Accept the Xcode license:

   ```bash
   sudo xcodebuild -license accept
   ```

4. Install **.NET 8 SDK**.

## Build from Terminal (Xcode-backed toolchain)

Build a **universal** macOS app (Apple Silicon + Intel):

```bash
./scripts/macos/build-app.sh universal
```

Build a **full production** universal app (Release + self-contained + ReadyToRun):

```bash
./scripts/macos/build-full-app.sh
```

Or explicitly:

```bash
./scripts/macos/build-app.sh universal full
```

This produces:

```text
artifacts/macos/universal/ShapeForge.App.app
```

Verify both architecture payloads exist:

```bash
file artifacts/macos/universal/ShapeForge.App.app/Contents/Resources/osx-arm64/ShapeForge.App
file artifacts/macos/universal/ShapeForge.App.app/Contents/Resources/osx-x64/ShapeForge.App
```

Expected output shows `arm64` for the first binary and `x86_64` for the second.

Single-architecture builds are still available:

```bash
./scripts/macos/build-app.sh osx-arm64
./scripts/macos/build-app.sh osx-x64
```

## Run

```bash
./scripts/macos/run-app.sh universal
```

Run and auto-build full profile when needed:

```bash
./scripts/macos/run-app.sh universal full
```

You can still run single-architecture outputs with `osx-arm64` or `osx-x64`.

## Run from Xcode UI (External Build System)

1. Open Xcode → **File > New > Project...**
2. Choose **macOS > Other > External Build System**.
3. Set Build Tool to:

   ```text
   /bin/bash
   ```

4. Set Arguments to:

   ```text
   -lc './scripts/macos/build-app.sh universal'
   ```

5. Edit the scheme:
   - Build action: keep external build step above.
   - Run action executable: set to

     ```text
     $(SRCROOT)/artifacts/macos/universal/ShapeForge.App.app/Contents/MacOS/ShapeForge.App
     ```

This provides an Xcode-driven build + run loop for local development.
