# Pure post-ACT movement tile scoring — ZOD-180

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Extends the
[ACT-first contract](../architecture/zod-176-ai-act-first.md) and
[movement-only baseline](sword-ai-movement-baseline.md) after actual ACT.

## Entry and source ordering

[`SwordAiMovementBaseline.EvaluateAfterAct`](../../src/Zodiac.Framework.Ai/SwordAiMovementBaseline.cs)
accepts the actual `AttackBattleSnapshot`, actor/target/destination, existing
candidate/score/baseline certificates and explicit hypothetical decision RNG.
It requires the active actor's ACT used and Move unused. Original `Evaluate`
still requires ACT unused and preserves already-moved stay admission. No fake
ACT-unused snapshot or caller-populated cached simulation result is accepted.

The same four baseline facts remain explicit: original scenario-zero destination,
fresh source score cell, normal scoring and canonical coordinates. Existing
ordinary two-living-opponent geometry, equipment, status/upkeep, team and scoring
certificates are unchanged. Current HP/status/positions/facing are used, including
actual Critical and knockback. KO/ended turns remain outside this entry.

At the pinned reference, `turn.c::reachable_tiles_score` installs move-before-ACT
and skillset zero, then calls `evaluation.c::ai_action_simulate_and_score` for each
source-mask tile. `movement_pickup_simulate` changes coordinates and marks Move
used only for a changed canonical coordinate word. Skillset zero skips action
preparation/formula work, retains actual `action_taken`, and sets hit to 100.
`clock.c::turn_end` adds each genuinely unused choice's 20 CT in u16 arithmetic
before capping at 60 and storing CT. Therefore this entry uses:

- Staying: `min(60, liveCT + 20)`; Move remains unused.
- Changing tile: `min(60, liveCT)`; Move is marked used.

The shared private clock and absolute scorer run once. One living upkeep draw
is retained, with no action/formula draw or automatic refresh. Highest CT then
original TieKey selects the first ready unit; its choices clear for its next
hypothetical turn. Thus actor ACT stays used unless the actor itself becomes
that first ready unit. Score remains allied-minus-enemy with unchanged equipment
and signed narrowing. No packed score grid is allocated.

Every rejection hides partial units/score/ticks and returns the incoming decision
RNG. Success returns immutable diagnostics and progressed hypothetical RNG;
live battle/turn RNG, choices, ticks, HP/status, positions and facing are unchanged.
Callers can explicitly carry fresh preparation's RNG into this query; the query
neither consumes nor trusts its public outcome.

## Validation and limits

Six scenario tests reuse existing ACT-first fixtures and the shared clock/scorer
coverage rather than duplicating their arithmetic matrices. Actual hit/miss
leave actor CT15 and opponent CT95. Staying ends at CT35 and advances the actor
to 40; moving ends at CT15 and advances it to 20. One tick selects the opponent
at CT100, reducing it to zero. Actual target HP25/50 gives absolute priority64;
an actual miss gives priority0. Their one-draw results are respectively
word3535774167/count25/raw21183 and word1343201072/count30/raw20495.
An actual power8 hit leaves target HP10/Critical, whose HP term25/status term-32
give priority135. Actual critical knockback frees the former target tile for
movement and blocks the target's new tile. Tests also cover Move0, CT99/cap,
legacy/source/choice/geometry admission, exact MAX draw/tick budgets, atomic
failure, either actor, collection order, carried RNG and input isolation.

All 31 focused old/new movement-baseline cases pass. Locked restore, clean Release
build, **3,967 .NET cases / 516 Python tests** and the existing pinned source audit
pass. Logs and seven full TRX results are ignored under
`scratch/ai-post-act-scoring-180/`. The audit verifies the 20 ACT-first anchors and
inherited receipts; no historical source evidence is rewritten. Owner content
drafts are included in validation and excluded from this AI commit. No C/original
execution or UI review; this does not establish full original AI equivalence.

ZOD-181 adds fresh post-ACT movement scenario derivation across the source mask,
including source ordering, out-of-reach adjustment and D0 copying D1 after ACT.
Empty-action ranking/lottery, live Move/Wait handoff, broad replacement interfaces
and host/editor activation remain separate work.
