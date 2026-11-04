# One fresh pair command entry — ZOD-223

Implemented 2026-11-04 from [ZOD-221](../architecture/zod-221-fresh-pair-command-entry.md),
pinned to read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`AttackThrowStoneAiCommandExecution.Execute` accepts the current Framework
session, immutable pair request/configuration and ordinary dispatch assertions.
It privately owns preparation, reach derivation, selection and dispatch within
one `FacingDecisionBattle` callback. Callers select neither reach nor command
order and provide no precomputed plan or externally advanced RNG.

Framework validates actor/session authority first. Move must remain unused.
Actual ACT-used selects fresh post-ACT preparation; otherwise both choices are
unused. Prepared enemy minimum reach and shared restored-state target-distance
rows select the branch before scoring. The existing scenario continuations then
own one movement prefix, action comparison/ranking and lottery. Out-of-reach
NonBlue halving still occurs after scoring and never reclassifies that branch.
The shared geometry query adds no refresh, score or random draw.

The selected plan dispatches in source order: needed Move when permitted,
otherwise ACT-used/empty END, otherwise actual typed Attack or Stone.
Before ACT, Move freezes its typed follow-up through the
[existing continuation authority](facing-decision-planned-move.md). After ACT,
Move ends the turn and settles actual-receipt facing internally. Immediate ACT
keeps unused Move available without a completion receipt. Same-tile empty END
uses Framework's ordinary refunds and facing. No hypothetical unit state or
predicted pre-ACT D1 becomes live authority.

Success diagnostics distinguish before/after ACT decisions and record initial
reach, actual command kind and decision RNG. Only committed pre-ACT Move returns
a continuation. Failed queries/commands expose reasons and preserve the exact
incoming session, empty events and no partial diagnostic/path/token. A new
post-ACT failure preserves an earlier published ACT; continuation failure
preserves its earlier Move. This is one command per invocation, not an atomic
whole-turn operation or a next-turn clock driver.

Existing explicit-entry APIs remain supported and share the same internal
scenario/selection kernels. Unknown in-range actor cast priority still rejects
without rescore or zero substitution. Fresh-refresh/dispatch, no-Defend and
other unrepresented context assertions remain explicit. Terminal living-pair
reselection remains unsupported, including after successful lethal ACT.

## Verification

Five scenario families compare all pre-ACT command shapes with established
literal streams and separate pure selectors, exercise actual ACT then automatic
post-ACT reach derivation (push/miss included), exact/short budgets, source/choice/
provenance/terminal guards, both actors and reordered replay. Move continuations
retain exact-session binding. Only nine new .NET cases were added across the
Framework extension and this adapter; existing behavioral coverage is reused.
Targeted TRX: ignored `scratch/ai-command-entry-221-223/223-trx/`.

Locked restore, zero-warning/error Release rebuild, all 4,122 .NET cases
(1,459 Framework and 918 AI), all 534 Python tests and the pinned source-guide
audit pass. Seven full TRX files and preservation/source reports are under
ignored `scratch/ai-command-entry-221-223/`. Owner content/editor drafts
participated in validation and remain excluded from these commits. No C/original
execution or UI review is claimed.

The next bounded step is composing a complete headless living-pair turn from
this entry, retained continuation and actual facing receipt, with explicit
partial-progress recovery. Host/editor activation, complete original AI,
terminal battle policy and broad replacement interfaces remain outside scope.
