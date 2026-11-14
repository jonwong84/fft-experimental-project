# Out-of-reach derived sword selection

ZOD-149, 2026-10-28. Implements the [ZOD-148 contract and admission correction](../architecture/zod-148-ai-out-of-reach-selection.md)
at reference **11464943bbdbd2a0af022d90cb7f10455cf911df**.
[Implementation receipt](../evidence/ai/out-of-reach-selection-implementation-v1.json).

`SwordAiDerivedSelection.EvaluateOutOfReach` owns one out-of-reach movement query,
its actual private S scores and post-adjustment RNG, then the existing validated
Attack scan/composition/lottery tail. It requires explicit original Blue/NonBlue
team context and the same closed ordinary state/source profiles. S/A remain
distinct: adding the actor to A does not score it or invent a priority cell.
Immutable output preserves movement adjustment/team/fallback diagnostics and
selection; public masks, destinations, cached scores and ranked results are not
accepted. The in-reach entry shares preflight and continuation but keeps its
explicit branch behavior.

Both derived entry points now require **FirstAttackEntry**, default false, in
addition to the terminal-entry certificate. This corrects ZOD-144's missing
earlier-ability fact: an empty list or fresh per-entry best cannot prove earlier
work consumed no RNG. Existing supported in-reach test contexts explicitly set
the stronger fact; their literal original outputs are unchanged. Legacy supplied
APIs retain their existing NoInterveningRandomWork certificate/signatures.

Out-of-reach flat geometry makes all ordinary Attack origins non-adjacent, so
the actual scan emits no candidates. Eight real zero-hit lottery draws then
choose MoveOnly at D0=D1 with deferred ChooseAtWait. The query still validates
weapon, profiles and NoDefendSupport before scoring, including inactive targets.
No live commands, HP/CT/choices/RNG, facing or host/editor flow are changed.

Source vectors use three S cells and no extra actor simulation. Seed1/count7
Blue ends word180171308/count18 at x5; NonBlue consumes raw17515 and ends
word836760821/count19 at x5. Seed19 Blue ends word1844253734/count18 at x5;
NonBlue raw224 halves reach4 to2 and ends word1165580391/count19 at x3.
Total cost is |S| + (Blue:0, NonBlue:1) + 8. Formula draw cost is zero.

Failures at admission, movement, adjustment, scan or lottery return original
whole-query RNG, typed nested failure/index and no partial scenario/plan.
20 focused cases cover the vectors, actual S reuse, inactive/singleton paths,
both derived APIs' missing-first rejection, invalid context, late failure,
exact counters and both-identity/order replay. Existing shared-tail cases remain
valid; 147 focused cases pass.

Locked restore, zero-warning/error Release build, **3,780 .NET / 496 Python**
tests passed. Full results: ignored `scratch/ai-out-of-reach-149-full-trx/`;
focused: `scratch/ai-out-of-reach-149-focused-trx/`; Python/source reports:
`scratch/ai-out-of-reach-149/`. No C/original execution or UI review. Owner
definition/refactor drafts present during validation were included in builds and
tests, excluded from this commit. Full ability search and live command execution
remain open; wait-facing source study is next.
