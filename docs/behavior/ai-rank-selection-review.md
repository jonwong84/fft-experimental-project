# AI ranking and lottery retrospective — ZOD-434

Completed 2026-11-07 as a bounded batch of the
[ZOD-428 programme](../development/documentation-review-programme.md), following
the [target/scenario review](ai-target-scenario-review.md). No new defect was
confirmed within the admitted rank/lottery/fallback paths. This batch changes no
runtime code, tests, fixtures, owner drafts or historical receipts. The owner's
additional controller formatting was preserved outside the commit.

Managed baseline: `0b6db614d0ab5cade08fc0dd9bd54c517cb7a29f`. The read-only
reference was rechecked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [receipt](../evidence/ai/rank-selection-review-v1.json) pins current inputs,
source symbols, audit results and independently recalculated lottery literals.
Broader source/caller branches remain outside this review.

## Rank, baseline and insertion

Native `rank_compare` compares decided byte, signed16 priority, then unsigned
hit byte. `ai_action_evaluate` delegates to simulation, which clears the current
marker for the absolute score; only the selected action later receives Decided.
`AiCandidateRank` therefore omits
the identical zero marker for privately owned fresh candidates. It is not a
general comparator for arbitrary retained native records.

`rank_baseline_seed` reverses the negated16-bit movement cell and sets hit100.
No draw or new movement evaluation is involved. Current owners use their freshly
scored D0 baseline. `AiRankedInsertion` requires a strict rank improvement before
any proximity comparison, matching `ai_best_rank_insert`. A safer destination
cannot admit an equal baseline rank. Physical candidates with hit<=100 thus
need strictly greater priority when the baseline hit is100.

Within an ability and between ranked entries, `ai_rank_is_higher` adds lower
foe proximity only after equal priority/hit. Complete ties retain the earlier
candidate/occurrence. Sword/Stone/Dash callers use destination proximity, including
D1 for ACT-first actions. Accumulate's ACT-first and Move-first self hypotheses
share the same final destination, so rank-only comparison gives the same result.
The magic current-origin query likewise has constant destination proximity; it
retains strict best comparison and the first complete tie across passes.

Source reset is priority-32767, marker0, hit0, ChooseAtWait facing and cleared
reflection. A null managed rank is equivalent only within the admitted score
range. Sixteen ordinary physical units contribute at most16*(128+357)=7760 in
absolute HP/equipment terms, plus the supported stat credit3. Critical/Dead
reduce those unit terms. Consequently a fresh scored candidate cannot fall below
the reset sentinel here. This does not authorize arbitrary public score objects
as trusted ranked inputs or generalize the helper to every native status score.

Rank arrays have the original eight slots, independently of content/catalog
storage. Insertion checks the last slot, shifts full entries and performs no
ability/destination deduplication. Repeated learned occurrences can occupy
distinct ranks. Each owning entry starts a new private best, which corresponds
to the source reset after every insertion attempt, including a failed attempt.
The legacy sword owner and the shared pair/roster insertion owner retain these
same bounded rules.

## Ordered lottery and fallback

`AiRankedLottery` matches `ranked_select`: visit slots0..7 in order, draw once
per visited slot, test raw%100<hit, and stop at the first success. Zero-hit reset
slots still draw; hit100 still draws before succeeding. This is an ordered
series of chance tests, not normalized-weight sampling across candidates.
All failures choose slot0 if any visited hit was nonzero; all-zero slots return
movement fallback after eight draws. Lists are not compressed before selection.

Independent seed1 literals use raw draws16838,5758,10113,17515,31051,5627,23010,
7419. Eight zeros consume all eight and end at word2633739833. Chances1,0,...,0
consume the same stream and select slot0 after failure. Chances38,59,0,... reject
the first exact38 boundary and select slot1 on58, ending at word2524885223.
Chance100 selects slot0 after one draw, word1103527590. Original modulo bias is
retained; no probability normalization or floating-point arithmetic is added.

Current owners preserve successful decision-stream progress through nonvaluable
or nonadmitted candidates. Unsupported contexts or late counter exhaustion
publish no partial result and return whole-query entry RNG/workspace. This is
the explicit managed atomic rejection contract, not native RNG rewind or VSync
resumption. Post-ACT preparation clears actor actions before entry work, yet
the terminal empty lottery still consumes eight draws.

Native `fallback_select` chooses the scenario destination and Defend only for
Defend support. Current terminal paths require explicit NoDefendSupport/default
certificates and produce MoveOnly at D0; they do not port Defend. The inspected
same-tile fallback adapter freshly selects, rejects a real action or different
MOVE destination, then enters the Framework wait transaction. This edge check
does not close authoritative command, facing, terminal or continuation review.

## Retained priority and integration boundaries

`AiPriorityWorkspace` represents knowledge of source tile memory for one exact
map instance. Fresh writes win; otherwise ranked origin reads require explicit
retained knowledge. Unknown never means zero. Current proximity is recomputed;
no old proximity, candidate or rank is adopted. Branches return immutable
successors; failure returns original knowledge. Forget discards knowledge after
unmodeled writers without claiming the source cleared memory. Host/event alias
maintenance remains a separate integration requirement.

The current `AiDecisionMaker`, pair/list composition and terminal selection
owners freshly evaluate entries in source order, retain per-entry RNG and use
the eight-slot helpers. The current-origin magic query only updates a best aim;
it neither seeds a full movement baseline nor inserts/lotteries/executes a spell
decision. Its byte255 unresolved-hit rank remains a distinct diagnostic path.
Full origin/aim consumption and general policy traversal remain separate.

## Validation and follow-up

All5,181 .NET tests, including1,384 AI, were rebuilt and passed against the latest
owner controller formatting. The preceding full ZOD-433 Python run passed794
tests; Python tools/tests are unchanged. The receipt records both the incoming
and latest owner controller blobs without including that file in the commit.
Existing rank-admission/composition/selection, variable-list and magic aim-rank
source audits pass. These offline descriptions do not execute reference C or
original code. No new regression was needed without a confirmed defect.

Ten owner draft blobs, immutable JobStats, approved targeting formatting and the
unstaged controller edit are preserved. ZOD-435 next reviews checked command and
continuation boundaries. Broader policies, Defend, full magic origins/commands,
frame RNG and final readability gate ZOD-432 remain separate.
