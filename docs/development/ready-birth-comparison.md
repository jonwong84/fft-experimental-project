# ZOD-088 — Ready birth through interrupted checkpoints

The two Ready birth scanouts missing from ZOD-087 now bind through verified BIOS
exception records and match the actual prepared C# compositor exactly. Displayed
logical ticks 0/1 use scales **48/56**. Each compares 1,202 opaque, 77 STP and 1,049
transparent glyph-rectangle pixels; all 77 STP pixels exercise background blending.
The renderer and simulation are unchanged. Owner review of the new two-row gallery
is pending: `scratch/ready-birth-088-b/review.html`.

Reference: read-only `C:/repositories/fft_decomp`, clean HEAD
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Managed base:
`4ec5ef00f03efe16ba7a27365f85fe3b31c3ed5f`. The
[metadata evidence](../evidence/visual/ready-birth-v1.json) contains identities and
scalar observations only. Original state/BIOS/executable bytes, offline decoded
instructions, assets and images remain ignored under scratch. This extends the
[ZOD-087 comparison](displayed-ready-comparison.md); its missing-observation
record and owner approval retain their historical meaning.

## What interrupted the two frames

The retained frontend-budget-6 checkpoint has live PC `80014630`, in
`src/lib/suzuki/tick.c::suzuki_sound_update_voice_attrs`, called by
`suzuki_sound_root_counter_2_handler` (return `800149f8`). Budget 8 has live PC
`80015370`, in `src/lib/suzuki/smd_interpreter.c::suzuki_smd_read_instructions`,
with return `80014d50` into that same root-counter handler. The function/source
bindings and native sizes come from `target/main.yaml`. Driver initialization in
`src/lib/suzuki/driver.c` installs this callback with
`OpenEvent(RCntCNT2, EvSpINT, EvMdINTR, ...)`.

Both are **BIOS event callbacks**, before the later libetc interrupt hook; they
are not the main VSync stack and are not classified by `trapIntr` merely because
an interrupt is active. The enabled event table record at `a000e124` is slot 9,
class `f2000002`, spec 2, mode `1000`, handler `800149dc`. Its callback stack
returns to BIOS event dispatch at `1c20`; the callback and dispatch frame sizes,
saved registers and return slots are traced from the actual instructions.

## Exception save and restore proof

The supplied, hash-verified SCPH-5501 BIOS and original `SCUS_942.21` executable
are study inputs only. Relevant resident sound functions match the original
executable's PS-X EXE load mapping. RAM save/restore, B0 dispatch and event-dispatch
blocks match verified BIOS ROM ranges exactly. The retail GTE exception prefix
at RAM `c80` matches the executable template at `8001dad0`; the installed card
jump at `6444` matches its executable template at `800288c4`. The observed card
gate is disabled (`75c0 == 0`), so its checked branch returns to the ordinary
exception-save body. The verifier hashes the actual patched prefix/gate as well
as the original save and restore bodies; it does not assume untouched BIOS code.

The decoded save path follows `RAM[108] → PCB a000e294 → TCB a000e29c`, then uses
TCB+8 (`a000e2a4`) as the register record. It stores SP at +`74`, RA at +`7c`,
resume PC at +`80`, status at +`8c` and cause at +`90`. BIOS B(17), whose dispatch
entry is checked, restores these registers and jumps to the saved PC with RFE.
The live k0 still points at this same save record. Saved status/cause/PC must agree
with live COP0; synchronous exceptions, branch-delay exceptions and unsupported
pipeline states are rejected.

For the two selected sites, the saved main context is SP **801fff40**, return
**8001dc64**, and PC **8001dd1c / 8001dd18**, respectively. The main stack returns
through `80093b68` (frame submission) and `8007a058` (battle entry). Those PCs lie
in the pinned VSync wait, before PutDispEnv / PutDrawEnv / DrawOTag. GPU origins,
polarity and pending draw/display environments must still agree with that phase.

`inspect_ready_interrupt.py` accepts only these two observed instruction sites,
their associated source ticks, native callback frames, enabled event, active
PCB/TCB and exact kernel/function fingerprints. It recovers selected saved fields;
it neither patches a state nor executes a BIOS instruction. The old main-context
entry point still rejects both IRQ checkpoints. Common stack/environment checks
and Ready identity/asset checks are factored for reuse, with no permissive fallback
or command-line bypass.

## Finite comparison procedure and results

`compare_ready_birth.py` first verifies both retained failures, their original
EXE/BIOS matches, the clean reference, prepared artwork and capture-profile inputs.
It writes a closed plan before launching any emulator. Each fresh run captures
original budgets **4/6/8** and transparent-bank-2 controls **6/8**, using separate
copies of the same archived Ready birth state at source frame 9003. Both controllers
are unplugged, audio is null, SRAM loading/saving is disabled, and each process has
a 55-second timeout. Normal configs, profile, tools/assemblies and source artifacts
are checked again before a report is published.

The original 6/8 output states repeat the retained interrupted states byte for
byte. Displayed birth at native frame 9005 binds to the checked birth submission
at 9003; displayed next at 9006 binds to the budget-4 submission at 9004. Binding
requires the same restoration epoch, counter distance 2, polarity and target
surface. Frontend budgets are selectors, not a universal timing conversion.

Only the 16 palette words of bank 2 at VRAM `(416,254)` differ in each control's
entry state. Original/control non-GPU subsystems and GPU registers remain equal.
Both raw screenshots equal their own saved VRAM scanout after the established
fixed 12-column crop; there is no alignment search. Original/control pixels
outside the nontransparent glyph footprint are unchanged.

The optional C# tool's separate `--ready-birth-sequence` plan accepts exactly
ticks 0/1, loads the existing authored hold-60 event and prepared artwork, and
composes actual accepted Ready snapshots over the explicit control backgrounds.
It reads no state, BIOS, original executable or C code. Both full 256x240 canvases
match exactly at zero tolerance, including opaque alpha and zero-padded RGB5.
No production C# code or schema changes are required.

Two final runs are retained at `scratch/ready-birth-088-a` and `-b`; all **32**
selected state/image/native-content artifacts repeat byte for byte. The metadata
records their identities. Owner approval of the earlier six-frame
gallery does not automatically approve these two new rows.

## Validation and remaining scope

- Locked solution/developer-host restores and zero-warning Release builds pass;
  **1,794 .NET tests** and **192 Python tests** pass.
- Thirteen new authored Python tests exercise both IRQ layouts, stale/wrong TCB
  pointers, saved/live COP0 disagreement, branch-delay/exception rejection, callback
  stack/event identity, missing code/fields, instruction/pipeline limits, independent
  main-stack/GPU checks, and CPU receipt identity/scale/order failures. No original
  instruction bytes or assets are test fixtures.
- All **16** retained ZOD-087 original/control snapshots reproduce their old
  context records; all **seven** C# output files from its six-anchor sequence
  remain byte-identical. The general-banner main-context tests remain unchanged.
- Five actual birth-host failures (wrong plan domain, last tick/hash, duplicate
  field and existing output) return failure while preserving output directories.

The combined evidence now covers eight selected Ready scanouts, including birth,
full brightness, fade-out and the empty yield. This is not a general interrupt
unwinder, original duration/physical Circle or USB/display latency result, hardware
GPU equivalence, audio emulation validation, managed map-rendering proof, or full
original event/startup playback. Owner review comes next; additional behavior or
scene expansion requires its own bounded source/observation scope.

To repeat with retained inputs and fresh output:

```powershell
dotnet restore tools/banner-blend-check/BannerBlendCheck.csproj --locked-mode
dotnet build tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-restore
python tools/verification/compare_ready_birth.py --output scratch/ready-birth-new
python -m unittest discover -s tools/verification -p 'test_*.py'
```

The optional reference procedure uses the established Windows/Pillow environment;
the normal managed application and test suite gain no BIOS/emulator dependency.
