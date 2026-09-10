#!/usr/bin/env bash
# Render a scene or a bare .glb to a PNG, to check layout and orientation without opening the editor.
#   devtools/shot.sh <scene_or_glb> <out.png> [yaw] [elevation] [distance] [target_y] [target_x] [target_z]
# Opens a window for a moment, so it needs a desktop session (Godot cannot render headless).
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT:-$(command -v godot || echo "/Applications/Godot_mono 4.app/Contents/MacOS/Godot")}"
"$GODOT" --path . --rendering-driver metal --resolution 1280x720 res://devtools/shot.tscn -- \
  "${1:-res://scenes/world/world.tscn}" "${2:-shot.png}" "${3:-35}" "${4:-25}" "${5:-20}" "${6:-3}" "${7:-0}" "${8:-0}"
