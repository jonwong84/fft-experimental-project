# ZOD-032 — Play the bounded two-unit battle

The Windows client can now play the supported M1 battle through mouse or keyboard.
This is a local two-player example: the player controls **both** units, and the first
KO ends the battle. Units, map and art are invented. The existing MAP003/Chocobo
inspection scene remains separate; its native height-1 terrain is unchanged and does
not become supported height-10 combat terrain through this UI work.

## Launch

With the SDK pinned in `global.json`, from the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --battle
```

No original media, prepared assets, BIOS, reference checkout, importer, network or C
toolchain is needed. The existing `--window` snapshot, `--prepared-scene` inspection,
and `--scenario two-unit-battle` headless commands retain their separate behavior.

## Controls

- Click a unit or its left-hand card to select/inspect it. `1` selects A, `2` selects B,
  and Tab cycles selection. The global turn label identifies the active unit.
- Click **Next Turn** or press `N`/Space to request the next ready unit. CT advances
  only on that explicit command. A second request during an active turn is rejected.
- Click **Move** or press `M`, then click a blue legal destination. The move request
  still goes through simulation validation. Occupied, out-of-range, inactive or
  already-consumed requests report their rejection without changing the battle.
- Click **Attack** or press `A`, then click an orange legal target. The current sword
  profile and action availability determine which targets are highlighted.
- Click **Wait** or press `W` to end the selected unit's active turn, preserving the
  existing unused-choice CT rules. Both choices consumed can also finish a turn.
- Arrow keys move the white cursor by map-grid X/Y; Enter selects/confirms the cursor
  tile in the current mode. Left/right change X; up/down change Y. These are isometric
  grid directions, not a screen-space camera navigation scheme.
- Backspace cancels Move/Attack mode. `R`/Restart restores the original setup and seed
  and clears the session journal. Escape or normal window close exits.

The cards show HP, CT and move/action consumption. The status area reports selection,
turn, attack result or a readable rejection. A completed battle keeps its winner/KO
banner; subsequent gameplay commands are rejected. Dead units remain represented by
their existing bodies with a KO label; no death countdown, revival or crystal scheduler
is implied. Action buttons remain command requests, with terminal actions visually
muted; the simulation remains authoritative when they are attempted.

To reproduce the authored first-unit victory manually:

1. Next Turn selects A. Move to the left tile `(0,1)`, then Attack B.
2. Next Turn selects B. Attack A, then Wait.
3. Next Turn selects B again under the existing CT refund rules. Move to the right
   tile `(1,0)`, then Wait.
4. Next Turn selects A. Move to the top tile `(0,0)`, then Attack B to win.

This is the same twelve accepted commands used by the
[headless scenario](../behavior/two-unit-battle-scenario.md), not a separate combat implementation.

## Ownership and deterministic command flow

`BattleInteraction` in Client owns selection, mode, status, a session command journal
and a reference to the current immutable `TwoUnitBattleState`. Every typed Advance,
Move, Attack or Wait attempt passes through `TwoUnitBattleScenario.Apply`. Only an
accepted returned state is published. Rejections retain the same state and add no
events or live RNG draws; the attempted command remains in the journal for replay.
Selection, mode changes and empty-target clicks are presentation choices, not invented
simulation commands. Restart calls the existing definition's `Start()`.

Legal highlights probe the same pure transition against the current immutable input
and discard its returned state/events. Attack probes can calculate a hypothetical
result using local RNG copies; they never publish that result, consume live RNG, add
history, alter status or reveal damage/roll predictions. Tests verify these properties.
This deliberately small setup avoids duplicating attack/movement legality in the UI.
It is not a general scalable battle-preview API or a promise about future targeting UI.

`BattleUiRouter` shares semantic keyboard, button, sprite and tile routes between the
window, tests and finite check. Pointer picking accounts for window/client dimensions,
backbuffer scale and the centered viewport; black bars have no pick. Sprite bounding
rectangles take precedence over floor diamonds, with the frontmost unit first. The
flat synthetic canvas is the only supported picking camera/layer in this slice.

Buffered [key-down/key-up events](https://docs.monogame.net/api/Microsoft.Xna.Framework.GameWindow.html)
feed a queue and held-key latch, so repeat events do not submit repeated Wait/Advance.
The [mouse state](https://docs.monogame.net/api/Microsoft.Xna.Framework.Input.Mouse.html)
uses a fresh left-press edge. Activation baselines already-held input and clears the
queued events. Inactive windows do not submit commands. Mouse polling can miss a press
shorter than a presentation update; broader buffered input/controller work remains ZOD-061.

`Update` processes input; no elapsed time is sent to CT or RNG. `Draw` only reads the
snapshot and legal-highlight queries. A diagnostic compares simulation data before
and after every draw. The host recreates/disposes GPU resources without replacing the
battle/controller. Core, Content and Engine, their rules, prepared formats, packages
and project references are unchanged.

Reference checkout HEAD rechecked on 2026-10-23:
`11464943bbdbd2a0af022d90cb7f10455cf911df`. No new C behavior is translated here.
The existing source-derived clock/movement/sword rules and their limits remain those
documented by the M1 behavior notes. This UI is not original FFT menu fidelity.

## Automated Windows check

Choose a fresh ignored output directory:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --battle-check scratch/playable-battle-check-001
```

The finite check supplies **programmed semantic keyboard and canvas-pointer input**,
not injected physical hardware events. Pointer input still traverses the window-size,
backbuffer and letterbox conversion used by the native mouse. It selects modes,
moves, attacks and waits through the same router/controller as interactive play.
It deliberately requests Advance once during an active turn and once after victory.
It also resizes the window to 1280x720 mid-battle and continues picking through the
resized viewport, verifying the resize did not alter simulation state.

Output contains `start.png`, `rejected.png`, `moves.png`, `targets.png`, `finished.png`,
`terminal-rejected.png` and `check.json`. The report records the actual 14 typed attempts
(12 accepted, 2 rejected), programmed inputs, final snapshot/digest, frame count,
resize/viewport, GPU/runtime/OS and replay comparisons. This is an opt-in developer
receipt, not a save format or supported persistent replay file.

The check requires both replaying the submitted journal and executing the original
headless script to match the interactive final simulation projection exactly:

- A wins; HP A/B `25/0`; A at `(0,0)`, B at `(1,0)`.
- Tick 20, CT A/B `0/40`, no active turn.
- RNG word `2473755763`, draw count `22`.
- Rejected requests preserve state; rendering and resize preserve state.

Exit codes are 0 for success/normal close, 1 for a host/check failure or premature
check close, 2 for invalid CLI syntax. Existing check directories are rejected. An
interrupted run can leave incomplete files; use a new directory name.

## Verification recorded on 2026-10-23

Locked restore, zero-warning Release build and all **868 tests** passed: Core 293,
Content 213, AssetTool 151, AssetTool CLI 91, Engine 24, Client 96. The 35 added Client
cases cover controller routing/replay, discarded legality probes, atomic rejections,
terminal/restart behavior, key/mouse edges, selection/keyboard routes, sprite/diamond
picking and letterbox/client/backbuffer mapping, including extreme integer dimensions.

Two final GPU runs under ignored `scratch/playable-battle-032-c` and
`scratch/playable-battle-032-d` each completed in 280 draws with 12 accepted and 2 rejected
commands. Both resumed pointer play after a programmed resize from 960x640 to 1280x720,
with final viewport `(100,0,1080,720)`. Both matched the journal replay and the original
headless script. All six checkpoint PNG hashes also matched between these runs.
Selection, legal move/target coloring, rejection text, HP/choice cards, winner/KO state
and terminal rejection captures were visually inspected.

```text
final diagnostic snapshot  9f441423bd0533074796128bf249e313178275797933a63d0adaab7757711ffb
finished.png              4aec4d56018539bc3309ba2b29b2666b66acd85b2f09e395d1ebedf83643400c
terminal-rejected.png     7b6e74c1d2e22479f755c5a1d7d79cae97e663c248d62b5f8cdbbbe197347d26
```

Environment: MonoGame DesktopGL 3.8.6/Reach, AMD Radeon RX 6600, Windows
10.0.26200, .NET 10.0.13. These image hashes are observations on this environment,
not cross-driver goldens or original-game comparison fixtures.

Physical keyboard/mouse timing, native drag-resize, mixed-DPI monitor movement,
controllers, focus changes on hardware, forced device loss and accessibility remain
manual follow-ups. Headless tests cover the shared edge latch, mapping, router and
simulation behavior; programmed Windows checks do not claim those physical observations.
Original prepared-scene controls, complete original combat/campaign and wider UI
authoring/navigation remain separate work. Completing this ticket does not complete M2.
