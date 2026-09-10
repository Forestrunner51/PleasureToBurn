#!/usr/bin/env bash
# Render a scene to a PNG so layout and orientation can be checked without opening the editor.
#   tools/shot.sh <scene_or_glb> <out.png> [yaw] [pitch] [dist] [target_y]
# Needs a desktop session: it opens a small window for one frame.
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT:-$(command -v godot || echo "/Applications/Godot_mono 4.app/Contents/MacOS/Godot")}"
out="${2:-/tmp/shot.png}"
"$GODOT" --path . --resolution 1280x720 --position 40,40 res://tools/shot/shot.tscn -- "${1:-res://scenes/world/world.tscn}" "$out" "${3:-35}" "${4:-25}" "${5:-20}" "${6:-3}"
