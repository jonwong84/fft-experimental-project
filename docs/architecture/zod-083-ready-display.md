# ZOD-083 — Ready display source and bounded contract

Date: 2026-10-25. Edition: `SCUS-94221`. Managed base:
`d10ca2630b5080e9ea1f67da0dd49e548fdde813`; working tree clean on entry.
Read-only reference HEAD verified: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

Select **DisplayConditions condition 0, argument H=0..255**, with Ready artwork
preloaded and ordered event → dispatcher → child execution. It is a separate
parent/child lifecycle, not another instance of the general banner's wipe reducer.
[Source/observation metadata](../evidence/events/ready-display-source-v1.json)
and [32 independently authored expectations](../evidence/events/ready-display-contract-v1.json)
support a bounded implementation split. The expectations are specification-only
and unexecuted. No runtime API, profile, asset preparer or renderer changes here.

## Distinguish the condition from the message

The pinned `include/fft/script_instruction.h` declares condition **0** as
`EVENT_DISPLAY_CONDITION_SPECIAL_CASES`. Its dispatcher selects message
`CONDITIONS_BANNER_READY = 0` from the separate banner enum in `after_battle.h`.
Condition **1**, named `EVENT_DISPLAY_CONDITION_SHOW_READY_BUGGED`, instead runs
`conditions_show_ready_bugged` in `party_changes.c`: a hovered-unit stats worker
with a fixed 60-frame-or-Circle wait. That path is excluded. Do not rename it into
the working condition-0 path or silently repair its behavior.

The BATTLE interpreter reads two unsigned bytes for opcode 78, allocates the
first idle slot and starts `script_conditions_display`, passing condition and H.
The wrapper clears numeric-panel hiding, then REQUIRE sets task **56** on the
dispatcher. For condition 0 it starts `conditions_special_thread` in another
first-idle slot with `(message=0, small-array=0, control=0)`.

The dispatcher calls `input_wait_frames_or_skip(H)`, then writes child parameters
`(0,0,2)` and waits for the child to become idle. Child message/layout selection
was cached on entry, so this release command does not reload/reselect its artwork.
The dispatcher exits only after its child clears. The child starts with task ID
**0** (`THREAD_TASK_NONE`) and never assigns itself task 56. E5(56) observes the
dispatcher; child completion alone cannot release it.

## Assets and readiness

`conditions_special_thread` does not load an image. It uses the resident BONUS
texture/palettes established by an earlier loader. For selected event 4, the
preceding general banner loads image 0; DarkScreen also loads image 0 outside the
final-event branch. The proposed authored profile must prepare/load its own
explicit image-0 binding before creating any logical task. It must not invent
a Ready loader delay or imply that a standalone original Ready opcode loads files.

Verified local identities retain the ZOD-077 input hashes: `EVENT/BONUS.BIN`
958,464 bytes, REQUIRE.OUT 127,684 bytes, load base `0x801bf000`. Image 0 occupies
the first `0x6800` bytes. Its 256x200 4bpp pixel section and eight RGB555/STP palettes
retain the existing offline format. Only summarized hashes/selection are committed;
raw quads, colors, palettes and diagnostic output remain ignored under scratch.

The Ready selection differs from the general banner:

- The first special range is `[0,6)`, using six glyph quads at
  `g_conditions_special_quads` (`0x801d0ee8`) and colors at
  `g_conditions_special_vertex_colors` (`0x801d1548`). Selected spans are 72 and
  120 bytes; the two range bytes are at `0x801d153c`. Hashes are in the evidence.
- `gfx_init_poly_gt4_array_32(32,0)` selects CLUT `(416,254)`, **palette bank 2**,
  image offset `0x6440`, with page **6** and semi-transparency enabled. The general
  banner's bank 4 and alternating add/subtract materials do not apply.
- Page 6 has ABR=0: average blending for STP-set texels; ordinary transparent and
  opaque texel rules still require the separate rendering contract. UVs sample
  initialized atlas rows 0..47, never the loader's uninitialized upload suffix.
- Six 24-row glyphs have widths 16/17, within visible bounds `(42,98)..(224,122)`
  under the selected native draw origin. Their four corner colors vary across X
  and Y. Source scaling is signed-32 `channel * scale / 128`, truncated before
  byte storage and GPU interpolation/modulation. The selected products fit s32.
- Both packet banks are initialized; `buffer=0` is explicit, with a toggle before
  each submission. First submission uses bank 1. There is no inherited arbitrary
  initial parity as in the general-banner routine.

The current prepared general-banner schema requires bank 4, its six overlapping
wipe pieces, horizontal color pairs and add/subtract materials. Its compositor
cannot be reused unchanged. ZOD-085 must select a bounded full-corner gradient,
average-blend and readiness contract before implementing presentation. Existing
general renderer policies/captures remain unchanged.

## Source order and exact logical yields

The source path is `entry_conditions.c` → `screen_support.c::input_wait_frames_or_skip`
and `conditions.c::conditions_special_thread`, with `thread_inactive_wait` and
`thread_current_exit` in `src/lib/thread/scheduler.c`. The scheduler scans increasing
slots. `thread_slot_resolve` scans from slot 1, not from the current slot: arbitrary
slot reuse could put a child below its dispatcher. This contract explicitly selects
**event before dispatcher before child**, matching both inspected original states;
it does not emulate arbitrary native slot occupancy or exhaustion.

Define admission/initialization as T=0, after readiness, with each worker stopped
at its first existing yield. This includes work performed before that yield:

- **H>0:** dispatcher first yields before reading Circle. Child initializes its
  glyphs and submits scale **48** before its first yield. Accepted birth therefore
  has a draw, consumes no input sample and never performs an extra resume step.
- **H=0:** dispatcher skips its input-wait loop, sends release 2 before the child
  starts, and enters its mandatory inactive wait. Child initializes, sees 2,
  jumps directly to fade-out and submits scale **128** at birth.
- **Fade-in:** absent release, draws 48/56/64/72/80/88/96/104/112/120 at T=0..9,
  then enters full-scale hold at T=10. The fade-in is **inside H's wait**, not
  followed by a new H-frame hold.
- **Release:** dispatcher resumes before the child, sampling fresh Circle only
  during its input wait. It releases at R=H without input, or the first sampled
  Circle tick 1..H; H=0 gives R=0. Child resets fade-out to 128 even when release
  interrupts a lower fade-in scale. Circle does not directly end the child and
  is ignored by this effect after release. Birth input is never deferred.
- **Fade-out:** submissions are 128/116/104/92/80/68/56 at R..R+6. At R+7 the
  loop has ended and the child enters an additional active yield with **no draw**.
  At R+8 it clears its running flag/task before yielding from exit.
- **Dispatcher clear:** its earlier poll at R+8 still saw the child running.
  At R+9 it observes the idle child and clears its own task 56. The earlier event
  poll still sees task 56 at R+9 and resumes at **R+10**, absent other class members.

Thus selected natural expectations are child clear **H+8**, task-56 clear **H+9**,
waiting-event resume **H+10**. For H=0 these are 8/9/10; H=60 gives 68/69/70;
H=255 gives 263/264/265. These are **source-derived authored expectations**,
not measured original animation durations or emulator/frontend frame conversions.
Do not reuse the general banner's H+31/H+32 lifetime or add ten fade-in ticks to H.

The child's control value 1 has separate early-exit branches, but this selected
condition-0 dispatcher writes only 0 then 2. Do not expose arbitrary parameter
mutation. A future acknowledged scene-owner cancellation may atomically cancel the
logical aggregate as an explicitly labeled native policy; it must not be called
an emulation of parameter-3 value 1 or of Circle behavior.

## What retained original states establish

Two independent existing capture histories were inspected, with hash checks for
the original archive/REQUIRE/image, resident event code, selected worker routines
and tables. The event inspector independently verifies current/cache event ID 4,
a unique whole archive-payload match, saved interpreter stack/wait context, the
condition-0 command at **810** and its E5-56 wait at **813**.

- Source frame **9003**, retained from the earlier Circle-hold continuation:
  event slot 1 waits at 813, dispatcher slot 3 runs condition 0/H=60/task 56,
  child slot 4 has task 0 and six queued glyphs at **scale 48**, bank 1.
- Source frame **9110**, retained from the independent no-input 600-budget probe:
  the same event/dispatcher/child relationship holds, with six queued glyphs at
  **scale 128**, bank 0. This does not make 9110−9003 a duration measurement.
- Both states' selected texture nibbles and palette-bank-2 VRAM match verified
  image 0. All six queued color tuples match the source scale calculation; page
  6/CLUT and reverse submission order 5..0 agree. Preceding draw mode is
  `0xE1000006`, with dithering disabled, for these selected queued packets.

A native-register trap was checked explicitly: at fade-in yield `0x801ca440`,
saved s3 contains the **next** scale because the preceding `jal thread_yield`
executes `addiu s3,s3,8` in its delay slot. Frame 9003 stores 56 but submitted 48.
At the hold yield `0x801ca524`, the draw uses constant 128; saved s3 is not its
scale. Do not classify Ready animation by an unverified saved-register guess.

Two final study reports repeat byte-for-byte. This is frozen RAM/queued-packet
inspection, without executing original instructions or an emulator. No new
scanout comparison, Circle response observation, release/clear continuation,
original event entry or display-latency result is claimed.

## Implementation split

**ZOD-084:** pure Ready aggregate and atomic ordered VM/ledger proof. One retained
dispatcher identity owns child progress, including the no-draw extra yield and
child-terminal/dispatcher-live gap. Task Start/Complete/Cancel share existing
command budgets; internal child phases are not fabricated class-56 tasks. Preserve
immutable IDs/state, managed update authority, checked counters, full rollback,
other-owner E5 membership and deterministic replay. Keep old general APIs/profiles
unchanged. Execute RDY-001..028 with meaningful boundary cases. No files or graphics
enter Core; no public half-ticks or arbitrary callbacks.

**ZOD-085:** select the Ready artwork/preparation/rendering contract from the pinned
tables and helpers, including full-corner arithmetic, average blending, explicit
draw state and preloaded resource ownership. RDY-031 stays unexecuted until that
presentation selection has an implementation. Original scanout/timing comparison
may need additional captures; it remains a separately stated validation gate.

**ZOD-086:** strict separate authored condition-0 profile and prepared-files-only
host. Decode unsigned operands, reject unsupported provenance/conditions even
unreachable, load resources before logical admission, then demonstrate finite
repeatable host/replay checks. Execute RDY-029/030/032 and presentation regressions.
One Ready per selected scene; no concurrent shared-pool effects or general→Ready
combined retail-event playback is implied. Broader sequences need their own
retained-lifetime/resource contract.

This research changes no runtime, tests, packages or assets. The last full
implementation validation remains **1,629 .NET / 163 Python tests** from ZOD-082;
source/report/specification/coverage checks are recorded separately. Standing
owner permission authorizes committing/pushing this completed source milestone.
