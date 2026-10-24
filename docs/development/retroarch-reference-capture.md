# RetroArch reference capture setup — ZOD-033 follow-up

Date: 2026-10-24. Managed baseline: `66e08c0`.

The owner identified an existing RetroArch installation. It is now located and
boot-checked: `C:/RetroArch-Win64/retroarch.exe`, **RetroArch 1.22.2 / Git 59e4fc7**,
built 2026-01-17. The installed **Beetle PSX software** core is used, not the HW
core. Core info advertises v0.9.44.1; the actual DLL is pinned by SHA-256 below.
The game reaches its introductory Sony Computer Entertainment America credit
screen. The owner has since captured the target battle banner and a state; two
bounded state restores reproduce the banner scene identically. Exact timing and
original-versus-managed visual comparison remain pending. **ZOD-033 remains partial.**

## Ready-to-use private launcher

From the repository root, run this generated local script when ready to play:

```powershell
& .\scratch\retroarch-reference\launch.ps1
```

This opens the original game using the private profile, with muted audio. The
launcher was syntax-checked; the finite verifier separately exercised its selected
configuration/core/BIOS/content. The owner's subsequent interactive run produced
a raw banner screenshot and a state with an identical thumbnail. Keyboard bindings
are explicit:

- Arrows: D-pad; Enter: Start; right Shift: Select.
- Z: Cross; X: Circle; A: Square; S: Triangle; Q/W: L1/R1.
- P: pause/resume; K: advance one frame while paused; F8: raw screenshot.
- F2: save state in slot 0; F4: load that state; Escape: quit.

States and screenshots belong to the private profile under
`scratch/retroarch-reference/states` and `/screenshots`; RetroArch may add a
`Beetle PSX` subdirectory. Each launch creates a fresh `/sessions/<id>` for its log,
BSV input recording and writable core-options copy. It does not autoload a state.
Private memory cards remain below `/saves`, separate from the installed emulator's
cards. F2 can replace a previous private slot-0 state, so freeze/copy a useful
checkpoint under a distinct local capture name before reusing the slot.

The launcher checks hashes of the emulator, core, verified BIN/CUE, source and
actual copied BIOS, and prepared policy files. If those inputs change, prepare a
new profile and review the change. Do not edit capture settings during a reference
run. A changed profile must have its own evidence identity.

## Preparation and bounded readiness check

The [preparer](../../tools/verification/prepare_retroarch_reference.py) requires
Python 3.12+ and the existing verified project inputs. The
[finite verifier](../../tools/verification/verify_retroarch_boot.py) also uses the
existing developer Pillow environment for screenshot dimensions. This workstation
used bundled Python 3.12.14/Pillow 12.3.0; replace `python` with its absolute path
if needed, as described in the [comparison guide](visual-comparison-checkpoints.md).

```powershell
python tools/verification/prepare_retroarch_reference.py --retroarch-root C:/RetroArch-Win64 --output scratch/retroarch-reference
python tools/verification/verify_retroarch_boot.py --profile scratch/retroarch-reference --output scratch/retroarch-boot-033-b --frames 1200
python -m unittest discover -s tools/verification -p 'test_*.py'
```

Choose fresh output directories on reruns; the demonstrated names already exist.
The preparer verifies the known SCUS-94221 BIN (541,315,152 bytes, SHA-256
`29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9`),
single-track Mode 2/2352 CUE at INDEX 01 00:00:00, and supplied US BIOS (524,288
bytes, SHA-256 `11052b6499e466bbf0a709b1f9cb6834a9418e66680387912451e971cf8a1fef`).
It creates a new staged root under ignored scratch, copying only the BIOS into
`system/scph5501.bin`; the CUE still addresses the existing ignored BIN.
Failed preparation preserves existing output. No emulator/core installation,
update, global settings change or .NET runtime dependency is introduced.

The final profile disables shaders, filtering, run-ahead, preemptive frames,
rewind, automatic overrides/remaps/shaders, autoload/autosave states, achievements
and command listeners. The selected software core uses 1x native resolution and
dither, CPU 100%, native GPU/CD speed, PGXP/widescreen/overclocking disabled,
no BIOS skip, uncropped horizontal overscan, NTSC scanlines 0..239 and uncorrected
aspect. Raw core screenshots are selected with `video_gpu_screenshot=false`.
Audio uses muted WASAPI; microphone input is disabled.

These choices are guided by the official [Beetle PSX core documentation](https://docs.libretro.com/library/beetle_psx/)
and its installed option names. Uncropped raw buffers retain border pixels and can
change dimensions with PS1 display modes. Do not silently crop/resize them to the
managed 256x240 banner canvas. Pixel alignment and the appropriate original
comparison regions must be measured at the actual battle checkpoint.

RetroArch's [separate-config CLI](https://docs.libretro.com/guides/cli-intro/) supports
the isolation. `global_core_options=true` and a per-session `core_options_path`
prevent loading the installed per-core preferences and preserve the frozen base
options even though RetroArch saves default-enriched options on exit. Override,
save, state, screenshot, playlist, cache and log paths are redirected below scratch.
Installed resources/core libraries are read from the existing installation.

## Observations and evidence limits

Two final **1,200-frontend-frame** cold-start checks exited 0. Controllers 1/2 were
disconnected, keyboard/joypad input drivers were null, patches were disabled, and
SRAM load/save was disabled for those finite checks. They created local log,
screenshot and BSV files, and verified that all selected core options were retained
in the saved session options. The selected policy files were unchanged. The
installed `retroarch.cfg` and `config/Beetle PSX/Beetle PSX.opt` hashes were identical
before and after each check; their shaders/run-ahead preferences were preserved.

Both final screenshots are native **350x240 RGB** images of the introductory game
credit, inspected locally. PNG SHA-256:
`5cd698c1ee357522e1abf33145fb767e90be563113b7d7b4e6bfeee0e26caec2`.
The earlier exploratory 600-frame capture showed the BIOS logo in another display
mode; it is not a battle checkpoint. A frontend frame budget is not asserted to be
a PS1 VSync count or one banner-effect tick.

Logs retain three core `GET_VARIABLE` errors for `beetle_psx_pgxp_vertex`,
`beetle_psx_pgxp_texture` and `beetle_psx_filter`, plus stock-GLSL/MIDI diagnostics.
The selected software options were verified, and these diagnostics did not prevent
introductory rendering. Their wider implications are not resolved by this readiness
check; logs remain available for capture review. Stock frontend presentation is
separate from the raw screenshot path. No PS1 GPU/hardware equivalence is claimed.

BSV files were recorded but are not byte-identical, and replay playback was not
validated. Do not treat them as a proved deterministic input oracle yet. Future
checkpoint reproduction must verify the actual state/recording and its outputs.
The emulator executable SHA-256 is
`323c7b8e27c76d8debcb2458966255b44e011afb57e2434ed99e4336192e7c7c`;
software core DLL SHA-256 is
`62760b374cdd4bbbb1d5c80265e36c6c2451df14e35bf4f0b2388bd8f2d1cffc`.

The [committed readiness metadata](../evidence/visual/retroarch-reference-readiness-v1.json)
records identities and local paths without original media. **30 Python tests pass**
(22 existing comparison cases plus 8 configuration/path/command regressions).
The generated PowerShell launcher parses without errors. No managed source changed
or .NET suite reran; the last complete managed validation remains 1,388 tests.
All original files, copied BIOS, profiles, saves, states, screenshots and recordings
remain ignored. No automatic commit was made.

## Owner-captured banner checkpoint

The owner supplied `states/Beetle PSX/fft-clean-base-game.state` after capturing
`screenshots/fft-clean-base-game-261024-021910.png` in the private profile.
The raw **280x240 RGB** screenshot visibly contains “Conditions for Winning” /
“Defeat all enemies!” over the battle scene. Its SHA-256 is
`a36a373373f002c25d5d415c775e4336591bf8369c477f47cc3ac196367b9485`.
The state's thumbnail is byte-identical to this screenshot. The compressed state
is 1,528,924 bytes, SHA-256
`133b893188d0abe7909725f910496b74bad9cfc85733cb38d76fece03048901a`.
Frozen copies and observation/restore reports are under
`scratch/banner-033-original-a`; slot reuse will not overwrite them.

Two hidden finite processes loaded separate copies using `--entryslot 0`, the
same hash-checked emulator/core/BIOS/profile, disconnected controllers, requested
null input drivers, no patches and no SRAM load/save. Each used a new core-options
copy and state directory. Installed configuration hashes were unchanged. The
first exploratory one-frame checks produced black PNGs after the state load;
they are retained as failed visual checkpoints, not accepted references.

At **four frontend frames**, both restores visibly reproduce the banner scene,
with byte-identical raw PNGs (SHA-256
`4fc1a51f516efbd1c94493476f0b382e901e2c0e0baf7cc865a391e4463d65aa`).
These images differ from the owner's earlier screenshot at 834 RGB pixels;
the cause and exact frame relationship are unclassified. This establishes bounded
repeatability of the restored scene, not the exact screenshot's animation tick,
a verified BSV replay, event 401 activation or original/managed equivalence.
The known core-option/GLSL/MIDI diagnostics remain in the logs. The local
`check-banner-state.py` procedure and each run's `command.json` preserve the
commands; the [observation metadata](../evidence/visual/original-banner-state-v1.json)
records identities without media. No managed source changed or .NET tests reran.

## Next comparison gate

The later [saved-task continuation study](original-banner-continuation.md) resolves
the old state's logical location to reveal counter 18 and validates its no-input
continuation through hide/exit. Appearance alone had only identified fully visible
artwork. Early reveal, loading, physical input and event identity remain open;
the historical gate below describes the evidence available at this setup milestone.

The current state freezes the visible banner, not a verified pre-banner anchor.
Record which encounter/event path was actually reached; appearance alone does
not prove event 401's fallback activation. Obtain a pre-banner state when tracing
the reveal, then retain screenshots at clearly described
visual anchors and document each frame-step/input. Keep the original background,
display mode, actual raw dimensions/borders, core/session options, state, BSV/log,
edition and file hashes. Name/copy snapshots before slot reuse.

Before accepting an original comparison, verify state/recording reproduction,
map/camera/palette/input context and the relationship between frame steps and
banner progress. The modern RGB8 compositor intentionally differs in precision,
dithering and background. Add an explicit observed-original comparison profile
and justified regions/tolerances to the currently synthetic-only harness; do not
relabel an original image as a managed synthetic capture to bypass that gate.
