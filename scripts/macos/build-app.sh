#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

RID="${1:-universal}"
CONFIG="${CONFIGURATION:-Debug}"
OUT_ROOT="$REPO_ROOT/artifacts/macos"

publish_rid() {
  local rid="$1"
  local out_dir="$OUT_ROOT/$rid"

  echo "Building ShapeForge.App for $rid ($CONFIG)..."
  dotnet publish "$REPO_ROOT/src/ShapeForge.App/ShapeForge.App.csproj" \
    -c "$CONFIG" \
    -r "$rid" \
    --self-contained false \
    -p:UseAppHost=true \
    -o "$out_dir"

  echo "Build complete: $out_dir"
}

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet SDK is required. Install .NET 8 SDK first." >&2
  exit 127
fi

if ! command -v xcodebuild >/dev/null 2>&1; then
  echo "error: xcodebuild not found. Install Xcode + Command Line Tools." >&2
  exit 127
fi

if [[ "$RID" == "universal" ]]; then
  publish_rid "osx-arm64"
  publish_rid "osx-x64"

  ARM_DIR="$OUT_ROOT/osx-arm64"
  X64_DIR="$OUT_ROOT/osx-x64"
  UNIVERSAL_DIR="$OUT_ROOT/universal"
  APP_DIR="$UNIVERSAL_DIR/ShapeForge.App.app"
  CONTENTS_DIR="$APP_DIR/Contents"
  MACOS_DIR="$CONTENTS_DIR/MacOS"

  rm -rf "$APP_DIR"
  mkdir -p "$MACOS_DIR" "$CONTENTS_DIR/Resources"

  # Keep per-architecture publish outputs intact, and dispatch at runtime.
  rm -rf "$CONTENTS_DIR/Resources/osx-arm64" "$CONTENTS_DIR/Resources/osx-x64"
  cp -R "$ARM_DIR" "$CONTENTS_DIR/Resources/osx-arm64"
  cp -R "$X64_DIR" "$CONTENTS_DIR/Resources/osx-x64"

  cat > "$MACOS_DIR/ShapeForge.App" <<'LAUNCHER'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CONTENTS_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
RES_DIR="$CONTENTS_DIR/Resources"

ARCH="$(uname -m)"
case "$ARCH" in
  arm64)
    TARGET="$RES_DIR/osx-arm64/ShapeForge.App"
    ;;
  x86_64)
    TARGET="$RES_DIR/osx-x64/ShapeForge.App"
    ;;
  *)
    echo "Unsupported macOS architecture: $ARCH" >&2
    exit 1
    ;;
esac

exec "$TARGET" "$@"
LAUNCHER
  chmod +x "$MACOS_DIR/ShapeForge.App"

  cat > "$CONTENTS_DIR/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>ShapeForge.App</string>
  <key>CFBundleDisplayName</key>
  <string>ShapeForge</string>
  <key>CFBundleIdentifier</key>
  <string>com.shapeforge.app</string>
  <key>CFBundleVersion</key>
  <string>0.1.0</string>
  <key>CFBundleShortVersionString</key>
  <string>0.1.0</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleExecutable</key>
  <string>ShapeForge.App</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
</dict>
</plist>
PLIST

  echo "Universal app bundle built: $APP_DIR"
  echo "Contains per-arch payloads:"
  echo "  - $CONTENTS_DIR/Resources/osx-arm64"
  echo "  - $CONTENTS_DIR/Resources/osx-x64"
else
  publish_rid "$RID"
fi
