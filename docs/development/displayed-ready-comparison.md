# ZOD-087 — Displayed Ready comparison

The actual prepared C# `ReadyPresentation.ComposeOver` matches six selected
original-emulator Ready scanouts exactly. The runtime renderer and deterministic
simulation are unchanged. This is a bounded pixel comparison, with an explicit
missing birth observation; it does not establish original startup/input duration,
hardware equivalence, map rendering or full event playback.

Reference checkout: `C:/repositories/fft_decomp`, read-only, clean, HEAD
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Managed base:
`70629be2e6f0bba0e353ee2553f31f7554de401c`. The
[metadata receipt](../evidence/visual/displayed-ready-v1.json) binds local artifacts,
tools, source selections and results without redistributing original bytes.
The [ZOD-083 trace](../architecture/zod-083-ready-display.md),
[ZOD-085 policy](../architecture/zod-085-ready-rendering.md) and
[ZOD-086 implementation](prepared-ready-event.md) retain their historical claims.

## Reference selection and displayed binding

The two original states retained by ZOD-083 belong to independent histories.
They establish queued scales 48/128, but cannot be paired to infer a displayed
sequence. This check starts each finite continuation from the same hash-bound
Ready birth state at source frame 9003, event 4 command 810 / task wait 813,
dispatcher slot 3 and child slot 4. The other history is not used for display lag.

The verifier checks original archive/REQUIRE/BONUS/BATTLE identities; resident
event/frame/VSync/thread/Ready function hashes; selected table bytes; six glyph
rectangles and texture nibbles; palette bank 2; exact four-corner scaled colors,
UV/XY, CLUT `0x3f9a`, page 6, reverse 5..0 DMA order and the preceding validated
`E1000006` mode writer. Unknown command payloads invalidate mode tracking.
Texture window, masks and dithering are explicitly disabled. Pending draw/display
environments, polarity and the actual GPU front/back origins must agree.

`src/battle/frame.c::battle_frame_submit` waits in VSync before PutDispEnv,
PutDrawEnv and DrawOTag; `src/battle/entry.c` toggles polarity and increments
the frame counter before submission. Saved main-stack return chains distinguish
these pre-submission checkpoints. A displayed image binds to a checked submission
two native counters earlier, with the same polarity and destination framebuffer,
within the same restoration epoch. Frontend frame budgets select checkpoints;
they are not treated as simulation ticks or a universal frame-rate conversion.

Worker checks include the native yield delay slots: saved s3 contains the *next*
scale during fade-in/fade-out. During the full-bright hold, s3 instead carries
the bank's 2080-byte offset. Child exit pushes a separate 24-byte call frame,
whose saved return must be `801ca610`. The event remains at E5 wait 813 for the
selected terminal dispatcher checkpoint. A separate Ready event-identity checker
permits that source-supported gap without weakening the older live-task inspector.

## Finite capture and control procedure

`tools/verification/compare_ready_display.py` writes its closed plan before
launching any emulator process. Each original continuation uses an isolated copy
of the same archived state; every control uses a separate copy with **only** the
16 words of bank 2 at VRAM `(416,254)` changed to zero. The helper now accepts a
checked palette location; the existing general-banner entry point still selects
its original bank at `(384,255)`. No global palette setting is changed.

The existing verified `scratch/retroarch-reference-033-b` profile supplies
RetroArch 1.22.2 / installed Beetle PSX core, verified BIOS, cue/BIN, null audio,
raw screenshot settings and isolated state/save directories. Both controllers are
disconnected. Each process has a 55-second timeout, finite budget at most 140,
and SRAM load/save disabled. Source copies, effective core options, installed
normal configurations and original input hashes must remain unchanged. Tools,
compiled C# assemblies and the clean reference revision are pinned before capture
and checked again before publishing a report.

Each final run captures original frontend budgets
`18,22,26,122,126,130,134,136,138,140` and controls at
`22,26,126,134,138,140`. Original/control non-GPU subsystems and all GPU records
except VRAM must match exactly. Cropping uses the previously established fixed
12-column border mapping from 280x240 to 256x240, with no alignment search.
Both screenshots must equal their own saved-state VRAM scanout.

The optional C# developer host loads the existing prepared artwork and authored
hold-60 event, advances the real Ready aggregate with no Circle input and composes
accepted snapshots over the explicit control backgrounds. It reads no original
event, state, BIN, emulator or C source. Its strict plan validates every background
and hash before advancing or creating output. The game gains no dependency.

## Results and missing observations

The selected displayed logical ticks and scales are:

- 8: late fade-in, scale 112.
- 10: full-bright hold, scale 128.
- 60: release/fade-out reset, scale 128.
- 64: fade-out, scale 80.
- 66: final fade-out submission, scale 56.
- 67: extra empty yield, no glyph submissions.

All six full canvases are exactly equal at zero RGB tolerance, with opaque alpha
and zero-padded RGB5 checked separately. Each visible frame compares **1,202 opaque,
77 STP and 1,049 transparent glyph-rectangle samples**. All 77 STP pixels differ
from the controlled background, so the comparison exercises background-sensitive
blending. Pixels outside the 1,279 nontransparent footprint are unchanged by the
probe. The empty frame is fully equal and explicitly reports no visible-artwork
comparison. Inherited background pixels do not validate managed map rendering.

Displayed birth and the immediately following frame remain **unobserved by the
closed verifier**. Finite budgets 6/8 stop inside interrupt handling rather than
the supported main VSync stack; budget 7 also fails the same guard. Those endpoints
and failed preflight receipts remain under scratch. We do not infer their displayed
pixels from saved worker scales or extend the classifier by guessing an interrupt
return chain. ZOD-088 owns the bounded source/context trace for that gap.

The final procedure runs twice in fresh roots
`scratch/ready-displayed-087-d` and `scratch/ready-displayed-087-e`.
All **96 selected content artifacts** repeat byte-for-byte. The metadata receipt
records their identities; reports/config/command
files intentionally retain distinct output paths. Gallery:
`scratch/ready-displayed-087-e/review.html`, with original, controlled background
and actual C# side by side. The owner approved the original comparison on
2026-10-25: "Visually, they look identical to my eyes." This approval covers the
six supported gallery rows; the missing birth observations remain ZOD-088.

## Reproduction and validation

With the retained local inputs and prepared Ready roots from ZOD-086:

```powershell
dotnet restore tools/banner-blend-check/BannerBlendCheck.csproj --locked-mode
dotnet build tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-restore
python tools/verification/compare_ready_display.py --output scratch/ready-displayed-new
python -m unittest discover -s tools/verification -p 'test_*.py'
```

The optional verifier requires the established Windows reference environment and
Pillow. Normal .NET builds/tests require neither emulator nor original media.
Use a fresh output directory; never stage any scratch content.

Validation: locked restore, zero-warning Release solution and developer-host
builds; **1,794 .NET tests** and **179 Python tests** pass. Sixteen new authored
Python regressions cover worker phase boundaries, saved-scale/bank reuse, exit
stack/callers, dispatcher ambiguity, reverse DMA/material/mode checks, unknown
command invalidation, same-history two-buffer binding, palette patch isolation,
opaque/STP/empty sample semantics, exact failures, alpha/padding and truncated
canvases. All existing general-banner regressions pass. No production C# source,
rendering policy, event schema or simulation behavior changes in this ticket.
Five actual C# host checks also reject a bad last-background hash, wrong last
tick, duplicate field, nonopaque background and existing output directory while
preserving existing files and creating no partial result.

### ZOD-088 follow-up (2026-10-25)

The later [birth comparison](ready-birth-comparison.md) identifies the missing
endpoints as BIOS timer-2 sound callbacks and verifies their saved main VSync
context through actual exception save/restore code. The birth/next scanouts now
match actual prepared C# exactly in a separate gallery, approved by the owner on
2026-10-25: "That looks correct."
The earlier missing-observation record above is historical; the old main-context
classifier remains closed, and original duration/full event playback stays open.
