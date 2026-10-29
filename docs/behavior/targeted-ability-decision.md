# Framework-owned targeted-ability ACT transaction — ZOD-204

Implemented 2026-10-29 from the
[ZOD-203 source/design contract](../architecture/zod-203-throw-stone-command-boundary.md),
pinned to clean read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.
This adds authoritative command plumbing, not a new formula or AI policy.

`FacingDecisionContext.PrepareTargetedAbility` creates an invocation-bound proposal
carrying `UseTargetedAbilityCommand`, immutable `TargetedAbilityBattleProfile`,
progressed decision RNG and diagnostic. The existing synchronous transaction
closes the context, rejects foreign/cached proposals, checks actor ownership and
verifies forward RNG continuity before staging both snapshot RNG copies. It
does not adopt hypothetical units, CT, ticks or positions.

`FacingTurnBattle.UseTargetedAbility` delegates to the existing actual
`TargetedAbilityBattleSimulator.Apply`. Ability/grant/range, current choices and
the bounded ordinary setup are checked against the actual state. The profile
currently supports Throw Stone only; its distinct command/profile do not imply
generic ability support or sword mechanics. Framework still has no AI dependency
and grants no new production friend access or public receipt constructors.

Actual resolution retains four evasion draws on a miss and seven resolution
draws on a hit, with current PA and real Brave-based push. Lethal damage and
blocked displacement do not refund hit draws. No projected midpoint damage or
knockback is copied into the result. Victim choices/CT/facing remain unchanged.

One successful ACT adds one revision; decision adoption adds none. With unused
Move, the actor remains active with ACT spent and no completion receipt. With
Move already spent, the receipt binds the complete final damage/KO/displacement
snapshot, actual RNG, revision and lineage. Explicit `FinishFacing` adds one
revision and no second refund, RNG draw or clock advance. It rejects a receipt
from an otherwise identical replay. No automatic post-ACT move or facing occurs.

Authority/revision/pending-facing guards run before planning. Invalid proposals,
RNG, ability/grant/range, spent ACT or late actual draw exhaustion publish the
identical incoming session, empty events and no success diagnostic. Both RNG
copies retain incoming values on failure, including after private planning
progress. A prior successful Move is already part of that incoming state and
is not rewound. Budgets use actual draws: exactly four remaining can admit a
miss. ACT at revision MAX-1 can succeed; a later facing operation at MAX rejects.

## Literal checks and validation

A healthy fresh session starts with seed1/count7, actor(2,1), target(6,1), PA5,
Brave70/70, actor CT95/Speed20 and peer CT0/Speed1. Advance costs one upkeep draw
and one tick, leaving actor CT15 and RNG `(1103527590,8)`. The test planner's
eight supplied draws reach the ZOD-203 checkpoint `(3210001534,16)`; they do not
claim a fresh AI query. Actual Throw Stone then deals 5, pushes to (7,1), and ends
RNG `(3297241969,23)`. PA6 deals 6 with the same schedule. Front evade100 misses
after four actual draws at `(595337866,20)`.

Already-moved cases bind post-push, KO or edge-canceled results in the final
receipt. The KO fixture starts healthy with maxHP5 rather than bypassing fresh
session admission; the first focused run caught and corrected its initial
Critical setup without changing runtime rules. Seven Framework scenarios cover
these outcomes, exact/short budgets, late rollback, invalid/stale/closed proposals,
actual command admission, receipt/revision limits and both-actor/order replay.

Locked restore, zero-warning/error Release rebuild, **4,061 .NET cases** across
seven projects (1,449 Framework, 867 AI), **532 Python tests** and the pinned
source/prepared-data audit pass. Logs and seven full TRX files are under ignored
`scratch/targeted-ability-decision-204/`. Owner content drafts participate in
validation and remain excluded from this commit. No original/C execution or UI
review; whole-frame RNG equivalence remains unresolved.

ZOD-205 extends the existing confirmed-Move proposal and exact-session continuation
with a frozen targeted ACT. Fresh AI execution adapters, pair post-ACT reselection,
general actions/policies and host/editor activation remain later work. Existing
Attack/Wait/Move transactions and unknown-score restrictions are unchanged.
