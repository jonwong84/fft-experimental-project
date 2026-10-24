# ZOD-026 — Independent attack-image decoder reference

Date: 2026-10-23. Managed baseline: `9012e42`.
Read-only decomp HEAD rechecked: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

**The original SCUS-94221 decompression instructions independently confirm the
existing Chocobo attack-image output digest.** This completes the bounded decoder
oracle branch of ZOD-026. It does not establish a rendered pose, palette, camera,
game timing, or a full-game emulator capture. Original visual comparison remains
an input needed for ZOD-033's original-scene checks.

## Inputs, source and execution

The existing local `scratch/all-data-extraction/BATTLE.BIN` is 1,397,096 bytes,
SHA-256 `7af579c7bcfd46f21f8d6886acb6aae156474dace667553e76079be7c7d6db94`.
`BATTLE/CYOKO.SPR` is 49,572 bytes, SHA-256
`7f7a94e5615137f9a4fa6e1dcfbad8b2760a6b66099154171b0e1feb137632d2`.
These match the previously verified SCUS-94221 extraction identities; no new
edition is inferred from filenames. Only the 12,196-byte tail at SPR offset
`0x9200` is passed to the routine. No BIN, executable code, decoded data or
disassembly is committed.

At this reference revision, `target/battle.yaml` maps the raw BATTLE module to
load address `0x80067000`, with no PS-X EXE header adjustment:

- `compressed_nibble_read`, address `0x8007a9b0`, 132 bytes, SHA-256
  `ccef6d3b68664059ae4cfb8d47b0a454f0f852b35b42895cffcfbbdd139857d7`.
- `sprite_attack_image_decompress`, address `0x8007aa34`, 412 bytes, SHA-256
  `caae250e7e6322ea9aec236b0eaf26768679408a6ef0920fef7a1d98a4349bd3`.
- Their current source is `src/battle/units/sp2.c`; the historical fixture's old
  `battle_scene.c`/`sprite_decompress_attack_image` names remain historical.
  `src/battle/units/evtchr.c::sprite_evtchr_load_unit_attack_image` calls the
  decoder on a sheet slot's compressed image before `LoadImage`. The harness
  executes only the decoder/helper, not that caller's GPU/upload lifecycle.
- The three original cursor words are at `0x800962c8`, `0x800962cc`, `0x800962d0`.
  The routine initializes them itself. The helper reads high nibble first;
  the original implementation packs its first output pixel high as well.

The optional [developer harness](../../tools/verification/verify_sprite_attack.py)
maps 2 MiB RAM, copies the **original binary overlay bytes unchanged** to their
linked address, puts the compressed tail at `0x80010000`, and supplies output at
`0x80020000`, stack `0x801ff000`, return sentinel `0x80040000` through the normal
argument/stack/return registers. It performs no source translation or C execution.
It uses existing Unicorn 2.1.4 dependencies from the BIOS investigation and Python
3.12.14; no package installation or runtime project change was needed.

Code hooks require every executed instruction to stay in the two pinned routines,
with a two-million-instruction budget. They inspect effective addresses of the
reviewed `lw/lbu/sw/sb` instructions before execution, rejecting other memory
opcodes or access outside the source, output, three globals and controlled stack.
Locally, Unicorn memory read/write hooks caused a CPU exception on this path even
with a no-op callback; the final harness uses code-hook inspection instead. The
counters therefore describe inspected instructions/accesses, not hardware bus
transactions. Independent final checks require return to the sentinel, every
output byte addressed by a store, unchanged input and intact output canaries.

Observed result: **1,210,937 instructions**, 24,392 source-byte reads (two nibble
reads per source byte), source high-water 12,196 bytes, 51,200 output stores.
Final cursor phase is 0 and byte offset is 12,196. No input overread or output
overrun was observed. The 25,600-byte output SHA-256 is
`d07fff4e49bf5e0a8b91343b2a722521ac5309e34ec23d2d5810e579a741c24f`.
It agrees with the separately executed managed verifier and the historical
[source-derived fixture](../fixtures/chocobo-attack-image.json).

## Reproduction and evidence retention

Use Python 3.12+ and the existing pinned developer dependencies. If they are not
already available, the optional dependency setup is documented in
[the BIOS oracle guide](../behavior/bios-rng-verification.md); it is never part of
game installation or ordinary .NET tests.

```powershell
python tools/verification/verify_sprite_attack.py --battle scratch/all-data-extraction/BATTLE.BIN --sprite scratch/all-data-extraction/BATTLE/CYOKO.SPR --dependencies scratch/bios-analysis/deps --output scratch/behavior/chocobo-attack-oracle/new-run
dotnet run --project src/Fft.AssetTool.Cli -c Release --no-build --no-restore -- verify-sprite --content-root scratch/prepared-chocobo-content
```

The capture root must be fresh and below this repository's ignored scratch.
It contains `decoded.bin` and `capture.json`. Full input hashes, routine ranges,
tool versions, harness/fixture hashes, scalar results and explicit null
palette/frame/camera/timing fields are retained. No such presentation field applies
to isolated decompressed-index bytes; null is not an invented observation.

[Committed capture metadata](../evidence/sprites/chocobo-attack-executable-v1.json)
uses ZOD-002's capture identity/path/hash/length/tool/procedure/edition fields in a
separately named evidence format. It is not a new movement fixture or an automatic
upgrade of the historical fixture's `oracleKind`. Capture-dependent checks remain
optional developer work; ordinary tests do not load these artifacts.

Two final runs under `scratch/behavior/chocobo-attack-oracle/c` and `/d` produced
byte-identical decoded files and reports. Report SHA-256:
`17b96b4213c1c6fde535049e1137edae328ca09daee2a5cf98ad84cbe2a2b997`.
Harness SHA-256:
`d898e777b386f2d236e596dc63a6f5c7114630794aa86d6f93a121f0a38c5d24`.
A same-size wrong-hash sprite rejected before creating output; reusing an output
root rejected without changing the valid report. The managed CLI verifier passed
again. No .NET code changed and the complete 1,388-test suite was not rerun for
this developer-tool/documentation milestone. No automatic commit was made.

## Limits and next capture

Evidence class: **executable-reference**, using actual original instruction bytes
under an architectural MIPS32 4KC emulator. It does not prove cycle-accurate
R3000A/load-delay behavior, GPU precision, palettes, frame selection, camera,
input/scheduler timing, malformed stream behavior, all SPR variants or a booted
game. Other initial RAM patterns and other input streams are outside this capture.
The existing managed truncation guard remains a documented difference from the
original routine's unchecked input boundary.

No full-game emulator was found on PATH or in the checked immediate Program Files,
Local AppData or WinGet-package locations. This bounded check is not a machine-wide
absence claim. A game-ready state, movie/input log and matched original screenshot
were not found in the inspected project artifacts. BIN and BIOS availability is
not the blocker. A subsequent visual capture needs an explicitly selected emulator
and version/settings, actual encounter/state, recorded inputs and a stable frame
boundary. For the held Chocobo pose, record sheet 134, palette, SHP frame, SEQ phase,
camera direction, scale and overlays as specified by ZOD-027. For the general banner,
record event-401 reachability, display state, Circle samples and readiness-relative
ticks; the modern RGB8 preview policy must remain visible in comparison reports.
