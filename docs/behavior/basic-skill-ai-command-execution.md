# Fresh Basic Skill command dispatch — ZOD-240–242

Implemented 2026-11-04 from managed base `54faaa7`. Read-only fft_decomp HEAD
was freshly checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [selection contract](basic-skill-ai-selection.md),
[self-ACT source study](../architecture/zod-237-self-act-command.md) and its
[function fingerprints](../evidence/ai/self-act-source-v1.json) pin the source
inputs; the [implementation receipt](../evidence/ai/basic-skill-command-implementation-v1.json)
records this change and validation. No original or C program was executed.

## Source trace and dispatch boundary

`ai_command_build` in `src/battle/ai/turn.c` checks saved-choice no-progress
before fresh selection. That earlier branch must not run again on a just-created
decision. For an undecided actor with both choices unused, refresh, ability
considerations, reach, general selection and `ai_simulation_end` precede dispatch.
Simulation restoration preserves consumed RNG. Command coordinates clear their
packed zero bits before comparison; managed coordinates are already canonical.

If Move is unused and the selected action is Move-first, a genuinely different
destination dispatches MOVE. Otherwise, an absent skillset dispatches END and a
nonempty action dispatches ACT at the current origin. Same-tile Move-first does
not manufacture a Move or suppress the ACT. The original source then preserves
the choice flags and command history. After actual Move changes its flag, the
next build retains the selection because Act is still unused. Our existing
opaque frozen continuation represents that retained command, with stricter
explicit session binding rather than exposing a mutable original workspace.

`battle_state_ai_command_update` in `src/battle/turn/controller.c` dispatches the
selected Move or ACT to actual rules. On END it sets runtime display action flags
and calls `turn_end`; those display flags do not consume the statistics' unused
choices. `src/battle/turn/clock.c::turn_end` adds20 for each unused choice in a
u16 temporary and caps at60. Existing closed-domain managed Wait owns that rule.
END with neither choice used leaves CT15→55, or CT35→60, without another clock
tick. Direction is chosen from the actual position when Wait executes. Display
pauses and frame-interleaved original RNG remain outside this implementation.

The existing [frozen targeted-ACT contract](targeted-move-continuation.md) owns
actual range revalidation after Move; the [planned Move contract](planned-move-continuation.md)
owns paths and token binding. The closed effect-free Accumulate entry wins its
ACT-first/Move-first complete tie as ACT-first. It has no admitted frozen
self-Move consumer, so this work adds no such token or Framework branch.

## Move-first — ZOD-240

`BasicSkillAiMoveFirstExecution.Execute` privately runs fresh supplied-scenario
selection inside `FacingDecisionBattle.Execute`. It requires both choices unused
and the existing initialized-context, present-records, canonical-coordinate and
ordinary-continuation assertions. Selection and source certificates are the same
as the ACT-first query; a public cached plan cannot enter this adapter.

Only a Move-first, different-tile decision is accepted. Typed Attack, Throw Stone
or empty MoveOnly prepares the existing `PrepareMoveThenAttack`,
`PrepareMoveThenTargetedAbility` or `PrepareMoveThenWait` proposal in that same
callback. Framework validates affine RNG progression, stages it once in both
snapshots, confirms the actual path and publishes Move plus its opaque token.
It copies no projected damage, PA, CT or units. There is no second planner.

Call `FacingMoveDecisionBattle.Continue` with that exact returned session, actor
and token. The existing authority binds session/battle identity, lineage and
revision. Replay branches, wrong actor and changed state cannot use the token.
Attack/Stone actual resolution produces its own pending completion receipt;
explicit `FinishFacing` then settles it. Empty MoveOnly freezes deferred Wait,
which refunds only the unused ACT and settles facing. None of these commands
reselects, re-adopts selection RNG or advances the clock.

Each published command is atomic. Selection/path rejection returns the identical
incoming state with no events, diagnostic, path or token. A later ACT failure
preserves the successfully published Move, RNG and token for deterministic retry
against the unchanged moved session. An intervening accepted Wait makes the
old token stale. Existing pair APIs and Framework authority types are unchanged.

## Same-tile empty END — ZOD-241

`BasicSkillAiSameTileEndExecution.Execute` uses the same fresh selection owner
but requires empty selection at the actual origin. The source terminal lottery
still tests eight empty ranks; it is not skipped because the eventual command is
END. It prepares one existing Wait with `ChooseClosedEnemy`, adopting only
decision RNG. Actual rules apply both unused-choice refunds, the CT60 cap and
facing in one revision. It returns no Move path, token or pending facing receipt.
Different-tile empty plans and every nonempty action reject atomically.

## Unified fresh command — ZOD-242

`BasicSkillAiCommandExecution.Execute` accepts the same state, request, supplied
scenario and source profiles, then derives the first actual command from its
privately owned fresh selection. Its outcome identifies Move, Attack, ThrowStone,
Accumulate or End and retains the selection and decision checkpoint as diagnostics.
Only Move returns a frozen continuation. Actual commands use Framework's current
validation and rules; the selection grants no authority by itself.

All three new entries and the existing `BasicSkillAiActFirstExecution.Execute`
share `BasicSkillAiCommandPipeline`. It owns admission, exactly one fresh
selection, dispatch and typed proposals. Specialized entries filter the derived
route before any publication; the old ACT-first result/failure contract remains
unchanged. There is no public selector delegate, cached-result input, session/
receipt constructor, production friend access or new Framework implementation.
Existing pair and derived sword executors remain supported.

[ZOD-248](basic-skill-ai-turn-execution.md) now composes this first-command API
with actual post-ACT selection/settlement or retained continuation/facing into
a complete supplied-scenario turn, preserving per-command progress on rejection.

This is a fresh-command entry, not a complete-turn executor. After ACT-first,
predicted D1 is still only a diagnostic: actual boosted PA and effects must feed
fresh post-ACT preparation before any later Move/END. Spent choices reject here.
[ZOD-243–246](basic-skill-ai-post-act.md) now implement that separate post-ACT
preparation, derived movement/selection and authoritative settlement. Supplied
pre-ACT S/D0/D1 known-score provenance remains required; general scenario
derivation, broader policies/rosters, terminal encounter routing, replaceable
decision interface and host/editor activation remain separate. ZOD-106 stays
deferred; boosted PA remains battle-only.

## Deterministic vectors and validation

The new vectors start at RNG word1/count7, actual origin(1,1), PA5 and CT15.
Each test builds a genuine session through Begin/Advance rather than constructing
trusted execution state. Supplied origin sets and destinations are scenario
certificates, not a claim that all original maps derive these same values.

- With target(4,1), S={origin,(0,1)} and zero evade, selection chooses Move-first
  Throw Stone at(0,1). Its10 draws end at word267834847/count17. Actual Move draws
  nothing; retained Stone deals10 and ends at word3791773014/count24. Its receipt
  binds the actual final state; explicit facing adds no RNG/refund/tick.
- With target(3,1), S={origin,(2,1)}, zero evade and D1=(2,1), Move-first Attack
  ends selection at word3791773014/count24. Move retains that checkpoint; actual
  Attack deals10 and ends at word1343201072/count30.
- With target(7,7), the skipped self coin and empty eight-rank lottery end at
  word836760821/count19. D0=(0,1) moves then Waits at CT35; D0=origin dispatches
  END at CT55. An initial CT35 proves the final60 cap. Neither advances ticks.
- Existing ACT-first self/Attack/Stone results match the unified entry exactly,
  including selection diagnostics, decision RNG, actual effects and events.
  Self remains at the actual origin; no predicted D1 Move is executed.

Thirteen new cases cover those branches, exact and short budgets, per-command
late failure/retry, token binding, mode/context/source rejection, both admitted
teams, actor identities, collection order and unchanged inputs. All previous
tests, including pair paths and old ACT-first expectations, remain active.
Locked restore and full Release build pass with zero warnings/errors;
**4,198 .NET cases** (1,467 Framework, 986 AI), **539 Python tests**, and pinned
source/prepared-data and algorithm-guide audits pass. Seven TRX reports and logs
are under ignored `scratch/basic-skill-command-240-242/`. Owner drafts were
included in validation and preserved separately from this commit. Unchanged
historical checkpoint audits were not rerun; no UI or original-program review.

Reproduce with the pinned SDK and [Python/Pillow setup](accumulate-ai.md#validation):

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_algorithm_guide.py --reference C:/repositories/fft_decomp
python tools/verification/verify_ai_accumulate_contract.py --reference C:/repositories/fft_decomp --prepared scratch/all-data-extraction/SCUS_942.21
```
