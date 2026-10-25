# ZOD-090 — Ready completion and battle ownership

Ready finishing does not itself give the player a turn. The original event still
releases its dark-screen task, restores scheduling state, performs EventEnd
cleanup and possibly chains to another event. Only after event-frame completion
does the battle controller select its next turn event. That selection may lead
to a player menu, AI, an ability, a status result or another scenario event.

Date: 2026-10-25. Managed base: `72586cf` (clean on entry). Read-only reference
HEAD, freshly verified and clean:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Evidence metadata](../evidence/events/ready-battle-handoff-v1.json) binds the
source files, executable ranges, original archive, two retained states and two
identical offline reports. No emulator was launched or new original capture made.
This source investigation changes no production C# or rendering policy.

## Commands after the selected Ready barrier

The hash-verified event-4 block has Ready at 810 and its task-56 barrier at 813.
Four records remain: RemoveDarkScreen at 816, its task-54 barrier at 817,
EventSpeed(1) at 820 and EventEnd at 822. The text boundary is 823.
The common event-401 block has the same release/wait/speed sequence after its
Ready barrier at 195, plus a CameraSpeedCurve restore before EventEnd at 206.
These are structural checks of the verified archive, not executed original tails.
Event 401's entry/reachability remains unobserved.

`src/battle/event/interpreter.c` handles RemoveDarkScreen by finding the held
task 55 and changing it to task 54. The REQUIRE worker in
`src/event/require/entry_dark_screen.c` notices that signal after yielding and
eventually exits. The following E5 waits for task 54; neither command means
"the Ready renderer finished". No release-duration constant is selected here.
`menu_sessions.c::script_event_speed_set` restores both the event input-repeat
divisor and BATTLE VSync interval. Event 401 additionally restores its camera
curve. These operations are absent from the authored ZOD-089 five-operation
sequence, whose last operation is a synthetic thread exit.

## EventEnd and event-frame completion

The BATTLE DB/E3 branch in `interpreter.c` performs these ordered operations:

1. Wait for map refresh, yield/test event-block tasks, yield/test file loads,
   then yield again. In player menu-input mode, wait for helper slots 2..13,
   yielding before each inactive test. Tutorial handling has a separate route.
2. Reset EVTCHR slots except for the two named event exceptions and clear the
   packed `BATTLE_NOT_STARTED` flag. This is a scene lifecycle operation, unlike D4.
3. In player mode, when the table's after-event code is zero, recheck scenario
   conditions for a nonzero event outside the common range 0x190..0x1a9.
   The comparison with `0x1AU` uses unsigned conversion: event 4 qualifies,
   event 401 does not. The acting-unit selector becomes 0xff. If the selected
   event differs from the loaded one, `script_next_event_load` can restart the
   interpreter instead of ending it. That loader first waits for task classes
   4..14. A zero finish word alone is insufficient to prove a return to battle.
4. On the non-restart route, snap facings, apply staged statuses, copy displayed
   facing quadrants to battle records, normalize camera yaw/tilt/zoom, then exit
   the event thread by clearing its running/task fields before yielding.

`src/battle/event/frame.c::script_event_frame_run` separately observes all helper
threads idle, sets an idle hint at the end of a running frame, and on the next
entry rechecks every helper before consuming the finish operation. A newly live
helper invalidates the hint. In the selected player-mode route a zero finish word
clears the menu-overlay state and returns `SCRIPT_EVENT_FRAME_NO_FINISH` (0).
Other finish words can route to world, next event, reset or ending. Blocked input,
tutorial and frame-result override paths are separate; this investigation does
not admit them to a new runtime profile.

The finish table contains unsigned 16-bit words. `scenario_finish_operation_get`
returns zero for an inactive scenario; otherwise it uses the current event's
entry. Frame routing separates next-event bits 0..7/10..11 from finish bits
8..9/12..15. The EventEnd recheck uses the masked after-event code, not the whole
word. The recorded states have event-4 finish word zero and active scenario one;
the next selected scenario condition has not been observed or inferred.

## Turn selection and input ownership

`battle/turn/controller.c::battle_state_event_update` handles NO_FINISH by
resetting EVTCHR slots, enabling status display, initializing AI workspace and
calling `turn_next_event_start`. The turn clock chooses the next unit/action;
the result encodes event kind in bits 8..15 and unit ID in bits 0..7. Deep Dungeon
reload, a requested status menu, no unit data and scenario reentry have separate
routes. Otherwise the controller enters ChangeTurn. Closed message/camera gates
then dispatch ability-ready events to casting, action-result/Mime to status
execution, and ordinary unit turns toward action menus.

Opening those menus performs another scenario check and waits for the camera and
window. `menu_action_dispatch` distinguishes the source unit's effective
player-control flag from AI. `unit_store_team_flags` clears that flag for a
non-manual auto-battle setting. Status display being enabled, a selected source
unit or a zero finish code therefore does not imply a player command can act.

Input polling continues throughout the event. `battle/frame.c::battle_input_update`
derives pressed edges as `~previous & current`; it does not create a fresh press
because a scene changes. `scene/control.c::script_event_input_update` has its own
edge/repeat history. The event frame resets a five-frame suppression counter on
non-Select input and on thread running-state changes, then decrements it; menu
help and menu acceptance have additional gates. There is no demonstrated universal
"wait five frames, then enable battle input" rule. Physical controller latency
and the first accepted original battle command remain unobserved.

## Retained observation and its limit

The audit reuses Ready birth at source frame 9003 and the existing ZOD-087 terminal
checkpoint at 9072. Ten mapped BATTLE functions match the verified original file
and resident RAM at both endpoints, including interpreter, event-frame, input,
event-controller and turn/menu dispatch functions. The existing closed Ready
identity/worker classifiers validate both endpoints without widening the older
active-banner inspector.

At 9003, event slot 1/task 68, held dark-screen slot 2/task 55, dispatcher slot
3/task 56 and child slot 4 are live. At 9072, dispatcher and child are idle, but
event slot 1 remains at barrier 813 and slot 2 still holds task 55. Both endpoints
retain battle state 52 (Event), menu overlay 1, idle hint 0, battle-not-started 1,
player menu-input mode 0 and suppression counter 4. This last mode describes
controller sourcing, not readiness to play. The states contain no pressed input.
These are two frozen observations; they do not measure when player control begins.

## Selected next implementation: ZOD-091

Build one **authored introduction → existing two-unit battle** path. Reuse
ZOD-089's prepared descriptor and workers, and the existing battle controller,
input latch and rendering resources. Do not translate the original EventEnd or
skip its dependencies under an original-compatibility claim. The authored
introduction ends at its own accepted event exit.

The pure ownership gate must publish battle availability exactly once, only when
the introduction's event has exited and all its display tasks are terminal.
Ready's hidden image, child completion and dispatcher completion alone cannot
open it. Until then battle state, CT, RNG and command journal stay unchanged.
An instruction/update-budget rejection preserves the entire combined input state
and publishes no availability event. The transition call consumes only its
introduction input; it never forwards that sample as a battle command.

The client clears queued intro input and baselines currently held keyboard/mouse
buttons at the accepted ownership change, reusing `BattleInputLatch`. A button
held across the boundary needs release and a fresh press. Focus, resize, resource
recreation and drawing cannot open/reset the gate or advance the simulation.
The first later explicit NextTurn command uses the existing battle rules; no CT
advance or automatic player-turn selection occurs at the ownership boundary.
The host supplies explicit introduction ticks; their presentation cadence is
an authored preview policy with no original-duration claim.

Both artwork packages and graphics resources must be ready before logical scene
admission. Use one Windows host and keep existing standalone formats/windows
available. Limit refactoring to reusable resources/input/command routing needed
by this path, preserving formula implementations and the existing turn loop.
Finite native checks must cover natural and skipped introductions, held input,
render/device/focus changes, one-time handoff and the existing battle scenario
with headless replay equality. This selects a contract, not an implementation.
[Twelve authored acceptance cases](../evidence/events/intro-battle-contract-v1.json)
are explicitly unexecuted until ZOD-091.

## Verification

The offline audit ran twice; the two reports are byte-identical. It verifies full
archive/BATTLE identities, resident function bytes, source revision/cleanliness,
closed tail shape and retained contexts. Eleven new authored boundary checks
cover alternate tail shape, unsigned holds, exact offsets, wrong task IDs,
incorrect speed/exit, truncated operands/sentinel, scalar widths and immutable
inspection. All **208 Python tests** pass. No production C# changed, so the last
full managed validation remains ZOD-089's **1,868 .NET tests**; it was not rerun.

Reproduce with the ignored, identity-bound plan named in the metadata and a fresh
output directory:

```powershell
python tools/verification/inspect_ready_handoff.py --plan scratch/ready-handoff-090-plan/plan.json --reference-root C:/repositories/fft_decomp --output scratch/ready-handoff-example
python -m unittest discover -s tools/verification -p "test_*.py"
```

Original event completion/conditional chaining, complete startup, map rendering,
physical input timing and hardware/audio fidelity remain separate. No original
payload, decoded script dump, state, image or generated report is committed.
