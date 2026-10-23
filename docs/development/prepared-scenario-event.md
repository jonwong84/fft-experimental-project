# Prepared conditional scenario event (ZOD-064)

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`.
Read-only reference HEAD: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

This milestone decodes an authored scenario block and loads a validated prepared
event without the original bytes. It does **not execute events**, import a retail
event archive, or make an original campaign playable. The selected event and
semantic expectations come from the [ZOD-040 investigation](../architecture/zod-040-event-script-investigation.md).
The evidence is source-derived and synthetic; no C executable, emulator, original
event, EntryEdit binary or independent semantic oracle was run for this milestone.

## Boundaries and API

- [ScenarioBlockInspector](../../src/Fft.AssetTool/ScenarioBlockInspector.cs) owns
  original block framing and operand metadata in AssetTool. It retains an immutable
  copy, SHA-256, instruction offsets/raw operands and inspection diagnostics.
- [ScenarioEventExporter.CreateSynthetic](../../src/Fft.AssetTool.Cli/ScenarioEventExporter.cs)
  composes inspection with the closed capability gate. It re-inspects retained
  bytes, resolves legal targets and constructs the prepared definition. Forged
  inspection records cannot bypass validation. This is an API bridge, with no new
  command-line switch. The caller supplies the scene; it is not encoded by the block.
- [PreparedScenarioEvent](../../src/Fft.Content/Events/PreparedScenarioEvent.cs)
  owns immutable prepared records, strict JSON validation and hash-verified
  `LooseAssetStore` loading in Content. No original parser enters Content/Client.
  No project reference, package lock, save schema or Zodiac API changes.

For an **authored synthetic** block, the preparation flow is:

```csharp
var inspection = ScenarioBlockInspector.Inspect(block, ScenarioBlockEncoding.Raw);
var definition = ScenarioEventExporter.CreateSynthetic(inspection,
    new AssetId("sample:event/conditional-outcome-v1"), ScenarioEventScene.Battle);
byte[] json = definition.ToUtf8();
```

Write that JSON and its ID/length/SHA-256 entry into the existing prepared manifest
under an ignored `scratch/` output root. Runtime loads with
`PreparedScenarioEvent.Load(store, id)`. The end-to-end tests create a fresh
manifest, discard the block, load the prepared file and detect subsequent corruption.
Do not label original imported data synthetic: this exporter supports authored
fixtures only. A future retail import must verify edition/archive identity and
provide a separate provenance/capability contract.

ZOD-065 must project this Content definition into an FFT Core-owned execution
definition, following the existing Content-to-Core direction. Core must not acquire
a Content dependency. That ticket owns variables, instruction position, arithmetic
limits, ordered traces, budget failures and pure execution; none exist here yet.

## Framing and structural inspection

The pinned [block header](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_instruction.h)
describes an 8,192-byte block. A raw block starts with a little-endian u32 text
offset relative to its base. Inspection requires an exact block extent and a text
offset from 4 through 8,191. Instructions begin at offset 4; the header is metadata.
The [loader](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/resources.c)
preserves the text pointer and replaces that header with `F2 F2 F2 F2`.
Processed inspection therefore requires both that marker and an explicitly
preserved text offset; the tool never guesses a lost boundary. It omits the four
leading NOPs the original processed interpreter traverses.

The 244 operand byte counts (`00` through `F3`) are protocol metadata traced from
[control.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c).
Their byte-array SHA-256 is
`5f50d72e0e4d2a62215be25bc01f9123c0727a2abdcb9a4ff038511c569d915f`;
tests pin that separately from the conversion subset. Unknown widths (`F4`–`FF`)
and truncated operands stop inspection with an offset diagnostic and preserved
bytes/prefix. Known unsupported operations remain inspectable with exact operands.
`Complete` means a structural walk reached the first `DB` sentinel before text,
not that the block is executable. Bytes after that sentinel, padding and text remain
opaque in the retained block. There is no full text decoder or full event inventory.

## Prepared capability gate

The fixed format is `fft.prepared-scenario-event`, schema 1, dialect
`fft.ps1-us:scenario-bytecode`, profile `sample:event-profile/conditional-v1`.
It carries a stable ID, explicit Battle/World scene, synthetic provenance,
edition/reference/block hash/encoding/text boundary, and semantic records with
block-relative offsets, validated operands and resolved jump landings. The JSON
contains no raw block or text. Both scenes share this selected structural profile;
that does not validate either scene's lifecycle.

The nine executable capabilities are `F2` NOP, `BE` zero variable, `B0` add
immediate, `B1` add variable, `A2` compare equal, `D0` conditional forward jump,
`D1` forward jump, `D2` target and `D4` target/exit. Variable indices are restricted
to 0..3; immediate operands retain their unsigned 16-bit encoding. This gate does
not establish runtime overflow semantics. `DB` is a required final **search
sentinel**, with no executable EventEnd semantics.

Source [battle](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c)
and [world](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/event/interpreter.c)
dispatchers and `script_jump_target_find` establish these distinctions:

- D0 accepts the first matching D2 **or D4** ahead; D1 accepts D2 only.
- Scanning follows instruction widths, skips operand bytes and stops at DB.
- Jumps land **after** the matching marker. Landing after D4 differs from executing
  D4 sequentially, which exits the selected thread/profile.
- Missing targets and any reachable DB/missing-code path reject preparation.
  Reachability checks both D0 branches conservatively, without variable inference.

Every decoded instruction must belong to the subset, including unreachable code.
Waits, packed/global variables, RNG, dialogue, concurrent tasks, unit manipulation,
scene changes, full EventEnd and all other opcodes reject executable preparation.
Inspection is still available. Contiguous offsets, encoded sizes, one final DB,
operand shapes and exact first-target landings are revalidated when loading JSON.
Unknown/duplicate/missing/null fields, numeric/unknown enum values, invalid
provenance and schema/profile mismatches fail. Input is bounded to 1 MiB, JSON
depth 12 and at most 8,188 records, within the preserved source text boundary.

## Fixtures and verification

The [fixture provenance](../fixtures/events/README.md) records the independently
authored conditional event, raw-block hash and minimal packed text section. Its
15 records occupy offsets 4..47, text starts at 48, and the expected jumps are
21 -> 35 and 31 -> 45. Existing source-derived true/false state traces remain in
ZOD-040; execution is deliberately left to ZOD-065.

Regression coverage lives in `ScenarioBlockInspectorTests`,
`ScenarioEventExporterTests` and `PreparedScenarioEventTests`. It checks raw and
processed framing, copy immutability, all operand widths, truncation/unknown data,
unsupported unreachable code, forged inspections, marker bytes inside operands,
duplicate/absent/DB-bounded targets, D0/D1/D4 differences, CFG rejection, strict
JSON and manifest-only loading/corruption. These are source-derived structural
checks and synthetic managed expectations, not original-game equivalence.

Full validation commands from the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Locked restore passed, the Release build reported zero warnings/errors, and all
**1,012 tests** passed (Engine 24, Core 293, Content 330, AssetTool 164, CLI 103,
Client 98). This adds 58 decoder/prepared-file cases to the previous 954 tests.
A separate read-only agent review found no actionable correctness issues.
Original activation/completion remains ZOD-066; general tools/authoring and retail
round-trips remain separate. Do not infer complete scripting, save integration or
campaign support from this decoder.
