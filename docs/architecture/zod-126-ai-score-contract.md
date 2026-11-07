# ZOD-126 — Closed sword candidate priority scoring

Completed source/design spike, 2026-10-27. Fresh clean read-only reference:
**11464943bbdbd2a0af022d90cb7f10455cf911df**. Eleven source files / 14 functions
and two prepared metadata records are pinned in [evidence](../evidence/ai/score-contract-v1.json).
The [44 vectors](../fixtures/behavior/ai-score-contract-v1.json) comprise 30
source-derived isolated kernels and 14 managed wrapper proposals. No C# scoring
implementation, original/C execution, search, ranking or policy is introduced.
All 14 ASC production cases remain unexecuted. The ZOD-125 projection and all
historical APIs/fixtures/evidence remain unchanged.

## Source entry and score terms

`ai_command_build` initializes the acting unit and decision flags before candidate
evaluation. `acting_unit_init` caches the actor's **current** team bits.
`decision_flags_refresh` marks a non-self unit enemy when its **initial** team
bits differ from that cached team; self is never enemy. An excluded untargetable
unit can retain old cache fields. The closed profile therefore certifies two
present, initially targetable, stable opposing units with no team/control changes
or stale classification. Different stable IDs alone do not prove this relation.

`ai_action_evaluate` calls `ai_action_simulate_and_score`; the latter snapshots
selected state, resets current priority 0/base hit 255, applies the action/movement,
ends the turn, simulates the first event under ZOD-125's certificate, restores
clock globals, and enters `score_state`. The refresh-only branch updates flags
and returns without a score. The normal branch clears the candidate's decided
marker and visits every compact-indexed present slot in original order. It does
not filter projected Dead units or score only the target.

For each unit, source order is:

1. Signed 32-bit local score = floor(unsigned 16-bit HP * 128 / unsigned 16-bit maxHP).
2. Add signed 16-bit table weights for current status bits. Dead (source ID 2) is −192;
   Critical (ID 23) is −32. The admitted statuses are exactly None/Critical/Dead,
   consistent with HP and floor(maxHP/5). Dead replaces Critical in ZOD-125.
   Other statuses have weights, interactions and unit-specific modifiers that
   are explicitly outside this score profile.
3. For a non-monster, add 51 for **each** equipment byte equal to that unit's
   baseline snapshot byte, including empty entries and entries on a Dead unit.
   The serialized unit table has seven slots: head, body, accessory, right weapon,
   right shield, left weapon, left shield. This differs from the five-slot menu.
4. If maxMP is nonzero, add the source MP-share/ability-cost/modifier contribution.
   The admitted model has MP=maxMP=0, so the branch is skipped.
5. Add team Golem HP scaled by 128 /average maxHP, then subtract 1 if current Golem
   is below baseline. Certify **both current and baseline** Golem 0 on each scored
   team. Certifying only current 0 would incorrectly erase the loss penalty.
6. Negate the complete unit score if the cached enemy flag is nonzero, then add
   it to signed 16-bit current priority. This narrowing occurs after each unit.

`ai_workspace_init` computes average maxHP across present units with signed 32-bit
sum/division and caps averages >=256 to 255 before storing the byte. It does not
wrap 256 to 0. With exactly two present units, each maxHP 1..65535, the denominator
is 1..255. Thus the zero-Golem division remains well defined even at large maxHP.
The largest admitted HP product is 8,388,480, within signed 32-bit range.

Only ordinary Attack's intended hostile target is hit. `strikes_simulate` uses
positive bonus 0 / wrong-side penalty 1 for abilities without AFFECTS_STATS. The
prepared Attack metadata confirms enemy+HP flags and no stat-effect flag. No
wrong-side hit, stat bonus, pickup priority, reaction or other action contributes
an initial priority here. Sword and effect-free candidate certificates exclude
those alternatives rather than silently dropping their score terms.

The source returns after restoring baseline selected state. Comparison against
the best action occurs later in `ai_action_evaluate`; this milestone stops before
that comparison. `ai_action_rank_reset`'s−32767 sentinel is not a score for an
unsupported candidate, and a legitimate priority 0 is not an absent score.

## Closed reduction, rounding and accuracy

Both units must be certified non-monsters with **all seven** equipment entries
unchanged since baseline. Their respective equipment terms are 357, so one allied
and one hostile contribution cancel those equal constants. The resulting final
priority is:

`(floor(actorHP * 128 / actorMaxHP) + actorStatusWeight)
 - (floor(targetHP * 128 / targetMaxHP) + targetStatusWeight)`

Retain the 357 term in per-unit diagnostics so cancellation is explicit. Do not
infer either human classification or seven unchanged entries from the presence
of a sword ID. A monster receives no equipment term; one changed human slot
changes the total by 51. Those isolated diagnostic vectors are source kernels,
not admitted inputs to the proposed human-only production profile.

With actor 50/50, target 50/50 gives 0; target 25/50 gives 64; target 10/50 Critical
gives 135; target 0/50 Dead gives 320. With actor 10/50 Critical, the corresponding
undamaged state is −135 and target 25/50 gives−71. The improvement is 64, but the
candidate's actual absolute priority remains−71. Do not replace the score with
damage, improvement from a pre-action baseline, HP percent or an expected value.

HP/maxHP/status comes from the first-event projection. CT, elapsed ticks, Move/
Act choices, tile, facing, death counter and upkeep raw rolls are not direct
terms in this closed unit-score loop. They can influence wider AI phases or
general status/action simulations, which remain outside scope. Projected Dead
is still present and scored with its unchanged equipment term; do not invoke
an initial-living validator or authored victory policy after projection.

For a living actor the HP/status component lies in −32..128; the target component
lies in −192..128. The final closed priority lies in **−160..320**. A human's
individual total lies in 165..485 (325..485 for the living actor), so neither
per-slot storage nor final priority can overflow signed 16-bit in the admitted profile.
General signed 16-bit wrap remains source behavior, documented by width-kernel tests;
this contract does not admit a larger roster/modifier set that could trigger it.

Hit accounting is separate. `action_hits_simulate` accumulates accuracy in u16,
uses a byte count (255 disables accounting), requires a valuable positive-accuracy
target/strike, and stores the integer mean in the byte base-hit field. Our one
pair retains its 1..100 percent unchanged; zero accuracy is NotValuable. No
probability multiplier is applied to state priority. Fresh prepared metadata
also confirms Attack skillset 1 uses menu type 8, not weapon-inventory type 2;
the throw-specific post-score hit halving does not apply. Only record hashes and
decoded facts are committed; original bytes remain under ignored scratch.

`rank_compare` later compares decided marker, signed priority, then hit percent;
foe-proximity tie handling occurs still later. These callers explain why the two
numbers are separate. No comparator, decided marker, lottery, rank insertion or
selection authority belongs to this score query.

## Managed follow-up and provenance decision

ZOD-125 result/outcome types are public immutable records: an external caller can
construct a record with disposition Projected or copy one from another candidate.
Structural plausibility does not prove that it came from the claimed original
state/weapon/order/RNG. Do not add a public scorer that trusts that enum, nor a
generic snapshot validator, serializer or new receipt mechanism to authenticate it.

**Proposed additive entry:** `SwordAiCandidateScoring.Evaluate(original, candidate,
weapon, candidateProfile, scoreProfile, decisionRng)`. It validates the extra
scoring certificate first, then invokes the unchanged ZOD-125 projection **once**
and scores that fresh result privately. Call this entry instead of separately
projecting then submitting a cached result. Existing projection remains available
and unchanged for its current callers. No old file or audit baseline needs edits.

The extra immutable scoring profile must bind the same two stable opponent IDs
and explicitly certify non-monster status, all seven unchanged equipment slots,
stable allied-actor/hostile-target classification and no current/baseline Golem.
Use named per-unit certification fields or an explicit closed-profile selector;
do not make omitted/default/unknown facts imply support. Known unsupported facts
must produce a typed UnsupportedScoreProfile before any projection draw. MP 0 is
already enforced by AttackUnitState; no new general MP model is authorized.
Unrepresented imported flags/equipment/team data remain a caller certification
boundary, not automatically detectable facts. The normalized proposal vectors
spell those facts out; they are not an exact future C# object schema.

Return an immutable result distinguishing:

- Unsupported scoring profile: Rejected, no projection/score, exact incoming RNG.
- Projection rejection: Rejected, no score, preserve the existing typed projection
  rejection and its incoming RNG. Do not silently substitute priority 0.
- NotValuable projection: NotValuable, no score, preserve its four advanced draws.
- Projected: Scored, retain the fresh projection plus signed priority, positive
  hit percent and original-order per-unit contributions. Preserve projection RNG
  **verbatim**. The scoring stage adds no draws, ticks, choices or state changes.

Required null arguments remain programmer errors. Identity/profile/unsupported
admission is atomic before projection. Existing late draw/tick overflow behavior
is forwarded exactly. A projection ending at draw count ulong.MAX still scores:
there is no score-stage draw to reserve. Full wrapper evaluation consumes the
projection's draws; “zero extra RNG” must not be misreported as zero total draws.
No external cached/forged outcome is an input. Because the scored projection is
fresh, avoid a second projection for validation or RNG replay inside the call.

Diagnostics should expose each HP term, status term, the 357 equipment term,
unit total before applying the enemy sign, signed contribution and optional running priority.
Actor/target and original tie-order identities must remain clear across reversed
collection order and either acting unit. Output provides no command receipt,
active turn, policy selection or live RNG commitment. Replay with identical inputs
must match; live state/definitions/profile and independent branches remain untouched.

## Validation and remaining work

The offline verifier checks 44 vectors in 5 families and 14 unexecuted ASC cases.
Eighteen new Python tests include literal arithmetic, historical ZOD-116 component
reconciliation, closed-range checks, enemy signs, empty/changed/monster equipment,
negative absolute scores, hit independence, certificate rejection, forward-only
RNG and fresh-projection provenance. They execute descriptions, not production
scoring or the original game. All 432 Python tests pass. Source/data audits additionally verify the fresh
clean revision, file/function fingerprints, Attack metadata and command-menu byte.

Reproduce with `tools/verification/verify_ai_score_contract.py --reference
C:/repositories/fft_decomp --check-managed-baseline --executable
scratch/all-data-extraction/SCUS_942.21 --output <new scratch JSON path>` and the
full Python discovery suite. Offline tests need neither original media nor the
reference checkout. Final results/log hashes are in evidence.

No .NET rerun is needed for this source/design-only change. Last implementation
validation remains ZOD-125's 3,824 .NET / 414 Python tests. Preserve its projection,
historical inventories and owner editor drafts. General MP/Golem/status/equipment,
roster/search/ranking/policy, authoritative decision RNG integration and replaceable
AI interface remain future work. Editor UI and ZOD-106 battle checkpoints stay
deferred. **Next: ZOD-127**, implement only this closed scoring companion and
execute ASC-001..014 with locked build/full .NET/Python and source/history checks.
