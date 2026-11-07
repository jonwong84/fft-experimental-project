# ZOD-092 — Original dark-screen transition contract

Select the shared event-4/event-401 **origin-mode DarkScreen** style: reserved
byte 0, mode 1, divisor 12, angle step 64 and radius step 4. First implement a
pure post-readiness worker and ordered task/barrier proof (ZOD-093); prepared
conversion and visual preview follow separately. This source milestone changes
no production C#, graphics policy implementation or existing event profile.

Date: 2026-10-25. Managed base: `6776ab6`, clean on entry. Read-only reference
HEAD, freshly verified and clean:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Evidence metadata](../evidence/events/dark-screen-source-v1.json) binds source
files, five original inputs, fourteen resident functions, three retained holding
states and two byte-identical offline reports. No emulator or original instruction
execution was used. The [thirty authored expectations](../evidence/events/dark-screen-contract-v1.json)
are future implementation cases, explicitly **unexecuted**.

## Admission, parameters and readiness

`src/battle/event/interpreter.c` sets `parameters = instruction + 1`.
Opcode 76 starts `script_dark_screen_thread` in the first idle slot and passes
that pointer as parameter 2. The bridge tags itself task 54, ensures REQUIRE is
available, and runs `require_dark_screen_entrypoint`. The worker skips the first
operand byte, reads mode/divisor as unsigned bytes, assembles an unsigned
little-endian angle step, and reads the unsigned radius step. The unused prefix
is not the mode. Both verified blocks select the same six operand bytes.

Event 4 admits DarkScreen at 794 and waits on class 54 at 801 before its general
banner at 804. Event 401 admits it at 154 and waits at 161 before its general
banner at 164. Their later RemoveDarkScreen operations are at 816/198. These are
structural observations from verified blocks, not complete original playback.

The worker sets the after-battle owner/global text pointer, then loads BONUS
image 0 except for the final-battle-won event, which chooses image 1. The selected
domain excludes that exception. `screen_support.c::conditions_load_banner_image`
yields for memory reservation, file-request acceptance, file completion and a
post-upload frame. REQUIRE loading adds its own readiness boundary. No fixed
loader duration is inferred or inserted into the logical effect.

Despite the source comment describing a tiled image, the actual drawing uses
**untextured `POLY_F4` quads**, not textured atlas samples. `SetPolyF4` gives opcode
28; semi-transparency changes it to 2A. Each packet has five payload words and
no UV/CLUT fields. BONUS loading is still an observable side effect that prepares
shared image state for later banners. A managed runtime should preload that
separate prepared artwork; it must not manufacture textured DarkScreen tiles.

## Layout and arithmetic

The 288 tiles form the observed 12×24 staggered effect layout. For row r and
column c, x = 12c−12; y = 24r−12 for even columns and 24r−24 for odd columns.
Origin mode computes `SquareRoot0(x*x+y*y)`, squares that result, then divides
by `divisor << 8`. Delay, initial delay, angle, radius and position are signed
32-bit fields. The unsigned divisor promotes the final division to unsigned;
the selected positive envelope stays safely within signed 32 bits beforehand.
A zero divisor triggers the original division guard; prepared input must reject
it as unsupported, without claiming the original clamps or recovers.

`SquareRoot0` is a leading-zero-normalized table approximation, not exact integer
square root. Its positive selected input is at most 127,296; normalization indexes
192 signed-16 table entries, shifts the factor and performs unsigned division by
4,096. All **288 calculated delays match retained original `initial_delay` fields**.
The selected range is 0..41. Exact integer square root changes 84 intermediate
roots and **six final delays**. Replacing the approximation would change ordering.

For each started tile, the worker constructs two z rotations at angle+512 and
angle+1,536, transforms `(radius*4096,0,0)` and truncates signed results by 16,384.
The four corners are the two offsets and their negatives, stored in signed-16
packet coordinates around `(x+128,y)`. The selected lattice has nine angles
(0..512 by 64) and thirteen radii (0..48 by 4): 117 combinations.

With zero x/y rotation, `RotMatrix` selects original packed Q12 sin/cos values.
For this positive x-axis input, `ApplyMatrixLV`'s split high/low passes reduce
exactly to coefficient×radius; products are safe and the low-pass scaling loses
no fractional bits because input is a multiple of 4,096. The final signed
division still truncates toward zero, especially for negative offsets. Ideal
floating-point trigonometry and arithmetic right shift are not replacements.
At angle 512/radius 48 the offsets form a diamond: `(0,12),(-12,0),(12,0),(0,-12)`.
Both saved packet banks for every retained tile have those full-size corners.
The 117 source-derived projections are calculated offline; they are not
original entry/release observations or pixel comparisons.

## Exact post-readiness lifecycle

Logical tick zero is the first animation-loop yield after loading and layout
initialization. It has no newly submitted tile draw. No Circle sample is read
anywhere in this worker. Original RemoveDarkScreen is the control signal.

Each body resume yields first, then scans tiles in row/column order. A positive
delay decrements once. Waiting entry tiles are not submitted; waiting release
tiles retain their full stored shape and are submitted. A started tile draws its
**pre-update** angle/radius before counters change.

Entry adds 64/4. The worker remains busy while the *unclamped* new angle is below
513 **or** radius below 49, then clamps to 512/48. Reaching 512/48 exactly is
insufficient: strict overshoot needs thirteen active draws. Release subtracts
64/4, stays busy while either unclamped result is nonnegative, and clamps to zero.
It likewise needs thirteen active draws, including a zero-size overshoot draw.
After the first all-finished body, the latched `done` flag causes **one additional
body draw** on the next resume before leaving either phase.

For maximum delay D, either body therefore takes D+14 resumes. Selected D=41:

- Tick 54 first finds every tile finished; task 54 still runs.
- Tick 55 draws the extra full-size entry pass, initializes the flash packets,
  changes the same worker to task 55, and yields. This Holding birth still has
  the final tile draw; it has not submitted its first flash yet.
- Tick 56 lets the earlier parent slot observe no task 54. The worker then
  submits its first hold flash. Class 55 remains a live task across the banners.

Opcode 77 calls `thread_task_find(55)`, which excludes the caller and selects
the first running match in native slot order. It changes that slot to class 54.
With no holding match it is a no-op; an early signal is not queued. The selected
managed domain permits one worker, avoiding a guessed multi-instance slot policy.
Repeated signals while entering, leaving or completed are also no-match no-ops.

Let R be an ordered tick whose event phase signals an already-holding worker.
The worker still submits **one final flash at R**, notices task 54, resets tile
delays and reaches release's first yield. Release tile stepping starts at R+1,
not in the same resume. The last release body is R+55; `thread_frames_wait(1)`
then yields with class 54 still active. At R+56 `thread_current_exit` clears
running/task and yields. The parent E5, polled before the worker, sees completion
at R+57. For authored R=100 these checkpoints are 155/156/157. The earliest
ordered parent release after Holding birth is tick 56.

These are source-derived scheduler transactions after readiness. They are not
wall-clock durations or a claim that the retained frontend budgets measure them.

## Drawing, material and shared resources

`gfx_append_gpu_primitive_to_secondary_otag` prepends each packet to the entry
after the current OT entry. Tiles are submitted in row-major order but the DMA
chain visits them in reverse. The draw-mode packet is appended last and therefore
executes before them. The same pattern places the hold mode before its flash.
The retained DMA chains contain one corresponding mode/flash pair per state.

The selected mode words are E1000006/E2000000: five-bit average semi-transparency,
dither off and zero texture window. Untextured semi-transparent pixels do not use
texture-index transparency or STP eligibility. The observed shared RGB is
48/40/16. Every submission reads that shared color; the flash's initialization
literal is overwritten before its first submission. The first bounded profile
uses that explicit constant and excludes concurrent RGB fades.

The full flash is `(128,0)..(384,256)` before the BATTLE x offset −128 and clipping
to a 256×240 view. Tiles use a separate burst pool from the general/Ready glyph
pool, so a held flash can coexist with those banner tasks, as the retained states
show. Two concurrent DarkScreen workers would share mutable burst storage and
are excluded. Do not reuse the general/Ready rule that only one *display* is live
to erase the independent holding DarkScreen task.

Select a separate **untextured RGB5 flat-quad CPU compositor** over an explicit
opaque 256×240 background. Reuse validated five-bit material/coverage primitives
where applicable; no gradient or texture sampler is needed. Preserve native
primitive order, two-triangle quad coverage, shared-edge single coverage,
degenerate-quad rejection, signed clipping, and average blending before zero-padded
RGB5 expansion. Do not merge overlapping tiles into a union or substitute one
screen rectangle during entry/release. Original transition pixel validation,
especially edge/overlap coverage, remains a required later presentation gate.

## Prepared boundary and next implementation

Offline conversion should verify the original EXE/table and selected-style
identities, then prepare immutable tile coordinates/initial delays plus the nine
selected pairs of rotation axes (four signed Q12 coefficients per angle).
This avoids a runtime PS1 math library or 16 KiB original table reader. Keep
prepared files under scratch and use stable logical IDs, strict lengths/bounds,
hash manifests, reference/provenance checks and a separate descriptor/profile.
DarkScreen needs no texture/palette asset; the enclosing scene separately
preloads the image-0 artwork required by later general/Ready displays.

ZOD-093 should implement only the pure immutable worker and atomic ordered
ledger/barrier adapter: same task identity through Entering → Holding → Leaving
→ Completed, explicit ticks/release, accepted no-match signals, retained terminal
tombstone, full late-budget rollback and render-independent replay. Managed
revisions are 0/1/2/3 for those accepted transitions. Rendering never produces
completion facts. Use prevalidated immutable definition data; no loading wait,
GPU dependency, wall clock, raw event import, native slot emulator, cancellation
or arbitrary mode support belongs in that proof. Existing authored intro/battle
and standalone profiles remain unchanged. Preparation/preview and integration
with concurrently held banners require subsequent bounded work.

## Observation and validation limits

The three retained source frames 9001/9003/9072 all have the same holding task 55
in slot 2, verified parameter pointer and yield site, the image-0 event-4 payload,
288 matching tile initial delays and 576 matching full tile packets per state.
Both flash banks and the submitted flat-color mode/flash are checked. Fourteen
functions from BATTLE, REQUIRE and the original EXE, plus the sqrt/sin-cos tables,
match resident RAM. This binds source and holding data; it does not bind an
original entry/release animation, displayed scanout or full startup duration.

Two offline reports are byte-identical. All **229 Python tests** pass, including
fourteen new authored parameter/math/timing/holding-packet checks. The literal
phase counter model and a closed-form calculation independently agree on the
55-resume body. These verify tooling/source deductions, not the thirty future
managed/presentation cases. Production C# is unchanged; the last full managed
validation remains **1,909 .NET tests**, not rerun here.

Reproduce with the ignored identity-bound plan and a fresh output:

```powershell
python tools/verification/inspect_dark_screen.py --plan scratch/dark-screen-092-plan/plan.json --reference-root C:/repositories/fft_decomp --output scratch/dark-screen-example
python -m unittest discover -s tools/verification -p "test_*.py"
```

Any new original entry/release continuation must first have a finite verified
capture plan. No new capture is necessary to close this source/contract milestone.
Other modes/steps, final-battle image 1, shared RGB fades, concurrent burst workers,
WORLD/tutorial behavior, cancellation, EventEnd, full startup and CRT/audio remain
outside the selected domain. Original bytes and generated study data remain ignored
under scratch; only authored tooling/expectations and metadata are committed.
