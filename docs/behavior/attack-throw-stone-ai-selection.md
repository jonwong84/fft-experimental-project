# Fresh terminal-pair selection — ZOD-194

Implemented 2026-10-29 against clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[ZOD-192 source design](../architecture/zod-192-two-entry-ai.md#zod-194-fresh-pair-selection).

`AttackThrowStoneAiSelection.Evaluate` owns one fresh
[pair composition](attack-throw-stone-ai-ranking.md), then a shared source
lottery. Terminal Throw Stone certifies no other retained/searchable entries;
no Defend support and fresh ordinary selection defaults are separate required
assertions. All default false. Existing family admission, order, masks/D0/D1,
prepared usage, fresh ranks and clear-direct-path certificates still apply.
The public query accepts original state/profiles/RNG, never cached ranked output.

`AiRankedLottery` now serves both this pair and the existing Attack-only selector.
Each visited slot consumes real BIOS RNG, including empty zero-hit slots, and
passes when raw%100 is below its hit percentage. Selection stops on the first
pass. If all eight rolls fail, any nonzero-hit action makes the source retain
slot0; otherwise it chooses movement-only at D0. The fresh admitted lists are
contiguous positive-hit actions followed by empties. The old selector maps
shared diagnostics back to its existing public roll/reason/result types, retaining
its admission and atomic whole-query failure behavior. No public cached-rank
API, changed live transaction or generic ability-list execution is introduced.

The new plan reports Attack, ThrowStone or MoveOnly, original actor, actual
planned destination/order, and deferred `ChooseFacingAtWait`. Its optional
typed action carries fresh family diagnostics. These are hypothetical values:
no projected damage is applied, facing resolved, command receipt issued, live
choice consumed or RNG adopted. Lottery failure exposes its slot and reason
only, dropping the privately computed ranks/rolls/plan and restoring incoming
whole-query RNG. This managed failure policy does not claim original C rewind.

Literal examples use the ZOD-193 two-cell mask, actor PA5, target HP50 and incoming
draw count7. Composition costs12 draws. With seed1 and accuracy100, slot0 rolls
raw9084/mod84 and selects immediately; final RNG is `(595337866,20)`. Sword
power1 selects Throw Stone; power4 selects Attack. With seed10, evade75 and
power4, Attack's raw7136/mod36 fails chance25, then Throw Stone's raw13/mod13
passes: final `(2148343772,21)`. The second plan retains Throw Stone's Move-first
destination rather than Attack's ACT-first destination.

With seed1 and both hits25, both real entries fail, six empty slots still draw,
and the first Attack survives: eight rolls, final `(3823029677,27)`. Inactive
entries cost only the two prefix draws then eight empty rolls, yielding move-only
at D0 and `(267834847,17)`. Both NotValuable entries retain their evasion draws
before the empty lottery, ending `(3535774167,25)`.

Five new scenario tests cover those literals, all typed plans/order/facing
deferral, first/second/last-roll exact and short budgets, source admission,
composition failure and both-actor/mask/order replay with no live changes.
All existing Attack/post-ACT lottery regressions exercise the shared refactor.
Locked restore, zero-warning/error Release build, **4,024 .NET cases** across
seven projects (837 AI), **524 Python tests** and the clean inherited source/
prepared-data audit pass. Logs, `source-194.json` and seven full TRX files are
in ignored `scratch/ai-two-entry-192-194/`. Owner content drafts participate
in validation and remain excluded from these commits. No original/C execution
or UI review. Original frame/VSync stream equivalence remains unresolved.

ZOD-195 traces fresh two-entry preparation/scenario facts before choosing their
next implementation. General lists/statuses, blocked trajectories, replaceable
AI interfaces, live pair command authority and host/editor activation remain
separate work.
