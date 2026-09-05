# Pleasure to Burn

A single-player 3D job simulator built with **Godot 4.8 .NET** and **C#**. You are a fireman in a dry, satirical
dystopia where books are banned, and the fire department's job is to burn them.

The prototype now runs the whole core loop with placeholder cubes: take a report at the depot, drive the truck to
the address, walk into the house, burn the contraband with the flamethrower (fire spreads on its own), drive back,
get paid. See `PROJECT_NOTES.md` for structure, conventions and what comes next.

## Requirements

- Godot 4.8 .NET build (created with 4.8 dev 3)
- .NET SDK 8.0 or newer

## Running

1. Open Godot, **Import**, pick `project.godot`.
2. Press **Build**, then **Play**. The startup scene is the world; `scenes/locations/test_room.tscn` is a
   single room for tuning fire.

Controls: mouse to look, **WASD** to move, **hold left mouse** to flame, **E** to use things (dispatch console,
fuel cans, the truck), **Esc** to pause. In the truck: **W/S** drive, **A/D** steer, **Space** brake, **E** get out.
The reticle ring fills as the object you aim at heats toward ignition.

## Tests

```sh
./tests/run_tests.sh
```

Builds the assembly and runs both headless suites: `fire_tests.tscn` (spread, upward bias, charring, cooling,
flamethrower, fuel cans, bookshelf segmentation, test room) and `world_tests.tscn` (sites, the full contract
loop including payment and respawn, truck enter/exit).
