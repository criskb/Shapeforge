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
  if ! command -v lipo >/dev/null 2>&1; then
    echo "error: lipo not found. Install Xcode + Command Line Tools." >&2
    exit 127
  fi

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

  cp -R "$ARM_DIR"/. "$MACOS_DIR"/

  lipo -create \
    "$ARM_DIR/ShapeForge.App" \
    "$X64_DIR/ShapeForge.App" \
    -output "$MACOS_DIR/ShapeForge.App"
  chmod +x "$MACOS_DIR/ShapeForge.App"

  # Merge additional native Mach-O binaries (e.g. .dylib) when present in both outputs.
  while IFS= read -r arm_file; do
    rel_path="${arm_file#"$ARM_DIR/"}"
    x64_file="$X64_DIR/$rel_path"
    out_file="$MACOS_DIR/$rel_path"

    if [[ -f "$x64_file" ]]; then
      if file "$arm_file" | grep -q "Mach-O" && file "$x64_file" | grep -q "Mach-O"; then
        lipo -create "$arm_file" "$x64_file" -output "$out_file" || true
      fi
    fi
  done < <(find "$ARM_DIR" -type f \( -name "*.dylib" -o -name "*.so" \))

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
  echo "Verify architectures with: lipo -archs '$MACOS_DIR/ShapeForge.App'"
else
  publish_rid "$RID"
fi
