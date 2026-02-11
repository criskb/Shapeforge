#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

RID="${1:-universal}"

if [[ "$RID" == "universal" ]]; then
  APP_BUNDLE="$REPO_ROOT/artifacts/macos/universal/ShapeForge.App.app"
  APP_BIN="$APP_BUNDLE/Contents/MacOS/ShapeForge.App"

  if [[ ! -x "$APP_BIN" ]]; then
    echo "Universal build output not found. Running build first..."
    "$SCRIPT_DIR/build-app.sh" universal
  fi

  exec "$APP_BIN"
else
  APP_BIN="$REPO_ROOT/artifacts/macos/$RID/ShapeForge.App"

  if [[ ! -x "$APP_BIN" ]]; then
    echo "Build output not found. Running build first..."
    "$SCRIPT_DIR/build-app.sh" "$RID"
  fi

  exec "$APP_BIN"
fi
