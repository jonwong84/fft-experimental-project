# Displayed banner hide frames — bounded ZOD-033 comparison

2026-10-24. Managed entry `702cc4a`, initially clean. Read-only FFT reference HEAD
freshly verified: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

Five **displayed** hide anchors now match actual C# composition at zero tolerance:
crop 0, 8, 16, 24 and 31, including the last 22 affected samples at crop 24 and the
fully hidden frame. The screenshot's displayed frame is bound from source order,
saved CPU/GPU context and earlier hash-bound submissions; no best-fit crop or
pixel-alignment search selects the expected result.

The existing [continuation](original-banner-continuation.md) correctly identified
the newer **queued** task progress. The observed lag was not an incorrect logical
banner counter: the game is waiting to submit that newer frame while an older
front buffer remains displayed. This follow-up resolves that correspondence for
these selected snapshots, not a universal lag rule.

## Frame-submission trace

At the pinned FFT revision:

- `src/battle/entry.c::battle_entrypoint` builds the frame, toggles
  `g_gfx_screen_polarity`, increments `g_frame_counter`, then calls
  `battle_frame_submit` with the active ordering table.
- `src/battle/frame.c::battle_frame_submit` first calls `DrawSync`, then waits in
  `VSync`. Only after that wait does it apply `PutDispEnv`, `PutDrawEnv` and
  `DrawOTag`.
- `battle_display_init` creates two physical 256x240 buffers at Y=0 and Y=240.
  Each selected draw environment points at one buffer; its corresponding display
  environment points at the other. X offset -128 normalizes the source quad X.
- `src/main/battle_otag.c` and `include/fft/render.h` identify the two alternating
  render buffers and current OT, as in the earlier packet trace.

The actual frozen instruction fingerprints are checked in every selected state:
`battle_entrypoint` at `0x80079a98` (1,852 bytes), `battle_frame_submit` at
`0x80093a98` (344), `VSync` at `0x8001dba8` (328), and `v_wait` at `0x8001dcf0`
(156). Full SHA-256 identities are in the
[evidence metadata](../evidence/visual/displayed-banner-hide-v1.json), not merely
moving line numbers or the source target table's shortened hashes.

The CPU's `BACKED_PC` is inside `v_wait`; GPR RA and saved stack return addresses
identify the complete call chain: `0x8001dc64` in VSync, `0x80093b68` in frame
submission, `0x8007a058` in the battle loop. Both helper stack frames are 32 bytes;
the inspected saved return addresses are main SP+`0x18`, +`0x38`, +`0x58`.
SP is bounded inside main RAM. This places the snapshot **before** the new
environment/ordering-table submission, rather than assuming that current RAM
packets have already become visible.

`g_gfx_screen_polarity` (`0x8004597c`) selects the pending draw/display structures
at `0x8004ea14`/`0x8004eacc`, with strides 92/20. The saved GPU display origin
matches the pending draw surface, while its last-applied draw offset selects the
opposite back buffer. GPU mode is native 15-bit, dither is off, display enabled,
and the command/FIFO fields are empty. The saved VSync interval is 2.

At this point in this two-buffer loop, submission N is queued, N-1 occupies the
back buffer and N-2 is still the front buffer. The verifier binds N-2 to a prior
state from the already committed continuation archive: game frame counters differ
by exactly two, logical banner counters differ by two, polarity agrees, and that
earlier pending draw target equals the current front surface. Original archived
states/PNGs are hash-checked before controlled captures start.

The selected correspondence is:

- Frontend budget 132: queued logical tick 83, displayed tick 81, crop 0/31 rows.
- Budget 134: queued 84, displayed 82, crop 8/23 rows.
- Budget 136: queued 85, displayed 83, crop 16/15 rows.
- Budget 138: queued 86, displayed 84, crop 24/7 rows.
- Budget 140: queued 87, displayed 85, crop 31/0 rows.

Each earlier submitted packet is independently checked against the prepared
colors, CLUT `0x3fd8`, UVs, material, explicit crop and six-piece traversal, with
the source-bound preceding dither-off mode. The historical full-visible trace
still rejects partial geometry unless the caller explicitly supplies the cropped
contract; its default results are unchanged.

## Pixels, isolation and actual C# execution

The displayed VRAM surface, zero-padded to RGBA8 with `channel5<<3`, matches each
original PNG's entire measured 256x240 crop exactly. The fixed twelve-column raw
border mapping is retained; no resampling is used. This confirms the selected
front surface against actual scanout independently of the banner color model.

The [verifier](../../tools/verification/compare_banner_hide.py) then repeats the
earlier [transparent-palette control](general-banner-background-blending.md) at
each selected budget. All 28 non-GPU subsystem payloads and every GPU register
remain identical to the archived unmodified exit state. The selected palette must
stay zero in the probe, and its PNG also matches its saved front VRAM exactly.
Every pixel outside the source-derived displayed footprint remains unchanged.

The standalone CPU host's explicit `--sequence` mode loads five hash-bound opaque
RGB5 backgrounds, replays the actual managed banner in order through ticks 81..85,
and calls the existing `ComposeOver` with explicit dither-off/origin state.
Its separate `fft.banner-hide-cpu-check` receipt binds counters, crops and pixel
hashes. The old single-checkpoint mode/receipt remains available. No Client/Core/
Engine runtime algorithm, original opcode admission or asset/save schema changes.

All affected samples match the original and the independent Python transcription:

- Tick 81: **3,128** affected samples, exact.
- Tick 82: **2,828**, exact.
- Tick 83: **939**, exact.
- Tick 84: **22**, exact.
- Tick 85: no visible samples; the whole image preserves the controlled background.

That is 6,917 affected pixel comparisons across four visible temporal anchors,
not 6,917 distinct screen locations. The fifth frame has no artwork comparison:
empty sample sets explicitly report `compared=false` and `exactEquality=null`,
rather than awarding a vacuous color-accuracy pass. Its no-extra-pixels/background
preservation check still compares the full canvas. All five full canvases match;
untouched background pixels are inherited original output, not managed map rendering.

The comparison distinguishes source-binding rejection, state isolation, original/
probe scanout differences, probe footprint, native/model differences and original/
native composition differences. Invalid evidence exits 2 without a final report;
valid diagnostic differences write the report and exit 1. Existing output is
preserved. Capture diagnostics remain under scratch on failure.

## Reproduce and review

Use the existing pinned Python/Pillow environment and previously recorded local
original media, continuation archive, profile and prepared banner. Choose fresh
output names:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet restore tools/banner-blend-check/BannerBlendCheck.csproj --locked-mode
dotnet build tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-restore
python tools/verification/compare_banner_hide.py --plan scratch/banner-033-comparison-plan/plan.json --profile scratch/retroarch-reference --output scratch/banner-hide-c
python -m unittest discover -s tools/verification -p 'test_*.py'
```

Final runs `scratch/banner-hide-c` and `-d` reproduce all five probe PNGs/exit states,
background RGBA, actual C# RGBA/receipt and review images/page byte-for-byte.
Reports retain distinct paths/log identities; metrics, source frame bindings and
state-isolation records match. The tool fingerprints its sources and input metadata
before/after capture, preserving base/installed configuration identities.

Each `review.html` shows original, controlled background and actual C# for the five
displayed anchors at 2x. Agent inspection records matching original/C# views as the
lines disappear, including the small tail and empty final frame. The owner confirmed "The hide-frame gallery looks correct" on 2026-10-24.
This approval covers the five displayed hide anchors; earlier reveal/loading,
physical Circle input and event identity remain open.
All original media, decoded state, backgrounds and generated captures stay ignored.

Validation: locked restores, zero-warning Release builds, **1,450 .NET tests**
(unchanged count) and **116 Python tests** (12 new authored stack/buffer/crop/
scanout/receipt/empty-frame/guard cases) pass. No runtime rule or dependency changes;
the developer host gains only its bounded optional sequence mode. No automatic commit.

Five additional actual CPU-host negative checks reject duplicate plan fields,
out-of-order checkpoints, a wrong background digest, nonopaque background data and
an existing output directory. The captured native files remain unchanged.

This closes the selected hide-pixel/framebuffer-correspondence gap. Full early
reveal, loading duration, physical Circle input and original event identity still
need earlier original anchors. There is no general PS1 rasterizer, managed scene
rendering or universal frame-lag claim; ZOD-033 remains partial. Deferred ZOD-079
does not become the active task.

Follow-up: the earlier anchor now supports a separate
[early reveal/readiness diagnostic](early-banner-reveal.md). Two partial-reveal
color differences remain open; this does not change the exact hide results or
the owner's approval of this gallery.
