#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

RID="${1:-osx-arm64}"
CONFIG="${CONFIGURATION:-Debug}"
OUT_DIR="$REPO_ROOT/artifacts/macos/$RID"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet SDK is required. Install .NET 8 SDK first." >&2
  exit 127
fi

if ! command -v xcodebuild >/dev/null 2>&1; then
  echo "error: xcodebuild not found. Install Xcode + Command Line Tools." >&2
  exit 127
fi

echo "Building ShapeForge.App for $RID ($CONFIG)..."
dotnet publish "$REPO_ROOT/src/ShapeForge.App/ShapeForge.App.csproj" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained false \
  -p:UseAppHost=true \
  -o "$OUT_DIR"

echo "Build complete: $OUT_DIR"
