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
    # The resident. Not flammable, does not move, does not stop you.
    ext += [
        '[ext_resource type="PackedScene" path="res://scenes/npc/npc.tscn" id="p_npc"]',
        '[ext_resource type="Script" path="res://resources/dialogue/DialogueSet.cs" id="dlg_script"]',
    ]
    occupants = ["occupant_teacher", "occupant_denial", "occupant_quiet", "occupant_watcher"]
    ext += [f'[ext_resource type="Resource" path="res://resources/dialogue/{d}.tres" id="d_{d}"]' for d in occupants]
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

    # One resident, facing the door, holding one of several conversations picked when the house spawns.
    conversations = ", ".join(f'ExtResource("d_{d}")' for d in occupants)
    n += f'''
[node name="People" type="Node3D" parent="."]

[node name="Occupant" parent="People" instance=ExtResource("p_npc")]
transform = {T(-2.6, FLOOR_TOP, 1.2, 0)}
DisplayName = "Resident"
Conversations = Array[ExtResource("dlg_script")]([{conversations}])
'''
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
# VehicleBody3D drives toward +Z on positive engine_force, and Kenney vehicles also face +Z, so the model
# is left unrotated. The truck's nose is +Z (unlike the player, whose forward is -Z).
front_z, rear_z = 0.95*V, -0.65*V
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
transform = {T(0, 0, 0, 0, V)}
''' + wheel("WheelFL", -1.25, front_z, True) + wheel("WheelFR", 1.25, front_z, True)
  + wheel("WheelRL", -1.25, rear_z, False) + wheel("WheelRR", 1.25, rear_z, False) + f'''
[node name="ExitPoint" type="Marker3D" parent="."]
transform = {T(-2.7, 0.1, 1.5)}

[node name="CabCamera" type="Camera3D" parent="."]
transform = {T(-0.55, 2.35, 2.2, 180)}
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

# ---------------- world ----------------
# The neighbourhood (roads, pavements, lots, dressing, signs) lives in gen_city.py, which splices itself
# into scenes/world/world.tscn. Keep it as the single owner of the world layout.
import gen_city
gen_city.build()

print("generated. truck size", [round(v,2) for v in tsize], "wheel r", round(wheel_r,2), "book", [round(v,2) for v in book_size])
