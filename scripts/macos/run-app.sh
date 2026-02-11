#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

RID="${1:-osx-arm64}"
APP_BIN="$REPO_ROOT/artifacts/macos/$RID/ShapeForge.App"

if [[ ! -x "$APP_BIN" ]]; then
  echo "Build output not found. Running build first..."
  "$SCRIPT_DIR/build-app.sh" "$RID"
fi

exec "$APP_BIN"
