# Later presentation decisions

Owner notes recorded 2026-10-24, after `91aea0f`. These are future requirements
and discussion topics, not completed research or selected implementations. They
do not change the current work order or the bounded banner comparison policy.
[ZOD-079](../BACKLOG.md#zod-079--specify-later-display-rendering-and-settings-options)
owns the cross-cutting design discussion; existing feature tickets retain their
implementation scope.

## Original display proportions

The owner wants an original presentation mode that accounts for the game's
intended CRT/4:3 display proportions. A square-pixel, 1:1 raw framebuffer view is
useful for inspecting raster output, but is not the requested final presentation.
Investigate pixel-aspect correction and how it applies to each actual display
mode, active image area, borders/overscan and scene type. Do not assume one
horizontal stretch factor applies to all captured widths or PS1 display modes.

Keep raw capture coordinates and displayed proportions explicitly distinct.
Preserve native-pixel diagnostic captures, then add separately identified display
comparisons for the corrected presentation. Record any viewport transform and
its inverse for picking/input. Original 4:3 presentation and the optional wider
layout in ZOD-059 need distinct profiles; widening the camera/UI is a separate
choice from correcting pixel aspect.

## Optional CRT shader

The owner wants a CRT shader option. Discuss the desired appearance, candidate
techniques, performance, output-resolution behavior and accessible controls when
representative scenes exist. Decide whether scanlines, phosphor/mask simulation,
curvature or other effects belong in the chosen preset; none is selected now.

Define the relationship between aspect correction, internal scene rendering,
output scaling and CRT processing, including how menus/text are treated. Retain
an explicit unfiltered mode for diagnosis and comparison. Shader choice and
parameters belong to presentation preferences, with no effect on simulation.
Any reused shader requires its own license/dependency review before adoption.

## Rendering approach and resolution

Discuss hardware/GPU rendering versus software/CPU rendering, including fidelity,
performance, portability, maintenance and reproducible validation. Clarify whether
each proposal concerns scene rasterization, a particular material/effect, final
composition or merely the display backend. The current MonoGame host and CPU
banner compositor are bounded implementations, not a decision to provide two
complete renderers or a final fidelity architecture.

Decide whether multiple rendering paths are worthwhile before implementing a
switch. Compare representative geometry, palettes, transparency/order, gradients
and effects against original observations; record supported differences. Keep
internal scene resolution, final window/output size, pixel aspect, sampling and
source asset detail separate. Coordinate selectable resolution with ZOD-060 and
optional higher-detail assets with ZOD-063; this note selects no new backend.

## In-game Settings menu

The owner expects an in-game Settings menu as display/rendering/audio options
grow. ZOD-042 owns the menu workflow, coordinated with ZOD-059/060/061 and ZOD-043.
Discuss original/aspect modes, CRT controls, supported rendering choices,
internal/output resolution, audio controls and device-appropriate navigation.
Do not expose a backend switch unless multiple supported paths actually exist.

Before implementation, decide defaults, capability diagnostics, persistence,
which changes apply live or require restart, and safe recovery from unusable
display settings. Keep presentation/device preferences separate from deterministic
battle commands and state. App-level preference storage versus campaign-save
settings needs an explicit decision alongside ZOD-053.

## Accurate music and sound effects

The owner wants a later discussion focused on accurate audio and sound effects,
not just successful playback. ZOD-043 owns source investigation, backend selection
and bounded preparation/playback proofs. Discuss decoding, sequencing/event
timing, pitch, envelopes, loops, interpolation/resampling, voice mixing, panning,
reverb and streamed paths where original evidence shows they are relevant.

Define the reference and comparison method for selected music/effect checkpoints,
including capture provenance, timing domains and justified tolerances. Separate
content/driver behavior from output-device latency and user volume preferences.
Audio playback consumes presentation events; device callbacks and sample clocks
must not drive deterministic gameplay. Original audio, decoded samples and
recordings remain local under ignored scratch; runtime uses prepared files.

## Revisit gate

Return to these decisions much later, when representative battle/menu/effect
scenes and the first source-traced audio slice can support useful comparisons.
Start with a design record and bounded follow-up tickets, not simultaneous
backend/shader/settings/audio implementation. The current next investigation
remains the observed banner's native color discrepancy and its timing anchors.
