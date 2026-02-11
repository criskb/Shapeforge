#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

"$SCRIPT_DIR/build-app.sh" universal full

APP_DIR="$REPO_ROOT/artifacts/macos/universal/ShapeForge.App.app"
ZIP_PATH="$REPO_ROOT/artifacts/macos/ShapeForge.App-macos-universal.zip"

if command -v ditto >/dev/null 2>&1; then
  rm -f "$ZIP_PATH"
  ditto -c -k --sequesterRsrc --keepParent "$APP_DIR" "$ZIP_PATH"
  echo "Packaged distributable: $ZIP_PATH"
else
  echo "warning: ditto not found, skipping zip packaging." >&2
fi
