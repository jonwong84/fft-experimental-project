# ZOD-096 — Combined authored DarkScreen introduction contract

Select one authored scene: enter DarkScreen, run the general banner and Ready
while DarkScreen holds, release/wait for DarkScreen, then make the existing
synthetic battle available. Use one event consumer, logical clock and retained
task ledger. This specification changes no production C# or existing profile.

Date: 2026-10-25. Managed base: `9e69322`, clean on entry. Freshly checked clean
read-only reference: `11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Source evidence](../evidence/events/dark-screen-intro-source-v1.json) binds forty
source files, fifteen resident function ranges, both original event tails and
two retained queued-layer observations. Two offline reports repeat exactly.
[Forty authored acceptance cases](../evidence/events/dark-screen-intro-contract-v1.json)
remain explicitly **unexecuted**. ZOD-097 implements the pure composition;
ZOD-098 adds its separate prepared profile, Windows composition and battle bridge.

## Original command and ownership trace

The verified event-4 tail has DarkScreen at 794, E5 class 54 at 801, general
condition 8/hold 60 at 804, E5 class 56 at 807, Ready condition 0/hold 60 at 810,
E5 class 56 at 813, RemoveDarkScreen at 816, E5 class 54 at 817, EventSpeed(1)
at 820 and EventEnd at 822. This is structural decoding of the hash-bound archive;
only the linked retained observations establish the stated original phases.

Event 401 shares the selected DarkScreen operands and display holds but is not
the same straight-line display tail. DarkScreen/general are at 154/164 with waits
161/167. It then waits for camera class 4 at 170, sets CameraSpeedCurve at 173
and starts a 16-operand-byte Camera command at 175. Ready is at 192, followed by
wait 195, release 198, wait 199, EventSpeed(1) at 202, camera curve restore 204
and EventEnd 206. Preserve these camera records in the source evidence. They are
excluded from the authored display composition; event-401 entry remains unobserved.

`battle/event/interpreter.c` admits the worker asynchronously using the first
idle slot, passes a pointer to the six DarkScreen operand bytes, and continues
to E5. `menu_overlays.c::script_dark_screen_thread` tags class 54 and loads REQUIRE.
`require/entry_dark_screen.c` loads image 0 and initializes its independent burst
pool before the first animation yield. The selected operand widths, approximate
sqrt delays, Q12 rotations, pre-update geometry and overshoot/extra-yield rules
remain the [ZOD-092 contract](zod-092-dark-screen-transition.md).

The worker changes the same native task to class 55 after entry. Class 54's
barrier can then release while class 55 remains live through both displays.
General condition 8 runs inline in the class-56 dispatcher. Ready condition 0
starts an untagged child; its class-56 dispatcher waits for input/hold, signals
the child and waits for child clear before clearing itself. General and Ready
reuse the same glyph packet pool and must remain sequential. DarkScreen's
separate burst pool can coexist with either display.

RemoveDarkScreen finds the first other live class-55 slot and changes it to 54.
The worker emits one last flash before entering the release loop. E5 yields
before testing the class set. The native switch scans increasing slots; retained
event-4 observations place the event in slot 1, DarkScreen in 2, the dispatcher
in 3 and Ready's child in 4. The exact 308-byte switch region and all relevant
resident routines are fingerprinted against verified BATTLE/REQUIRE inputs.

Original EventSpeed/EventEnd are broader operations. EventEnd can load another
scenario, waits for helpers/files and performs battle cleanup; event-frame idle
observation and turn/menu gates follow separately. Reuse the
[ZOD-090 exclusions](zod-090-ready-battle-handoff.md). The selected scene ends
with an explicitly **synthetic exit** at its final barrier observation, running
no original speed restoration, camera logic, EventEnd or automatic turn command.

## One atomic scene and nine typed operations

The new pure Framework program has authored operation indices 0..8:

1. Start one prevalidated DarkScreen worker.
2. Wait for class 54 (DarkScreen transition).
3. Start general condition 8 with an explicit hold in 0..255.
4. Wait for class 56 (condition display).
5. Start Ready condition 0 with an explicit hold in 0..255.
6. Wait for class 56.
7. Signal the retained holding DarkScreen worker to leave.
8. Wait for class 54.
9. Synthetic exit and battle-availability decision.

These indices are not original byte offsets. There is no generic bytecode import,
branch, event restart, injected external task batch, cancellation or additional
worker in this closed profile. Reject attempts to modify managed task identities
or bypass the fixed program. The existing standalone no-match release API and
broader scene APIs retain their behavior; they are not widened by this profile.

Retain three stable ledger identities in order: DarkScreen sequence 1, general
sequence 2 and Ready dispatcher sequence 3, all under the descriptor's one owner
and one scene-run identity. Ready's child is derived from its dispatcher identity,
not another ledger task. At most two ledger tasks are live together. Terminal
tombstones are retained. DarkScreen revisions remain 0/1/2/3 for Entering/Holding/
Leaving/Completed; display tasks use existing Running/Completed revisions 0/1.
Reject mismatched owner/definition/readiness/scene/revision/clock bindings.

At admission all three immutable definitions and readiness bindings already
exist. The initial transaction starts DarkScreen at tick 0 with no submitted
draw and enters a pending class-54 barrier. `RunUntilBlocked` consumes no Circle
sample and resumes no worker. `AdvanceTick` requires an explicit Boolean Circle
sample, including ticks where no worker reads it. Tick arithmetic is checked.

Every accepted tick is one transaction in this order:

1. Validate the entire input snapshot, command and budgets.
2. Increment the single clock once and poll the event's suspension using only
   previously published ledger facts.
3. Pump event operations to the next suspension or synthetic exit. Admit newborn
   workers or signal release in the candidate snapshot. A new barrier always
   starts pending; never poll it again in this transaction.
4. Resume workers that existed before this call: DarkScreen, then general or
   Ready's dispatcher followed by its child. DarkScreen uses the **candidate**
   release signal from step 3; a newborn does not get another resume. Ready's
   initialization draw is retained without a birth-tick input sample.
5. Publish worker phase/completion facts after the parent poll. Do not resume
   the parent again when a later worker clears. Decide ownership from the fully
   accepted candidate, then publish the state, trace and counters together.

Use one cumulative instruction budget in 0..8192 and one cumulative ledger-update
budget in 0..64. Births, phase changes and completion updates share that update
budget; workers do not each get a fresh allowance. If any late operation fails,
return the exact input state, empty trace, zero accepted counts and no availability
publication. Roll back event offset/suspension, all cells/counters/draw snapshots,
signals, ledger identities/revisions, clock and input-sample progress. Retrying
with adequate budgets must match a clean execution. No file, device, time or
renderer callback enters the transaction.

## Source-derived logical checkpoints

For the selected verified grid, maximum initial delay is 41. DarkScreen is born
at 0, first finds all cells finished at 54, enters Holding at 55 while retaining
its final tile draw, and emits its first flash at 56. The event observes the
entry transition at 56 and admits general; the newborn general does not advance
or consume that tick's Circle sample.

Let `G` and `Q` be the authored general and Ready holds. With all samples false:

- General clears at `87 + G`; Ready is born at `88 + G`.
- Ready release is `88 + G + Q`; child clears eight ticks later and its
  dispatcher clears nine ticks later.
- The event releases DarkScreen at `R = 98 + G + Q`. DarkScreen submits its
  final flash at R, starts release tiles at R+1, finishes the extra body at
  R+55, clears at R+56 and is observed by the parent at R+57.
- Synthetic exit and one-time availability occur at `155 + G + Q`.

The natural 60/60 example has general birth/clear 56/147, Ready birth/release
148/208, child/dispatcher clear 216/217, DarkScreen release/last body/clear
218/273/274 and event exit 275. Holds 0/0 exit at 155; holds 255/255 at 665.
These are future implementation expectations derived from existing worker rules,
not observations of original durations or executed combined C# scenarios.

Circle is a fresh per-tick logical sample. DarkScreen never reads it. General
reads it during reveal/hold, ignores it during hide and clears one tick after
its skip exit yield. Ready samples only its dispatcher wait; it releases the
child in that same tick, preserving all seven fade-out draws and subsequent
child/dispatcher/parent gaps. A sample is never queued for a successor.
With 60/60 holds and samples at 1/55/56/57/59/60, general skips at 57 and clears
58; Ready birth 59 discards its sample, releases at 60, clears child/dispatcher
68/69; DarkScreen releases 70, clears 126 and the event exits 127.

## Presentation order and readiness

`lib/menu/display.c` puts DarkScreen in the OT entry after the current script
entry; general/Ready use the current entry. `addPrim` prepends each packet.
Both frozen full-display chains contain the burst mode immediately before the
flash at nonempty position 365, followed contiguously by six glyph packets at
366..371. General executes pieces 0..5; Ready executes glyphs 5..0. Burst and
glyph bank parity are independent: the general observation uses banks 0/1.
These are queued-chain observations, not a new combined displayed comparison.

The prepared compositor must start every paint from an explicit opaque native
256×240 RGB5 background, apply DarkScreen's accepted quads in their existing
order, then apply the active general or Ready display with `ComposeOver`.
Use fixed DarkScreen RGB 48/40/16, excluding concurrent fades and other styles.
Preserve each compositor's point sampling, clipping, triangle/color arithmetic,
palette/STP and per-pass blend saturation. Never blend two completed full-screen
images or flatten overlapping dark tiles into a union. Reset material state
explicitly at each component; do not inherit another component's blend mode.
General and Ready remain mutually exclusive; DarkScreen remains independently
live through their hidden/yield/clear gaps. A fresh paint inherits no prior frame.

Load descriptor, dark math, general artwork and Ready artwork before constructing
a logical scene. Both banner atlases are prepared image 0, with separate palettes
and geometry; Ready must not depend on inherited original VRAM. Allocate every
required graphics resource before admission. Any missing/corrupt/wrong-bound
package or graphics failure creates no scene, tick, input sample, availability
or new check output. Dispose partial resources normally. Device recreation may
rebuild presentation resources from retained prepared data, preserving state.

Use the existing checker background for the first authored integration. This
continues the current diagnostic introduction-to-battle switch; it does not
render an original map or claim a scene-resolution transition matches FFT.
The host can present an accepted snapshot before requesting the next tick and
pause when inactive. This is authored cadence only. Extra, omitted or repeated
paints, focus changes, resizing and recreation cannot advance any worker.

## Closed prepared boundary and limited refactoring

Select `fft.prepared-dark-screen-introduction`, schema 1, profile
`sample:scene-profile/preloaded-dark-screen-introduction-v1`. Its exact fields
are `format`, `schemaVersion`, `profile`, `id`, `darkScreenId`,
`generalArtworkId`, `readyArtworkId`, `ownerId`, `generalHold`, `readyHold`,
`provenance` and `referenceRevision`. Provenance is `authored`, pinned to the
reference above. Reject unknown/duplicate/missing/null fields, unrecognized
versions/profiles, original-event provenance and holds outside unsigned-byte
range. Cap descriptor size at 16 KiB and JSON depth at 4. Validate stable IDs,
pairwise resource/descriptor/owner identity separation, and derived general/Ready
definition IDs (`id + "/general"`, `id + "/ready"`) without collisions; cap `id`
at 248 characters and all other IDs at 256.

The referenced DarkScreen package retains its existing strict schema. Its owner
must match the descriptor, and its selected verified-retail math/provenance must
validate. General/Ready artwork must match the selected image/palette/policy and
reference revision. The scene descriptor remains authored even when its data is
verified original data. Runtime reads only hash-verified prepared packages.

DarkScreen's existing `holdFrames` belongs to its standalone preview program.
The combined profile deliberately does not use that timer: it releases only
after Ready's barrier. Add a small validated logical-definition projection if
needed; do not call `PreparedDarkScreen.ToExecutionDefinition`, attach a second
interpreter, or change the old package format to achieve composition.

Implement a focused pure aggregate that calls existing worker transitions and
scene-ledger phases. Do not nest `DarkScreenExecutor` and `BannerReadyExecutor`:
each owns a clock, program and binding assumptions. Keep old public entry points,
formats and traces unchanged. Mechanical extraction of private admission,
binding or completion helpers is justified only with old-trace regression proof;
no plugin registry, scheduler framework or Tessera abstraction is selected.

For the Windows bridge, reuse the existing battle interaction/router, renderer,
input buffer and one-window loop. A narrow internal client adapter may expose
tick/advance/availability/compose to both typed introductions, with separate
receipt formats. Do not make simulation depend on that interface. Reuse the
existing typed gate where practical or add a focused new gate checking all
three terminal tasks; preserve the old gate's API. Combat formulas, turn/CT/RNG,
replay and save schemas need no changes for this milestone.

## Battle/input gate and implementation validation

Battle remains frozen, including command journal, RNG, CT, selection and cursor,
until an accepted synthetic exit with DarkScreen/general/Ready terminal and no
live ledger tasks. Hidden Ready, child clear, dispatcher clear, dark zero-area
draw and dark clear alone cannot open it. Publish availability once. No battle
command runs in that call. Clear queued intro input and baseline held keyboard/
mouse buttons; release and a fresh later press are required. Restart after
handoff restarts the battle only. Early post-exit tick/restart attempts reject.

The forty contract cases split between ZOD-097's pure transaction checks and
ZOD-098's package/presentation/ownership checks. Test complete state/trace replay
with natural, zero, maximum and skipped holds; birth/dispatched/yield boundaries;
cumulative budgets and late rollback; protected IDs and wrong bindings; external
update rejection; no-match behavior retained in the standalone API; and battle
state/input isolation. Mark each future case executed only in separate later
implementation evidence. The fourteen new Python tests here check inspector
structure and authored DMA chains only.

ZOD-098 must write a finite native validation plan before running. Select repeat
natural 60/60, zero/zero, 255/255, the explicit 127-exit skip schedule above,
and a general-hide Circle sample. Use at most seven Windows runs, each bounded to 4096 logical ticks and 120
seconds, with no original emulator or physical input automation. Record actual
CPU/GPU readback and independently composed full-frame pixels, per-component
state/trace and one-time handoff, plus the existing battle regression. Probe
readiness failures before output; repeat a normal run and a prepared-only run;
recheck old standalone and intro artifacts. These planned runs are unexecuted.

Provide a separate owner gallery covering entry, dark-under-banner, dark-under-
Ready, hidden gaps, release tail and battle handoff, labeling any slowed review
cadence. A new gallery needs its own visual approval. No new original observation
is necessary for this authored implementation, but broader original startup,
missing DarkScreen endpoints, physical timing, map, hardware, CRT and audio
equivalence remain independent work.

## This milestone's executed checks

The offline audit verifies full original input hashes, source revision/cleanliness,
resident bytes, unique event-4 payloads, parent/display/held-worker identities,
all 288 tile counters and both stored packet banks, and relative queued layer
order. It verifies both complete selected command tails and records event 401's
camera gap. It executes no original instructions, creates no new capture and
establishes no combined displayed pixel or timing equivalence.

Reproduce with the retained private plan and a fresh scratch output:

```powershell
python tools/verification/inspect_dark_screen_intro.py --plan scratch/dark-screen-intro-096-plan/plan.json --reference-root C:/repositories/fft_decomp --output scratch/dark-screen-intro-example
python -m unittest discover -s tools/verification -p "test_*.py"
```

All **269 Python tests** pass, including fourteen new authored inspector checks.
The last full managed validation remains ZOD-095's **2,031 passing tests**; .NET
suites and native implementation checks were not rerun for this source-only task.
