import sys, math, os
sys.path.insert(0, os.path.dirname(__file__))
from glb_bounds import bounds
P = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
M = P + "/assets/models"
F, C, V = 2.0, 7.5, 2.0   # furniture, city, vehicle scale

def T(x, y, z, rot=0.0, s=1.0):
    c, si = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    f = lambda v: f"{round(v, 5):g}"
    return f"Transform3D({f(c*s)}, 0, {f(-si*s)}, 0, {f(s)}, 0, {f(si*s)}, 0, {f(c*s)}, {f(x)}, {f(y)}, {f(z)})"

def header(ext, sub):
    return f"[gd_scene load_steps={len(ext)+len(sub)+1} format=3]\n\n" + "\n".join(ext) + ("\n\n" if sub else "\n") + "\n".join(sub) + "\n"

def model_info(rel, scale):
    lo, hi = bounds(f"{M}/{rel}")
    size = [(hi[i]-lo[i])*scale for i in range(3)]
    off = (-(lo[0]+hi[0])/2*scale, -lo[1]*scale, -(lo[2]+hi[2])/2*scale)
    return size, off

def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "w").write(text)

# ---------------- props ----------------
def prop(name, rel, profile=None, contraband=False, scale=F, layer=None, collider_size=None, collider_center=None, extra_ext=(), extra_nodes=""):
    size, off = model_info(rel, scale)
    cs = collider_size or size
    cy = collider_center if collider_center is not None else cs[1]/2
    flammable = profile is not None
    ext = [f'[ext_resource type="PackedScene" path="res://assets/models/{rel}" id="1_model"]']
    if flammable:
        ext += [f'[ext_resource type="Script" path="res://systems/fire/Flammable.cs" id="2_flammable"]',
                f'[ext_resource type="Resource" path="res://resources/burn_profiles/{profile}.tres" id="3_profile"]']
    ext += list(extra_ext)
    sub = [f'[sub_resource type="BoxShape3D" id="BoxShape3D_prop"]\nsize = Vector3({cs[0]:.3f}, {cs[1]:.3f}, {cs[2]:.3f})']
    layer = layer or (4 if flammable else 2)
    body = f'''
[node name="{name}" type="StaticBody3D"]
collision_layer = {layer}
collision_mask = 0

[node name="Model" parent="." instance=ExtResource("1_model")]
transform = {T(off[0], off[1], off[2], 0, scale)}

[node name="CollisionShape3D" type="CollisionShape3D" parent="."]
transform = {T(0, cy, 0)}
shape = SubResource("BoxShape3D_prop")
'''
    if flammable:
        body += f'''
[node name="Flammable" type="Node" parent="."]
script = ExtResource("2_flammable")
Profile = ExtResource("3_profile")
IsContraband = {"true" if contraband else "false"}
'''
    write(f"{P}/scenes/props/{name}.tscn", header(ext, sub) + body + extra_nodes)
    return size

book_size = prop("book", "furniture/books.glb", "paper", True)

def bookshelf(name, rel, plank_tops_model, xs, z):
    book_nodes = '\n[node name="Books" type="Node3D" parent="."]\n'
    for r, top in enumerate(plank_tops_model):
        for c, x in enumerate(xs):
            book_nodes += f'\n[node name="Book_{r}_{c}" parent="Books" instance=ExtResource("4_book")]\ntransform = {T(x, top*F, z)}\n'
    prop(name, rel, "wood", False,
         extra_ext=['[ext_resource type="PackedScene" path="res://scenes/props/book.tscn" id="4_book"]'],
         extra_nodes=book_nodes)

bookshelf("bookshelf", "furniture/bookcaseClosedWide.glb", [0.07, 0.31, 0.55], [-0.62, -0.31, 0.0, 0.31, 0.62], 0.04)
bookshelf("bookshelf_small", "furniture/bookcaseOpen.glb", [0.13, 0.37, 0.61], [-0.18, 0.18], 0.02)

prop("table", "furniture/table.glb", "wood")
prop("chair", "furniture/chair.glb", "wood")
prop("desk", "furniture/desk.glb", "wood")
prop("side_table", "furniture/sideTable.glb", "wood")
prop("sofa", "furniture/loungeSofa.glb", "fabric")
rug = model_info("furniture/rugRectangle.glb", F)[0]
prop("rug", "furniture/rugRectangle.glb", "fabric", collider_size=[rug[0], 0.04, rug[2]])
prop("crate", "furniture/cardboardBoxClosed.glb", "paper")
prop("crate_open", "furniture/cardboardBoxOpen.glb", "paper")
prop("lamp", "furniture/lampRoundFloor.glb", "wood")
prop("coat_rack", "furniture/coatRackStanding.glb", "wood")
prop("plant", "furniture/pottedPlant.glb")
prop("radio", "furniture/radio.glb")
prop("tv", "furniture/televisionVintage.glb")

# ---------------- house (Location) ----------------
FLOOR_TOP = 0.1
def tile(name, rel, x, y, z, rot, parent):
    return f'\n[node name="{name}" parent="{parent}" instance=ExtResource("t_{rel}")]\ntransform = {T(x, y, z, rot, F)}\n'

def house_interior(prefix_parent):
    """Wall/floor tiles, roof, light, colliders, props. Returns (ext, sub, nodes)."""
    ext = [
        '[ext_resource type="Script" path="res://scenes/locations/Location.cs" id="1_location"]',
        '[ext_resource type="PackedScene" path="res://assets/models/furniture/floorFull.glb" id="t_floorFull"]',
        '[ext_resource type="PackedScene" path="res://assets/models/furniture/wall.glb" id="t_wall"]',
        '[ext_resource type="PackedScene" path="res://assets/models/furniture/wallWindow.glb" id="t_wallWindow"]',
        '[ext_resource type="PackedScene" path="res://assets/models/furniture/wallDoorway.glb" id="t_wallDoorway"]',
    ]
    props_used = ["bookshelf", "bookshelf_small", "rug", "table", "chair", "sofa", "side_table", "lamp", "plant",
                  "coat_rack", "desk", "radio", "tv", "crate", "crate_open", "book", "fuel_can"]
    ext += [f'[ext_resource type="PackedScene" path="res://scenes/props/{p}.tscn" id="p_{p}"]' for p in props_used]
    sub = [
        '[sub_resource type="StandardMaterial3D" id="StandardMaterial3D_roof"]\nalbedo_color = Color(0.3, 0.2, 0.18, 1)\nroughness = 0.9',
        '[sub_resource type="BoxMesh" id="BoxMesh_roof"]\nmaterial = SubResource("StandardMaterial3D_roof")\nsize = Vector3(10.6, 0.3, 10.6)',
        '[sub_resource type="BoxShape3D" id="BoxShape3D_floor"]\nsize = Vector3(10, 0.2, 10)',
        '[sub_resource type="BoxShape3D" id="BoxShape3D_wall"]\nsize = Vector3(10.3, 3.2, 0.3)',
        '[sub_resource type="BoxShape3D" id="BoxShape3D_wall_half"]\nsize = Vector3(4.5, 3.2, 0.3)',
        '[sub_resource type="BoxShape3D" id="BoxShape3D_lintel"]\nsize = Vector3(1.0, 1.2, 0.3)',
    ]
    n = f'''
[node name="Room" type="Node3D" parent="."]

[node name="Floor" type="Node3D" parent="Room"]
'''
    for i in range(5):
        for j in range(5):
            n += tile(f"Floor_{i}_{j}", "floorFull", -5+2*i, 0, 5-2*j, 0, "Room/Floor")
    n += '\n[node name="Walls" type="Node3D" parent="Room"]\n'
    y = FLOOR_TOP
    for i in range(5):
        n += tile(f"Back_{i}", "wallWindow" if i in (1, 3) else "wall", -5+2*i, y, -5, 0, "Room/Walls")
        n += tile(f"Front_{i}", "wallDoorway" if i == 2 else "wall", -5+2*i, y, 5.1, 0, "Room/Walls")
        n += tile(f"Left_{i}", "wallWindow" if i in (1, 3) else "wall", -5, y, 5-2*i, 90, "Room/Walls")
        n += tile(f"Right_{i}", "wallWindow" if i in (1, 3) else "wall", 5, y, -5+2*i, -90, "Room/Walls")
    n += f'''
[node name="Roof" type="MeshInstance3D" parent="Room"]
transform = {T(0, 2.83, 0)}
mesh = SubResource("BoxMesh_roof")

[node name="InteriorLight" type="OmniLight3D" parent="Room"]
transform = {T(0, 2.4, 0)}
light_color = Color(1, 0.9, 0.75, 1)
light_energy = 2.5
omni_range = 9.0

[node name="Collision" type="StaticBody3D" parent="Room"]
collision_layer = 2
collision_mask = 0

[node name="Floor" type="CollisionShape3D" parent="Room/Collision"]
transform = {T(0, 0, 0)}
shape = SubResource("BoxShape3D_floor")

[node name="WallBack" type="CollisionShape3D" parent="Room/Collision"]
transform = {T(0, 1.7, -5.05)}
shape = SubResource("BoxShape3D_wall")

[node name="WallFrontLeft" type="CollisionShape3D" parent="Room/Collision"]
transform = {T(-2.75, 1.7, 5.05)}
shape = SubResource("BoxShape3D_wall_half")

[node name="WallFrontRight" type="CollisionShape3D" parent="Room/Collision"]
transform = {T(2.75, 1.7, 5.05)}
shape = SubResource("BoxShape3D_wall_half")

[node name="Lintel" type="CollisionShape3D" parent="Room/Collision"]
transform = {T(0, 2.7, 5.05)}
shape = SubResource("BoxShape3D_lintel")

[node name="WallLeft" type="CollisionShape3D" parent="Room/Collision"]
transform = {T(-5.05, 1.7, 0, 90)}
shape = SubResource("BoxShape3D_wall")

[node name="WallRight" type="CollisionShape3D" parent="Room/Collision"]
transform = {T(5.05, 1.7, 0, 90)}
shape = SubResource("BoxShape3D_wall")

[node name="Props" type="Node3D" parent="."]
'''
    layout = [  # name, prop, x, y(above floor), z, rot
        ("ShelfBackLeft", "bookshelf", -3.1, 0, -4.65, 0), ("ShelfBackCenter", "bookshelf", 0, 0, -4.65, 0), ("ShelfBackRight", "bookshelf", 3.1, 0, -4.65, 0),
        ("ShelfSideLeft", "bookshelf_small", -4.7, 0, -1.5, 90),
        ("Rug", "rug", -1.2, 0, -2.2, 0),
        ("Table", "table", 2.4, 0, -2.0, 0), ("Chair", "chair", 2.4, 0, -0.9, 180),
        ("Sofa", "sofa", 4.55, 0, 1.6, -90), ("SideTable", "side_table", 4.6, 0, 3.1, -90), ("Lamp", "lamp", 4.6, 0, 4.3, 0),
        ("Plant", "plant", -4.5, 0, 4.5, 0), ("CoatRack", "coat_rack", -1.8, 0, 4.5, 0),
        ("Desk", "desk", -3.2, 0, 4.4, 180), ("Radio", "radio", -3.2, 0.76, 4.4, 180), ("Tv", "tv", 4.6, 0.76, 3.1, -90),
        ("Crate", "crate", 3.9, 0, 3.9, 0), ("CrateOpen", "crate_open", 3.3, 0, 4.4, 20),
        ("HiddenBookUnderTable", "book", 2.4, 0, -2.0, 90), ("HiddenBookBySofa", "book", 4.5, 0, 0.9, 0),
        ("HiddenBookBehindCrate", "book", 4.5, 0, 4.45, 0), ("HiddenBookOnDesk", "book", -3.0, 0.76, 4.35, 160),
        ("HiddenBookOnTable", "book", 2.6, 0.66, -2.1, 10),
        ("FuelCanByDoor", "fuel_can", 1.6, 0, 4.4, 0), ("FuelCanCorner", "fuel_can", -4.5, 0, -4.5, 0),
    ]
    for name, p, x, dy, z, rot in layout:
        n += f'\n[node name="{name}" parent="Props" instance=ExtResource("p_{p}")]\ntransform = {T(x, FLOOR_TOP+dy, z, rot)}\n'
    return ext, sub, n

ext, sub, nodes = house_interior(".")
write(f"{P}/scenes/locations/house.tscn", header(ext, sub) + '\n[node name="House" type="Node3D"]\nscript = ExtResource("1_location")\n' + nodes)

# ---------------- test room: the house plus environment, player and UI ----------------
tr_ext = [
    '[ext_resource type="PackedScene" path="res://scenes/locations/house.tscn" id="1_house"]',
    '[ext_resource type="PackedScene" path="res://scenes/player/player.tscn" id="2_player"]',
    '[ext_resource type="Script" path="res://systems/fire/FireVfx.cs" id="3_vfx"]',
    '[ext_resource type="PackedScene" path="res://scenes/ui/hud/hud.tscn" id="4_hud"]',
    '[ext_resource type="PackedScene" path="res://scenes/ui/pause_menu/pause_menu.tscn" id="5_pause"]',
]
tr_sub = ['''[sub_resource type="Environment" id="Environment_room"]
background_mode = 1
background_color = Color(0.04, 0.035, 0.03, 1)
ambient_light_source = 2
ambient_light_color = Color(0.7, 0.62, 0.55, 1)
ambient_light_energy = 0.4
tonemap_mode = 2''']
write(f"{P}/scenes/locations/test_room.tscn", header(tr_ext, tr_sub) + f'''
[node name="TestRoom" type="Node3D"]

[node name="WorldEnvironment" type="WorldEnvironment" parent="."]
environment = SubResource("Environment_room")

[node name="Sun" type="DirectionalLight3D" parent="."]
transform = Transform3D(0.866025, -0.353553, 0.353553, 0, 0.707107, 0.707107, -0.5, -0.612372, 0.612372, 0, 4, 0)
light_color = Color(1, 0.93, 0.82, 1)
light_energy = 0.8
shadow_enabled = true

[node name="House" parent="." instance=ExtResource("1_house")]

[node name="FireVfx" type="Node3D" parent="."]
script = ExtResource("3_vfx")

[node name="Player" parent="." instance=ExtResource("2_player")]
transform = {T(0.5, FLOOR_TOP, 2.5)}

[node name="HUD" parent="." instance=ExtResource("4_hud")]

[node name="PauseMenu" parent="." instance=ExtResource("5_pause")]
''')

# ---------------- truck ----------------
tsize, toff = model_info("cars/firetruck.glb", V)
wsize, _ = model_info("cars/wheel-truck.glb", V)
wheel_r = wsize[1]/2
def wheel(name, x, z, steer):
    return f'''
[node name="{name}" type="VehicleWheel3D" parent="."]
transform = {T(x, wheel_r + 0.25, z)}
use_as_traction = true{chr(10)+"use_as_steering = true" if steer else ""}
wheel_radius = {wheel_r:.2f}
wheel_rest_length = 0.25
wheel_friction_slip = 3.0
suspension_travel = 0.25
suspension_stiffness = 45.0
damping_compression = 0.9
damping_relaxation = 1.1

[node name="Mesh" parent="{name}" instance=ExtResource("4_wheel")]
transform = {T(0, 0, 0, 0, V)}
'''
truck_ext = [
    '[ext_resource type="Script" path="res://scenes/vehicles/Truck.cs" id="1_truck"]',
    '[ext_resource type="Script" path="res://scenes/vehicles/ChaseCamera.cs" id="2_camera"]',
    '[ext_resource type="PackedScene" path="res://assets/models/cars/firetruck.glb" id="3_model"]',
    '[ext_resource type="PackedScene" path="res://assets/models/cars/wheel-truck.glb" id="4_wheel"]',
]
truck_sub = [f'[sub_resource type="BoxShape3D" id="BoxShape3D_chassis"]\nsize = Vector3({tsize[0]-0.2:.2f}, 1.6, {tsize[2]-0.2:.2f})']
# Kenney vehicles face +Z; Godot forward is -Z, so the model is turned 180 degrees.
front_z, rear_z = -0.95*V, 0.65*V
write(f"{P}/scenes/vehicles/truck.tscn", header(truck_ext, truck_sub) + f'''
[node name="Truck" type="VehicleBody3D"]
collision_layer = 2
collision_mask = 2
mass = 1800.0
center_of_mass_mode = 1
center_of_mass = Vector3(0, 0.3, 0)
linear_damp = 0.6
angular_damp = 2.0
script = ExtResource("1_truck")

[node name="CollisionShape3D" type="CollisionShape3D" parent="."]
transform = {T(0, 1.3, 0)}
shape = SubResource("BoxShape3D_chassis")

[node name="Model" parent="." instance=ExtResource("3_model")]
transform = {T(0, 0, 0, 180, V)}
''' + wheel("WheelFL", -1.25, front_z, True) + wheel("WheelFR", 1.25, front_z, True)
  + wheel("WheelRL", -1.25, rear_z, False) + wheel("WheelRR", 1.25, rear_z, False) + f'''
[node name="ExitPoint" type="Marker3D" parent="."]
transform = {T(-2.7, 0.1, -1.5)}

[node name="CabCamera" type="Camera3D" parent="."]
transform = {T(-0.55, 2.35, -2.2)}
fov = 80.0
near = 0.05

[node name="ChaseRig" type="Node3D" parent="."]
top_level = true
script = ExtResource("2_camera")
Distance = 12.0
Height = 4.5

[node name="SpringArm3D" type="SpringArm3D" parent="ChaseRig"]
collision_mask = 2
spring_length = 12.0
margin = 0.3

[node name="ChaseCamera" type="Camera3D" parent="ChaseRig/SpringArm3D"]
fov = 70.0
''')

# ---------------- world: swap grey blocks for city-kit houses, add trees and parked cars ----------------
w = open(f"{P}/scenes/world/world.tscn").read()
blocks = [(-40,-45,'a',0),(-20,-48,'b',0),(30,-46,'c',0),(75,-40,'d',0),(-70,-10,'e',90),(-70,40,'f',90),(80,45,'g',-90),(30,60,'h',180),(-30,60,'i',180),(85,5,'j',-90),
          (-20,-70,'k',0),(10,-72,'l',0),(50,-70,'m',0),(-85,-45,'n',90),(-85,65,'o',90),(60,80,'p',180),(-5,80,'q',180),(90,-70,'r',0),(-60,82,'s',180),(95,70,'t',-90)]
ext_add, sub_add, nodes = [], [], '\n[node name="Blocks" type="Node3D" parent="."]\n'
kinds = sorted(set(b[2] for b in blocks))
for k in kinds:
    ext_add.append(f'[ext_resource type="PackedScene" path="res://assets/models/city/building-type-{k}.glb" id="city_{k}"]')
    size, _ = model_info(f"city/building-type-{k}.glb", C)
    sub_add.append(f'[sub_resource type="BoxShape3D" id="BoxShape3D_city_{k}"]\nsize = Vector3({size[0]:.2f}, {size[1]:.2f}, {size[2]:.2f})')
for i, (x, z, k, rot) in enumerate(blocks):
    size, _ = model_info(f"city/building-type-{k}.glb", C)
    nodes += f'''
[node name="Block{i}" type="StaticBody3D" parent="Blocks"]
transform = {T(x, 0, z, rot)}
collision_layer = 2
collision_mask = 0

[node name="Model" parent="Blocks/Block{i}" instance=ExtResource("city_{k}")]
transform = {T(0, 0, 0, 0, C)}

[node name="CollisionShape3D" type="CollisionShape3D" parent="Blocks/Block{i}"]
transform = {T(0, size[1]/2, 0)}
shape = SubResource("BoxShape3D_city_{k}")
'''
# trees
ext_add += ['[ext_resource type="PackedScene" path="res://assets/models/city/tree-large.glb" id="city_tree_large"]',
            '[ext_resource type="PackedScene" path="res://assets/models/city/tree-small.glb" id="city_tree_small"]']
sub_add.append('[sub_resource type="BoxShape3D" id="BoxShape3D_trunk"]\nsize = Vector3(0.6, 4, 0.6)')
trees = [(-52,-38,'large'),(-38,-52,'small'),(48,-38,'large'),(62,-52,'small'),(-52,50,'small'),(-38,38,'large'),(67,44,'large'),(53,58,'small'),
         (-15,-20,'large'),(15,-20,'small'),(-15,35,'small'),(15,35,'large'),(-60,10,'large'),(70,-10,'small'),(20,-58,'small'),(-25,10,'small')]
nodes += '\n[node name="Trees" type="Node3D" parent="."]\n'
for i, (x, z, k) in enumerate(trees):
    nodes += f'''
[node name="Tree{i}" type="StaticBody3D" parent="Trees"]
transform = {T(x, 0, z, (i*37) % 360)}
collision_layer = 2
collision_mask = 0

[node name="Model" parent="Trees/Tree{i}" instance=ExtResource("city_tree_{k}")]
transform = {T(0, 0, 0, 0, C)}

[node name="CollisionShape3D" type="CollisionShape3D" parent="Trees/Tree{i}"]
transform = {T(0, 2, 0)}
shape = SubResource("BoxShape3D_trunk")
'''
# parked cars along the avenue (decoration, world layer). Kenney cars face +Z: rotate 180 to face -Z like the truck.
cars = [("sedan",-30,-24,180),("van",25,-24,0),("taxi",-8,26,90),("suv",60,14,180),("police",12,-36,0)]
for name, *_ in cars:
    ext_add.append(f'[ext_resource type="PackedScene" path="res://assets/models/cars/{name}.glb" id="car_{name}"]')
ext_add.append('[ext_resource type="PackedScene" path="res://assets/models/cars/wheel-default.glb" id="car_wheel"]')
wd, _ = model_info("cars/wheel-default.glb", V)
nodes += '\n[node name="ParkedCars" type="Node3D" parent="."]\n'
for i, (name, x, z, rot) in enumerate(cars):
    size, _ = model_info(f"cars/{name}.glb", V)
    sub_add.append(f'[sub_resource type="BoxShape3D" id="BoxShape3D_car_{name}"]\nsize = Vector3({size[0]:.2f}, {size[1]*0.8:.2f}, {size[2]:.2f})')
    nodes += f'''
[node name="Car{i}" type="StaticBody3D" parent="ParkedCars"]
transform = {T(x, wd[1]/2, z, rot)}
collision_layer = 2
collision_mask = 0

[node name="Model" parent="ParkedCars/Car{i}" instance=ExtResource("car_{name}")]
transform = {T(0, 0, 0, 180, V)}

[node name="CollisionShape3D" type="CollisionShape3D" parent="ParkedCars/Car{i}"]
transform = {T(0, size[1]*0.4, 0)}
shape = SubResource("BoxShape3D_car_{name}")
'''
    for wx, wz in ((-0.62*V, 0.7*V), (0.62*V, 0.7*V), (-0.62*V, -0.7*V), (0.62*V, -0.7*V)):
        nodes += f'\n[node name="Wheel{"".join(str(int(v>0)) for v in (wx,wz))}" parent="ParkedCars/Car{i}" instance=ExtResource("car_wheel")]\ntransform = {T(wx, 0, wz, 0, V)}\n'

# splice: drop old Blocks section (up to the Depot node), insert new nodes there
start = w.index('[node name="Blocks" type="Node3D" parent="."]')
end = w.index('[node name="Depot" type="Node3D" parent="."]')
w = w[:start] + nodes.lstrip("\n") + "\n" + w[end:]
# remove old block sub_resources and anything this script generated on a previous run (keeps it idempotent)
import re
w = re.sub(r'\n\[sub_resource type="(BoxMesh|BoxShape3D)" id="(BoxMesh|BoxShape3D)_block_\d+"\]\n(?:[^\n\[]+\n)+', '\n', w)
w = re.sub(r'\[ext_resource type="PackedScene" path="res://assets/models/(city|cars)/[^"]+" id="(city_|car_)[^"]+"\]\n', '', w)
w = re.sub(r'\[sub_resource type="BoxShape3D" id="BoxShape3D_(city_\w+|trunk|car_\w+)"\]\nsize = [^\n]+\n\n?', '', w)
# insert new ext/sub resources
first_sub = w.index('[sub_resource')
w = w[:first_sub] + "\n".join(ext_add) + "\n\n" + "\n\n".join(sub_add) + "\n\n" + w[first_sub:]
w = re.sub(r'\n{3,}', '\n\n', w)
n_sub = w.count('[sub_resource'); n_ext = w.count('[ext_resource')
w = re.sub(r'\[gd_scene load_steps=\d+ format=3\]', f'[gd_scene load_steps={n_sub+n_ext+1} format=3]', w)
# give each site a couple of trees for dressing
site_trees = ''
for name, x, z in (("LarkspurLane",-45,-45),("TallowCourt",55,-45),("AshgroveRoad",-45,44),("MillerTerrace",60,50)):
    for j, (dx, dz) in enumerate(((-7.5, 6), (7.5, -6))):
        site_trees += f'''
[node name="SiteTree_{name}_{j}" type="StaticBody3D" parent="Trees"]
transform = {T(x+dx, 0, z+dz, (j*90+len(name)*13) % 360)}
collision_layer = 2
collision_mask = 0

[node name="Model" parent="Trees/SiteTree_{name}_{j}" instance=ExtResource("city_tree_{'large' if j==0 else 'small'}")]
transform = {T(0, 0, 0, 0, C)}

[node name="CollisionShape3D" type="CollisionShape3D" parent="Trees/SiteTree_{name}_{j}"]
transform = {T(0, 2, 0)}
shape = SubResource("BoxShape3D_trunk")
'''
w = w.replace('\n[node name="ParkedCars" type="Node3D" parent="."]', site_trees + '\n[node name="ParkedCars" type="Node3D" parent="."]')
open(f"{P}/scenes/world/world.tscn", "w").write(w)
print("generated. truck size", [round(v,2) for v in tsize], "wheel r", round(wheel_r,2), "book", [round(v,2) for v in book_size])
