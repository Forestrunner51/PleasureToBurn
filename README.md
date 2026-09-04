# Pleasure to Burn

A single-player 3D job simulator built with **Godot 4.8 .NET** and **C#**. You are a fireman in a dry, satirical
dystopia where books are banned, and the fire department's job is to burn them.

This repository is at **prototype 1**: one placeholder room, a fire simulation with fuel, heat, ignition thresholds
and neighbour spread, and a first-person flamethrower. See `PROJECT_NOTES.md` for structure, conventions and what
comes next.

## Requirements

- Godot 4.8 .NET build (created with 4.8 dev 3)
- .NET SDK 8.0 or newer

## Running

1. Open Godot, **Import**, pick `project.godot`.
2. Press **Build**, then **Play**. The startup scene is the test room.

Controls: mouse to look, **WASD** to move, **hold left mouse** to flame, **R** to refill, **Esc** to pause.

## Tests

```sh
./tests/run_tests.sh
```

Builds the assembly and runs `tests/fire_tests.tscn` headless: spread along a row, out-of-range isolation,
charring, cooling, flamethrower fuel/heat/shielding/range, and the test room's contraband count.
