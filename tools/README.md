# tools

Python 3, no dependencies. Run from anywhere.

- `gen_scenes.py` — regenerates every prop, the house, the test room, the truck and the world dressing from the
  Kenney `.glb` files in `assets/models/`. Reads each model's bounds so collision and centring stay correct.
  Edit the `layout` list in `house_interior()` to rearrange a room, then run it and re-import in Godot.
- `gen_city.py` — the world layout: roads, pavements, lots, yards, street signs and the four job addresses.
  Splices itself into `scenes/world/world.tscn` and is idempotent. Edit the `ROADS`, `SITES` and `PALETTE` lists
  at the top, run it, re-import in Godot. Also writes `city_map.svg`, a top-down plan.
- `glb_bounds.py <file.glb ...>` — prints size / min / max of a model (node transforms applied).
- `glb_verts.py facing <file.glb>` — which way a vehicle faces; `glb_verts.py levels <file.glb>` — shelf heights.

Godot needs a `.gdignore` here so it does not try to import the scripts; one is included.
