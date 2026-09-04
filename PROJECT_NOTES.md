# Project Notes — Pleasure to Burn

Living document for structure, naming and conventions. Update it when a convention changes.
Engine: **Godot 4.8 .NET**, C# 12, `net8.0`. Godot.NET.Sdk `4.8.0-dev.3` (pinned in the csproj; bump together with the editor).

## Current slice (prototype 1)

One placeholder room, the fire system, and the flamethrower. Goal: is burning a bookshelf fun with cubes?
No contracts, truck, hub, save/load, or story yet. Do not add scope until this is fun.

Slice 2 added (design-review follow-ups): heat reticle, ignition feedback (burst + light + procedural whoomp,
per-object flames), segmented bookshelves with upward spread bias, scarce fuel (no refill key; fuel cans in the room).

## Folder layout

```
autoload/          Autoload singletons (registered in project.godot). Access via `X.Instance`.
systems/fire/      Fire simulation: FireSystem (autoload), Flammable (component), BurnProfile (resource),
                   FireVfx (presentation only; one per location)
resources/         Data-only tuning as .tres files + their C# Resource classes
  burn_profiles/     paper / wood / fabric
  flamethrower/      one .tres per flamethrower tier
scenes/
  player/            Player (controller) + Flamethrower (child of camera)
  props/             Placeholder props. One .tscn per prop, instanced into locations. FuelCan is the first IInteractable.
  interaction/       IInteractable contract
  vfx/               Ignition burst, burning flames, ProceduralAudio (placeholder sounds generated in code)
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

## Next slices (in brief priority order)

1. **Playtest.** Is burning a shelf fun? Tune profiles, bias, fuel before adding anything.
2. Extinguish phase: a hose/extinguisher that removes heat (negative AddHeat path) and puts fires out. Same sim, run backwards.
3. Collateral damage counter + two contract types (precision / release) from one penalty number.
4. Neighbour refresh on a slow timer so thrown/moved objects can catch fire.
5. Location/contract data: `ContractDefinition` resource (location scene, contraband count, pay), modular room kit.
6. Truck driving (basic), hub scene, money and upgrades (swap `FlamethrowerStats`), save/load.
