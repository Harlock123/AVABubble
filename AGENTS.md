# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A bubble-blowing platform game in the style of Taito's 1986 arcade game Bubble Bobble, in C# on .NET 10 and
Avalonia 12. One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). The hero, the
monsters and the twelve levels are this game's own: the pixel art is drawn in code (`Models/Sprites.cs`), and the
sounds are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVABubble.slnx                 # the SDK version is in global.json (10.0.400 or later)
dotnet test AVABubble.slnx                  # every test runs headless: no window opens
dotnet run --project src/AVABubble          # play it; --no-audio for silence
BUBBLE_SHOTS=$PWD/Images dotnet test tests/AVABubble.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                # rebuild src/AVABubble/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVABubble/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract mode, play, high scores) and the fixed-step loop.
  - `GameEngine.Play.cs` is the rules within a level: the hero, bubbles, monsters, fruit, the EXTEND letters and
    the shade that comes when you take too long.
  - `Entities.cs` has the things that move (`Body`, `Hero`, `Monster`, `Bubble`, `Fruit`, `Shade`) and `Controls`.
  - `AttractPilot.cs` is the computer player, used by the attract mode and the tests.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVABubble/Models/`:
  - `Field.cs` has `Vec`, the screen measures (`Field`), tiles, and the twelve levels written as text (`Levels`).
  - `Sprites.cs` has the pixel art.
- `src/AVABubble/Views/`: `GameCanvas` draws the fixed 1024x768 screen and runs the 60 Hz timer. `GameCanvas.Draw.cs`
  draws play, and `GameCanvas.Screens.cs` the panels, title, high scores and initials entry.
- `src/AVABubble/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVABubble.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: gameplay tests and screenshot tests.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` runs the game in fixed 1/60 s steps (`Field.Step`), independent of the frame
  rate, so a seed always plays the same game. Draw random numbers from the engine's seeded `_rng`
  (`new Random(seed)`) or the pilot's own, never from `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/` and `Models/`, where tests can reach it without
  a window. `Views/` only reads the game and draws it.
- **Input goes through `InputHandler`.** `Controls` is `Dx` (-1 left, +1 right), `Jump` and `Blow`: there is no
  up/down axis in play, and Up is a jump key. In `Vec`, y grows downwards. Menus read the pad through `PadUp` and
  `PadDown` (stick up is `LeftStickY < -0.5`).
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVABubble`); tests load the
  table from a fresh folder under the system temp path, and the screenshot tests use a silent `SoundManager`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
