# Project Notes — Alexandria

Living document for structure, naming and conventions. Update it when a convention changes.
Engine: **Godot 4.8 .NET**, C# 12, `net8.0`. Godot.NET.Sdk `4.8.0-dev.3` (pinned in the csproj; bump together with the editor).

## Current slice (prototype 1)

One placeholder room, the fire system, and the flamethrower. Goal: is burning a bookshelf fun with cubes?
No contracts, truck, hub, save/load, or story yet. Do not add scope until this is fun.

Slice 2 added (design-review follow-ups): heat reticle, ignition feedback (burst + light + procedural whoomp,
per-object flames), segmented bookshelves with upward spread bias, scarce fuel (no refill key; fuel cans in the room).

Slice 3 added the core loop end to end: an outdoor world with a depot, a drivable truck, four enterable houses on
sites, and a dispatch contract loop (take report → drive → burn → return → get paid). Startup scene is now
`scenes/world/world.tscn`; `scenes/locations/test_room.tscn` stays as the fast fire-tuning scene.

Slice 5 renamed the project to **Alexandria** and added people. The premise settled: the fire is not fire. It
feeds on what is written down and grows cleverer with every book burned, and the department's line that burning
enough of them will kill it is a lie the player is meant to work out. The bureaucracy stays exactly as it is,
because the invoices and the star ratings are the machine that keeps the player from noticing.

Slice 4 built the neighbourhood, after a playtest found the game boring and pointless: no world, no framing, no
onboarding. This slice is the world half of that. The premise settled here too — a mundane municipal day job you
cannot get out of — so the streets are a repeating grid and the same few house types recur down every road. What
is still missing, and matters more: a scripted opening, occupants, and a reason to care. See "Next slices".

## People and dialogue

- `Npc` (`scenes/npc/npc.tscn`) is a `StaticBody3D` on layer 2 implementing `IInteractable`, so the existing aim
  ray and interact action find it with no new plumbing. It does not move, does not follow and has no face.
- **Occupants are deliberately not `Flammable`.** They stand in the room you came to burn and talk to you, which
  is the whole effect; whether that ever changes is a design decision, not an oversight.
- Dialogue is data. A `DialogueSet` resource is one conversation (speaker plus lines) in
  `res://resources/dialogue/`. An `Npc` exports an array of them and picks one at random in `_Ready`. Because a
  `Site` respawns its building for every contract, the same address gets a different resident each job.
- `DialoguePanel` is a `ModalPanel` found by group, one per world scene. Interact or the button advances a line;
  the last line closes it. `Advance()` and `LineIndex` are public so tests can walk a conversation.
- The house occupant is generated into `house.tscn` by `gen_scenes.py`; the watch officer is hand-placed in the
  depot in `world.tscn`, which `gen_city.py` preserves.
- The officer's first-day lines are currently the only briefing in the game. They carry the premise, the controls
  and the lie all at once. That is a stopgap for a real scripted opening, not a replacement for one.

## The neighbourhood

`tools/gen_city.py` owns the whole world layout and splices itself into `scenes/world/world.tscn`. It is
idempotent: it replaces the nodes between `Roads` and `Depot`, and between `Sites` and `Beacon`, on every run, and
leaves everything else (environment, sun, ground, depot, truck, player, HUD) alone. `gen_scenes.py` calls it last,
so one command regenerates everything.

- **The plan is data.** `ROADS` is a list of (name, axis, coordinate, from, to, width, sign text); `SITES` places
  the four job addresses; `PALETTE` says which house types repeat down each street. Change those lists, re-run,
  re-import. Nothing else needs touching.
- **Roads are a 4x4 grid** with the depot inside the centre block, plus a short Depot Road from its doorway south
  to Main Avenue. Blocks must be at least `2 * SETBACK + house depth` apart or the two rows of houses collide;
  at 46 m spacing a block side holds one or two lots.
- **Lots are filled per block segment**, not by striding the whole road: free runs along each frontage are what is
  left after subtracting the other roads, the depot and the four addresses, and each run is filled with as many
  `LOT`-wide lots as fit, centred. Striding the whole road instead loses most candidates to intersections.
- **Every lot gets the same yard**: driveway, front path, a low fence with a gap at the path, a tree. The four job
  addresses get it too, so they do not stand out; the beacon and the house number are what find them.
- **Kenney city buildings front +Z**, same as the house and the vehicles. Yaw maps local +Z to `(sin, cos)`.
- Street names are `Label3D` at every junction, and each job address has its number on the house. The addresses on
  the docket are real places you can navigate to.
- Pavements are split at intersections so no two quads are coplanar; carriageways along X and along Z sit at
  different heights for the same reason.
- `tools/city_map.svg` is a top-down plan written on every run, for checking a layout without opening Godot.

## Seeing the game without the editor

`devtools/shot.sh <scene_or_glb> <out.png> [yaw] [elevation] [distance] [target_y] [target_x] [target_z]` renders
one frame to a PNG. It needs a desktop session because Godot cannot render in `--headless`. Use it to check layout,
model orientation and lighting. It is how the buildings were confirmed to face +Z and how the city was iterated.

## Art assets (Kenney, CC0)

`assets/models/{furniture,city,cars}/` hold the `.glb` files actually used from Kenney's Furniture Kit, City Kit
Suburban and Car Kit (licences alongside). Commit the `.import` sidecars. Godot imports `.glb` as a PackedScene,
so a prop instances it under a `Model` node.

- **Scales differ per kit.** Furniture ×2.0, cars ×2.0, city ×7.5. The furniture kit's pivot is the back-left floor
  corner; prop scenes centre the model with an offset so the prop origin is centre-bottom.
- **Vehicles face +Z.** `VehicleBody3D` applies positive engine_force toward its +Z (the opposite of Godot's usual
  -Z forward), and Kenney vehicles are modelled facing +Z, so vehicle models are NOT rotated, steering wheels go at
  +Z, and the chase camera / exit logic treat +Z as the nose. Getting this wrong makes W drive backwards.
- The car and city kits reference `Textures/colormap.png` next to the `.glb`; keep that folder with them.
- All prop/house/world/truck `.tscn` files are generated by `gen_scenes.py` (kept in the session scratchpad for now;
  worth moving into `tools/` if regenerating becomes routine). They are plain scenes and can be hand-edited.
- Bookshelf shelf heights were read from the model's vertex data: wide bookcase plank tops at model y 0.07/0.31/0.55,
  open bookcase at 0.13/0.37/0.61. Five book stacks fit the wide one, two the open one.
- The house is built from furniture-kit `wall`, `wallWindow`, `wallDoorway` and `floorFull` tiles (2 m each);
  collision is still four box colliders plus a lintel over the 1 m doorway.

## Folder layout

```
autoload/          Autoload singletons (registered in project.godot). Access via `X.Instance`.
systems/fire/      Fire simulation: FireSystem (autoload), Flammable (component), BurnProfile (resource),
                   FireVfx (presentation only; one per location)
resources/         Data-only tuning as .tres files + their C# Resource classes
  burn_profiles/     paper / wood / fabric
  dialogue/          one .tres per conversation
  flamethrower/      one .tres per flamethrower tier
scenes/
  world/             world.tscn (startup), Site (a lot that spawns a building), ContractManager, Dispatch
  vehicles/          Truck (VehicleBody3D) + ChaseCamera
  player/            Player (controller) + Flamethrower (child of camera)
  props/             Placeholder props. One .tscn per prop, instanced into locations. FuelCan is the first IInteractable.
  interaction/       IInteractable contract
  vfx/               Ignition burst, burning flames, ProceduralAudio (placeholder sounds generated in code)
  locations/         Buildings. Root node has Location.cs. house.tscn is the enterable version of test_room
                     (doorway on +Z, roof, interior light); props are copied from test_room, keep them in sync.
  ui/                HUD, pause menu, dialogue (one folder per scene)
  npc/               Npc + npc.tscn, a blocky primitive figure (the kits ship no character models)
tests/             Headless test scenes. `tests/run_tests.sh` builds + runs them (exit code = result).
```

## Naming

- C# files and classes: `PascalCase.cs`. Scenes, resources, folders: `snake_case`.
- A scene's script lives next to it, same name (`player.tscn` / `Player.cs`).
- Namespace `Alexandria`; tests in `Alexandria.Tests`.
- Node names in a scene are PascalCase and are the API other code uses (`GetNode("Head/Camera3D/Flamethrower")`).
- Signals: past tense / event style, `XxxEventHandler` delegates (`FuelChanged`, `ObjectCharred`).

## Physics layers (project settings, 3D)

| # | name       | who                                                   |
|---|------------|-------------------------------------------------------|
| 1 | player     | Player body                                           |
| 2 | world      | Floors, walls, non-burnable geometry                  |
| 3 | flammable  | Any prop with a `Flammable` child; flamethrower rays only care about 2 and 3 |

Props are `StaticBody3D` on layer 3 with mask 0 (they block the flame but never collide with anything themselves).
The player collides with layer 2 only, so you can walk through books for now. Add layer 3 to the player's mask when
props have proper collision sizes.

## Fire system contract

- `Flammable` is a **direct child named `Flammable`** of the prop's body. The flamethrower finds it by that name.
- Its `Profile` (a `BurnProfile`) says how it burns. Tick `IsContraband` for anything the contract wants burned.
- `FireSystem` ticks at 10 Hz in `_PhysicsProcess`. Only burning objects do work. Neighbours are cached at
  ignition from a spatial hash (`CellSize` >= biggest `SpreadRadius`).
- Heat model: sources give `HeatOutput × Intensity × falloff` per second; falloff is linear to zero at `SpreadRadius`.
  Unburnt objects ignite at `IgnitionTemperature`. Heat only bleeds away (`CoolingRate`) once nothing has heated the
  object since the last tick.
- States: `Unburnt → Burning → Charred`. Charred objects leave the simulation.
- Signals to hang effects on: `Flammable.Ignited/Charred` (per object), `FireSystem.ObjectIgnited/ObjectCharred/BurningCountChanged`.
- Fire rises: neighbour weight is multiplied by `FireSystem.VerticalFactor` (`UpwardBias` above, `DownwardPenalty` below).
- The bookshelf is now one wooden body (the imported model) holding 15 separate book bodies; the crawl comes from
  the books. Segmenting large single meshes is still the plan when a prop over ~2 m needs to burn progressively.
- Prop origins sit at floor level (centre-bottom), so spread distance between a book on the floor and the furniture
  it sits under is small: a burning hidden book will take the table with it. Intended, but remember it when tuning.
- Fire tint duplicates every surface material of every mesh under the body (`Flammable.SetupMaterials`), so
  multi-colour models keep their palette while charring. Meshes under a nested Flammable body are skipped.
- Call `FireSystem.Instance.Reset()` before loading a new location.

## Moving

- **Shift runs**, at `Player.RunMultiplier` times `MaxSpeed`. Running and firing are mutually exclusive: the
  trigger has to come off before you move properly. A free sprint is strictly better than walking, so without a
  cost the button may as well have been a larger `MaxSpeed`.
- The decision lives in `Player.WantsToRun(bool)` and `Player.SpeedFor(bool)`, split out of `_PhysicsProcess` so
  tests can check them without synthesising input. `Flamethrower.SetFiring` is public for the same reason.
- No stamina yet. When the fire starts hunting the player, a stamina bar is the obvious next knob, and running
  out of it in a burning room is the beat that slice wants.

## Aim, interaction, fuel

- The flamethrower casts its centre ray every physics tick, firing or not. `AimCollider`, `AimFlammable`, `AimDistance`
  are the single source of truth for "what am I looking at". It publishes `EventBus.AimChanged` for the reticle.
- Interactables implement `IInteractable` on the body itself (not a child), sit on layer 2 or 3, and respond within
  `Player.InteractRange` (2.5 m). The player calls `Interact` on the interact action.
- Fuel is the economy. No refill key. `FuelCan` props refill once each (`Charges`). Tank 100, drain 6/s ≈ 17 s of flame;
  the test room has two cans, so the player has ~50 s of flame for 56 books and must let fire spread.
- Flame cone is a fixed rotating ring pattern with small `Jitter`, so a steady aim gives a steady result.

## The day loop (ContractManager + Career)

Reference points: PowerWash Simulator (job list + itemised invoice), Euro Truck Simulator 2 (money → upgrades),
Papers Please (a shift is a clock, the day ends, tomorrow is harder).

- **Career** (autoload, persistent, saved to `user://career.cfg`): money, day, reputation (-5..20), upgrade levels.
  `EffectiveStats(base)` returns an upgraded *copy* of a FlamethrowerStats; `TruckPowerMultiplier` for the truck.
  Upgrade lines are `UpgradeDefinition` resources in `resources/upgrades/`; their effects are a switch in Career.
- **Shift clock**: `ShiftLengthSeconds` (480) counts down in ContractManager. When it hits zero no new jobs can be
  taken; the active one can be finished. Dispatch then offers "Sign off".
- **Job board** (`JobBoard` panel): `GenerateOffers()` gives `OffersPerVisit` distinct sites with type, item count,
  estimated pay, distance and a return-by `BonusSeconds` derived from distance. `Accept(i)` respawns the building.
- **Invoice** (`InvoicePanel`): `PreviewInvoice()` / `Settle()` itemise base pay (precision rate is +30%), collateral
  penalty (precision only), deadline bonus (+25%), reputation multiplier (+5%/rep), total, 1–3 stars, rep delta.
- **Day end** (`DayEndPanel`): summary + upgrade shop; "Start next day" calls `EndDay()` and reloads the world so
  upgrades apply (they are read on `_Ready` by Flamethrower and Truck).
- States: `Idle → Accepted → Cleared → Idle`. Dispatch is the only input; its prompt reflects state and shift.
- Panels derive from `ModalPanel` (pause + show mouse; Esc closes; `AnyOpen` stops the pause menu stacking). They
  are found by group, so a world scene just needs one instance of each.
- The Beacon marks the target site (20 m up), then the depot. Objective/radio/money/clock go through EventBus.
- Sites are found by the `"sites"` group, not an exported array (see gotchas).
- Report lines live on the manager's `ReportLines` export. Keep them dry and original.

TUNE BY EYE: shift length vs. job duration (aim for 3–5 jobs a day), pay vs. upgrade costs (first upgrade after
day 1, all three lines maxed around day 6–8), deadline allowance (`AssumedSpeedMetresPerSecond`).

## Truck

- **The depot's only opening is +Z, and positive engine force drives toward the truck's own +Z.** A truck parked
  rotated 180 inside it drives into the back wall on the player's first input; that was the case until slice 4.
  `WorldTests` now holds the throttle from the spawn point and asserts the truck clears the doorway.
- Input is read in `_PhysicsProcess` and handed to `Drive(throttle, steer, braking, dt)`, which is public so tests
  can drive without synthesising input. Disable the node's `_PhysicsProcess` first or it will zero the throttle.
- `VehicleBody3D` with four `VehicleWheel3D`; all wheels drive, fronts steer. Interact to enter, interact again
  to exit at `ExitPoint`. While driving the Player is `ProcessMode.Disabled`, hidden, collision off, and the
  `ChaseCamera` (TopLevel, follows yaw only) is current.
- Truck is on layer 2 so the player's aim ray sees it; mask 2 so it never touches the player.
- The depot has a `FuelPump` (a FuelCan with 99 charges). Houses still carry two single-use cans.

## Communication rules

- **Call down, signal up.** Parents call child methods; children never reach for parents or siblings by path.
- Gameplay → UI goes through `EventBus` signals. UI never calls gameplay.
- Data lives in `.tres` resources. New book type / material / flamethrower tier = new resource, not new code.

## Things tuned by eye (not by code)

- All `BurnProfile` numbers. Ratios matter: paper ignites easily and burns out fast, wood is slow both ways, fabric
  is the bridge that carries fire across a floor.
- `FlamethrowerStats.HeatPerSecond` relative to ignition temperatures sets how long you hold the flame on something.
- Particle looks (`FlameJet`, `ignition_burst`, `burning_flames`) are placeholders; code only toggles/scales them.
- `UpwardBias` / `DownwardPenalty` on FireSystem: watch a shelf, it should catch bottom to top.
- `ProceduralAudio.Whoomp` is a stand-in; swap for a real sample in `IgnitionBurst` when you have one.
- Intensity ramp (1.5 s) and the fade curve in `Flammable.TickBurn` — feel, not physics.

## Godot 4.x gotchas hit so far

- `System.Threading.Timer` clashes with `Godot.Timer` under implicit usings; write `Godot.Timer`.
- Ray queries use `PhysicsRayQueryParameters3D.Create(from, to, mask)` and `DirectSpaceState.IntersectRay`; only valid
  from `_PhysicsProcess` (or a test that runs between frames).
- Typed arrays of custom resources serialize as `Array[ExtResource("script")]([...])` in `.tres`; hand-editing works.
- C# exported property names are PascalCase in `.tscn`/`.tres` (`IsContraband = true`).
- Each `Flammable` currently duplicates its material for tinting. Fine for tens of objects; hundreds want a shader
  with per-instance data.
- **Node exports in hand-written .tscn need `node_paths=PackedStringArray("Prop")` on the node header** or they load
  as null. A C# `Node[]` export did not resolve even with it (4.8 dev 3); use a group lookup instead.
- Freed nodes and cached references: `QueueFree` runs at end of frame, so a respawned building overlaps its
  predecessor for one frame and the fire spread cache can hold soon-to-be-freed objects. `Flammable.IsRegistered`
  is a plain C# flag checked in the hot loop; never call into a possibly freed Godot object there.
- An exception inside an `async Task` test is swallowed and the process hangs. Tests wrap their body in
  `RunGuardedAsync` and quit with code 2 on exception.

## Next slices (in brief priority order)

0. **The demon.** One intelligence value on the career save that rises with every book burned, and a malice term
   in `FireSystem`'s neighbour weight so the fire stops obeying physics by degrees: first leaning toward unburnt
   contraband, then toward the player and the door they came in by. Needs player heat and a real failure state,
   or the threat is theatre, and a world book counter on the HUD so the arc is visible.
0a. **Make a house worth entering.** One room, one prop list, every job. Randomise which props spawn, where books
   hide, and how many, so clearing a house is a search and not a sweep. Then a second and third floor plan. The
   enterable house is also still a flat-roofed box among pitched-roof neighbours; it should look like the street.
0c. **The first ten minutes.** No cold open, no briefing, no tutorial: the player spawns in a depot next to a job
   board and is told nothing. A scripted first morning would fix framing, onboarding and stakes at once.
0d. **The economy has no friction.** One cleared house pays about $1820 and the dearest upgrade costs $600, so
   every upgrade is affordable after the first job and money stops meaning anything on day one. Either the rate
   per item or the upgrade costs are wrong by an order of magnitude. Fix before tuning anything else.
1. **Playtest the day loop.** Does a day feel like a day? Tune shift length, pay, upgrade costs, deadlines.
1b. Shelf-as-expected-loss rule so precision jobs are fair (shelf bodies should not count as collateral).
2. Extinguish phase: a hose/extinguisher that removes heat (negative AddHeat path) and puts fires out. Same sim, run backwards.
3. Neighbour refresh on a slow timer so thrown/moved objects can catch fire.
4. More building kits: a second house layout, a shop, an apartment. `Site.Building` already takes any Location scene.
5. Upgrades shop at the depot: spend money to swap `FlamethrowerStats` / truck stats. Then save/load.
6. Story drip: a line of text on `BookData`, read before you burn, optional 'save it' choice.
