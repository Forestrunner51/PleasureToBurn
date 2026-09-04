# Project Notes — Pleasure to Burn

Living document for structure, naming and conventions. Update it when a convention changes.
Engine: **Godot 4.8 .NET**, C# 12, `net8.0`. Godot.NET.Sdk `4.8.0-dev.3` (pinned in the csproj; bump together with the editor).

## Current slice (prototype 1)

One placeholder room, the fire system, and the flamethrower. Goal: is burning a bookshelf fun with cubes?
No contracts, truck, hub, save/load, or story yet. Do not add scope until this is fun.

## Folder layout

```
autoload/          Autoload singletons (registered in project.godot). Access via `X.Instance`.
systems/fire/      Fire simulation: FireSystem (autoload), Flammable (component), BurnProfile (resource)
resources/         Data-only tuning as .tres files + their C# Resource classes
  burn_profiles/     paper / wood / fabric
  flamethrower/      one .tres per flamethrower tier
scenes/
  player/            Player (controller) + Flamethrower (child of camera)
  props/             Placeholder burnable props. One .tscn per prop, instanced into locations.
  locations/         Playable areas. Root node has Location.cs.
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
- Call `FireSystem.Instance.Reset()` before loading a new location.

## Communication rules

- **Call down, signal up.** Parents call child methods; children never reach for parents or siblings by path.
- Gameplay → UI goes through `EventBus` signals. UI never calls gameplay.
- Data lives in `.tres` resources. New book type / material / flamethrower tier = new resource, not new code.

## Things tuned by eye (not by code)

- All `BurnProfile` numbers. Ratios matter: paper ignites easily and burns out fast, wood is slow both ways, fabric
  is the bridge that carries fire across a floor.
- `FlamethrowerStats.HeatPerSecond` relative to ignition temperatures sets how long you hold the flame on something.
- Particle looks (`FlameJet` on the player, later smoke/char) are placeholders; only `Emitting` is driven by code.
- Intensity ramp (1.5 s) and the fade curve in `Flammable.TickBurn` — feel, not physics.

## Godot 4.x gotchas hit so far

- `System.Threading.Timer` clashes with `Godot.Timer` under implicit usings; write `Godot.Timer`.
- Ray queries use `PhysicsRayQueryParameters3D.Create(from, to, mask)` and `DirectSpaceState.IntersectRay`; only valid
  from `_PhysicsProcess` (or a test that runs between frames).
- Typed arrays of custom resources serialize as `Array[ExtResource("script")]([...])` in `.tres`; hand-editing works.
- C# exported property names are PascalCase in `.tscn`/`.tres` (`IsContraband = true`).
- Each `Flammable` currently duplicates its material for tinting. Fine for tens of objects; hundreds want a shader
  with per-instance data.

## Next slices (in brief priority order)

1. Fire visuals: ignition/char signals → particles, smoke, charring material. Then a "is it fun" playtest.
2. Flamethrower feel: nozzle jet that matches the cone, hit sparks, fuel pickup at the truck.
3. Location/contract data: `ContractDefinition` resource (location scene, contraband count, pay), modular room kit.
4. Truck driving (basic), hub scene, money and upgrades (swap `FlamethrowerStats`), save/load.
