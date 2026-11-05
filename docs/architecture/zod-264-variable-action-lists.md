# Variable supported-action lists — ZOD-264

Source/design study, 2026-11-04, managed base `414d84a`. Read-only fft_decomp
was freshly checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/variable-list-contract-v1.json) pins31
functions across nine files, extending the [list-construction study](zod-261-usable-ability-lists.md).
No original, C or new managed runtime was executed for this study.

Sixteen [isolated descriptions](../fixtures/behavior/ai-variable-list-contract-v1.json)
check list arithmetic, opaque cursor completions/resumption and supplied rank/
lottery tapes. They do not execute preparation, scenarios, projections, clocks
or managed AI and establish no complete decision RNG schedule.

## Initialization, usable summaries and source order

Normal humans always retain Attack and appended Frog Attack. Within this study,
only learned/AI-eligible Accumulate and Stone occurrences vary. Missing Attack
would require another source profile, not an empty human action list. Primary
then secondary slot order, holes and duplicates follow ZOD-261. Source NONE or
34 bounds end traversal; the semantic builder's collection size does not prove
that arbitrary lists fit original storage.

`ai_unit_abilities_init` counts all inserted occurrences, including Frog Attack,
before current usability is rebuilt. Each duplicate contributes separately to
the denominator and relevant numerators. In the admitted ordinary zero-MP
profile, Attack and Stone are AI-evadeable; self and Frog are not. Frog's separate
secondary evadeable bit still recomputes exhaustive origins. Quarter-share
arithmetic uses floor(numerator*4/initialized count).

`ability_considerations_set` has two distinct passes. First, every in-play unit's
entries are loaded and usability is rebuilt using current statuses, MP and
entry-specific rules. Non-Frog skips Frog Attack. Harmful/helpful and ranged
summaries count usable occurrences, not distinct IDs or executable grants.
Enemy harmful/physical totals come from the peer's own list; they are not always2.

Mostly Support uses **floor(helpful*128/(helpful+harmful)) >=77**, not the
initialization quarter share or a one-half threshold. Only allied flags are
rebuilt. The source then clears the flag of the lowest-share ally. With the
current single allied actor, that final flag is false even for a self-heavy
list. Example: Attack/self/self/Frog has helpful share85, initially sets the
flag, then loses it through the lowest-ally override. Attack/self/Frog has
share64 and never sets it. The peer's retained Mostly Support stays unknown.
The current canonical helper's simplified expression must not be generalized
by removing its list guard; its canonical result remains correct and unchanged.

## Actor-only preparation and movement consequences

The second pass derives actor runtime policy and minimum reaches from usable
entries before clearing usable bits when ACT is spent. Attack remains exhaustive;
self forces ConsumeAim and makes no preparation usefulness draw; Stone follows
its existing non-exhaustive consume policy. Repeated self entries do not add
preparation coins. These actions are not random-use entries, so the prepared
random-use threshold remains0. All prior no-charge/default-policy/context gates
still apply.

Attack fixes minimum hostile range1 for all admitted lists, hence enemy reach
Move+1. Self's ally range0 and the no-ally-ability default both yield ally reach
Move; equal numerical reach does not prove self exists. Stone presence sets
HasRangedAbility and relaxes the focused in-reach pre-ACT D0 distance bound to
max(3,distance-1). Without Stone that ranged floor disappears. Out-of-reach
filtering, team reach draw, D0=D1, actor insertion, priority history and unknown
cast-key rules keep their source order.

After ACT, summaries and reach survive the actor usability clear. List traversal
still loads entries, but the outcome sign-bit gate skips all action work: no
self usefulness coin, Attack scan or Stone scan. The existing empty eight-slot
lottery and remaining movement settlement are still required.

## Per-occurrence evaluation, ranks and terminal context

`general_select` owns one scenario build and baseline seed before traversing
the list. Do not call standalone refreshed queries for every occurrence.
`ability_list_evaluate` loads the current entry and calls its outcome evaluator,
including entries subsequently skipped as unusable. Completion increments the
cursor; suspension does not. Resume re-enters the same outcome without reload
or a repeated earlier usefulness filter. Original frame/VSync interleaving is
still unresolved and is not a persisted battle-save feature.

The outcome gate precedes command assignment. Every entered usable occurrence
copies its command and restores target eligibility before its effect-specific
passes. No listed self means no self filter or coin; two listed self entries
mean two independent fresh-entry passes. Duplicated Stone entries each own
their own target/aim work; aim consumption in one entry does not authorize
omitting the next. Candidate state restores actual HP/PA/choices while RNG
continues, so two hypothetical self entries cannot accumulate PA6 then PA7.

Each occurrence can contribute a best action to the same ranked array. Equal
rank/proximity retains the earlier occurrence ahead of the later one, but the
later duplicate can occupy another slot while space remains. The insertion
gate requires a strict rank improvement over the move baseline before proximity
is considered. The source lottery visits ranked slots sequentially, including
empty slots. Two identical supplied Stone ranks can fail at slot0 and succeed
at slot1; deduplication would change the stream and selected occurrence. After
eight failures, any nonzero hit chooses slot0; all-zero hits use fallback D0.

The last loaded Frog changes the considered record but fails the usable gate
before assigning current command fields. The last entered usable command may
therefore be Attack, self or Stone depending on list order, even after a
NotValuable/early outcome return. `TerminalThrowStoneEntry` is no longer a valid
universal assertion for variable lists. Chosen ranks copy their own payload;
fallback overwrites movement/order/skillset and retains other unread fields.
Derive terminal context from owned traversal when selection is broadened rather
than inventing zero payload or falsely certifying a terminal Stone.

## Concrete managed gaps and selected child — ZOD-265

The current owned builder/Content adapter can describe missing, repeated and
reordered supported actions. `AccumulateAiPreparation.ListsMatch` deliberately
rejects them before refresh. The search then hardcodes Attack → self → Stone;
derived composition indexes three entries and calls Single(Stone), uses a
range4 cast-key guard, and selection requires a terminal Stone assertion.
Removing only the preparation guard would reach invalid indexing, perform
absent passes, reject unread keys unnecessarily or consume the wrong RNG.

**ZOD-265: implement a separate pure variable-list preparation entry.** Require
owned lists and the existing bounded two-opponent state, ordinary sword/effect
definitions and explicit missing source-context facts. Admit supported missing/
repeated/reordered self/Stone occurrences while preserving Attack/Frog structure
and the original compatibility extent. Return ordered occurrence provenance,
initialization summaries, ordinary usable summaries, actor per-occurrence policy,
peer threats, minimum reaches and existing common refresh/focus diagnostics.
Own one refresh before/after ACT and preserve whole-query RNG/output rollback.

Validate executable membership for each listed usable action, collapsing only
the grant-membership check to distinct IDs. Do not require a second gameplay
grant for a repeated list occurrence. Conversely, an omitted AI entry does not
prove an ability is unlearned or absent from executable grants: metadata may
disable it for AI. Keep that distinction explicit. Factor state/profile admission
from candidate action eligibility so an absent self/Stone entry is not rejected
by trying to validate an invocation that will never occur.

Leave existing canonical preparation, scenarios, composition, selection, live
commands, history/recovery and result shapes unchanged. The new owner stops
before movement scoring or action search; no variable preparation may leak into
old canonical consumers. Later children can generalize occurrence traversal,
terminal context and provenance for only the actions actually present, then
connect commands/post-ACT/recovery through existing authority. Do not duplicate
rank/lottery kernels or introduce a broad replacement interface.

Check literal list variants and asymmetric peers, helpful share77/lowest-ally
behavior, missing grants versus AI-disabled entries, initialization/usability
separation, before/after-ACT reaches and clearing, both teams/actors, exact/late
refresh budgets, immutable replay and old canonical parity. Full learning rows,
special menus, rosters/effects/maps/policies, terminal routing, host/editor work,
frame RNG and ZOD-106 remain separate/deferred. Owner drafts stay unchanged.

## Validation

Three new authored Python tests cover all16 literal descriptions, input
preservation, missing/repeated passes, terminal context, duplicate rank slots,
the support override and malformed/tampered claims. All **542 Python tests**,
the pinned-source, inherited prepared-data, guide and coverage audits pass.
Reports are under ignored `scratch/variable-lists-264/`.
Managed runtime/tests/project files are unchanged; the last full managed
validation remains ZOD-263's **4,272 .NET cases** (1,467 Framework, 1,060 AI),
locked restore and zero-warning/error Release build, without rerunning .NET for
this source-only ticket. Historical checkpoint audits and UI were not rerun.

Reproduce the new study with:

```powershell
python -m unittest discover -s tools/verification -p 'test_ai_variable_list_contract.py'
python tools/verification/verify_ai_variable_list_contract.py --reference C:/repositories/fft_decomp --output scratch/variable-lists-264/new-source-report.json
```

## Implemented follow-up

[ZOD-265–270](../behavior/variable-basic-skill-ai.md) now implement the separate
owned variable-list preparation and subsequent shared scenarios, occurrence
composition/lottery, actual command and complete-turn/recovery milestones. Old
canonical guards remain intact. This study's source-only descriptions and
historical validation remain unchanged.
