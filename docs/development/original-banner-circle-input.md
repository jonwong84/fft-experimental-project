# Original banner Circle input — bounded reveal, hold and hide observations

On 2026-10-24 the owner used the USB controller's Circle-mapped button in the
private RetroArch state-review session. The frozen neutral reveal checkpoint,
post-press checkpoint and two no-input continuations match the existing managed
driver: reveal count 5 stays unchanged, the task takes one extra active exit yield,
then clears on the next source update. No runtime behavior changed.

The subsequent hold-phase observation below verifies the same extra-yield exit
with retained progress 20/1/0. The hide-phase observation verifies normal progress
1 -> 2 despite Circle-only input. Each phase has one owner-operated press observation.
Repeatable input-movie playback, USB/display latency and original event 401
identity remain unverified. ZOD-033 remains partial.

## Source and evidence boundary

The read-only reference is `C:/repositories/fft_decomp` at
`11464943bbdbd2a0af022d90cb7f10455cf911df`, freshly checked for this observation.
At that revision, `src/event/require/conditions.c` samples Circle after each
reveal/hold yield and exits through an additional yield before drawing or
incrementing that loop counter. `src/lib/menu/display.c` selects the event
pressed word for `script_get_controller_input_pointer(0)`;
`include/psx/pad.h` defines Circle as `0x0020`. The existing
[logical trace](../architecture/zod-075-general-banner-effect.md) retains the
arithmetic/ordering contract.

The original executable merges skip and natural completion into the same final
yield tail: saved return address `0x801c9f68`. That address alone cannot prove a
skip. The new [inspector](../../tools/verification/inspect_banner_circle.py) checks
the paired prior phase, unchanged stack counter, condition 8/hold 60/task 56 in
slot 3, Circle-only input words, selected pressed-input pointer, exactly one
source frame of progress and an empty current banner packet list. It verifies
the original banner/scheduler/frame/input-getter code fingerprints before
interpreting the states. The natural-completion inspector still rejects this
early exit instead of treating it as ten completed hide iterations.

The [metadata-only fixture](../evidence/visual/original-banner-circle-reveal-v1.json)
records input/report/tool hashes and the three managed checkpoints. Original
states, screenshots, instructions, configuration copies and logs stay ignored
under `scratch/`.

## Observed sequence

- `scratch/banner-circle-before-a`: source frame 8984, reveal count 5, task 56
  active at `0x801c9384`, all five sampled resident/event input words neutral.
  Three whole pieces are queued. This maps to managed logical tick 5.
- The owner was asked to hold Circle, tap frontend K twice while paused, release
  Circle, and save. The successful save came through Quick Menu → Save States →
  Save State. `scratch/banner-circle-after-a` records source frame 8985, the same
  reveal count, active task at `0x801c9f68`, all five input words `0x20`, and no
  current banner packets. This maps to logical tick 6 / `ExitYield` /
  `CircleSkipped`, with reveal/hold/hide counts 5/0/0. The input values are the
  last sampled words; they do not mean the physical button was still held when
  the file was saved.
- Two independent four-frontend-frame restores of the after-state, with input
  disconnected, reach source frame 8986. Slot 3 is cleared at scheduler exit
  `0x8014c984`, input is neutral, and no banner packets are queued. Both state
  files and PNGs repeat byte-for-byte. This maps to logical tick 7 / `Completed`,
  preserving 5/0/0. A dead task's stack is not a valid loop counter; the inspector
  retains progress from the paired witness. The exploratory exit report's raw
  `phaseCounter` field is not used as animation progress.

Frontend K counts are not a universal conversion to source updates. Here the
saved source counter proves one update between the physical pair. The after-state
thumbnail still contains part of the banner; empty queued draws do not establish
instantaneous displayed disappearance in the two-buffer renderer. No visual
latency measurement or new pixel comparison is claimed.

## Reproduce the inspection

Using Python 3.12+ with the project's existing Pillow environment:

```powershell
python tools/verification/inspect_banner_circle.py --plan scratch/banner-circle-observation-plan/plan.json --output scratch/banner-circle-observed-new
python tools/verification/inspect_banner_circle.py --plan scratch/banner-circle-hold-observation-plan/plan.json --output scratch/banner-circle-hold-observed-new
python tools/verification/inspect_banner_hide_circle.py --plan scratch/banner-circle-hide-observation-plan/plan.json --output scratch/banner-circle-hide-observed-new
python -m unittest discover -s tools/verification -p 'test_*.py'
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Choose a fresh output directory. The retained `banner-circle-observed-a` and `-b`
reports match byte-for-byte. Inspection reads existing states and does not launch
an emulator or inject input. The two retained hold reports also match byte-for-byte.
Ten authored Python checks cover wrong input,
task/counter/frame/pointer corruption, shared exit-PC ambiguity, queued packets,
slot reuse, dead-stack handling, missing code fingerprints and fresh-output
preservation, hold counters 0/1/59 and rejection of an exhausted hold counter.
Seven additional authored hide-witness checks cover exact counter/crop progression,
buffer alternation, active-task/input/frame/stack identity, bounded hide counters,
skip misclassification and fingerprint/output rejection. Both retained hide
inspection reports match byte-for-byte. Eight metadata-only C# replay cases compare phase, retained counts,
exit cause, termination and projected draws with authored artwork.

Zero-warning Release build and all **1,494 .NET tests** passed;
all **152 Python tests** passed. Locked restore passed for the unchanged dependency
graph. The reveal-only checkpoint previously passed 1,489 .NET / 143 Python tests;
the hold follow-up passed 1,492 / 145. Existing simulation, rendering, schemas,
dependencies and the original recording launcher are unchanged.

## Operator follow-up

The original boot-recording launcher refuses a state from a different recording
with `[Replay] Not from current recording.` A separate ignored
`scratch/retroarch-reference/launch-state-review.ps1` keeps the input/policy hash
guards and private paths but omits `--record-replay`. The live session is
`b42ada1aa583401ab46ee921e4b95683`; its actual session configuration and log are
frozen beside the observation plan. It reports XInput / Xbox 360 Controller on
port 1 with the PlayStation controller core setting. These facts and the owner's
action report support the physical-input provenance; there is no independent
input recording of the press.

The base configuration disables `video_font_enable`, hiding save notifications.
The state-review helper now enables that and `notification_show_save_state` for
future sessions only. The saved helper is a later revision than the script that
started the live session; its hash is not launch provenance for that session.
Use the Quick Menu save command and verify the resulting file/log rather than
depending on F2 notifications. Installed settings and protected base profile
remain unchanged; see the [capture guide](retroarch-reference-capture.md).

To prepare the physical hold case, two separate no-input restores of the neutral
before-state produce the same Holding count-1 checkpoint (logical 21), state and
PNG. A copy is installed in previously unused private **slot 1**; slot 0 was
preserved. Local preparation evidence is
`scratch/banner-circle-hold-anchor-a/report.json`. Loading it through the menu
may advance the source before another save: save and freeze the actual neutral
starting state first, then inspect its phase/input before requesting Circle.
The owner then loaded slot 1 and saved the actual neutral starting state before
being asked to press Circle. The initial load attempt has an explicit failure in
the frontend log; the saved baseline independently verifies Holding count 1,
source frame 9000 and all input words neutral. A later load has no corresponding
failure message. The observation relies on the saved baseline, not an assumption
that the failed load succeeded.

## Physical hold result

The owner performed the same paused Circle/K/release/menu-save sequence in slot 1.
`scratch/banner-circle-hold-intake-a` freezes the neutral baseline;
`scratch/banner-circle-hold-after-a` freezes the result. The
[hold metadata](../evidence/visual/original-banner-circle-hold-v1.json) binds both
states, capture commands/configurations, logs, reports and tool identities.

- Source frame 9000: Holding count 1, task 56 active at `0x801c9ad8`, neutral input,
  six whole pieces queued; managed logical tick 21, progress 20/1/0.
- Source frame 9001: all five input words are Circle-only, the hold count stays 1,
  the task is active at the shared `0x801c9f68` exit yield, and no banner packets
  are queued; managed logical tick 22 / `ExitYield` / `CircleSkipped`, progress
  still 20/1/0.
- Source frame 9002: two independent no-input restores find the slot cleared at
  scheduler exit `0x8014c984` with neutral input and no banner packets; managed
  logical tick 23 / `Completed`, retaining 20/1/0. Both original exit states and
  screenshots repeat byte-for-byte; both inspector reports also repeat.

The first four-frontend-frame probe reached source frame 9003 and a new condition-0
task in the same slot (`0x801c3bc8`). The unchanged task-clear guard correctly
rejects that later state as slot reuse. A dedicated three-frame probe preserves
all existing isolation arguments and samples frame 9002 before reuse. Its script,
actual command and outputs remain under ignored scratch. The general image
capture command retains its existing 4..148-frame budget; the shorter probe does
not broaden that API. Both private state slots, frozen inputs and installed/base
configuration hashes stayed unchanged during the finite restores.

No renderer or Core behavior was changed. The combined six C# replay checkpoints
cover the before/extra-yield/cleared states of both observed phases. Input during
hide, replay playback, latency, original event identity and wider integration
remain separate gates. The next optional physical case should test Circle during
hide, where the source does not sample it; it requires its own neutral checkpoint.

## Prepared hide-input checkpoint

The owner selected the hide-input case next. The two hash-bound original
continuation reports retained in the
[natural-continuation evidence](../evidence/visual/original-banner-continuation-v1.json)
contain identical Hiding count-1 states and PNGs. Fresh inspection verifies the
original code fingerprints, active task 56, condition 8/hold 60, neutral input and
progress 20/60/1 at logical tick 81 (source frame 6435). Copies are frozen in
`scratch/banner-circle-hide-anchor-a`; its preparation report records the source
report/state/PNG identities. The state and matching thumbnail were installed by
exclusive creation in previously unused private **slot 2**, preserving slots 0/1
and all protected input/configuration hashes. No emulator ran for this preparation.

Next have the owner load slot 2, leave controller buttons released and the game
paused, and save through Quick Menu. Freeze and inspect that actual neutral
baseline before requesting Circle. This preparation is not a physical hide-input
result. No managed source or test changed; the last full validation remains
1,492 .NET and 145 Python tests.

## Physical hide result

The owner loaded slot 2, saved a neutral baseline, then performed the paused
Circle/K/release/menu-save sequence. The
[hide metadata](../evidence/visual/original-banner-circle-hide-v1.json) binds the
two frozen states, actual session configuration/log, tools and repeated reports.
The initial load has a frontend failure message, as in the hold preparation;
the subsequently saved baseline independently verifies the required neutral hide
state. Later loads have no corresponding failure message.

- `scratch/banner-circle-hide-intake-a`: source frame 6435, active task 56 in slot
  3 at `0x801c9bc4`, Hiding count 1, all input words neutral, six pieces with
  crop 0 / 31 visible rows. This maps to logical tick 81 and progress 20/60/1.
- `scratch/banner-circle-hide-after-a`: source frame 6436, the same active task
  and saved stack, Hiding count 2, the other quad buffer, all five resident/event
  input words Circle-only, six pieces with crop 8 / 23 visible rows. This maps to
  logical tick 82 and progress 20/60/2, with no exit cause or skip yield.

The pinned hide loop never reads Circle. The global input fields show the press
arrived; they do not imply that the banner sampled it. The
[dedicated inspector](../../tools/verification/inspect_banner_hide_circle.py)
requires exactly one source update, the same live task/stack, the next loop count,
buffer alternation and source-derived crops in both states. It preserves the
separate reveal/hold skip inspector and its fingerprints unchanged. Two fresh
reports match exactly; no emulator was launched for this paired inspection.

The existing managed driver matches both metadata checkpoints when Circle is
provided at tick 81. All eight combined observed C# cases and seventeen authored
Python witness checks pass as part of the full suites above. This establishes
one bounded ignored-Circle hide observation, not later natural completion after
the press, all input boundaries, repeated movie playback or USB/display timing.
The reveal/hold/hide evidence is ready for review and an owner-created commit;
select input-movie playback or original event-entry identification separately.
