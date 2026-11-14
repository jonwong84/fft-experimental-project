# ZOD-176 — Ordinary ACT-first and fresh post-ACT selection

Source/design, 2026-10-28. Read-only clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base is recorded in the
[source receipt](../evidence/ai/act-first-contract-v1.json). Twenty pinned function
bodies extend the [command lifecycle](zod-165-ai-command-continuation.md) and
[unmoved preparation](zod-171-ai-unmoved-preparation.md) evidence. No production
code, C/original execution or host activation is added in this source ticket.

## Actual ACT and remaining Move

After fresh selection, ai_command_build emits ACT when ACT is unused and the
selected Attack is act-before-move. This is true even if its hypothetical future
destination differs from the actor's tile. The current action payload is copied;
the saved moved/acted bytes describe **pre-command** state. No live movement or
RNG work occurs in this dispatch. Saved00/current01 after actual ACT is therefore
a change, not no-progress.

Controller action_execute_ability calls turn_act_taken_set before resolving the
ordinary immediate action. turn_move_act_taken_set leaves Move unchanged.
With no movement/action restrictions, Move unused and ACT used does not end the
turn, refund CT or advance the clock. Real combat rolls, HP/status/KO and possible
critical knockback settle before control returns. Presentation temporarily turns
the actor; reward_grant calls facing_all_after_attack_restore. The existing
[settled-facing contract](zod-112-sword-settled-facing.md) and implemented sword
preserve incoming living actor facing at the managed settled boundary.

AfterCommand routes through ContinueTurn. An ongoing unit turn reopens action
menus and eventually ai_command_enter; an ended turn opens Wait without inventing
another AI decision. ai_command_enter resets readiness/frame counters, not selected
action data. Mounts, charge, reactions, restrictions, scripts and control changes
remain excluded by this ordinary profile. Presentation frames still have original
BIOS draws; managed pauses do not add gameplay draws or establish frame equivalence.

## Post-ACT search is fresh

On re-entry the exact decided0xff/saved-choice equality guard runs first. Unchanged
choices return END without refresh, even with ACT used. Changed saved00/current01
passes that guard and action_taken forces fresh refresh/preparation/selection.
Keeping the earlier selected Move as executable continuation would skip real work.
The old command-history pair is not this guard or a managed authorization token.

Refresh again installs act_before_move=1 and skillset0, so its no-action path does
not simulate a move or another action. Actual post-ACT HP, status, positions,
facing, CT and RNG are now the baseline. With Move unused/ACT used the temporary
turn ends at min(60, liveCT+20): only the unused-Move refund applies. A physical
move-only score later ends at min(60, liveCT), or min(60, liveCT+20) if staying.
The source uses u16 CT arithmetic before its byte store. Each closed living clock
pass still takes one upkeep draw. Baseline unit/clock state restores after each
hypothetical pass; decision RNG is retained, never restored to the pre-ACT stream.

ability_considerations_set first computes ordinary ability facts. In the actor's
second pass, reach/usage counts are computed **before** action_taken clears each
usable bit. For the closed range-one Attack list, enemy reach remains raw Move+1
and ally reach Move; ACT-used must not be translated as zero reach. ai_ability_entry_load
maps the cleared usable bit into the loaded flags; ai_ability_outcome_evaluate
returns before target/formula work when that bit is absent. Enemy summaries remain
their own data. No new ordinary Attack is ranked on this post-ACT pass.

Reachable tiles still use the restored unmoved body. scenarios_build uses the
same source distance/reach partition and post-score NonBlue adjustment when
out of reach. With action_taken, D0 copies D1 instead of choosing a stricter
closer destination. Current movement-only helpers reject ACT-used input and add
the unused-ACT refund, so they cannot be reused unchanged. Fresh ranks are empty;
ranked_select still consumes eight zero-hit rolls before fallback. NoDefend stays
explicit; fallback skillset0 and act_before_move0 describe movement only.
Fresh dispatch produces Move for a different tile, otherwise END. A completed
post-ACT Move ends the turn and uses existing settled-facing authority; do not
append an unconditional new refresh after an ended turn.

## Existing managed authority and selected child

FacingDecisionBattle already owns fresh planner invocation, forward-RNG validation
and one actual Attack transaction. FacingTurnBattle.Attack with both choices unused
returns a newer active session with ACT used, Move unused and **no PendingFacing**.
It neither issues a completion receipt nor needs a Move continuation token. A
failed actual Attack restores the entire pre-ACT input including planned RNG;
after success no later query failure can rewind the published Attack.

**ZOD-177** implements only fresh prepared in-reach ACT-first execution. Use a
separate opt-in AI entry over the existing Framework single-command transaction.
Check active actor and both unused choices, remaining ordinary dispatch facts,
then privately own EvaluateUnmoved and DispatchFreshSelection. Require a selected
Attack with ActBeforeMove and actual Attack dispatch; reject Move-first/Wait rather
than route automatically. The actual Attack validates current geometry/weapon.
Do not execute or freeze the selected future destination. No external RNG, cached
selection, public planning callback or GeneralSelectionEntryContext is accepted.

Return complete preparation/decision/dispatch diagnostics only with successful
actual ACT. Expose no Move token or post-ACT command authority. Rejection returns
the identical original session, empty events and no partial diagnostics. Preserve
source/list/cache/NonBlue/policy certificates. Test complete literal hit/miss
streams, exact and exhausted budgets, current active choices/CT/facing/revision,
input isolation and either actor/order replay. Include actual terminal result
handling without silently running a second selector or Wait. Existing Move-first
and already-moved APIs remain unchanged.

Subsequent work must add explicit post-ACT refresh/scoring/selection entries and
then their live handoff. Reuse shared internals with an explicit action-used mode;
do not fabricate an ACT-unused snapshot or remove old API admission. KO, Critical,
knockback, no surviving target, scripts and automatic battle-end routing require
separate admission/coverage before general lifecycle activation. Framework already
publishes real combat results; AI policy support is not inferred from those events.

## Checkable descriptions

The [fixture](../fixtures/behavior/ai-act-first-contract-v1.json) has five dispatch
branches, two post-ACT arithmetic descriptions and two supplied-cost tapes. These
are offline descriptions, not an original/C execution or fresh selection oracle.
Actor x2/target x1, Move1 on the 5x3 board gives four scored origins and one
five-draw projected Attack. From word1/count7, one refresh plus those costs and
one full-accuracy lottery roll ends word180171308/count18. Actual noncritical
Attack deals25, ending word3791773014/count24. Hit11 instead uses eight lottery
rolls, deciding at word3535774167/count25; its actual miss ends
word2473755763/count29. Live CT remains15 with unused Move, and settled facing
remains the incoming East. A subsequent refresh would start at the **actual**
word/count and refund Move only; it is not included in these ACT-only tapes.

The separate audit pins source files/functions and inherited receipts; the three
Python checks validate descriptions, input preservation and malformed post-ACT
facts without freezing current C# files. Reproduce with:

```powershell
python tools/verification/verify_ai_act_first_contract.py --reference C:/repositories/fft_decomp --output <new scratch report>
python -m unittest discover -s tools/verification -p 'test_ai_act_first_contract.py'
```

Managed ACT-first cases await ZOD-177. Full validation/logs are recorded in the
current handoff. Broad interfaces, full search and host/editor work stay separate.
