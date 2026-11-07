# Supplied VSync and the frame RNG callback — ZOD-430 supporting trace

The owner's SCUS ETC VSYNC page is the SDK `VSync` at0x8001DBA8. All82
supplied instruction words match the hash-verified prepared SCUS executable.
The stated return0x80193E74 matches `ai_command_build` calling it; mode1 is
loaded at0x80193E64 before the call, not in its delay slot. The
[receipt](../evidence/ai/vsync-rng-trace-v1.json) pins reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`,15 source/metadata files and10
functions. Reference HEAD was checked clean/read-only; original code was not run.
Function fingerprints identify these call/registration inputs, not full review
of every startup, IRQ or GPU path.

## What AI reads

`VSync` initially captures the low16 bits of timer1_count-Hcount. Mode1 returns
that captured elapsed value immediately, before either `v_wait` call and before
updating the SDK baseline. `startIntrVSync` configures timer1 to count horizontal
blanks. The result is an elapsed hardware-counter reading, not a gameplay CT
tick count, callback count or request to wait one frame.

`ai_command_build` compares that reading with441 and can return-1 before fresh
decision work or resumption. Other source AI sites use decision threshold441,
pathfinding325 or turn-simulation313. These timing checks are separate from the
between-turn controller's1201 counted phase iterations and the AI simulation's
256 event-attempt limit. They must not be collapsed into one budget.

Mode<0 returns `Vcount`. Mode0 and modes>1 reach the two verified calls to
`v_wait` at0x8001DCF0; then optional GPU height/field-change polling and baseline
updates occur. Even those modes return the elapsed value captured before the
waits. The pasted480p/240p labels are not used as a hardware-mode specification
here: the reviewed code checks the height480 status bit and field-change bit.
`v_wait` also has timeout/interrupt-control work, but that branch is not entered
by AI's mode1 query. No separate wait-helper wiki page is needed to establish
this query path.

## Where the RNG draw happens

`VSync(1)` contains no direct `rand()` call. The source registration chain is:

1. `boot_startup_run` installs `system_vsync_callback` using `VSyncCallback`.
2. SDK initialization binds the VBlank slot setter through
   `startIntrVSync`/`setIntrVSync`; the callback is stored in slot0.
3. `trapIntrVSync` increments `Vcount` and invokes installed callbacks in slot
   order when handling VBlank.
4. `system_vsync_callback` at0x800434DC calls `rand()` once, unconditionally,
   before its play-time and wait-frame counters are updated. Its one direct
   RNG call at0x800434E4 matches the prepared executable.

This is one draw per invocation of this installed callback, not one draw per
`VSync(1)` query or per AI candidate. Other game routines can draw as well.
Interrupt execution can interleave with AI; absence of a direct RNG call in the
query does not prove no asynchronous RNG progress around it. No new generator
is selected: the callback calls the same BIOS RNG wrapper already verified in
the [upkeep/RNG review](turn-upkeep-review.md).

## Compatibility boundary and next evidence

The static call chain confirms the source of frame-related draws. It does not
establish exact IRQ timing, all competing callers, number of callback invocations
during one AI decision, or a complete retail incoming RNG state. Those remain
part of the existing frame/VSync compatibility investigation.

Current managed decisions remain independent of host rendering, wall time and
file I/O. This trace does not authorize wiring the renderer's frame callback to
gameplay RNG, or adding hardware-timing suspension to pure queries. No runtime
code or historical fixture changed; unchanged gameplay tests were not repeated.
The owner's local `AiTurnController.cs` edit is preserved. ZOD-430 remains open.

If available, the most useful next wiki excerpt for this thread is the **installed
VSync/VBlank callback at0x000434DC**, which directly advances RNG. Additional
timing evidence would still be needed to settle whole-game stream equivalence.
