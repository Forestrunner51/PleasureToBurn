#!/usr/bin/env bash
# Builds the C# assembly and runs the headless smoke test.
# Set GODOT to your Godot .NET editor binary if it is not on PATH.
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT:-$(command -v godot || echo "/Applications/Godot_mono 4.app/Contents/MacOS/Godot")}"
dotnet build --nologo -v quiet
"$GODOT" --headless --path . --import >/dev/null 2>&1 || true
"$GODOT" --headless --path . res://tests/smoke_test.tscn
