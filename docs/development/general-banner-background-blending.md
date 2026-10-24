# General-banner background blending — bounded ZOD-033 check

2026-10-24. Managed entry `56300cc`, initially clean. Read-only FFT reference HEAD
freshly verified: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

The actual C# five-bit compositor matches **all 3,128 affected original pixels**,
including the **1,551 background-sensitive samples** excluded by the earlier
foreground comparison. Tolerance is zero. A controlled palette-transparent copy
of the original state supplies the background; its game state and all other
visible pixels match the unmodified continuation. This validates one full-visible
banner's blend/transparent-texel/coverage behavior, not general PS1 rendering.

## Controlled original background

The earlier [continuation](original-banner-continuation.md) provides the verified
state at reveal counter 18, its exact software emulator/profile, and repeatable
24-frontend-frame restores reaching hold counter 9 (logical checkpoint 29).
That state already has the loaded banner texture and palette. No earlier save,
new manual input, original code patch or guessed pre-banner background is used.

Pinned `src/event/require/screen_support.c::conditions_load_banner_image` uploads
the BONUS image and palette, then yields and frees its allocation. Its palette
layout has four 16-color palettes per VRAM row. The ZOD-077 bank-4 selection and
all twelve initialized RAM quad packets (both parity banks) agree on CLUT
`0x3fd8`: X=`(clut & 63)*16`=384, Y=`clut>>6`=255. The selected 32 VRAM bytes at
offset `0x7fb00` exactly equal the pinned prepared `palette-4.bin` before editing.
The [draw-state reconstruction](five-bit-general-banner.md) also binds the six
full quads and their preceding dither-off command to this original checkpoint.

The [optional verifier](../../tools/verification/compare_banner_background.py)
creates a new scratch-only compressed state in which just these sixteen palette
words are zero. Source word zero discards a textured sample. The original palette's
first word is already zero, so exactly **30 uncompressed bytes change** inside
the 32-byte selected range. No code, RAM, task, texture indices or other palette
is edited. Every other byte of the decompressed state wrapper/core remains intact,
including the old thumbnail; that retained thumbnail is not a new probe image.

The existing bounded RZIP/RASTATE/labeled-state reader first validates the whole
input. Replacement requires unique, exact VRAM and core payload locations and
round-trip verification after recompression. This is specific developer tooling
for the pinned state, not a general save editor or game feature. The original state
and owner files are never overwritten. The control is explicitly labeled an
**intentionally altered palette probe**, not an unmodified retail capture.

Separate finite processes restore the original and probe with identical frontend
budgets, disconnected controllers, null input requested, no patches/SRAM writes,
and private scratch state/save/options paths. Private auto-save captures each exit
state; base/installed configurations stay unchanged. Both still have the same
six full-visible packets, Holding phase, reveal=20/hold=9/hide=0. The probe palette
must remain zero; a later upload or different task anchor rejects this comparison.

## Isolation and exact comparison

At the exit anchor, **all 28 non-GPU serialized subsystems match byte-for-byte**,
including main RAM/CPU and the remaining emulated devices. Every GPU field except
the VRAM array also matches exactly. Main RAM SHA-256 is
`d4f68a333c97c5030ea75124f773a6cf583d802c238b0c520800da8a5fde5ab7`.
The counterfactual therefore preserves much more than just the banner's counters
at this selected checkpoint. It is not a general claim that arbitrary palette
changes cannot affect an original game.

The already reviewed raw 280x240 buffer mapping removes exactly twelve black
columns on either side, with no alignment search, scaling or color adjustment.
All **58,312 pixels outside the banner's nonzero-texel sample footprint** are
identical between the original and probe. Exactly 3,128 pixels change inside that
footprint. No other visible object using the selected palette is changed here;
other scenes/offscreen palette users remain outside this check.

The transparent-palette image supplies an opaque, zero-padded RGB5 background.
The new pure `FiveBitGeneralBannerPresentation.ComposeOver` accepts that explicit
256x240 RGBA8 canvas, checks opaque alpha and zero low RGB bits, and composes using
the existing five-bit material/gradient/dither rules. It never silently quantizes
an RGB8 background. Caller memory is not modified or retained. Draw state and
prepared artwork validation remain explicit, and simulation/resource lifetimes
remain independent of composition.

The standalone [CPU evidence host](../../tools/banner-blend-check/Program.cs)
loads the prepared files, replays the actual managed banner to checkpoint 29,
supplies explicit dither-off/origin-(0,0) draw states, and writes raw RGBA plus a
separate `fft.banner-background-cpu-check` v1 receipt. It runs without a game window
or GPU readback and does not read the BIN, BIOS, reference checkout or state.
The Python verifier then compares this **actual C# output**, not just a model,
against the unmodified original, the controlled background and the independent
Python arithmetic transcription.

- Original versus C#: 3,128/3,128 affected samples exact.
- Background-sensitive subset: 1,551/1,551 exact; no exclusions relaxed.
- Earlier foreground subset: 1,577/1,577 exact.
- Whole 256x240 canvas: 61,440/61,440 exact, including 58,312 inherited pixels.
- C# versus Python model: exact over the whole canvas.

The inherited background is original emulator output, not managed map rendering.
Whole-image equality therefore does not validate a managed battle scene. The
sample footprint also excludes texture-word-zero holes; their background stays
unchanged. Coverage evidence applies only to these full, axis-aligned, native-size
quads. Arbitrary triangles, partial-gradient rasterization and other scenes are
not established. No fidelity tolerance is selected or widened.

Failure categories remain separate: `state-isolation`, `probe-footprint`,
`native-model-mismatch` and `original-composition-mismatch`. Valid differences
write diagnostics and exit 1; invalid/unavailable inputs exit 2, retain capture
diagnostics and publish no final report. Existing output is rejected before any
original/emulator access. Exit 0 means this controlled comparison agrees;
`originalFidelityEstablished` remains false.

## Repeat and inspect

Use the existing Python 3.12.14/Pillow 12.3.0 environment; no new Python package is
needed. The standalone .NET developer project references the existing Client and
has its own committed lock file using the same package versions. It is outside
the main solution and adds no game/runtime dependency or original-format reader.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet restore tools/banner-blend-check/BannerBlendCheck.csproj --locked-mode
dotnet build tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-restore
python tools/verification/compare_banner_background.py --plan scratch/banner-033-comparison-plan/plan.json --profile scratch/retroarch-reference --output scratch/banner-background-c
python -m unittest discover -s tools/verification -p 'test_*.py'
```

Choose a fresh output root; demonstrated runs already exist. Final runs
`scratch/banner-background-c` and `-d` repeat the original/probe PNGs and exit
states, generated probe state, background RGBA, actual C# RGBA/receipt and review
page byte-for-byte. Reports have different capture paths/log identities; comparison
metrics and state-isolation records are identical. Source/tool/input identities and
the repeat audit are retained in [metadata](../evidence/visual/general-banner-background-blending-v1.json).
All media and generated artifacts remain ignored under scratch.

Each `review.html` shows the unmodified original, the controlled background and
actual C# result side-by-side at 2x nearest-neighbor display. Agent inspection finds
the original and C# views visually identical, including text shading/shadows, while
the middle view removes both banner lines with the battle intact. The owner
accepted this comparison on 2026-10-24: “The original and C# views look identical
to me.” This visual approval is specific to the displayed full-visible comparison;
the remaining timing, scene-rendering and event-reachability gaps stay open.

The existing default checker API/policy and preview options remain unchanged.
A fresh five-bit dither-off Windows check under
`scratch/banner-background-checker-regression` passes CPU/GPU readback and matches
all 16 historical `scratch/banner-five-bit-a` files byte-for-byte. The explicit
background path itself is validated through the CPU host, not a new GPU claim.

Locked restores, zero-warning Release builds, **1,450 .NET tests** (14 new Client
background cases) and **104 Python tests** (12 new authored probe/receipt/model/
isolation/guard cases) pass. Core/Engine, event admission, asset/save schemas,
existing package lock files and the main solution remain unchanged. No commit.

Next: establish screenshot/framebuffer correspondence for partial reveal/hide pixel
comparisons, or obtain a pre-banner anchor for early reveal/loading/input checks.
Physical Circle input and original event reachability remain open. ZOD-033 stays
partial; full scene integration and deferred ZOD-079 remain separate.

Follow-up: [displayed hide comparisons](displayed-banner-hide.md) now establish
the selected pre-submission/front-buffer correspondence and compare the partial
crops and fully hidden anchor. The full-visible control above remains historical
evidence with its original identities; early reveal/loading/input and wider scene
rendering remain open.
