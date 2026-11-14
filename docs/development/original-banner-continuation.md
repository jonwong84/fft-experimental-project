# Original banner continuation — bounded ZOD-033 timing evidence

2026-10-24. Managed entry `a022cb9`, initially clean. Read-only FFT reference HEAD
freshly verified as `11464943bbdbd2a0af022d90cb7f10455cf911df`.

The existing original state can validate the **end of reveal, hold/hide boundaries,
all ten hide iterations and natural task exit** without another owner capture.
The source-bound saved task and quad crops agree with the managed no-Circle replay
at 18 checkpoints. The earlier fully visible artwork anchor did not establish the
task's phase: its saved task is actually in reveal, with 18 completed iterations.
Full reveal onset, original loading time and event identity remain open.

## Source and state bindings

The entry is the previously frozen SCUS-94221 state, SHA-256
`133b893188d0abe7909725f910496b74bad9cfc85733cb38d76fece03048901a`.
The existing reviewed comparison plan binds that state, prepared banner definition
and exact private RetroArch/Beetle software profile. The strict labeled-state
reader supplies `MAIN/MainRAM.data8`; no original code executes in the managed
application or automated .NET/Python tests.

Pinned `target/battle.yaml` identifies `g_threads` at `0x80165f98`, the backing
`g_thread_contexts` at `0x8016986c`, and `thread_current_exit` at `0x8014c958`.
`include/fft/thread.h` defines sixteen 1,024-byte BATTLE contexts: parameters
at +0/+4, saved s7 at +`0x2c`, SP at +`0x3c`, saved return address at +`0x44`,
running/task fields at +`0x48`/+`0x4c`. The selected task is slot 3, class 56,
condition 8 and hold 60. The verifier discovers its slot at entry and retains that
identity rather than searching for a new class-56 task at every sample.

`target/event.yaml` binds `conditions_show_general` to `0x801c8f80`, 4,132 bytes,
source `src/event/require/conditions.c`. Frozen RAM SHA-256 is
`231240f68206f06c842633bd08d76cb806eaac8b3e032ba673417f590ed12607`,
matching that target entry's hash prefix. The 60-byte scheduler exit is
`23aa4a3549fe6cb707b4f4e7a19e7f36356ce2411a96dfbb3d64c6fc5d1c2392`.
Every captured state must retain both complete function fingerprints.

The original instruction bytes and source loop boundaries identify these saved
return addresses after `thread_yield`:

- `0x801c9384`: reveal, completed counter 0..19.
- `0x801c9ad8`: hold, completed counter 0..59.
- `0x801c9bc4`: hide, completed counter 0..9.
- `0x801c9f68`: final yield, exactly ten hide iterations completed.
- `0x8014c984`: scheduler exit yield, with running=0 and task=0.

The function spills its signed-32 loop counter at SP+`0x18`, hold argument at
SP+`0x10` and quad count at SP+`0x20`. Instruction loads/stores around the yields
bind these stack slots; the verifier bounds SP and its frame inside the selected
thread and checks hold=60/count=6. It rejects unknown PCs, unexpected parameters,
suspension, out-of-range counters and the observed next task's slot reuse. These
guards are specific to this bounded continuation, not a general native task
generation identifier. The entry's SP is `0x8016a7cc`,
saved PC `0x801c9384`, counter 18. Its already complete artwork cannot distinguish
late reveal from hold by appearance alone.

The current render buffer/OT roots use the preceding
[five-bit draw-state trace](five-bit-general-banner.md). The verifier follows the
bounded RAM DMA chain, locates all six banner packets in ascending traversal order
and checks their bank against saved s7. All four normalized XY and UV vertices
must describe the same half-open crop against the pinned prepared definition.
This checks actual submitted packet geometry rather than reconstructing it from
the loop counter. It does not establish GPU execution, triangle edge rules or
pixel colors. Completed task exit requires no banner packet in the current OT.

## Observations and managed replay

Two fresh capture sets each perform 17 independent no-input restores from the
same entry state, at frontend budgets 4, 6, 8, 24, 124, 126, 128, 130, 132, 134,
136, 138, 140, 142, 144, 146 and 148. The entry itself is checkpoint 18.

- Budgets 4/6/8 identify reveal 19, hold 0 and hold 1: logical checkpoints 19/20/21.
- Budget 24 identifies hold 9, checkpoint 29; 124 identifies hold 59, checkpoint 79.
- Budget 126 identifies hide 0, checkpoint 80; the last submitted quads remain full.
- Budgets 128 through 144 cover hide iterations 1..9, checkpoints 81..89.
  Submitted crops are 0, 8, 16, 24, then 31, with visible rows 31, 23, 15, 7,
  then 0. Degenerate quads remain submitted; invisibility does not end the task.
- Budget 146 identifies final yield at checkpoint 90, with ten completed hide
  iterations and a still-running task. Budget 148 identifies cleared task exit,
  checkpoint 91, with no current banner draws.

At these sampled budgets, checkpoint `17 + frontendFrames/2` agrees with the
independently decoded task. This is a bounded empirical cadence for these restores,
not a general conversion between frontend frames, VSync, scheduler resumes or
simulation ticks. The entry restore has startup overhead; checkpoint numbers
mean completed post-readiness work in the selected model, not event-entry age.
The hold interval is sampled at its boundaries/interior anchors, not at every
iteration. The early reveal, physical Circle input and other hold arguments are
not observed here.

A retained exploratory 150-frame continuation has already started another
ConditionDisplay in slot 3. The selected verifier rejects it. A slot/class match
alone cannot extend the completed banner's lifetime or establish event 401.

The .NET regressions replay the actual `BannerSceneExecutor` and Client projection
from the authored prepared artwork through each recorded logical checkpoint, then
compare phase, reveal/hold/hide counts, terminal state and six observed crops (or
empty exit projection). They consume only
[normalized evidence metadata](../evidence/visual/original-banner-continuation-v1.json).
No original artwork, state, RAM or executable instructions are test fixtures.
This corroborates the selected existing logical model; it changes no runtime rule.

## Repeatable capture and review

Use the existing bundled Python 3.12/Pillow environment and choose fresh scratch
output names. Each run launches sequential hidden processes with a 55-second
per-process timeout:

```powershell
python tools/verification/capture_banner_continuation.py --plan scratch/banner-033-comparison-plan/plan.json --profile scratch/retroarch-reference --output scratch/banner-continuation-c
python -m unittest discover -s tools/verification -p 'test_*.py'
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

The verifier pins the entry plan/profile, source functions, tools, emulator/core,
BIN/CUE and actual BIOS. Controllers are disconnected, null input is requested,
patches disabled and SRAM load/save disabled. Each process has separate scratch
state/save/options directories. It overrides `savestate_auto_save=true` only in
that process's private appended config, obtaining an exit state without altering
the frozen input or base profile. Input copies, selected core options, base policy
and installed configuration hashes are checked afterward. No replay playback is
claimed. Expected existing core-option/GLSL/MIDI diagnostics remain in each log.

The final runs are `scratch/banner-continuation-c` and `-d`. Their `review.html`
pages show raw 280x240 PNGs without cropping/resampling. Screenshots are visual
context; the partial-banner screenshots visibly lag the submitted quads. That
display latency relative to the saved task is not calibrated, and this
milestone performs no partial-banner pixel comparison. Logs/commands/exit states
remain local; failed runs retain diagnostic files and publish no success report.
Existing output is rejected before launching or loading original evidence.

All 17 PNGs, exit-state files and normalized task/geometry observations repeat
between final runs. Paths, commands and logs identify their individual runs, so
whole reports are not asserted byte-identical. The metadata records file identities
and the repeat comparison. Visual inspection confirms the hide removes the lines
and the task exits; it does not establish general background blending/coverage.

Owner visual review (2026-10-24): accepted the continuation gallery with
“This looks right to me.” This records approval of the displayed sequence's
appearance; the timing, input, event and rendering gaps below remain open.

Validation: zero-warning Release build, **1,436 .NET tests** (18 new Client replay
cases), and **92 Python tests** (11 new authored context/packet/guard cases) pass.
No runtime, package, prepared-asset or save-schema change. All original media,
states, captures and decoded data remain ignored below scratch. No automatic commit.

Next: obtain a state before the banner becomes visible for reveal/loading/input
anchors, or select original background-sensitive blend/coverage comparisons.
ZOD-033 remains partial, full scene/event integration is separate, and ZOD-079's
later CRT/aspect/backend/Settings/audio discussion remains deferred.

Follow-up: the [controlled blend comparison](general-banner-background-blending.md)
uses the no-input full-visible anchor and an isolated palette-transparent state
copy to match all affected pixels with actual C# composition over the recovered
background. Partial-frame correspondence and early reveal/input gates remain open.

The later [displayed hide check](displayed-banner-hide.md) resolves the selected
queued-task/front-buffer relationship through the VSync stack/source order and
saved scanout. Its labels distinguish queued progress from displayed progress;
the continuation metadata here retains its original task-counter meaning.
