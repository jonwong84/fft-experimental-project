# Remaining pair ACT and END entries — ZOD-216

Source/design study, 2026-11-04. Read-only fft_decomp HEAD is clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The existing
[algorithm index](../evidence/ai/algorithm-guide-v1.json) pins the full files and
function bodies below; its verifier checks their hashes against this revision.
This extends the [ZOD-199 provenance study](zod-199-pair-out-of-reach.md) and
[ZOD-203 command design](zod-203-throw-stone-command-boundary.md).

## Source path and ordering

In `turn.c::scenarios_build`, distance greater than the prepared enemy minimum
reach selects the out-of-reach branch before movement scoring. The NonBlue
modulo-seven reach draw follows scoring. D0 is copied from D1 on this branch;
there is no in-reach ranged distance-floor calculation.

`evaluation.c::origin_reachable_evaluate` constructs cast origins, ranks them
through `origin_next_take`, and calls `origin_destination_finalize`. The latter
sets ACT-first exactly when the considered origin equals the actor's actual
origin, then substitutes the scenario destination. Neither the out-of-reach
branch nor D0=D1 implies Move-first. With Move0, the no-closer-cell fallback
freshly scores the actor; a living enemy at distance2..4 remains in Stone range.
An ACT-first Stone therefore has a fully described fresh-origin path here.

This does **not** resolve Move1/distance3..4: the unscored inserted actor can be
read by `origin_next_take`. Preserve `UnwrittenCastPriority` before command
authority; do not fill it with zero, rescore it or accept caller-supplied memory.
At distance5 the actor cannot cast Stone, so a known-score selection can still
produce an empty MoveOnly plan. Use the existing owning out-of-reach selector
unchanged to enforce these distinctions.

`turn.c::general_select` seeds movement ranks, evaluates abilities and calls
`ranked_select`. Failure through all eight slots calls `fallback_select` at
scenario0. Without Defend support it selects skillset0, clears ACT-first and
uses D0. `ai_command_build` then checks different-tile MOVE first; otherwise,
skillset0 emits END, even when ACT is still unused. This is a **fresh selection**
result, distinct from its earlier saved-choice/no-progress guard.

Pinned source entry points:

- [ai_command_build / general_select / scenarios_build / ranked_select / fallback_select](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/turn.c).
- [origin_reachable_evaluate / origin_next_take / origin_destination_finalize](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/evaluation.c).

The local checkout and hash index are the evidence authority; these links identify
the pinned paths. No C code or original executable was run for this study.

## ZOD-217: out-of-reach ACT-first

Add an explicit `StartOutOfReach` to the pair ACT executor, using the current
four-input request/configuration/dispatch shape. Share admission, typed actual
ACT proposal and failure handling with the in-reach path. Keep distinct typed
diagnostics for the two owning selections. Run selection inside the existing
Framework callback, once, starting from its current RNG. Only an ACT-first plan
with a nonempty action may propose Attack or Stone. Framework revalidates the
actual command and adopts the verified decision stream once.

Check actual Stone hit/miss/push/KO at a fresh Move0 origin, source reach-halving
and RNG order, unknown-key rejection, empty-plan rejection, current choices,
exact/short actual draw budgets, both actor identities and replay. Keep Move
unused and publish no completion receipt after ACT. Subsequent movement must
use fresh post-ACT selection from actual results. Terminal reselection remains
unsupported; a successful lethal ACT must remain published.

## ZOD-218: same-tile fresh fallback END

Add a separate fresh both-unused fallback entry for each reach branch. It owns
the appropriate existing selector inside `FacingDecisionBattle`; admit only
MoveOnly with null action and destination equal to the actual actor tile.
Reject action plans and different-tile plans rather than taking a cheaper END.
Use `PrepareWait` and deferred closed-enemy facing; Framework owns both unused
choice refunds, CT cap, revision and atomic publication. No action, path, clock
advance or continuation token is introduced. Preserve no-Defend/source context
guards and all unknown cast-key restrictions.

Check no-useful-target/zero-accuracy fallback, eight failed lottery draws,
both-unused refunds, wrong branch/current choices, short budgets and replay.
This completes a missing dispatch shape, not the original general AI policy.

## Boundary and later work

ZOD-219 will group AI source/tests into meaningful folders while preserving
namespaces and public contracts, and repair current documentation links. Historical
hash snapshots remain historical. ZOD-220 will exercise composed command flows
and document caller routing, actual-result branch changes and per-command failure
recovery. It must not introduce a host, cached-plan API, speculative retry router,
global turn transaction or broad replacement contract.

Broader ability/roster/status/map policy, event/workspace history, original frame
RNG and terminal battle routing remain in the [completion roadmap](../development/ai-completion-roadmap.md).
