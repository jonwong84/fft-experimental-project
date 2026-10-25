# Selected original DarkScreen comparison (ZOD-095)

On 2026-10-25, thirteen selected original event-4 scanouts match the actual
prepared C# CPU compositor at **zero tolerance**. The clean read-only reference
is `11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base is `e5b130d`.
[Metadata](../evidence/events/original-dark-screen-v1.json) records private input,
tool, assembly, plan, capture and report identities. Original data and generated
media remain under ignored `scratch/`.

Review `scratch/dark-screen-095-f/review.html`: each row shows the original,
controlled background and actual C# composition at 2× nearest-neighbor scale.
Owner approval is **pending**. The eleven nonempty rows cover early and large
entry tiles, overlapping/clipped tiles, the held full-screen flash, release and
the 24-pixel late tail. Entry tick 2 and release tick 213 have zero coverage;
they validate selected empty geometry/history, not visible rasterization.

## Binding and scope

The original event archive uniquely matches live event 4 after the four-byte
loader marker. Command 794 admits the selected six-byte origin-style DarkScreen;
the parent waits at 801 during entry. RemoveDarkScreen at 816 leads to the
class-54 wait at 817 during release. Fourteen resident functions, frame/VSync
code, selected parameters, stack, saved animation ordinal, shared RGB, complete
288-cell counters, packet order/bank/material, OT and framebuffer environment
are checked for every queued reference. The holding reference retains the
post-Ready parent wait at 813.

Selected logical ticks are `2,6,14,26,42,46,163,165,167,171,179,211,213`.
The counter-derived native birth epoch is 8909, and release epoch is 9073,
so the selected release epoch is logical 164. Complete counter arrays and the
done latch identify body ordinals; clamped arrays alone cannot distinguish the
last two body yields. Each output has the same polarity and binds the queued
source submission two native game frames earlier through the checked VSync
return chain and double-buffer environments. No pixel alignment search occurs;
the established 12-column screenshot crop remains fixed.

The C# package uses verified original delay/rotation arithmetic with **authored
hold 108**, placing release at 164. This synchronizes selected snapshots; C# does
not execute the original script, loading, emulator or saved state. It consumes
only prepared files and explicit opaque RGB5 backgrounds, advances the pure
executor to accepted snapshots, and calls `DarkScreenPresentation.Project` and
`Compose`. All **2,823** selected packet geometries, colors and submission order
per run match actual C# output. Production code in `src/` is unchanged.

## Controlled backgrounds and isolation

Each control starts from a copied queued source state. Only the selected packet
RGB bytes become black, and its selected E1 material changes average blending
(`0xe1000006`) to additive (`0xe1000026`). Additive black preserves destination
RGB5. Geometry, packet headers/opcodes/links, counters, original instructions,
palettes, inputs and timing fields are unchanged at input. Keeping the complete
polygon workload avoids the invalid timing effects of the earlier degenerate
geometry probes.

After every paired continuation, all CPU bytes (including COP0 TAR), other
subsystems, non-RAM MAIN records and GPU registers match exactly. Main RAM may
differ only in the selected RGB bytes and E1 byte; textures/palettes and all VRAM
outside the two framebuffer surfaces remain exact. Saved front-buffer pixels
must equal each screenshot crop. Original/control pixels outside the selected
geometric footprint must match. These checks preserve the inherited map scene;
they do not validate C# map rendering.

Earlier finite selection/probe runs are retained and excluded from the final
proof. Some frontend endpoints were mid-body or had the wrong submission
distance. Collapsing XY produced a historical COP0 TAR difference in an entry
pair, a VSync bookkeeping difference in the hold pair, and wider CPU/subsystem
differences in a release pair. A pinned emulator CPU source study investigated
TAR, but **the final procedure allows no CPU exception**. The material-control
pilot and both final runs passed the original strict isolation requirements.

## Reproduction and validation

The private finite plan is `scratch/dark-screen-095-plan/plan-v5.json`.
It records the thirteen source identities, profile, prepared files and archive.
The runner verifies those identities and clean pinned reference files, preflights
all source contexts, then writes an execution plan before launching at most
26 original processes. Each continuation has a selected budget of four or five
frontend frames, a 55-second timeout, disconnected controllers and muted audio.
These budgets select output observations; **they are not original durations**.
Source states and installed RetroArch configuration are checked unchanged.

```powershell
dotnet restore tools/banner-blend-check/BannerBlendCheck.csproj --locked-mode
dotnet build tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-restore
python tools/verification/compare_dark_screen_reference.py --plan scratch/dark-screen-095-plan/plan-v5.json --reference-root C:/repositories/fft_decomp --output scratch/dark-screen-095-new
python -m unittest discover -s tools/verification -p "test_*.py"
```

Use a fresh output directory and the preserved identified inputs. The comparison
is developer evidence tooling; the running game has no original-data or emulator
dependency. The Python unit suite uses authored fixtures and needs no game media.

Final runs `scratch/dark-screen-095-e` and `scratch/dark-screen-095-f` both pass:

- Thirteen full 256×240 canvases, **798,720 pixels per run**, match actual C#;
  independent scanline composition also matches exactly. The eleven nonempty
  anchors cover **446,578** visible samples, including edge/overlap cases.
- All comparisons have zero changed pixels, zero channel delta and zero
  original/control changes outside the selected footprint. Coverage agrees at
  these anchors; arbitrary geometry and other DarkScreen styles remain unproven.
- **118 artifacts** repeat byte-for-byte: 39 review PNGs, 27 native output files,
  13 background buffers, 13 control input states and 26 original/control exit
  states. Path-bearing reports/configuration are not claimed byte-identical.
- Nine developer-host negatives reject existing output, missing background,
  wrong tick/hash, unknown field, non-RGB5/alpha, wrong hold and missing package.
  No new output is created; an existing marker survives.
- Locked solution/tool restores and zero-warning Release builds pass. All
  **2,031 .NET / 255 Python tests** pass; 15 Python checks are new. This ticket
  adds no managed unit cases and performs no new native GPU readback.

The selected entry/hold/release scanouts now have exact original/emulator pixel
evidence. Birth, phase-transition and final body/exit/parent endpoints, full
original animation, loading/input/display duration, original EventEnd/player
ownership, hardware fidelity and combined startup remain open. ZOD-094's native
CPU/GPU proof remains separate. After owner review, ZOD-096 specifies composition
of DarkScreen with the existing authored banner/Ready introduction and battle
handoff before changing that integration.
