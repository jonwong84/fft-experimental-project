# ZOD-025 — Synthetic Windows host

The `Fft.Client --window` path uses MonoGame DesktopGL **3.8.6** selected by
[ADR 0005](../adr/0005-windows-rendering-and-input-stack.md). It displays the immutable
starting snapshot from `SyntheticTwoUnitBattle`: four invented tiles, two invented palette
sprites, and HP labels. No original media, BIN, prepared asset directory, importer, reference
checkout or C toolchain is needed. This is a presentation milestone, not a playable battle.

## Windows launch

From the repository root, with the SDK pinned in `global.json` (10.0.402):

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --window
```

Resize, maximize and restore through the normal window controls. The close button exits;
an Escape key-down handler also requests exit. The process returns 0 after a normal run,
1 on a host/device/check failure, and 2 for invalid command syntax. Non-Windows hosts reject
the graphical command before constructing a `Game`. No-argument bootstrap, prepared-file
loading, and `--scenario two-unit-battle` retain their headless behavior.

For a finite, opt-in GPU check, use a **new** output directory:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --window-check scratch/window-host-check-001
```

After 120 rendered frames it checks an opaque palette pixel and a transparent sprite corner
against authored colors, verifies the simulation diagnostic, writes `scene.png` and `check.json`,
and exits. Existing output directories are rejected; an interrupted/failed run may leave an
incomplete directory, so choose a new one. These files remain ignored local diagnostics.
The check must run on a desktop with a working graphics driver; it is not part of headless CI.

## Ownership, timing and resource lifetime

- `Fft.Client/Presentation` owns MonoGame, SDL/window input, CPU-authored diagnostic art/font,
  texture upload, presentation timing and layout. Core and Engine gain no graphics/input dependency.
- The host receives one `AttackBattleSnapshot`, projects its units once, and holds it read-only.
  Neither `Update` nor `Draw` calls a simulation transition. A nominal 60 Hz presentation clock
  does not advance CT, consume RNG, or play the scenario command script.
- A startup/end diagnostic compares combatants, turn state, battle RNG and unit positions/move
  flags. These and the referenced map/profile objects are immutable. This diagnostic is not a
  save format or an original-game fixture.
- `LoadContent` creates the sprite batch, three textures and a fixed 960x640 render target.
  Device reset recreates them from authored CPU data. `UnloadContent`/`Dispose` release them
  idempotently and detach handlers before the graphics manager is disposed. Mid-run device-loss
  recovery has not been forced on hardware.
- Explicit point/clamp sampling, no mipmaps and straight-alpha blending preserve the synthetic
  transparent palette entry. Terrain is drawn first; units follow stable depth/X/ID order.
  This order only covers this flat, single-layer scene. It does not implement PS1 blend modes,
  interleaved map occlusion, animation, or an original camera transform.

The complete 960x640 scene is fitted into the actual backbuffer with centered black bars.
Resize never changes simulation coordinates. This scales a finished frame; it does **not**
implement ZOD-059 widescreen cameras or ZOD-060 higher-resolution geometry rendering.
The host sets the process-local `SDL_WINDOWS_DPI_AWARENESS=permonitorv2` hint before constructing
`Game`, and uses backbuffer dimensions for its final viewport. The versioned SDL backend updates
the backbuffer on client resize; the handler logs dimensions without recursively applying
graphics changes. [SDL hint](https://wiki.libsdl.org/SDL2/SDL_HINT_WINDOWS_DPI_AWARENESS),
[MonoGame 3.8.6 resize implementation](https://github.com/MonoGame/MonoGame/blob/v3.8.6/MonoGame.Framework/Platform/SDL/SDLGameWindow.cs),
[buffered keyboard event contract](https://docs.monogame.net/api/Microsoft.Xna.Framework.GameWindow.html#Microsoft_Xna_Framework_GameWindow_KeyDown).

## Executed checks and evidence limits — 2026-10-23

Evidence is **synthetic expectation**, with live Windows GPU execution and manual window
inspection. No original-game comparison or decomp-derived behavior was added in this ticket.
No reference revision is required for this invented presentation; existing source fixtures
retain their own pinned revisions.

- Windows `10.0.26200`, .NET `10.0.13`, x64, AMD Radeon RX 6600, Reach profile:
  actual window/device creation and texture upload succeeded. The 120-frame check passed
  transparent-corner and opaque-color readback. The capture was visually inspected for the
  grid, sprite placement, text, transparency and crisp sampling.
- Local evidence: `scratch/window-host-check-20261023-a/scene.png` and `check.json`.
- Maximizing produced a 1920x1009 client/backbuffer with viewport `(203,0,1513,1009)`;
  restoring returned to 960x640 with viewport `(0,0,960,640)`. Both views were inspected.
  Native close returned 0 after 6,943 frames, with ticks 0 and RNG `1/0` unchanged.
- Automated keyboard injection did not establish an Escape/Alt-F4 result in this environment.
  The buffered Escape handler compiles; physical-key validation remains a manual follow-up.
  Window close and finite-check exit were exercised successfully.
- Layout tests cover wide/portrait/tiny/minimized dimensions, integer overflow boundaries,
  synthetic axis conventions and stable unit ordering without opening SDL or a GPU.
  The normal solution suite stays headless.

Locked restore and Release build passed with zero warnings; the full suite passed **656 tests**
(Core 293, Content 152, AssetTool 96, AssetTool CLI 67, Engine 24, Client layout 24).
The headless scenario still finishes with `FirstUnitWon`, HP 25/0, ticks 20 and RNG `2473755763/22`.

Different display scaling factors, movement between monitors with different DPI, physical
controllers, gameplay input/picking, forced graphics-device reset, self-contained publishing
and clean-machine installation were **not tested**. They are not implied by this single-display
window check. Physical input and later viewport/picking work remain ZOD-032/061/063 concerns;
prepared map/sprite rendering is ZOD-031 after ZOD-027/028 and ZOD-029/030.

## Runtime package integration

The exact central version and restored transitive versions are locked in the client and its
test project: MonoGame DesktopGL 3.8.6, MonoGame.Library.SDL 2.32.10.2,
MonoGame.Library.OpenAL 1.24.3.4, NVorbis 0.10.4. The framework's native runtime files must remain
alongside published output. License inventory and deployment constraints are recorded in
[ADR 0005](../adr/0005-windows-rendering-and-input-stack.md).
This ticket does not create a redistributable package or claim license-notice packaging has
been validated. Linux CI can restore/build and run the pure layout tests; only Windows is
supported for the graphical command.
