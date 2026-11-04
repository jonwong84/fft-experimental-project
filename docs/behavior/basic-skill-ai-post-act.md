# Three-entry post-ACT movement and settlement — ZOD-244–246

Implemented 2026-11-04 from managed base `c5dab73`, following the
[ZOD-243 source trace](../architecture/zod-243-three-entry-post-act.md).
Read-only fft_decomp was freshly checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/three-entry-post-act-source-v1.json) pins 25
functions across seven files; the
[implementation receipt](../evidence/ai/three-entry-post-act-implementation-v1.json)
records the managed boundaries and validation. No C/original program was executed.

## Fresh preparation — ZOD-244

`AccumulateAiPreparation.EvaluateAfterAct` shares admission and summary
construction with the existing pre-ACT entry. It requires ACT-used/Move-unused
and the same closed lists, grants, matching profiles, zero equipment PA bonus,
PA1..99, reachable PA99 weapon envelope, ordinary movement/upkeep/cache facts and
raw Move<=124. Both admitted teams remain supported; Blue requires explicit AI
control and HP*128/maxHP>=45. FreshRefreshEntry still certifies the earlier source
no-progress guard rather than being inferred from the action flag.

The private family validator accepts an explicit post-ACT choice mode. Targeted
content/setup validation uses the existing `ValidateAbilitySetup`; it never
ignores the early ActionAlreadyTaken result of live command admission or
fabricates an ACT-unused snapshot. Both units must retain self and Stone grants.
Actual command validators are unchanged. Unknown effects, invalid profiles,
wrong choices and terminal input reject without output or RNG progress.

One existing `SwordAiRefresh.EvaluateAfterAct` uses actual PA, HP/status,
displacement and post-command RNG, temporarily refunds only Move and restores
actual geometry/policy through the shared preparation owner. It adds one
hypothetical upkeep draw in this domain. PA5→6 and PA99→99 use the same preparation
path; no hypothetical PA is copied into battle state or bumped a second time.

Four initialized entries include unusable Frog Attack; the evasion quarter-share
is2, Silence/paid-MP shares0. Both units retain first-pass harmful2/helpful1,
HasRangedAbility and no healing. Actor mostly-support is false; the enemy retained
bit remains unrepresented. Actor enemy reach is Move+1 and ally reach Move.
Attack, Accumulate and Stone all have usable=false after the second pass.
The self forced consume-aim and other actor policies remain diagnostic; first-pass
summaries are not recomputed from those cleared flags. Pre-ACT entry outputs and
streams remain covered by the existing tests.

## Derived movement and empty selection — ZOD-245

`BasicSkillAiPostActSelection.EvaluateInReach` and `EvaluateOutOfReach` own that
fresh preparation before calling the existing lower post-ACT movement kernels.
`BasicSkillAiPostActConfiguration` requires normal/canonical/ordinary scenario
facts, fresh general-entry/empty-ranked-list assertions and NoDefendSupport.
No public operation accepts cached preparation, scored cells or a selected plan.

The three-entry list independently derives enemy minimum range1, so the shared
`SwordAiMovementScenario` internals apply without claiming an Attack-only list.
They retain source masks, actual occupancy, movement clocks, scores, proximity,
tile/tie ordering and diagnostics. In-reach and out-of-reach both derive D0=D1.
NonBlue out-of-reach consumes its modulo7 reach adjustment after scoring;
Blue skips it. The pre-ACT ranged distance-floor branch is not called.

Cleared usable bits return before action evaluation, so there are no Attack/
Stone scans, Accumulate usefulness coins, stat projections or formula draws.
The shared eight-empty-slot lottery still consumes eight rolls. Its fallback
creates a movement-only plan with deferred facing. Every failure discards the
owned preparation/scenario/lottery and returns the whole query's incoming RNG.

These are derived **post-ACT** scenarios. The existing fresh pre-ACT three-entry
composition still needs caller-supplied known-score S/D0/D1 provenance. The new
queries require the caller to choose the truthful in/out-of-reach entry; automatic
reach routing and complete turn orchestration remain separate work.

## Actual remaining command — ZOD-246

`BasicSkillAiPostActExecution.ExecuteInReach` and `ExecuteOutOfReach` own fresh
selection inside `FacingDecisionBattle.Execute`. Framework validates actor,
pending facing and revision before planning. The adapter requires ACT spent,
Move unused and the existing dispatch-context assertions. The shared fresh
dispatcher receives the current choice flags and the privately selected MoveOnly
plan; its FreshSelectionCompleted assertion is derived internally.

A different-tile plan prepares the existing turn-ending confirmed path plus
actual completion-facing transaction. A same-tile plan prepares Wait. Framework
verifies and adopts decision RNG once in both snapshots, validates actual rules
and settles facing using the actual final placement. Actual movement/facing adds
zero RNG in this domain. There is no second ACT, Move-first continuation token,
next-clock advance or speculative CT/HP/PA publication.

Moving leaves actual CT15 with neither choice unused, and adds two revisions
for Move and facing. Same-tile END adds one revision and refunds only unused
Move: CT15→35 or CT55→60. Boosted PA survives both routes. Turn progress stops
after settlement; no clock or next actor is advanced.

Per-command rollback preserves earlier accepted ACT. Selection/budget rejection
returns the identical post-ACT input with no events, path, token or diagnostic.
A late revision-exhaustion failure during facing settlement also rolls back the
entire remaining-command transaction, including RNG adoption and path Move,
while retaining the input's actual PA6 and spent ACT. Retry against that unchanged
session deterministically repeats the rejection. Existing pair APIs and the
Framework authority types/constructors are unchanged.

## Literal vectors and validation

The direct-self vectors build a genuine Begin/Advance session with word1/count7,
actor(1,1), target(2,1), Move3, PA5 and CT15. Actual self ACT adds zero draws and
publishes PA6. Fresh preparation ends at word1103527590/count8, with hypothetical
turn-end CT35 and actual CT15 unchanged. Fourteen fresh movement scores end at
word2111915288/count22; the eight empty lottery draws end at word1343201072/count30.
The derived destination is(1,4). Actual Move and facing retain that RNG and PA6.

With Move0, only origin is scored. In-reach same-tile END ends selection at
word267834847/count17 and leaves CT35. With target(7,7), out-of-reach NonBlue adds
the post-score raw10113 reach draw, ending at word180171308/count18; Blue skips
that draw and ends at word267834847/count17. Both derive D0=D1=origin.

The composed public-command witness begins with fresh three-entry ACT-first
selection and actual self ACT at word3794901700/count26, PA6. Its new post-ACT
selection/Move/facing ends at word995405759/count49, actual(1,4), CT15 and PA6.
The test explicitly calls both public adapters; no complete-turn runner is added.

Thirteen new AI cases cover boosts/cap, first-pass summaries, actual damage/push,
missing self/Stone grants, choice/cache/terminal guards, exact/exhausted RNG/ticks,
both movement branches, team-specific reach draws, short lottery rollback,
actual Move/END/refunds, composed public commands, late facing failure and both
teams/actor identities/collection orders. Existing pair and pre-ACT cases remain.
Locked restore, a zero-warning/error full Release build, **4,211 .NET cases**
(1,467 Framework, 999 AI), **539 Python tests**, and pinned-source/prepared-data,
algorithm-guide and coverage checks pass. Logs and seven full TRX reports are
under ignored `scratch/three-entry-post-act-243-246/`. Unchanged historical
checkpoint audits were not rerun, and no UI or original-execution review occurred.

Use the pinned SDK and [Python/Pillow setup](accumulate-ai.md#validation):

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_algorithm_guide.py --reference C:/repositories/fft_decomp
python tools/verification/verify_ai_accumulate_contract.py --reference C:/repositories/fft_decomp --prepared scratch/all-data-extraction/SCUS_942.21
```

ZOD-247 next traces complete headless-turn orchestration and recovery, including
single-preparation reach routing. Broader policies/rosters/maps, pre-ACT scenario
derivation, terminal encounter routing and host/editor activation remain separate.
ZOD-106 stays deferred and PA stays battle-only. Original VSync/frame RNG remains
unresolved. Preserved owner definition drafts, including AbilityToggles, are
unchanged by this work.
