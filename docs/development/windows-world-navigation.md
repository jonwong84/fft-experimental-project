# ZOD-103 — Windows authored world navigation

Follow-up (2026-10-26): [ZOD-069](world-save-load-ui.md) now enables Save/Load in
normal `--world-menu`. The navigation-only `--world-menu-check` keeps the earlier
profile below; its approvals and captures retain their historical scope.

Implementation entry: 2026-10-26, managed
`164469cefc1fbd0989ed9b936e304b56fb2313b8`, clean working tree.
The [ZOD-102 model and source context](synthetic-world-navigation.md) define
the authored flow. No additional original algorithm or asset is translated.

## Selected behavior and finite native plan before execution

Add explicit `--world-menu` and `--world-menu-check <new-directory>` commands.
Use the existing MonoGame DesktopGL dependency, authored font, 960x640 canvas,
point sampling and centered fit. Display Home, live checkpoint, read-only Party
and Cancel-first confirmations. The model remains the only state owner.
No save operation or real user-directory write occurs. Existing launch routes,
intro/battle, Engine/Framework/Content production and save schemas are preserved.

Keyboard: Up/Down, Enter/Space confirm, Escape/Backspace back. Mouse: fresh left
press on an enabled entry, through client/backbuffer/letterbox mapping. First
controller: D-pad, A confirm, B back (SDL mapping; no PS1 mapping claim).
No held repeat or analog navigation. Per-device edges are consumed together;
at most one action runs per sample, priority Back, Up, Down, Confirm, Pointer.
Inactive and first-reactivated samples baseline all held controls. Controller
connection changes baseline controller buttons; keyboard/mouse remain usable.
One transition cannot activate the destination screen with the same press.
The close callback requests the model's Exit confirmation and cancels closing
until Accept; another modal cannot be bypassed by closing.

Compile and headless-test before graphics. Use **two native check processes**,
normal and repeat, each at most **600 draws / 60 seconds**. No emulator or
physical input automation. Each follows the same finite scripted samples:
fresh Home; New; held-confirm suppression; travel; Party; controller hot-plug
with A held; controller back/held suppression; inactive/reactivated held controls;
pointer Home/New; cancel and accept New; resize to 1280x720; pointer travel/Party;
close-callback cancellation; resize to 720x960; pointer Back; Home; Exit and
controller confirmation. Release samples separate deliberate presses.

Capture the listed screen checkpoints, CPU pixels/GPU target equality, raw input,
dispatched semantic action, complete model view and travel events. Replay every
semantic action against a fresh headless model and compare each result/view.
Validate actual fit/dimensions and scaled pointer routes after both resizes;
drawing/recreation must preserve state. The two runs' PNG/model/action artifacts
must match. Save directories must remain absent. Audit the final authored
state/events independently and build a local review gallery.

Any failed launch is retained and investigated. Revise this plan explicitly
before exceeding two graphics launches. Programmed focus/close callbacks are
not physical Alt-F4 or actual focus-loss observations. Physical keyboard/mouse/
controller mapping, mixed-monitor DPI, forced device loss and owner visual
approval are separately reported gates. ZOD-069 waits on this bounded host;
the full ZOD-042 UI epic remains open.

## Launch and manual review

Open PowerShell in the repository and launch the already-built client:

```powershell
Set-Location C:\repositories\fft-experimental-project
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --world-menu
```

Manual route (owner subsequently confirmed this checklist on 2026-10-26:
"It's good."):

1. With the keyboard, Enter starts New. Hold it briefly: the new checkpoint
   should remain at funds 100/elapsed 42 until a fresh press. Release and press
   Enter again to travel to Hill (funds 93/elapsed 53). Down/Enter opens Party.
2. Escape returns to World, then Home. Resume returns to the same checkpoint.
   Click entries with the mouse; test New's Cancel, then its explicit replacement
   choice. Replacement starts Harbor again with funds 100/elapsed 42.
3. Try the USB controller's D-pad/A/B. Check one action per press and that a
   held button does not activate a second screen. The game's SDL mapping may
   differ from RetroArch's controller configuration.
4. Resize the window and click entries again. Switch to another window and back;
   held controls should not trigger a menu action on reactivation.
5. Use the window close button. Cancel should retain the session; closing again
   and selecting Exit without saving should exit. Report any unexpected behavior
   with the device, screen and action that triggered it.

The window starts at Home. Enter starts the authored checkpoint; Enter again
travels to Hill. Down/Enter opens Party. Escape goes back, then Home; Resume
returns to the same live checkpoint. Home/New and Exit open confirmations with
Cancel selected. Use Up/Down or click an entry. Controller navigation uses the
first SDL-mapped controller's D-pad/A/B; RetroArch's mapping is independent.
Window close requests Exit confirmation; only Accept exits. Escape is Back.
The navigation-only profile never writes saves; the new normal interactive
profile saves only through explicit ZOD-069 actions. Short presses entirely between polling samples
can be missed; no held repeat or configurable bindings are claimed here.

The owner approved [the local gallery](../../scratch/world-menu-103-review/review.html)
on 2026-10-26: "Looks good." After receiving the five-step interactive checklist,
the owner confirmed "It's good." This records keyboard/mouse/controller use,
resize/click behavior, native close/cancel/confirm and focus/reactivation as
owner-reported manual observations, separately from synthetic connection/focus
samples. Exact device identity/mapping and measured timing were not supplied;
mixed-monitor DPI and forced device loss remain later validation scope.

## Executed validation — 2026-10-26

Locked restore and Release build pass with zero warnings/errors. All **2,300
managed tests** pass (15 new): Engine 24, Framework 830, Content 644, AssetTool
202, CLI 211 and Client 389. All **284 Python tests** pass (3 new).

Exactly the two planned native processes ran successfully, each in under four
seconds, **51 draws / 51 scripted samples / 13 captures**. There were no failed
or extra graphics launches. All **26 GPU target images** match CPU pixels in full
(**15,974,400 pixels**); every dispatched action's result/events and complete
view match a fresh headless replay. Actual backbuffers reached 1280x720 and
720x960; routed pointer clicks worked after both changes. Explicit resource
recreation and rendering preserved the model. The close event pipeline was
invoked programmatically and canceled to its modal; actual OS close is a manual
gate. Focus/connection changes were programmed samples, not physical observations.

The independent Python audit checks the authored route, modal isolation, disabled
Resume, all checkpoint fields, travel events, fixed date/progress/party/inventory,
final Hill/funds 93/elapsed 53, PNG hashes/palette/opacity and exact repeated
**16 files**. No save directories exist. The gallery includes unchanged canvas
captures after wide/tall resizes; it does not show a widescreen or CRT mode.
Party and New-confirmation PNGs were inspected for legibility and layout by the
implementation agent. The owner subsequently approved the displayed gallery
on 2026-10-26: "Looks good." The later owner report "It's good." completes the
manual checklist above. No implementation suites or agent-run graphics checks
were repeated, and no reviewed artifacts changed for these approval updates.

[Executed metadata](../evidence/navigation/windows-world-navigation-v1.json)
binds implementation, tests, logs, TRX files, captures and local audit. Existing
ZOD-102 source evidence remains historical at its recorded reference SHA;
the read-only checkout is still clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. No original UI comparison is claimed.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --world-menu-check scratch/menu-example-a
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --world-menu-check scratch/menu-example-b
python tools/verification/verify_world_menu.py --first scratch/menu-example-a --repeat scratch/menu-example-b --output scratch/menu-example-audit.json --review scratch/menu-example-review/review.html
```

Check/audit/review outputs must be fresh. Check mode ignores actual device input
in favor of its recorded samples and exits itself; normal mode is interactive.
No package, Core/Content rule, save format, reference checkout or existing
intro/battle implementation changes. ZOD-103 is complete for the bounded authored
host; ZOD-069 is Ready for slot navigation. Original UI fidelity and the broader
device/remapping/DPI requirements remain open under their existing tickets.
