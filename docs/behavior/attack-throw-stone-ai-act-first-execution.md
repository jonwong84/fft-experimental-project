# Fresh in-reach pair ACT-first execution — ZOD-208

Implemented 2026-10-29 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following
[fresh pair selection](attack-throw-stone-ai-prepared-selection.md), the
[command-boundary design](../architecture/zod-203-throw-stone-command-boundary.md)
and Framework's [ACT transaction](targeted-ability-decision.md).

`AttackThrowStoneAiActFirstExecution.StartInReach` owns fresh in-reach selection
inside `FacingDecisionBattle.Execute`. Framework first validates session/actor
authority. The callback requires both choices unused and the existing ordinary
dispatch-context assertions, then evaluates the pair against the exact current
battle and RNG. It accepts only an ACT-first, nonempty typed Attack or Throw
Stone. Move-first, MoveOnly and unsupported selection/context reject before
adoption; the caller cannot supply a cached plan or replacement command.

Framework verifies and adopts decision RNG once, then applies the selected
actual command with its real rules. Successful execution publishes actual
effects, synchronized RNG copies and one session revision. ACT is spent; the
actor retains the turn and unused Move, including when Move is zero. There is
no automatic END, Move token or completion/facing receipt. Predicted D1 remains
diagnostic and cannot authorize movement after the real ACT.

A late command failure rolls back the entire transaction, including decision
RNG adoption. Earlier selection failures also publish the original session,
empty events and no partial decision. These are Framework's existing authority
and atomicity rules; this adapter adds no AI access to trusted state/receipts.

## Literal checks and validation

The healthy two-unit setup starts at CT15/95 and decision RNG `(1,7)`, actor(1,1)
and target(2,1). With Move3 and sword power2, selection chooses ACT-first Attack
and predicts D1=(1,4), consuming36 draws to `(3397090333,43)`. Actual Attack at
the original origin consumes6 more, deals10 and ends `(995405759,49)`. The actor
stays (1,1), ACT is spent and Move remains available.

With Move0 and sword power1, selection chooses Throw Stone after13 draws at
`(595337866,20)`. Actual Stone deals10 without push and ends `(3823029677,27)`.
Entry word20 instead selects at `(2711922929,20)`; actual Stone deals10 and
pushes the target to (3,1), ending `(2928661168,27)`. Neither result spends Move.
With front evade75, selection takes20 draws to `(3823029677,27)`; real Stone
misses after4 more, ending `(675780521,31)` with no damage or push.

A healthy maxHP10 target selects Stone at `(2937909499,21)`; real10 damage
produces Dead/KOCount1 without displacement at `(3914758370,28)`. The ACT
adapter still stops with unused Move; it does not invent terminal-battle or
post-ACT policy. Re-entering this fresh both-unused adapter then rejects.

Exact total query+ACT budgets42/20/20/24/21 reach draw-count MAX for these
branches. One draw short rejects the actual command and discards the private
query/adoption as well. Earlier refresh/selection exhaustion also rolls back.
Six meaningful scenarios cover these literals, admission, pure-query agreement,
actual state/events, both actor identities, reordered inputs and replay without
changing the original snapshot.

Locked restore, zero-warning/error Release rebuild, **4,085 .NET cases** across
seven projects (885 AI, 1,455 Framework), **532 Python tests** and the pinned
source/prepared-data audit pass. Reports and seven full TRX files are under
ignored `scratch/ai-pair-act-first-208/`. Owner drafts participate in validation
and remain excluded from this commit. No original/C execution or UI review;
frame/VSync stream equivalence remains unresolved.

ZOD-209 should trace pair-specific post-ACT refresh, preparation, movement and
settlement from real hit/miss/push/KO results, then select one bounded child.
The Attack-only post-ACT list certificate cannot substitute for a pair list.
ACT-first out-of-reach entry, retained/event score history, general policies,
replacement interfaces and host/editor activation remain later work.
