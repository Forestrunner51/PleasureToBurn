#!/usr/bin/env bash
# Builds the C# assembly and runs every headless test scene. Exit code is non-zero if any fails.
# Set GODOT to your Godot .NET editor binary if it is not on PATH.
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT:-$(command -v godot || echo "/Applications/Godot_mono 4.app/Contents/MacOS/Godot")}"
dotnet build --nologo -v quiet
"$GODOT" --headless --path . --import >/dev/null 2>&1 || true
status=0
for scene in tests/fire_tests.tscn tests/world_tests.tscn; do
  echo "== $scene"
  "$GODOT" --headless --path . "res://$scene" || status=1
done
exit $status
