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

From repo root:

```bash
./scripts/macos/build-app.sh osx-arm64
```

For Intel Macs:

```bash
./scripts/macos/build-app.sh osx-x64
```

## Run

```bash
./scripts/macos/run-app.sh osx-arm64
```

## Run from Xcode UI (External Build System)

1. Open Xcode → **File > New > Project...**
2. Choose **macOS > Other > External Build System**.
3. Set Build Tool to:

   ```text
   /bin/bash
   ```

4. Set Arguments to:

   ```text
   -lc './scripts/macos/build-app.sh osx-arm64'
   ```

5. Edit the scheme:
   - Build action: keep external build step above.
   - Run action executable: set to

     ```text
     $(SRCROOT)/artifacts/macos/osx-arm64/ShapeForge.App
     ```

This provides an Xcode-driven build + run loop for local development.
