# Project Notes — Pleasure to Burn

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

## Folder layout

```
autoload/          Autoload singletons (registered in project.godot). Access via `X.Instance`.
systems/fire/      Fire simulation: FireSystem (autoload), Flammable (component), BurnProfile (resource),
                   FireVfx (presentation only; one per location)
resources/         Data-only tuning as .tres files + their C# Resource classes
  burn_profiles/     paper / wood / fabric
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
  ui/                HUD, pause menu (one folder per scene)
tests/             Headless test scenes. `tests/run_tests.sh` builds + runs them (exit code = result).
```

## Naming

- C# files and classes: `PascalCase.cs`. Scenes, resources, folders: `snake_case`.
- A scene's script lives next to it, same name (`player.tscn` / `Player.cs`).
- Namespace `PleasureToBurn`; tests in `PleasureToBurn.Tests`.
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
- Big props are **segmented**: the bookshelf is a Node3D with four wooden bodies (back + 3 planks), each with its own
  Flammable, so fire crawls across it. Do the same for any prop longer than about a metre.
- Call `FireSystem.Instance.Reset()` before loading a new location.

## Aim, interaction, fuel

- The flamethrower casts its centre ray every physics tick, firing or not. `AimCollider`, `AimFlammable`, `AimDistance`
  are the single source of truth for "what am I looking at". It publishes `EventBus.AimChanged` for the reticle.
- Interactables implement `IInteractable` on the body itself (not a child), sit on layer 2 or 3, and respond within
  `Player.InteractRange` (2.5 m). The player calls `Interact` on the interact action.
- Fuel is the economy. No refill key. `FuelCan` props refill once each (`Charges`). Tank 100, drain 6/s ≈ 17 s of flame;
  the test room has two cans, so the player has ~50 s of flame for 56 books and must let fire spread.
- Flame cone is a fixed rotating ring pattern with small `Jitter`, so a steady aim gives a steady result.

## Contract loop (ContractManager)

- States: `Idle → Accepted → Cleared → Idle`. Dispatch (an IInteractable console at the depot) is the only input:
  it takes a report when Idle and pays out when Cleared.
- Taking a report picks a random `Site`, calls `Site.Respawn()` for a fresh building, and subscribes to its
  `Location.ProgressChanged`. Precision jobs (`PrecisionChance`) deduct `PrecisionPenaltyPerObject` per charred
  non-contraband item; standard jobs do not care.
- The Beacon (a tall translucent cylinder) marks the target site, then the depot. Objective and radio text go
  through EventBus (`ObjectiveChanged`, `RadioMessage`, `MoneyChanged`).
- Sites are found by the `"sites"` group, not an exported array (see gotchas).
- Report lines live on the manager's `ReportLines` export. Keep them dry and original.

## Truck

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

1. **Playtest the loop.** Drive, enter, burn, return. Tune truck handling, house distance, fuel, pay.
2. Extinguish phase: a hose/extinguisher that removes heat (negative AddHeat path) and puts fires out. Same sim, run backwards.
3. Neighbour refresh on a slow timer so thrown/moved objects can catch fire.
4. More building kits: a second house layout, a shop, an apartment. `Site.Building` already takes any Location scene.
5. Upgrades shop at the depot: spend money to swap `FlamethrowerStats` / truck stats. Then save/load.
6. Story drip: a line of text on `BookData`, read before you burn, optional 'save it' choice.
