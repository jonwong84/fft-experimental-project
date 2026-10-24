# Bounded five-bit general-banner profile — ZOD-033 follow-up

Implemented 2026-10-24. Managed entry `ec50eb7`, clean working tree.
Read-only FFT reference HEAD freshly checked:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

The Client now has an explicitly selected five-bit material profile alongside
the unchanged modern RGB8 preview. Its actual Windows GPU capture matches all
**1,577 previously selected original foreground pixels exactly**, with zero
tolerance. The original draw-state writer is identified in the frozen RAM
ordering table. This is a bounded material/gradient implementation, not a general
PS1 rasterizer or a complete ZOD-033 fidelity/timing result.

## Draw-mode provenance at this checkpoint

The [precision study](../architecture/general-banner-native-color-study.md)
previously established the frozen GPU's dtd=0 and matching VRAM foreground. The
new developer [chain reconstruction](../../tools/verification/trace_banner_draw_state.py)
also inspects labeled `MAIN/MainRAM.data8` from that same hash-bound state.
Its pointers and layouts are pinned by `target/main.yaml`, `target/battle.yaml`,
`target/event.yaml`, `include/fft/render.h` and `include/fft/gfx.h` in the read-only
FFT reference. The strict labeled-state reader is shared with the prior study;
neither tool executes the state, native code or an emulator.

`g_render_current_buffer` at `0x8011a2d4` selects `g_render_buffers[0]`, base
`0x800fc55c`. Its OT pointer at +`0xe5b4` is `0x80056b18`.
`src/main/battle_otag.c::gfx_battle_otag_get` and
`src/battle/entry.c` select OT slot `0x17f`, giving root `0x80057114`.
`src/battle/frame.c::battle_frame_submit` applies the draw environment then
submits that chain. `g_gfx_current_otag_entry` at `0x80173f44` also points at
the selected OT base.

The frozen linked list terminates normally after **755 nodes / 371 nonempty
packets**. Before the six full-visible banner packets at `0x8004c6c8 + 52*i`,
the last reconstructed draw-mode write is **`0xE1000006`**, at packet address
`0x801d71e0`. This is **`g_conditions_burst_draw_modes[1]`**, not an inference
from battle initialization. Its bit 9 is clear. Subsequent fixed-size polygons
do not replace the dither state; banner texture-page attributes preserve it.
All six packets bind back to prepared vertex colors, geometry, UVs and add/subtract
materials in traversal order 0..5. Unknown packet payloads invalidate tracked
state until another validated draw-mode command; they cannot silently hide a
state write. Cycles, invalid RAM aliases/pointers and malformed packets reject.

The source writer is
`src/event/require/entry_dark_screen.c::require_dark_screen_entrypoint`:
both burst draw-mode packets are initialized by `SetDrawMode(...,0,0,...)`.
The worker enqueues its selected mode through
`gfx_append_gpu_primitive_to_secondary_otag`, which head-inserts into the next
script OT entry. That entry traverses before the banner's entry. Together with
the initializer in `SetDrawMode.c`/`get_mode.c` and the stored command, this
explains the captured banner's dither-off state even though battle setup initially
enabled dithering.

This establishes command provenance in **frozen RAM**, not a live GPU/DMA trace,
the complete original event path or event-401 activation. It does not establish
the draw state of every other banner. The original first-buffer uncertainty from
ZOD-077 remains historical; this held checkpoint has concrete full geometry.

## Runtime contract and policies

`FiveBitGeneralBannerPresentation` accepts prepared `LoadedGeneralBanner` data
and an ordered list of `BannerRasterDraw(Geometry, State)` records. Each draw
requires `BannerRasterState(DitherEnabled, FramebufferX, FramebufferY)`.
There is no inherited/global default or mutable state cache. The physical origin
must keep the 256x240 canvas inside a 1024x512 framebuffer and determines the
4x4 dither phase. Origins `(0,0)` and `(0,240)` have the same phase; the preview
supplies `(0,0)` explicitly. Future scene integration must supply its own traced
state rather than learn a global dither-off rule from this capture.

The profile uses the studied signed Q12 vertical slope and half-unit color bias,
five-bit source texels, modulation followed by dither/quantization, and saturating
five-bit add/subtract after each pass. Source word zero discards; STP-clear words
replace; STP-black preserves the destination. Index zero is not inherently
transparent. The authored checker is initially quantized to five-bit channels;
final RGB8 output zero-pads each channel with `<<3`. Geometry remains point-sampled
and half-open. These scalar rules have authored boundary vectors and do not
import emulator implementation code or libraries.

The reduced vertical model also renders projected cropped quads, interpolating
over the visible quad without scaling source texels. Only the full-visible
original anchor has an exact original comparison; reveal/hide color/raster/timing,
triangle coverage, packet parity, shadow/background blending and other contexts
remain unverified. The final CPU canvas is still presented by MonoGame with an
opaque point-sampled blit. This does not decide the later hardware/software,
aspect, CRT or Settings design in ZOD-079.

## Select and preview

The legacy command still selects modern RGB8. Five-bit rendering requires an
explicit dither choice; missing/unknown/duplicate options reject:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --general-banner --content-root scratch/banner-078-a
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --general-banner --content-root scratch/banner-078-a --precision five-bit --dither off
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --general-banner --content-root scratch/banner-078-a --precision five-bit --dither on --check scratch/banner-five-bit-on
```

Space/C/Escape controls are unchanged. Use `--check <fresh-scratch-root>` for the
finite 15-checkpoint schedule ending at logical tick 91. Choose fresh output names
on repeats; demonstrated directories already exist. The native receipt is a
separate `fft.general-banner-native-capture` v1 contract, with policy, explicit
precision/dither/origin and per-draw state records. The legacy receipt shape,
policy and bytes remain available; no old RGB8 image is relabeled five-bit.
Prepared resources still load before scene/task creation; no logical event,
Core/Engine, asset schema, content preparation, package or save change is made.

## Actual capture comparisons and validation

Locked restore, **zero-warning Release build and all 1,418 .NET tests pass**
(30 new Client cases). These cover gradient/modulation, saturation/order, native
output encoding, the full dither matrix and phase, explicit per-draw state,
transparent/opaque/STP-black distinctions, crop/degenerate geometry, repaint
invariance, bounds and CLI state selection. No original artwork is a test fixture.

Two dither-off Windows runs, `scratch/banner-five-bit-a` and `-b`, complete at tick
91 with exact CPU/GPU readback at all 15 checkpoints. **All 15 PNGs and the receipt
are byte-identical between runs.** Native tick-20 PNG SHA-256 is
`4d9a7c7c43e8ab97cee792d5d0babf1f2843a28daa3c8679c6d4b937b5a3e6a9`.
A fresh default RGB8 run, `scratch/banner-rgb8-regression`, matches all 16 files of
the historical `scratch/banner-078-preview-a` byte-for-byte. The native hold image
was inspected: same complete two-line placement, darker foreground consistent
with the original; checker/background-sensitive regions still differ.

The optional [native comparator](../../tools/verification/compare_native_banner.py)
binds the old reviewed plan/original state and the new actual Client receipt/PNG:

```powershell
python tools/verification/compare_native_banner.py --plan scratch/banner-033-comparison-plan/plan.json --native-root scratch/banner-five-bit-a --output scratch/banner-five-bit-report-g
python -m unittest discover -s tools/verification -p 'test_*.py'
```

The native actual capture, not just the earlier Python model, matches **969/969
first-line and 608/608 second-line samples**, with zero changed pixels or channel
deltas. No tolerance was widened. Raw primitive-union regions remain separately
reported with their unmatched backgrounds. Two reports/review pages from the
same inputs are byte-identical (`-g`/`-h`); the independent second capture also
passes (`-i`). [Committed metadata](../evidence/visual/five-bit-general-banner-v1.json)
records these identities and the scoped command trace without media.

The deliberate dither-on run exercises the other state: its model remains exact,
but the original comparison reports both `draw-state-mismatch` and
`foreground-color-mismatch`. Only 1,433 samples remain background-independent;
144 newly sensitive pixels are excluded and counted. Of those 1,433, 657 differ
from the original. This is the expected negative control, not a tolerance
calibration or a regression in the explicitly selected dither-on behavior.

The comparator validates native metadata, completed schedule, exact full-visible
anchor, raw PNG digest and explicit per-draw states, and retains reconstructed
original command context. Exact sample/model equality and state agreement are
separate from `originalFidelityEstablished`, which remains false. Exit 0 means
the selected diagnostics and draw state agree; 1 writes the distinct differences;
2 rejects invalid/unavailable evidence without publishing output. Existing output
is preserved. The original synthetic-only comparator remains unchanged.

All **81 Python tests pass** (15 new authored DMA/receipt/comparison cases), including
chain corruption, unrecognized payloads, per-vertex binding, missing state,
partial anchors, wrong hashes, separate state/color failures and report/review
repeatability. Source media, decoded RAM, prepared assets, captures and reports
remain ignored under scratch. No automatic commit was made.

Next: obtain a pre-banner anchor/input sequence for reveal/hide timing and extend
comparisons to original background-sensitive blending and coverage. Full scene
state/event integration and a general compatibility default remain separate gates.
