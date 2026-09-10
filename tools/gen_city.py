"""Generates the neighbourhood in scenes/world/world.tscn: roads, pavements, lots, dressing and signs.

Run directly, or let gen_scenes.py call build() at the end of its run. Idempotent: it replaces the
node region between "Roads" and "Depot", and the region between "Sites" and "Beacon", every time.

The layout is deliberately repetitive. The premise is a municipal day job you cannot get out of, so the
streets are a ring with one cross avenue, the same few house types repeat down each street, and the only
things that tell one corner from another are the street signs and the four job addresses.

Also writes tools/city_map.svg, a top-down plan for checking the layout without opening Godot.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from glb_bounds import bounds

P = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
M = P + "/assets/models"
C, V = 7.5, 2.0                  # city kit scale, vehicle kit scale
WORLD = 96.0                     # keep everything inside this half-extent of the 200 m ground
PAVE = 3.0                       # pavement width either side of a carriageway
LOT = 16.0                       # frontage per lot
SETBACK = 15.0                   # road centreline to building centre
Y_ROAD_X, Y_ROAD_Z, Y_PAVE = 0.03, 0.05, 0.012   # separated so overlapping quads never z-fight


def T(x, y, z, rot=0.0, s=1.0):
    c, si = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    f = lambda v: f"{round(v, 5):g}"
    return f"Transform3D({f(c*s)}, 0, {f(-si*s)}, 0, {f(s)}, 0, {f(si*s)}, 0, {f(c*s)}, {f(x)}, {f(y)}, {f(z)})"


_info_cache = {}
def model_info(rel, scale):
    key = (rel, scale)
    if key not in _info_cache:
        lo, hi = bounds(f"{M}/{rel}")
        size = [(hi[i] - lo[i]) * scale for i in range(3)]
        _info_cache[key] = size
    return _info_cache[key]


# ---------------- the plan ----------------
# name, axis ('x' runs east-west at a fixed z, 'z' runs north-south at a fixed x), coord, from, to, width, sign text
ROADS = [
    ("KilnRow",       'x', -76.0, -WORLD, WORLD, 10.0, "KILN ROW"),
    ("LarkspurLane",  'x', -30.0, -WORLD, WORLD, 10.0, "LARKSPUR LANE"),
    ("MainAvenue",    'x',  34.0, -WORLD, WORLD, 12.0, "MAIN AVENUE"),
    ("AshgroveRoad",  'x',  78.0, -WORLD, WORLD, 10.0, "ASHGROVE ROAD"),
    ("TallowCourt",   'z', -76.0,  -81.0,  83.0, 10.0, "TALLOW COURT"),
    ("SectorFourWest",'z', -30.0,  -81.0,  83.0,  8.0, "SECTOR 4 WEST"),
    ("SectorFourEast",'z',  30.0,  -81.0,  83.0,  8.0, "SECTOR 4 EAST"),
    ("MillerTerrace", 'z',  76.0,  -81.0,  83.0, 10.0, "MILLER TERRACE"),
    ("DepotRoad",     'z',   0.0,    6.0,  34.0, 12.0, "DEPOT ROAD"),
]

# The four job addresses, each a lot on the street it is named after, dressed like every other house.
# name, address, x, z, yaw (the house doorway is local +Z and must face the road)
SITES = [
    ("LarkspurLane",  "14 Larkspur Lane",  -8.0, -45.0,   0.0),
    ("TallowCourt",   "3 Tallow Court",   -61.0,  10.0, -90.0),
    ("AshgroveRoad",  "22 Ashgrove Road",  20.0,  63.0,   0.0),
    ("MillerTerrace", "8 Miller Terrace",  91.0, -10.0, -90.0),
]

# Each street repeats a short list of house types. Sameness is the point.
PALETTE = {
    "KilnRow":        ["h", "i", "j"],
    "LarkspurLane":   ["a", "c", "e"],
    "MainAvenue":     ["b", "d", "o"],
    "AshgroveRoad":   ["p", "q", "s"],
    "TallowCourt":    ["k", "l", "r"],
    "SectorFourWest": ["f", "g"],
    "SectorFourEast": ["g", "f"],
    "MillerTerrace":  ["a", "e", "c"],
    "DepotRoad":      ["l", "r"],
}
CORNER_TYPES = ["n", "t", "u", "m"]   # the only buildings that tell one corner from another

DEPOT_RESERVE = (-13.0, -13.0, 13.0, 24.0)   # x0, z0, x1, z1


def rect(cx, cz, sx, sz):
    return (cx - sx / 2, cz - sz / 2, cx + sx / 2, cz + sz / 2)


def overlaps(a, b, pad=0.0):
    return not (a[2] + pad <= b[0] or a[0] - pad >= b[2] or a[3] + pad <= b[1] or a[1] - pad >= b[3])


def road_rect(r, pad=0.0):
    _, axis, coord, lo, hi, w, _ = r
    if axis == 'x':
        return (lo, coord - w / 2 - pad, hi, coord + w / 2 + pad)
    return (coord - w / 2 - pad, lo, coord + w / 2 + pad, hi)


def subtract(interval, cuts):
    """interval minus a list of (lo, hi) cuts, as a list of surviving intervals."""
    out = [interval]
    for clo, chi in cuts:
        nxt = []
        for lo, hi in out:
            if chi <= lo or clo >= hi:
                nxt.append((lo, hi))
                continue
            if clo > lo:
                nxt.append((lo, clo))
            if chi < hi:
                nxt.append((chi, hi))
        out = nxt
    return [(lo, hi) for lo, hi in out if hi - lo > 0.5]


# ---------------- emitters ----------------
class Build:
    def __init__(self):
        self.ext, self.sub, self.nodes = [], [], []
        self._ext_seen = set()

    def ext_scene(self, ident, path):
        if ident not in self._ext_seen:
            self._ext_seen.add(ident)
            self.ext.append(f'[ext_resource type="PackedScene" path="res://{path}" id="{ident}"]')
        return ident

    def add(self, text):
        self.nodes.append(text)


def quad(b, ident, cx, cz, sx, sz, y, material):
    b.sub.append(f'[sub_resource type="BoxMesh" id="{ident}"]\nmaterial = SubResource("{material}")\n'
                 f'size = Vector3({sx:.2f}, 0.04, {sz:.2f})')
    return f'transform = {T(cx, y, cz)}\nmesh = SubResource("{ident}")\n'


def facing_yaw(fx, fz):
    """Yaw that points a model's local +Z along (fx, fz)."""
    return round(math.degrees(math.atan2(fx, fz)), 3)


def building_node(b, parent, name, kind, x, z, yaw):
    size = model_info(f"city/building-type-{kind}.glb", C)
    shape = f"gen_shape_{kind}"
    if not any(shape in s for s in b.sub):
        b.sub.append(f'[sub_resource type="BoxShape3D" id="{shape}"]\n'
                     f'size = Vector3({size[0]:.2f}, {size[1]:.2f}, {size[2]:.2f})')
    b.ext_scene(f"city_{kind}", f"assets/models/city/building-type-{kind}.glb")
    return f'''
[node name="{name}" type="StaticBody3D" parent="{parent}"]
transform = {T(x, 0, z, yaw)}
collision_layer = 2
collision_mask = 0

[node name="Model" parent="{parent}/{name}" instance=ExtResource("city_{kind}")]
transform = {T(0, 0, 0, 0, C)}

[node name="CollisionShape3D" type="CollisionShape3D" parent="{parent}/{name}"]
transform = {T(0, size[1] / 2, 0)}
shape = SubResource("{shape}")
'''


def tree_node(b, parent, name, kind, x, z, yaw):
    b.ext_scene(f"city_tree_{kind}", f"assets/models/city/tree-{kind}.glb")
    if not any("gen_trunk" in s for s in b.sub):
        b.sub.append('[sub_resource type="BoxShape3D" id="gen_trunk"]\nsize = Vector3(0.6, 4, 0.6)')
    return f'''
[node name="{name}" type="StaticBody3D" parent="{parent}"]
transform = {T(x, 0, z, yaw)}
collision_layer = 2
collision_mask = 0

[node name="Model" parent="{parent}/{name}" instance=ExtResource("city_tree_{kind}")]
transform = {T(0, 0, 0, 0, C)}

[node name="CollisionShape3D" type="CollisionShape3D" parent="{parent}/{name}"]
transform = {T(0, 2, 0)}
shape = SubResource("gen_trunk")
'''


def flat_node(b, parent, name, ident, rel, x, z, yaw, scale=C):
    """A piece of yard dressing from the kit: driveway, path, fence. No collision, it is decoration."""
    b.ext_scene(ident, f"assets/models/city/{rel}")
    return (f'\n[node name="{name}" parent="{parent}" instance=ExtResource("{ident}")]\n'
            f'transform = {T(x, 0.02, z, yaw, scale)}\n')


def label_node(parent, name, text, x, y, z, size=48, colour="Color(0.95, 0.93, 0.85, 1)", pixel=0.010):
    return f'''
[node name="{name}" type="Label3D" parent="{parent}"]
transform = {T(x, y, z)}
pixel_size = {pixel}
billboard = 1
shaded = false
double_sided = true
no_depth_test = false
text = "{text}"
font_size = {size}
outline_size = 14
modulate = {colour}
outline_modulate = Color(0.05, 0.05, 0.06, 1)
'''


# ---------------- the build ----------------
def build():
    b = Build()
    b.sub.append('[sub_resource type="StandardMaterial3D" id="gen_mat_pave"]\n'
                 'albedo_color = Color(0.38, 0.37, 0.35, 1)\nroughness = 1.0')
    b.sub.append('[sub_resource type="StandardMaterial3D" id="gen_mat_post"]\n'
                 'albedo_color = Color(0.13, 0.14, 0.15, 1)\nroughness = 0.8')
    b.sub.append('[sub_resource type="BoxMesh" id="gen_post"]\n'
                 'material = SubResource("gen_mat_post")\nsize = Vector3(0.18, 4, 0.18)')

    x_roads = [r for r in ROADS if r[1] == 'x']
    z_roads = [r for r in ROADS if r[1] == 'z']

    # --- carriageways and pavements -------------------------------------------------------------
    b.add('\n[node name="Roads" type="Node3D" parent="."]\n')
    b.add('\n[node name="Pavements" type="Node3D" parent="Roads"]\n')
    strip = 0
    for r in ROADS:
        name, axis, coord, lo, hi, w, _ = r
        crossing = z_roads if axis == 'x' else x_roads
        cuts = [(c[2] - c[5] / 2 - PAVE, c[2] + c[5] / 2 + PAVE) for c in crossing if c is not r]
        for side in (-1, 1):
            edge = coord + side * (w / 2 + PAVE / 2)
            for a, z2 in subtract((lo, hi), cuts):
                strip += 1
                ident = f"gen_pave_{strip}"
                if axis == 'x':
                    body = quad(b, ident, (a + z2) / 2, edge, z2 - a, PAVE, Y_PAVE, "gen_mat_pave")
                else:
                    body = quad(b, ident, edge, (a + z2) / 2, PAVE, z2 - a, Y_PAVE, "gen_mat_pave")
                b.add(f'\n[node name="Pave{strip}" type="MeshInstance3D" parent="Roads/Pavements"]\n{body}')

    b.add('\n[node name="Carriageways" type="Node3D" parent="Roads"]\n')
    for r in ROADS:
        name, axis, coord, lo, hi, w, _ = r
        y = Y_ROAD_X if axis == 'x' else Y_ROAD_Z
        if axis == 'x':
            body = quad(b, f"gen_road_{name}", (lo + hi) / 2, coord, hi - lo, w, y, "StandardMaterial3D_road")
        else:
            body = quad(b, f"gen_road_{name}", coord, (lo + hi) / 2, w, hi - lo, y, "StandardMaterial3D_road")
        b.add(f'\n[node name="{name}" type="MeshInstance3D" parent="Roads/Carriageways"]\n{body}')

    # The depot forecourt, on the side the building actually opens onto.
    b.add('\n[node name="DepotApron" type="MeshInstance3D" parent="Roads"]\n'
          + quad(b, "gen_road_apron", 0, 10.5, 26, 9, Y_ROAD_X, "StandardMaterial3D_road"))

    # --- lots ------------------------------------------------------------------------------------
    taken = [DEPOT_RESERVE]
    site_rects = {}
    for _, _, sx, sz, _ in SITES:
        rc = rect(sx, sz, 30, 30)
        site_rects[(sx, sz)] = rc
        taken.append(rc)

    b.add('\n[node name="Lots" type="Node3D" parent="."]\n')
    lot_plan = []          # for the map: (x, z, sx, sz, kind)
    BAND = 8.0             # half-depth of the strip a row of houses occupies
    for r in ROADS:
        name, axis, coord, lo, hi, w, _ = r
        if name not in PALETTE:
            continue
        palette = PALETTE[name]
        for side in (-1, 1):
            perp = coord + side * SETBACK
            fx, fz = (0.0, float(-side)) if axis == 'x' else (float(-side), 0.0)
            rx, rz = (1.0, 0.0) if axis == 'x' else (0.0, 1.0)
            yaw = facing_yaw(fx, fz)
            band = ((lo, perp - BAND, hi, perp + BAND) if axis == 'x'
                    else (perp - BAND, lo, perp + BAND, hi))

            # Anything the row cannot run through: the other roads, the depot, the four addresses.
            obstacles = [road_rect(o, PAVE + 1) for o in ROADS if o is not r]
            obstacles.append(DEPOT_RESERVE)
            obstacles += [rect(sx, sz, 20, 20) for _, _, sx, sz, _ in SITES]
            cuts = []
            for o in obstacles:
                if not overlaps(band, o):
                    continue
                cuts.append((o[0] - 1, o[2] + 1) if axis == 'x' else (o[1] - 1, o[3] + 1))

            span = (max(lo, -WORLD), min(hi, WORLD))
            runs = subtract(span, cuts)
            placed_here = []
            for run_lo, run_hi in runs:
                count = int((run_hi - run_lo) // LOT)
                if count < 1:
                    continue
                first = (run_lo + run_hi) / 2 - count * LOT / 2 + LOT / 2
                for k in range(count):
                    t = first + k * LOT
                    bx, bz = (t, perp) if axis == 'x' else (perp, t)
                    if abs(bx) > WORLD or abs(bz) > WORLD:
                        continue
                    placed_here.append((len(placed_here), bx, bz))

            for pos, (i, bx, bz) in enumerate(placed_here):
                kind = palette[i % len(palette)]
                # the ends of a row get the only buildings that look different, so corners are landmarks
                if pos in (0, len(placed_here) - 1) and len(placed_here) > 2:
                    kind = CORNER_TYPES[(abs(hash(name)) + pos) % len(CORNER_TYPES)]
                lot = f"Lot_{name}_{'A' if side > 0 else 'B'}{i}"
                size = model_info(f"city/building-type-{kind}.glb", C)
                b.add(f'\n[node name="{lot}" type="Node3D" parent="Lots"]\n')
                b.add(building_node(b, f"Lots/{lot}", "Building", kind, bx, bz, yaw))
                lot_plan.append((bx, bz, size[0] if axis == 'x' else size[2],
                                 size[2] if axis == 'x' else size[0], kind))
                dress(b, f"Lots/{lot}", bx, bz, fx, fz, rx, rz, yaw, size, w)

    # --- the four job addresses ------------------------------------------------------------------
    sites_nodes = ['\n[node name="Sites" type="Node3D" parent="."]\n']
    for name, address, sx, sz, yaw in SITES:
        fx, fz = math.sin(math.radians(yaw)), math.cos(math.radians(yaw))
        number = address.split(" ", 1)[0]
        sites_nodes.append(f'''
[node name="{name}" type="Node3D" parent="Sites"]
transform = {T(sx, 0, sz, yaw)}
script = ExtResource("4_site")
Building = ExtResource("3_house")
Address = "{address}"
''')
        # the number on the house, so the address on the docket is something you can actually read
        sites_nodes.append(label_node(f"Sites/{name}", "Number", number, 0, 3.5, 5.4, 48,
                                      "Color(0.9, 0.87, 0.78, 1)"))
        dress_site(b, name, sx, sz, fx, fz, yaw)

    # --- street signs -----------------------------------------------------------------------------
    b.add('\n[node name="StreetSigns" type="Node3D" parent="."]\n')
    sign = 0
    for xr in x_roads:
        for zr in z_roads:
            if not (zr[3] <= xr[2] <= zr[4] and xr[3] <= zr[2] <= xr[4]):
                continue
            if not xr[6] or not zr[6]:
                continue
            sign += 1
            px = zr[2] + zr[5] / 2 + PAVE / 2
            pz = xr[2] + xr[5] / 2 + PAVE / 2
            b.add(f'\n[node name="Sign{sign}" type="Node3D" parent="StreetSigns"]\ntransform = {T(px, 0, pz)}\n')
            b.add(f'\n[node name="Post" type="MeshInstance3D" parent="StreetSigns/Sign{sign}"]\n'
                  f'transform = {T(0, 2, 0)}\nmesh = SubResource("gen_post")\n')
            b.add(label_node(f"StreetSigns/Sign{sign}", "Along", xr[6], 0, 3.9, 0, 32))
            b.add(label_node(f"StreetSigns/Sign{sign}", "Across", zr[6], 0, 3.35, 0, 32,
                             "Color(0.72, 0.74, 0.7, 1)"))

    # --- a few parked cars ------------------------------------------------------------------------
    b.ext_scene("car_wheel", "assets/models/cars/wheel-default.glb")
    wheel_h = model_info("cars/wheel-default.glb", V)[1]
    cars = [("sedan", -40.0, 29.5, 90), ("van", 44.0, 38.5, -90), ("taxi", -52.0, -26.5, 90),
            ("suv", -72.5, -40.0, 0), ("police", 79.5, 20.0, 180), ("delivery", 10.0, -72.5, 90)]
    b.add('\n[node name="ParkedCars" type="Node3D" parent="."]\n')
    for i, (kind, x, z, rot) in enumerate(cars):
        size = model_info(f"cars/{kind}.glb", V)
        b.ext_scene(f"car_{kind}", f"assets/models/cars/{kind}.glb")
        b.sub.append(f'[sub_resource type="BoxShape3D" id="gen_car_{kind}"]\n'
                     f'size = Vector3({size[0]:.2f}, {size[1]*0.8:.2f}, {size[2]:.2f})')
        b.add(f'''
[node name="Car{i}" type="StaticBody3D" parent="ParkedCars"]
transform = {T(x, wheel_h / 2, z, rot)}
collision_layer = 2
collision_mask = 0

[node name="Model" parent="ParkedCars/Car{i}" instance=ExtResource("car_{kind}")]
transform = {T(0, 0, 0, 180, V)}

[node name="CollisionShape3D" type="CollisionShape3D" parent="ParkedCars/Car{i}"]
transform = {T(0, size[1] * 0.4, 0)}
shape = SubResource("gen_car_{kind}")
''')
        for wx, wz in ((-0.62 * V, 0.7 * V), (0.62 * V, 0.7 * V), (-0.62 * V, -0.7 * V), (0.62 * V, -0.7 * V)):
            tag = "".join(str(int(v > 0)) for v in (wx, wz))
            b.add(f'\n[node name="Wheel{tag}" parent="ParkedCars/Car{i}" instance=ExtResource("car_wheel")]\n'
                  f'transform = {T(wx, 0, wz, 0, V)}\n')

    # --- the depot forecourt ----------------------------------------------------------------------
    b.ext_scene("car_cone", "assets/models/cars/cone.glb")
    b.add('\n[node name="Forecourt" type="Node3D" parent="."]\n')
    for i, (cx, cz) in enumerate(((-11.0, 13.0), (-11.0, 8.0), (11.0, 13.0), (11.0, 8.0))):
        b.add(f'\n[node name="Cone{i}" parent="Forecourt" instance=ExtResource("car_cone")]\n'
              f'transform = {T(cx, 0, cz, 0, V)}\n')
    b.add(tree_node(b, "Forecourt", "YardTreeWest", "large", -16.0, 2.0, 0))
    b.add(tree_node(b, "Forecourt", "YardTreeEast", "large", 16.0, 2.0, 0))
    b.add(f'\n[node name="SignPost" type="MeshInstance3D" parent="Forecourt"]\n'
          f'transform = {T(-10, 2, 8)}\nmesh = SubResource("gen_post")\n')
    b.add(label_node("Forecourt", "StationName", "MUNICIPAL FIRE STATION 7", -10, 4.1, 8, 44))
    b.add(label_node("Forecourt", "StationMotto", "IT IS A PLEASURE TO SERVE", -10, 3.5, 8, 28,
                     "Color(0.72, 0.7, 0.64, 1)"))

    splice(b, "".join(sites_nodes))
    write_map(lot_plan)
    print(f"city: {len(lot_plan)} lots, {sign} signed junctions, {len(cars)} parked cars")


def dress(b, parent, bx, bz, fx, fz, rx, rz, yaw, size, road_width):
    """Driveway, front path, fence and a tree, so a lot reads as somebody's home and not a prop."""
    depth = size[2]
    kerb = SETBACK - (road_width / 2 + PAVE)          # building centre to pavement inner edge
    yard = kerb - depth / 2
    fence_yaw = round(math.degrees(math.atan2(-rz, rx)), 3)
    at = lambda d, o: (bx + fx * d + rx * o, bz + fz * d + rz * o)

    tiles = max(1, int(yard / 3.0))
    for k in range(tiles):
        d = depth / 2 + 1.5 + k * 3.0
        x, z = at(d, 4.5)
        b.add(flat_node(b, parent, f"Drive{k}", "city_drive", "driveway-long.glb", x, z, yaw))
        x, z = at(d, 0.0)
        b.add(flat_node(b, parent, f"Path{k}", "city_path", "path-long.glb", x, z, yaw))

    # A low front fence, in panels, with the middle left open where the path reaches the pavement.
    for j, o in enumerate((-7.0, -4.4, -1.8, 1.8, 4.4, 7.0)):
        x, z = at(kerb - 0.8, o)
        b.add(flat_node(b, parent, f"Fence{j}", "city_fence", "fence.glb", x, z, fence_yaw, C * 0.72))

    x, z = at(max(2.0, yard * 0.5), -10.0)
    b.add(tree_node(b, parent, "Tree", "large" if (int(bx) + int(bz)) % 2 else "small", x, z, yaw))


def dress_site(b, name, sx, sz, fx, fz, yaw):
    """The job addresses get the same yard as every other house, so they do not stand out."""
    lot = f"Lot_Site_{name}"
    b.add(f'\n[node name="{lot}" type="Node3D" parent="Lots"]\n')
    dress(b, f"Lots/{lot}", sx, sz, fx, fz, -fz, fx, yaw, [10.6, 3.0, 10.6], 10.0)


def splice(b, sites_text):
    import re
    path = f"{P}/scenes/world/world.tscn"
    text = open(path).read()
    head, sep, body = text.partition('\n[node ')
    body = sep + body

    doomed_sub = re.compile(r'id="(gen_\w+|BoxMesh_road_\w+|BoxMesh_block_\d+|BoxShape3D_block_\d+'
                            r'|BoxShape3D_city_\w+|BoxShape3D_trunk|BoxShape3D_car_\w+)"')
    doomed_ext = re.compile(r'path="res://assets/models/(city|cars)/')

    # Godot wants every ext_resource before the first sub_resource, so the header is rebuilt in that order.
    lines = head.split('\n')
    ext_lines, sub_blocks, current = [], [], None
    for line in lines[1:]:
        if line.startswith('[ext_resource'):
            current = None
            if not doomed_ext.search(line):
                ext_lines.append(line)
        elif line.startswith('[sub_resource'):
            current = [line]
            sub_blocks.append(current)
        elif current is not None:
            if line.strip() == '':
                current = None
            else:
                current.append(line)
    kept_sub = ['\n'.join(blk) for blk in sub_blocks if not doomed_sub.search(blk[0])]
    head = (lines[0] + '\n\n' + '\n'.join(ext_lines + b.ext)
            + '\n\n' + '\n\n'.join(kept_sub + b.sub) + '\n')

    start = body.index('[node name="Roads"')
    end = body.index('[node name="Depot"')
    body = body[:start] + "".join(b.nodes).lstrip('\n') + '\n' + body[end:]

    start = body.index('[node name="Sites"')
    end = body.index('[node name="Beacon"')
    body = body[:start] + sites_text.lstrip('\n') + '\n' + body[end:]

    # The depot's only opening is +Z and positive engine force drives a VehicleBody3D toward its own +Z,
    # so the truck must sit unrotated or W drives it straight into the back wall.
    body = re.sub(r'(\[node name="Truck" parent="\."[^\]]*\]\n)transform = [^\n]+',
                  lambda m: m.group(1) + f'transform = {T(-3, 0.6, 0)}', body)

    text = head + '\n' + body.lstrip('\n')
    text = re.sub(r'\n{3,}', '\n\n', text)
    steps = text.count('[sub_resource') + text.count('[ext_resource') + 1
    text = re.sub(r'\[gd_scene ([^\]]*?)load_steps=\d+ ?', rf'[gd_scene \1load_steps={steps} ', text, count=1)
    if 'load_steps' not in text.split('\n')[0]:
        text = re.sub(r'\[gd_scene ', f'[gd_scene load_steps={steps} ', text, count=1)
    open(path, "w").write(text)


def write_map(lots):
    """Top-down plan, so the layout can be sanity-checked without opening the editor."""
    s, pad = 3.0, 20
    to = lambda v: (v + 100) * s + pad
    w = 200 * s + pad * 2
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{w:.0f}" height="{w:.0f}" '
           f'viewBox="0 0 {w:.0f} {w:.0f}"><rect width="100%" height="100%" fill="#26282b"/>']
    for r in ROADS:
        x0, z0, x1, z1 = road_rect(r)
        out.append(f'<rect x="{to(x0):.1f}" y="{to(z0):.1f}" width="{(x1-x0)*s:.1f}" '
                   f'height="{(z1-z0)*s:.1f}" fill="#3a3d41"/>')
    for bx, bz, sx, sz, kind in lots:
        out.append(f'<rect x="{to(bx-sx/2):.1f}" y="{to(bz-sz/2):.1f}" width="{sx*s:.1f}" '
                   f'height="{sz*s:.1f}" fill="#8c8f93"/>')
    x0, z0, x1, z1 = DEPOT_RESERVE
    out.append(f'<rect x="{to(x0):.1f}" y="{to(z0):.1f}" width="{(x1-x0)*s:.1f}" '
               f'height="{(z1-z0)*s:.1f}" fill="#c8552b"/>')
    out.append(f'<text x="{to(0):.0f}" y="{to(6):.0f}" fill="#fff" font-size="13" '
               f'font-family="sans-serif" text-anchor="middle">DEPOT</text>')
    for name, address, sx, sz, yaw in SITES:
        out.append(f'<rect x="{to(sx-5.3):.1f}" y="{to(sz-5.3):.1f}" width="{10.6*s:.1f}" '
                   f'height="{10.6*s:.1f}" fill="#e0b341"/>')
        out.append(f'<text x="{to(sx):.0f}" y="{to(sz+13):.0f}" fill="#e0b341" font-size="12" '
                   f'font-family="sans-serif" text-anchor="middle">{address}</text>')
    out.append('</svg>')
    open(f"{P}/tools/city_map.svg", "w").write("\n".join(out))


if __name__ == "__main__":
    build()
