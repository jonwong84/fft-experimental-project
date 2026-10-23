# ZOD-040 — Event scripts and bounded interpreter recommendation

Date: 2026-10-23. Target: North American PS1 `SCUS-94221`.
Read-only reference: `C:\repositories\fft_decomp`, independently confirmed HEAD
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

**Start with an offline scenario-bytecode decoder and a pure, single-thread
variable/branch interpreter over a hand-written synthetic event.** Keep both FFT-owned.
Do not port the cooperative PS1 scheduler, add a general scripting language, or
interpret presentation instructions as battle commands in this first slice.
Original campaign event selection and completion remain later work.

This is source-derived research and a synthetic design checkpoint. No original
event bytecode, dialogue or media is included. No original executable, C routine,
emulator, editor binary or original-game comparison was executed. Reading a tool's
source establishes an interface candidate, not a demonstrated interoperable workflow.

## Distinct script families

The following paths and symbols are all at the pinned decomp revision. Links use
that revision rather than moving line numbers.

- **Battle and story scene events:**
  [script_instruction.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_instruction.h)
  defines `event_file_block_t`, `event_opcode_e` and operand layouts.
  [BATTLE `script_execute_event`](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c)
  and [WORLD `script_execute_event`](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/event/interpreter.c)
  interpret the same byte-oriented family. Story cutscenes are not a separately
  established language. A scene context matters: WORLD's `WALK_TO` does nothing
  outside a block, `ARROW` only resolves units, and map/light behavior differs.
  A common opcode number does not establish identical semantics in both scenes.
- **Battle scenario conditions:**
  [scenario.c::scenario_conditions_run / condition_check](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/scenario.c)
  walks condition chains encoded as 16-bit type plus a type-specific number of
  16-bit operands. It inspects the first ten of sixteen chain offsets, stops a
  chain at its first failing condition, and selects an event through `RUN_EVENT`.
  Unit presence/HP, party identity, funds, date and battle-outcome dependencies
  belong here. These are selectors for events, not scenario bytecode opcodes.
- **World-map location conditions/actions:**
  [location_script.c::location_script_run](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/map/location_script.c)
  uses a different 16-bit instruction stream, offset lists and a 41-entry dispatch
  table. A block runs until a condition fails or an action is staged; the first
  staged action matching `action_mask` wins. Requests include event load, choice,
  text, variable changes and visible paths/locations. Do not decode it using the
  battle/story operand-size table.
- **Dialogue/text controls:**
  [event/dialog](https://github.com/adamrt/fft_decomp/tree/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/dialog)
  and text-control definitions form another stream layered beneath message
  instructions. Typewriter progression, choices, substitutions and variable
  writes require separate text decoding and presentation acknowledgements.
- **Menu display scripts:**
  [menu_script_core.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/menu_script_core.c)
  and [world/menu/script.c::g_menu_opcode_handlers](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/menu/script.c)
  drive drawing, groups and lists. They are presentation programs. Windows menus,
  including the future Save/Load replacement, need application actions and UI
  state rather than direct execution of PS1 drawing operations.
- **Other presentation/edition-dependent families:**
  [effect_script.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/effect_script.h)
  describes effect playback; sprite SEQ/SHP animation is covered by ZOD-027/028;
  [sound_novel/script.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/sound_novel/script.c)
  has its own dispatch and state. Presence of sound-novel runtime code does not
  establish that every novel/content path is available in this US edition.

## Archive, decoding and original execution order

[resources.c::script_event_load](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/resources.c)
reads one 8,192-byte block from `EVENT/TEST.EVT`, using four logical sectors per
event ID. Its first little-endian `u32` is a text-section offset relative to the
**block base**. The original loader turns it into a pointer, then replaces the
header with `0xF2F2F2F2`. The interpreter starts at block offset zero and therefore
executes four one-byte NOPs before instructions at offset four. An offline decoder
must preserve the original header, distinguish raw and processed inputs explicitly,
and retain block-relative offsets. It must not execute raw header bytes as opcodes.
Archive capacity, valid used-event IDs and exact text-section boundaries still
require a verified local import; fixed block size alone proves none of those counts.

[runtime.c::script_current_event_thread_start](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/runtime.c)
starts the selected event thread after scene setup when the current-event variable
is nonzero. [event_thread.c::script_event_thread_run](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/event_thread.c)
calls `script_next_event_load` and then `script_execute_event`.
[scene_commands.c::script_next_event_load](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/scene_commands.c)
waits for task IDs 4 through 14 to finish, then loads only if the selected event
differs from the loaded ID. This is cooperative scheduling, not a bytecode CALL stack.

An instruction is one `u8` opcode plus the operand-byte count in
[control.c::g_script_event_instruction_sizes](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c).
That array has entries for `0x00..0xF3`, not every possible byte.
`scene_commands.c::script_s16_read` assembles unaligned little-endian halfwords.
Dispatch determines their meaning: arithmetic variable IDs and immediate values
are masked/cast to unsigned 16-bit even though this helper returns `s16`.
Ordinary execution advances by `1 + operand count`; jumps replace the offset and
`continue`, avoiding an additional advance. A zero length does not make an opcode
supported: the table includes bytes with no interpreter dispatch case.

`BLOCK_START`, `ADD_UNIT_START` and `CAMERA_FUSION_START` launch cooperative tasks
and skip their corresponding inline blocks in the parent. Waits can yield on
movement, file loads, other tasks or input. `WAIT` also has tutorial-specific
frame scaling. These mechanisms need explicit scheduling and acknowledgement
contracts before they can be implemented deterministically on Windows.

`EventEnd` (`DB`, also `E3`) is a scene lifecycle operation: BATTLE waits for work,
may chain another event through scenario conditions, and updates unit/status/camera
state before exiting. `D4` reached in sequence simply leaves this interpreter
thread; it is not interchangeable with `EventEnd`. Unknown dispatch bytes also
end the original thread. The bounded managed profile should reject unsupported
instructions before execution, reporting a capability limit rather than claiming
to emulate those accidental/unsafe termination paths.

## Variables and state dependencies

[script_variable.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_variable.h)
and `control.c::script_variable_command_run`, `variable_word_get`,
`variable_shift_get`, plus
[variables.c::script_variable_get / script_variable_set](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/variables.c)
establish that the logical bank is not an array of interchangeable integers:

- IDs `000..07F` select signed 32-bit words; `080..35F` select packed flag bits;
  `360..3FF` select packed four-bit fields. Neighbor fields must survive writes.
  IDs outside the domain stop the original thread; managed validation should
  diagnose them without attempting an out-of-range read.
- Variable `0` is the comparison result and `1` the comparison operand. Comparisons
  `A0..A5` overwrite variable 0 with a boolean integer.
- Arithmetic `B0..BD` alternates immediate/source-variable operands; `BE` zeroes.
  Reads of variable `21` use a separate event RNG, not the battle BIOS RNG already
  implemented in this project. Its seed/order is outside the proposed first slice.
- Weather/time reads can rebuild another variable; text, camera, party, date and
  progression operations have other state dependencies. Variable writes have
  replay/progress-suppression and person-profile vetoes; convenience setters can
  additionally cap war funds. Do not replace all accessors with a dictionary and
  assume compatibility.
- Addition/multiplication overflow, signed division edges and unchecked modulo
  require MIPS/executable evidence before widening the arithmetic domain. The
  first proof below uses small nonnegative additions that cannot overflow.

The first runner supports only variables `0..3` in an explicit proof profile,
with progress suppression off and no camera/text/party dependencies. This is a
deliberate capability restriction, not a proposed limit for the eventual framework.

## Chosen event: synthetic conditional outcome

Choose `synthetic:event/conditional-outcome-v1`. This hand-written instruction
sequence tests one branch and a variable result without dialogue, timing, RNG,
unit movement or original campaign content. Input variable 2 is supplied by the
test/application; output variable 3 becomes 1 when input is exactly 1, otherwise 2.
All other state is absent. Variables 0, 1 and 3 may start nonzero; the script clears
them explicitly. Only the small proof-domain values are initially admissible.

Offsets below are decimal and **block-relative**. Offsets 0..3 are the processed
header's four `F2` NOPs, shown only to explain the source address convention.
The bytes below are a synthetic instruction payload, not a complete original
archive block or a text-bearing event file.

```text
 4  BE 00 00          ZeroVariable(0)
 7  B1 00 00 02 00    AddVariable(0, 2)
12  BE 01 00          ZeroVariable(1)
15  B0 01 00 01 00    AddImmediate(1, 1)
20  A2                CompareEqual: v0 = (v0 == v1)
21  D0 01             JumpForwardIfZero(label 1)
23  BE 03 00          ZeroVariable(3)
26  B0 03 00 01 00    AddImmediate(3, 1)
31  D1 02             JumpForward(label 2)
33  D2 01             ForwardTarget(label 1)
35  BE 03 00          ZeroVariable(3)
38  B0 03 00 02 00    AddImmediate(3, 2)
43  D2 02             ForwardTarget(label 2)
45  D4 03             ForwardTargetExit(label 3)
47  DB                EventEnd search sentinel; never executed by either path
```

The required opcode map is:

- `F2`: no operands; NOP. The prepared program can enter directly at offset 4,
  provided provenance retains that offset convention.
- `BE`: two operand bytes, destination `u16`; zero a supported variable. The
  original dispatch eagerly reads additional bytes, but they are not meaningful
  operands and a safe decoder must not read beyond the encoded record.
- `B0/B1`: four operand bytes, destination `u16` and immediate/source ID `u16`.
  The source accessor can have side effects in general; variable 2 has none here.
- `A2`: no operands; signed 32-bit equality of variables 0 and 1, writing 0 or 1
  into variable 0. Other comparison opcodes are not required by this event.
- `D0/D1/D2/D4`: one unsigned label byte each. `D0` branches only when v0 is zero;
  `D1` always branches. Search starts after the jump. `D0` accepts the first
  forward `D2` **or `D4`** with the matching label; `D1` accepts `D2` only.
  Both land **after** the two-byte marker. A sequential `D2` falls through;
  a sequential `D4` exits. Thus a taken jump to `D4` does not itself exit.
- `DB`: no operands; terminates a forward target search. Keep it as a structural
  sentinel in this proof, while marking semantic `EventEnd` unsupported. A missing
  target is an import/validation error in the managed proof, with no state applied.

The true path executes `4,7,12,15,20,21,23,26,31,45` then exits, leaving
`v0=1, v1=1, v2=1, v3=1`. The false path executes
`4,7,12,15,20,21,35,38,43,45`, leaving
`v0=0, v1=1, v2=input, v3=2`. The marker at 33 is skipped by a taken `D0`;
the marker at 43 is skipped by a taken `D1`, but reached in sequence on the false
path. Neither path executes `DB` or any `EventEnd` side effects.

These outcomes are **source-derived synthetic expectations**, not independently
observed retail behavior. The ignored scratch check verifies the byte offsets,
operand lengths and both hand-written control-flow paths against this design;
it does not validate the original interpreter independently.

## Community tools and interchange

Public source metadata and the following text/source files were inspected on
2026-10-23. No tool was installed or used to write an image/save state. Game-derived
files present in community repositories were not downloaded or copied.

### FFTPatcher suite: EntryEdit, FFTacText and FFTorgASM

Pinned suite commit: `26d45cc8c019194625f078398e0c6ef6b09c1a78`.
[EntryEdit/MainForm.cs](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/EntryEdit/MainForm.cs),
[DataHelper.cs](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/EntryEdit/DataHelper.cs),
[EntryData.cs](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/EntryEdit/EntryData.cs),
[Settings.cs](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/EntryEdit/Settings.cs)
and [PatchHelper.cs](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/EntryEdit/PatchHelper.cs)
show an actual event/condition editor, text script import/export, binary event
conversion, XML ISO patches and PSX save-state paths. Exported event text has
separate `{SECTION:TEXT}` / `{SECTION:DATA}` portions. This is a promising offline
inspection/interchange candidate, not a runtime dependency.

The inspected entry UI defaults to `Context.US_PSX`; settings loading initializes
PSX settings. A `US_PSP` switch arm exists but does not establish a working PSP
EntryEdit configuration. Treat EntryEdit's demonstrated static scope as US PSX;
PSP, Japanese and remaster event support remain unverified. The broader suite's
PSX/PSP features do not automatically extend to every member tool.

[PSX/EventCommands.xml](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/EntryEdit/EntryData/PSX/EventCommands.xml)
agrees with the selected lengths/numbers, but calls `D4` a forward-if-zero target.
That label alone omits its sequential-exit behavior; use the pinned interpreter
for semantics. A separate `EventInstructionUpgrade` directory defines a modified
dialect. Require an explicit edition/dialect and reject upgrade-only instructions
in baseline mode.

[COPYING](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/COPYING)
contains GPL version 3. No suite code or XML is proposed for copying into this
project. Reuse requires an intentional license-compatible decision and notices;
source inspection does not license game text/data packaged alongside the tool.
FFTacText edits text, while FFTorgASM applies executable patches; neither should
be described as the scenario interpreter. A future optional adapter should test
synthetic text and byte round-trips before promising EntryEdit interoperability.

### Juravis

The author's [FFT command-line guide](https://juravis.ffhacktics.com/final-fantasy-tactics/command-line?do=)
describes compilation from a file/directory to `TEST.EVT`, PSX images or quicksaves,
including selectable event size and automatic recompilation. This establishes a
compiler-oriented workflow rather than a Windows event runtime. Exact supported
regional editions, bytecode dialect, source revision and license could not be
verified: direct requests were blocked by the site's browser challenge; the
guide was available through the search index. No permissive license or semantic
compatibility is inferred. Do not import its source/configuration or make it a
required build tool until a pinned source distribution and terms are inspectable.

### FFHacktics event syntax support

Pinned commit: `a3efef7bf9c632bc6dc4fb96b93d0f2707e675d9`.
The [README](https://github.com/equinox-and-solstice/ffhacktics-event-support/blob/a3efef7bf9c632bc6dc4fb96b93d0f2707e675d9/README.md)
documents `.evt` syntax highlighting, completion, signature help and snippets;
upgrade-required instructions are marked. It does not establish binary
compilation, a semantic interpreter, regional coverage or compatibility with an
unmodified PS1 executable. Its [LICENSE.md](https://github.com/equinox-and-solstice/ffhacktics-event-support/blob/a3efef7bf9c632bc6dc4fb96b93d0f2707e675d9/LICENSE.md)
is CC BY-NC 4.0. Use as an optional author aid, with no copied snippets/metadata
or project dependency proposed here.

### libFFT and other candidates

Pinned libFFT commit: `700ecf557ccd726266f0827adc8ae260c51642fe`.
Its [README](https://github.com/adamrt/libfft/blob/700ecf557ccd726266f0827adc8ae260c51642fe/README.md)
explicitly targets PS1 US `SCUS-94221`; [fft.h](https://github.com/adamrt/libfft/blob/700ecf557ccd726266f0827adc8ae260c51642fe/fft.h)
contains event records, opcode metadata and instruction reading, including a
raw-block header/code separation. This makes it a possible **offline independent
parser comparison**, not proof of script execution or a managed runtime option.
The [LICENSE](https://github.com/adamrt/libfft/blob/700ecf557ccd726266f0827adc8ae260c51642fe/LICENSE)
is BSD 2-Clause; any intentional code reuse must retain its notices. No C link or
invocation enters the game. Exact edition hash equivalence with our local image
must be checked before any original-byte comparison.

EasyVent Editor Super Perfect is a named community candidate, but no inspectable
pinned source/license/edition contract was established in this investigation.
Do not advertise its interchange as supported. Ganesha's map editing and other
games' event tools do not establish FFT scenario scripting support. Remaster
event JSON/compiler projects are different edition targets and are not selected
as the baseline format.

## Ownership, prepared representation and unsupported behavior

- `Fft.AssetTool` owns archive/block selection, source edition/hash verification,
  structural parsing, offsets and diagnostics. Imported bytes stay local under
  ignored `scratch/`; the existing prepared-manifest boundary remains in force.
  Preserve unknown original records in an inspection representation if their
  lengths are known; never infer a length or silently omit them in an executable
  conversion. Reject out-of-range opcodes, truncated operands and bad text offsets.
- `Fft.Content` owns a versioned prepared event definition and its validation:
  stable logical event/variable IDs, source byte offsets, scene/dialect identity,
  required capabilities and resolved instruction targets. Preserve raw label IDs
  and resolution order in provenance. Convert a supported subset to typed
  operations; do not persist pointers, PS1 addresses or mutable thread objects.
- `Fft.Core` owns the pure transition of explicit event state: selected program
  identity/version, instruction position, proof-profile variables and terminal
  status. A step budget is a deterministic diagnostic, not a silent script end.
  If an unsupported instruction is present, reject before any state change.
  Keep ZOD-035's battle-save proof unchanged until a separate checkpoint design
  includes event state and content versions.
- `Fft.Client` eventually turns typed presentation requests into dialogue, camera,
  animation and audio, returning explicit acknowledgements. The first event needs
  none. Simulation must not read a display frame counter, key state or file loader.
  `Zodiac.Engine` receives no FFT opcode table, variable bank or campaign flags;
  no reusable interpreter abstraction has yet earned extraction.

The first executable capability set is exactly `F2, BE, B0, B1, A2, D0, D1, D2, D4`
with `DB` permitted only as a structural search sentinel. Everything else is
unsupported, including `DB/E3` execution, `D3/D5/D9` backward control flow, other
arithmetic/comparisons, packed variables, random reads, replay suppression,
concurrent blocks, waits, calls, dialogue, choices, units, maps, effects and sound.
Source no-ops outside the set remain unsupported until deliberately added. The
importer and runtime must diagnose scene/profile/capability mismatches explicitly.
Do not silently reinterpret script-unit IDs as current `BattleUnitId` values.

## Proposed bounded child tickets

The main backlog assigns these scopes to ZOD-064, ZOD-065 and ZOD-066 respectively;
they are independent of a
full campaign interpreter and do not require a new assembly or project rename.

1. **Decode one synthetic PS1 scenario block into a prepared event.** Depends on
   ZOD-040. Implement raw/processed header distinction, the selected operand subset,
   source-offset retention and exact forward-label resolution. Use authored
   8,192-byte fixtures with a synthetic valid text section; document its header
   structure before building it. Validate bounds, truncation, first matching label,
   D0-to-D4 versus D1-to-D4, missing targets, DB search stop and unsupported opcodes.
   Preserve inspection data separately from executable supported programs. A
   locally imported retail event may be inspected later, without committing it.
2. **Execute the conditional-outcome event deterministically.** Depends on the
   decoder child and ZOD-035's state/persistence lessons, without changing its save
   schema. Implement the exact nine-opcode capability set and variables 0..3 under
   the bounded arithmetic profile. Test true/false paths, dirty initial values,
   unchanged input, exact ordered instruction/variable traces, malformed-program
   rejection, identical replay, budget behavior and D4 landing versus fall-through.
   Reject `EventEnd` execution rather than inventing campaign completion.
3. **Trace one real event's activation and completion prerequisites.** Depends on
   the first two children and a verified local `TEST.EVT` import. Select a small
   original event by numeric ID and hash, inventory all opcodes and trigger/finish
   records, and name missing campaign, dialogue or unit capabilities. Record an
   original/emulator observation if available. Scope one next semantic addition
   from that evidence; do not bulk-port the remaining dispatcher.

An EntryEdit synthetic export/import comparison can be done alongside the first
child if its exact binary and configuration are pinned; it is not required for
running prepared events. General authoring UI, custom mechanics and menu
contributions remain ZOD-048/050/062, after actual baseline needs establish their
contracts.

## Validation and remaining evidence limits

Reviewed the shared operand-size array, both scene dispatchers, forward/backward
search helpers, variable operations/accessors, event loader/thread lifecycle,
scenario conditions and location script dispatch at the recorded SHA. Checked
public tool source/metadata/licenses at the pinned revisions above. A synthetic
scratch check (`scratch/zod-040/check-synthetic-event.ps1`, local and ignored)
reads the operand-length table from the pinned checkout and the payload from this
document. It verified all 15 records through byte 47, and inputs 0, 1 and 2 from
dirty initial variables `(7,8,input,9)` produced the exact offset/state traces
above. This is a design consistency check, not independent semantic evidence.
No runtime code, package/project/namespace, save schema or original file changed.

Still unverified: tool binary round-trips, Juravis/EasyVent version/license and
regional support, complete retail event inventory, text decoding boundaries,
original event trigger/scheduling observations, full arithmetic edge cases and
retail semantic equivalence. These limits do not prevent the bounded decoder and
interpreter proof; they block claims of campaign/script compatibility.
