# ZOD-066 — Original common BattleStart event

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`.
Read-only reference HEAD, verified during this investigation:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
Managed base: `0e7a09c` (ZOD-065).

**Select event 401 (`0x191`, `EVENT_ID_BATTLE_START`). Next, prove one bounded
non-tutorial frame wait with explicit scheduler-tick input; do not enable this
original event yet.** The event is structurally small but depends on concurrent
camera work, mutable camera operands, banners, task barriers and full EventEnd.
ZOD-065's synchronous variable/branch proof cannot reproduce it safely.

This is verified local input plus static source/structural evidence, not observed
retail behavior or an executed original interpreter. No event script, text,
image, decoded instruction dump or generated archive inventory is committed.
[Summarized evidence metadata](../evidence/events/battle-start-0401.json) identifies
the local inputs, hashes, opcode counts, observation gap and pinned source paths.

## Selected input and independent structural checks

The source loader in
[resources.c::script_event_load](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/resources.c)
uses `id * 4` logical sectors into `EVENT/TEST.EVT`; the ID is a zero-based block
index, without subtracting one. The
[named event IDs](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_variable.h)
identify `0x191` as the common battle-start event. Selection favors its named,
source-visible activation path over smaller blocks with unresolved context.

- BIN `scratch/fft-clean-base-game.bin`: 541,315,152 bytes, SHA-256
  `29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9`.
  Recomputed the whole-file hash against the managed supported profile and checked
  the matching single-track Mode 2/2352 CUE with INDEX 01 at 00:00:00.
- Archive `scratch/all-data-extraction/EVENT/TEST.EVT`: 4,096,000 bytes, ISO LBA
  3,707, SHA-256
  `107b109254ccbb150aca7baf336b632837b22e340677e2b9e746ac61b3de4405`.
  Matches the recorded reference extraction inventory. Independently reread its
  2,000 Form 1 payload sectors from the verified BIN, checking sector sync, mode,
  duplicate subheaders and Form 1 flags: the entire payload matches byte-for-byte.
- Selected block: offset 3,284,992 in the archive, 8,192 bytes, first logical LBA
  5,311, SHA-256
  `a12ad52f48c2b6684993f196ead1721ecb8895cfd0f6690faed1a0d9ccca483f`.
  Raw text offset 207; first code offset 4; final DB at 206. Code plus sentinel
  ends exactly at the declared text boundary. Text/padding remains opaque.

A Python structural walk used the managed operand-length metadata (244 entries,
SHA-256 `5f50d72e0e4d2a62215be25bc01f9123c0727a2abdcb9a4ff038511c569d915f`).
A temporary .NET program separately called the existing managed
`ScenarioBlockInspector.Inspect(..., Raw)` and agreed on the complete 31 records,
13 opcode kinds, offsets, boundary and block hash. These are two structural checks
using the same pinned length metadata, **not independent semantic validation**.
The original loader replaces the raw header with four F2 NOPs and executes those
before offset 4; they are not included in the 31-record count.

The archive physically holds 500 block-sized slots. This is not a finding that
all 500 are valid, used, reachable or supported events. A temporary selection scan
was only a candidate filter, not a complete event inventory.

## Activation: a menu fallback, not a condition-chain event

The selected normal path is source-derived and conditional on these gates:

1. [battle/entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/entry.c)
   finishes the deployment/map initialization step `0x0d`, loads fixed CLUTs and
   calls `battle_state_enter_event_at_battle_start`.
2. [battle/turn/controller.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/controller.c)
   enters the event battle state and dispatches `MENU_REQUEST_SCENARIO_CHECK`,
   `MENU_SCENARIO_CHECK_START`, with no acting unit (`0xff`).
3. [command_prompts.c::request_dispatch](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/command_prompts.c)
   requires a non-tutorial current event, player input mode, a non-SKIP option and
   an idle event slot. The game-over branch requires the battle-not-started flag
   to be zero, so it does not take precedence while that flag is set. If
   `g_scenario_event_active != 0`, the scenario-condition runner takes precedence;
   this is **not** a universal battle-start instruction for all scenarios.
4. In the fallback branch, `g_scenario_event_active == 0` and
   `EVENT_SCRIPT_VAR_BATTLE_NOT_STARTED != 0` select this event when
   `EVENT_SCRIPT_VAR_WORLD_DEBUG_BATTLE_STYLE == 0`. Debug style selects `0x193`
   instead. START hides unit views, marks the menu overlay active, writes current
   event `0x191` and starts its thread. TEST only stages the prospective next ID;
   it does not play the event.

[scenario_setup.c::scenario_load_conditionals](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/attack/scenario_setup.c)
initializes scenario-local variables, marks battle-not-started, rebuilds the
finish-operation table and explicitly sets entries `0x191` and `0x193` to zero.
A recognized scenario activates scenario conditions; that path must not be
conflated with this common fallback. No particular original encounter/save state
has been independently demonstrated to reach the selected fallback in this task.

[runtime.c::script_current_event_thread_start](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/runtime.c)
resets unit animation state; before battle it stores camera variables and resets
the map light transform. It unlocks camera position, clears the speed curve,
starts a tint fade and starts the event slot. The
[event-thread body](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/event_thread.c)
loads the selected block through `script_next_event_load` before interpreting it.
That helper waits for task IDs 4..14 to finish and reloads only when the event ID
differs from its loaded ID. Initialization, task state, map, units and current
camera are therefore prerequisites; the block is not a self-contained scenario.

## Every used opcode and its required capability

Counts and operand widths below are structural metadata for the selected block.
The dispatch and semantic entry points are in the pinned
[BATTLE interpreter](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c),
[instruction definitions](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_instruction.h)
and the helpers linked below. All thirteen require semantics outside ZOD-065's
executable subset; DB is only structurally recognized there.

- **19 Camera — 6 records, 16 operand bytes.** Starts `camera_thread` in an idle
  slot, task 4. Seven signed halfword component targets and a duration feed camera
  interpolation; 10,000 is a hold sentinel. Requires current camera/script
  variables, signed fixed-point transforms, curve selection and task lifetime.
- **E5 WaitForInstruction — 8 records, 2 bytes.** Reads a little-endian task ID,
  yields before checking, then scans running slots 1..15 until none match. This
  block waits on camera (4), dark-screen transition (54) and condition dispatch
  (56). A no-op or immediate acknowledgment would erase ordering.
- **73 RelativeCamera — 3 records, 14 bytes.** `script_relative_camera_apply`
  patches the following Camera operands using current/key camera values, retaining
  hold sentinels and signed halfword writes. Position conversion divides script
  coordinates by 1,024. This is mutation of the loaded execution buffer, not just
  a pure camera command. Immutable imported definitions must remain separate
  from any future per-execution working operands or normalized relative commands.
- **63 CameraSpeedCurve — 3 records, 1 byte.** Writes `g_camera_speed_curve`;
  camera workers interpret mode, component-group and easing bits. Preserve that
  policy and timing of reads when implementing interpolation.
- **4D RequestMapJumpIn — 1 record, 1 byte.** Stages a duration and waits two
  yields. The frame's pending-request processor invokes
  `battle_state_start_change_map_jump_in`; it is not completed at dispatch.
- **F1 Wait — 1 record, 2 bytes.** Signed halfword frame count; here it requests
  60 yields before the later speed change. Non-tutorial dispatch calls
  `thread_frames_wait` directly. Tutorial dispatch may divide counts of at least
  three by the VSync interval. Neither path is a wall-clock sleep.
- **1C EventSpeed — 2 records, 1 byte.** `script_event_speed_set` changes the event
  speed and, in BATTLE, VSync interval. It affects presentation/input scheduling;
  treating each count as milliseconds would be incorrect.
- **1D CameraFusionStart — 1 record, no operands.** Starts a task-4 fusion worker
  and moves the parent instruction position to the matching end marker before
  its normal advance. The child reads the inline camera/relative-camera keys.
- **1E CameraFusionEnd — 1 record, no operands.** Structural terminator for fusion;
  ordinary dispatch is empty. The selected parent's skip means the inline keys
  and this marker are not a simple sequential list of parent operations.
- **76 DarkScreen — 1 record, 6 bytes.** Starts `script_dark_screen_thread`, task
  54, and the BATTLE REQUIRE overlay's burst/banner transition. The worker changes
  to holding task 55 after transition; waiting for 54 does not require the hold to
  have ended. Requires prepared banner graphics, transition state and task identity.
- **78 DisplayConditions — 2 records, 2 bytes.** Starts
  `script_conditions_display` / `require_conditions_entrypoint`, task 56. The
  selected IDs are general banner index 0 and special-case Ready flow, with a
  hold argument. Banner reveal/hold/skip input and nested worker completion are
  separate from scenario victory/defeat predicate evaluation. The general banner
  can end early on Circle; do not infer a fixed event duration from its argument.
- **77 RemoveDarkScreen — 1 record, no operands.** Finds a task-55 holding worker
  and changes its task back to 54; the worker then performs its release phase.
  This is a task signal, not an unconditional immediate black-overlay removal.
- **DB EventEnd — 1 record, no operands.** Enters the BATTLE event lifecycle
  described below. It is not the synchronous proof's D4 thread exit.

Camera helpers and operand rewriting are in
[scene_commands.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/scene_commands.c).
The [fusion worker](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/camera_fusion.c)
constructs component tracks from the current camera plus inline keys and ends
when its tilt track finishes. In this block its start/end are at 88/153, with two
inline RelativeCamera/Camera pairs. Camera work overlaps the parent's dark-screen
flow; barriers join it later. There are no D0/D1 branches or D4 exits in this block.

Task constants live in
[thread.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/thread.h).
[Menu overlay bridges](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/menu_overlays.c),
[REQUIRE dispatch](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_conditions.c),
[dark-screen worker](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_dark_screen.c)
and [banner presentation](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/conditions.c)
explain why these cannot be skipped or acknowledged immediately by the runtime.

## Completion and next state

At DB the BATTLE interpreter waits for pending map refresh, event-block tasks,
file loads and relevant helper slots. It resets EVTCHR slots for this event and
clears `EVENT_SCRIPT_VAR_BATTLE_NOT_STARTED`. Its immediate scenario recheck/chaining
path excludes common IDs `0x190..0x1a9`, so event `0x191` does **not** take that path.
It snaps unit facing quadrants, applies staged statuses, copies displayed facings
to battle units, normalizes the camera and exits the event thread.

[battle/event/frame.c::script_event_frame_run](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/frame.c)
waits until helper threads are idle before consuming the finish operation. For
this common event the explicit finish word is zero (and an inactive scenario also
makes `scenario_finish_operation_get` return zero). It clears the menu overlay
and returns `SCRIPT_EVENT_FRAME_NO_FINISH`, without selecting a next event/world
route. `battle_state_event_update` handles that result by restoring status display,
initializing AI workspace and starting the next turn event. This is the ordinary
battle-event path, not the alternate intro-state routing of finish codes.

Replacing DB with D4 would omit these state changes, barriers and continuation.
Ignoring any unsupported presentation operation would also lose task lifetime,
mutable operands or input timing. The current prepared exporter is explicitly
synthetic: original bytes must not be relabeled as synthetic or passed through it.
The inspector accepts this block structurally; semantic execution remains rejected
from its first opcode (Camera at offset 4).

## Observation gap and next bounded addition

**No independent original/emulator observation is available for this event in the
checked local artifacts.** No matched event-entry save/state, input log and timed
capture were found; this task did not run an original execution harness. The
managed client cannot play this event because its camera workers, REQUIRE banners,
task scheduler and EventEnd lifecycle are absent. Availability of an installed
emulator was not established; the blocker is not a missing BIN or BIOS.

A future ZOD-026 observation must establish an original non-debug, non-tutorial
battle checkpoint actually reaching the fallback above; retain edition/image/block
hashes, map/party/camera and gate values, input/skip sequence, speed changes and
capture timing under ignored scratch. Compare banner/camera/barrier ordering,
clearing battle-not-started and continuation into turns. Source inspection alone
does not prove encounter reachability, exact duration or original behavior.

**Selected child ZOD-071: one non-tutorial F1 frame wait in an explicit timed proof
profile.** At speed 1, the selected F1 at offset 83 requests 60 yields. The
[scheduler helper](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/thread/scheduler.c)
yields exactly `count` times; zero and negative counts perform no yields in that
helper. BATTLE's frame scheduler/VSync relation and
[event-speed setter](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/menu_sessions.c)
remain explicit presentation-boundary concerns.

Prove a bounded nonnegative signed-halfword count (0..32,767), with initial support
restricted to non-tutorial speed 1. Reject negative counts as an unsupported proof
domain, recording their source behavior; do not silently clamp or claim full F1.
Use caller-supplied scheduler-tick commands, immutable pending-wait state and
ordered suspend/resume traces. Specify when a wait is entered, which tick consumes
its first yield and when following operations resume. No wall-clock/frame-rate
dependency or generic PS1 stack scheduler is required.

Fixtures must be independently authored: zero wait, one tick, 60 ticks, maximum
count, sequential waits, arithmetic before/after wait, unchanged inputs and replay,
negative/truncated/unsupported input, budget failure and post-exit behavior.
Define transactional rejection per call and distinguish suspended from exited;
keep instruction and tick budgets separate. Preserve the synchronous conditional
profile and battle save schema. Even after that proof, this original event still
requires every other capability above; F1 is not permission to skip them.

## Implementation follow-up

ZOD-071 now implements the [separate explicit-tick wait proof](../behavior/timed-event-waits.md)
with signed counts, immutable suspension, exact tick boundaries and atomic errors.
The investigation and validation below remain historical; no original BattleStart
playback or missing worker/lifecycle behavior was added. ZOD-072 now
[defines the scene task-lifetime/barrier contract](zod-072-event-task-contract.md);
ZOD-073 now [implements its pure ledger/barrier proof](../behavior/scene-task-barriers.md).
ZOD-074 now [integrates E5 in a separate synthetic profile](../behavior/scene-event-barriers.md); original workers and
BattleStart playback remain unsupported.

ZOD-075 now [selects the general condition-8 banner's logical lifecycle](zod-075-general-banner-effect.md),
using the selected DisplayConditions at offset 164 as its path anchor. Post-load
phase/input/termination expectations are separate from unimplemented startup
loading, rendering and the later Ready condition. ZOD-076 now
[implements that pure logical proof](../behavior/general-banner-effect.md); original
opcode-78 admission and full event playback remain unsupported.

## Verification and completion boundary

Whole-image/CUE identity, reference inventory/archive hash, direct sector payload
comparison, selected block hash and Python/managed structural agreement all passed.
Source links, metadata/count consistency, coverage view and whitespace are checked
separately. All original blocks, decoded records, generated candidate inventory and
temporary managed harness/output stay under ignored `scratch/zod-066/`.

This ticket changes research/planning documents and summarized evidence only.
The last full application validation remains ZOD-065's **1,057 passing tests** and
zero-warning Release build; it is historical evidence, not a new suite run here.
No original semantic test, renderer check, runtime capability or campaign support
is added by this investigation.
