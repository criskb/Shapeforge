# Building ShapeForge with Xcode (Native Swift)

ShapeForge is developed as a native Swift package for macOS.

## Prerequisites

1. Install **Xcode** from the App Store.
2. Install command line tools:

```bash
xcode-select --install
```

3. Accept Xcode license:

```bash
sudo xcodebuild -license accept
```

## Build from Terminal

```bash
cd native
swift build
```

## Test

```bash
cd native
swift test
```

## Run CLI

```bash
cd native
swift run shapeforge-native version
swift run shapeforge-native operators
```

## Open in Xcode

```bash
cd native
open Package.swift
```

Xcode will load package targets for `ShapeForgeCore`, `ShapeForgeCLI`, and `ShapeForgeCoreTests`.
