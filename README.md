# Pleasure to Burn

A first-person furnace-tending simulator built with **Godot 4.8 (.NET)** and **C#**.
Gather books from the shelves, carry them to the furnace, and burn enough to meet each shift's quota
before the clock runs out. A hot furnace pays a score multiplier, so keep it fed.

## Requirements

- [Godot 4.8 .NET build](https://godotengine.org/download) (the project was created with 4.8 dev 3)
- [.NET SDK 8.0 or newer](https://dotnet.microsoft.com/download)

## Running

1. Open Godot, click **Import**, and pick `project.godot`.
2. Press **Build** (top right) or run `dotnet build` in this folder.
3. Press **Play**. The main menu scene is the startup scene.

Controls: mouse to look, **WASD** or arrows to move, **E** or **Space** to interact, **Esc** or **P** to pause.

## Tests

```sh
./tests/run_tests.sh
```

This builds the assembly and runs `tests/smoke_test.tscn` headless. It exercises the core loop
(pickup, carry limit, burning, heat multiplier, restocking, heat decay) and exits non-zero on failure,
so it can run in CI.

## Project layout

```
autoload/          Singletons registered in project.godot
  EventBus.cs        Global signals; scenes talk through here, never to each other
  GameState.cs       Run-wide state: score, shift index, saved high score
  SceneLoader.cs     Scene paths and fade transitions
resources/         Data-driven tuning as Godot Resources
  BookData.cs        One kind of book (title, value, heat, colour)  -> books/*.tres
  ShiftConfig.cs     One shift (quota, duration, restock rate, pool) -> shifts/*.tres
scenes/            One folder per scene; each holds its .tscn and its C# script
  main_menu/         Title screen
  game/              The level: room, shelves, furnace, player, UI; runs one shift
  player/            First-person controller, carrying, raycast interaction
  interactable/      Base class for anything the player can use
  book/              Pickup on a shelf
  shelf/             Slots + restock timer
  furnace/           Burns books, tracks heat, fire and light effects
  ui/                HUD, pause menu, shift results, floating score popup
tests/             Headless smoke test scene and runner script
```

## Design notes

- **Call down, signal up.** Parents configure children directly (the Game scene calls
  `Shelf.Configure`); children report upward through `EventBus` signals. The HUD only listens.
- **Data lives in resources.** Add a book by creating a `BookData` `.tres`; add a level by creating
  a `ShiftConfig` `.tres` and listing it in `GameState.ShiftPaths`. No code changes needed.
- **Physics layers** are named in project settings: 1 player, 2 world, 3 interactable.
  The player's raycast only checks layer 3.
- **Menus own their side effects.** The pause menu toggles pause and mouse capture itself and
  emits what the player chose; the Game scene decides what happens next.

## Adding content

- New book: duplicate a file in `resources/books/`, edit it in the Inspector, add it to a shift's pool.
- New shift: duplicate a file in `resources/shifts/`, then add its path to `GameState.ShiftPaths`.
- New interactable: extend `Interactable`, put the node on physics layer 3, override
  `GetPrompt` and `Interact`.
