# ZOD-089 — One prepared battle-banner → Ready scene

The general banner and Ready now run as one bounded authored scene, with one
event consumer, clock and retained task ledger. Both prepared artwork packages
and all graphics resources are ready before the first logical task starts.
The existing general and Ready simulation/presentation policies are reused.

Managed base: `24674d4` (working tree clean on entry). Read-only reference HEAD:
`11464943bbdbd2a0af022d90cb7f10455cf911df`, freshly checked and clean. The
[validation evidence](../evidence/events/banner-ready-sequence-v1.json) binds
source files, prior observations, prepared inputs and finite outputs. No new
emulator/original capture was needed. This does not execute original event 4 or
401, reproduce original loading duration, or establish a combined original
scanout/timing comparison.

## Source trace and shared-resource boundary

The pinned original flow is interpreter opcode 78 →
`src/lib/event/scene/menu_overlays.c::script_conditions_display` → REQUIRE
`entry_conditions.c`. The interpreter reads two unsigned bytes and admits the
worker asynchronously. General condition 8 runs inline under task 56; Ready
condition 0 uses a task-56 dispatcher and an untagged child. E5 waits for the
whole class, not a renderer acknowledgement. Increasing native slot order makes
the parent poll before the worker resumes. Worker exit clears running/task
before yielding, so the parent observes completion on the following update.

Event 4's verified adjacent commands are general display 804, E5 wait 807,
Ready display 810, E5 wait 813. Retained source frames 9001/9002/9003 show general
skip-exit-yield, general task clear, then the parent's new wait with Ready's
birth draw. Their state/code/payload identities and the earlier trace report
are reverified; their source counters are not converted into frontend timing.
See [ZOD-080](../architecture/zod-080-banner-opcode-admission.md) and
[ZOD-083](../architecture/zod-083-ready-display.md).

`conditions_show_general` loads image 0 for condition 8 through
`screen_support.c::conditions_load_banner_image`. That loader yields for memory,
file completion and a final post-upload frame; its duration is not a fixed
logical delay. Ready does not load that image itself. Both routines use the
same native polygon pool/banks and copy all 40 packet slots when initializing;
Ready resets UV/XY, palette 2, page 6 and bank parity. General uses palette 4,
different geometry and add/subtract materials. Ready uses reverse glyph order,
four scaled corners and STP average blending. Selected products fit signed 32
bits and truncate before byte storage/GPU interpolation. Existing RGB5 arithmetic,
crop bounds, dither-off, texture-window/mask and readiness policies remain intact.

Both prepared index files are byte-identical image-0 atlases. Their palettes and
geometry remain separate. The native shared pool makes overlapping admissions
unsafe; the selected managed scene therefore permits one active display at a
time. It loads both packages explicitly rather than relying on inherited VRAM.
No PS1 pool sizes, addresses, C routines or original format readers enter runtime.

## Bounded authored contract

`BannerReadyProgram` owns exactly five typed operations: general admission,
ConditionDisplay barrier, Ready admission, another barrier, synthetic exit.
Diagnostic offsets 0..4 are authored operation indices, not original byte offsets.
There are no jumps, external task updates, event restarts, arbitrary conditions,
concurrent effects or cancellation API in this profile. Existing broader/standalone
profiles and their APIs remain unchanged. This focused composition reuses
`GeneralBannerState`, `ReadyDisplayState`, `SceneEventExecutor` and `SceneTaskLedger`;
it does not add another general event interpreter or replace the pure workers.

Admission requires both logical definition/artwork/owner bindings, holds 0..255,
condition 8 then 0, distinct artwork/definition IDs and one owner. A fresh scene
starts a single consumer. Every tick polls preceding ledger facts, pumps the
event, then resumes workers that existed before the call. A successor born during
that pump runs only to its initial yield and consumes no Circle sample from its
birth tick. There is never a tick with two live display tasks. IDs are sequences
1/2; the completed general task remains a tombstone while Ready owns task 2.

Instruction budgets 0..8192 and task-update budgets 0..64 are cumulative within
each call. Initial admission, handoff and terminal-update failures return the
exact input state with no trace, tick advance or identity consumption. The event
observes Ready's child and dispatcher completion gaps; rendering never completes
a task or changes the clock. Final exit closes this selected sequence.

For authored holds 60/60 without Circle, general task clear is 91, Ready birth
92, Ready release 152, child clear 160, dispatcher clear 161 and event exit 162.
With both holds zero the sequence exits at 42; both 255 exits at 552. These are
authored logical checkpoints after readiness. General Circle at 5 clears it at
6 and admits Ready at 7. Circle at that birth tick is discarded; a new sample at
8 can release Ready to scale 128 and produce final exit 18. Circle during the
general hide or after Ready release does not shorten those workers.

## Prepared format, tools and host

`fft.prepared-banner-ready-sequence` schema 1 / profile
`sample:scene-profile/preloaded-banner-ready-v1` is a separate strict descriptor.
It names the sequence, both artwork IDs, owner, two holds, authored provenance
and pinned reference revision. It accepts no bytecode or instruction array.
The closed JSON loader rejects unknown/duplicate/missing/null fields, bad types,
identities, provenance, revision and holds; size/depth are capped at 16 KiB/4.
The old Ready and ordered-banner parsers still reject this descriptor.

`prepare-banner-ready-sequence` takes explicit authored options, validates before
publishing, writes a hash manifest and `sequence.json` through a private staging
directory, and verifies Content loading before atomic directory publication.
Outputs stay under scratch; reparse/escape/existing paths reject without damage.
Artwork is independently prepared by the existing commands. No original event
bytes are copied and labelled authored.

`BannerReadySession.Load` validates the descriptor and both packages/policies
before returning a startable session. The window creates GPU resources before
`Start`. Each draw reads an accepted snapshot and selects exactly one existing
compositor, always over its fresh diagnostic checker background. No old banner
framebuffer/palette survives into Ready. Device recreation preserves state and
trace. The normal managed runtime requires only prepared files.

Example from the repository root, choosing fresh outputs:

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build -- prepare-banner-ready-sequence --id sample:sequence/banner-ready --general-artwork-id fft.ps1-us:banner/general-0 --ready-artwork-id fft.ps1-us:ready/image-0 --owner-id sample:owner/banner-ready --general-hold 60 --ready-hold 60 --provenance authored --output scratch/banner-ready-example
dotnet run --project src/Fft.Client -c Release --no-build -- --banner-ready-sequence --sequence-root scratch/banner-ready-example --sequence-id sample:sequence/banner-ready --general-artwork-root scratch/banner-078-a --ready-artwork-root scratch/ready-086-artwork-a --check scratch/banner-ready-example-check
```

Omit `--check` for Space/false-Circle or C/true-Circle manual ticks, Esc to close.
Finite checks can take `--circle-ticks 5,7,8` (distinct ticks 1..4096); they retain
each accepted state image, hash, phase/counter, final state and complete trace hash.
This schedule is a logical test input, not physical controller timing.

## Executed validation and review

Locked restore, zero-warning Release build and **1,868 .NET tests** pass: Engine
24, Framework 647, Content 516, AssetTool 192, CLI 196, Client 293. The **74 new
managed regressions** cover natural/zero/max holds, the retained handoff relation,
Circle birth/release, cumulative-budget rollback, both bindings, closed descriptor
parsing/preparation, missing/corrupt resources and rendering-independent replay.
All **197 Python tests** pass, including five new independent timeline/crop,
unsigned cross-product gradient and material vectors.

Eight final Windows runs cover repeated 60/60, 0/0, 1/1, 255/255, general/Ready
Circle skipping, a discarded birth sample, and ignored hide/post-release samples.
All **1,257 PNGs** agree exactly across CPU composition, actual GPU readback and
the independent prepared-data model. The natural pair repeats all **164 files**;
three preparations repeat both descriptor/manifest files. Resource recreation at
tick 5 is exercised in every run. Missing sequence, either missing artwork and
unavailable SDL graphics driver produce no check output. Legacy RGB8/ordered/
Ready windows retain all **16/17/72** historical files byte for byte.
An additional run from an isolated working directory containing only copied
prepared packages reproduces all 164 natural-sequence artifacts. Host libraries
remain in the built application directory; no original input or reference files
are present in that working directory.

Review `scratch/banner-ready-089-review/review.html`: Play previews the captured
sequence; Show handoff jumps to tick 91, immediately before Ready's first draw.
The slider and selected snapshots also show the child/dispatcher/event gaps.
Playback speed is for review only. The owner approved this gallery on
2026-10-25: "This looks great." The eight prior
Ready original scanouts retain their separate approvals; this gallery validates
authored composition, not combined original event/startup fidelity. CRT/aspect,
audio, physical input latency and broader original gameplay remain later work.
