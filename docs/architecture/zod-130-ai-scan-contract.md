# ZOD-130 — Ordinary sword scan in one supplied scenario

Source/design checkpoint, 2026-10-27. Fresh clean read-only reference:
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[evidence](../evidence/ai/scan-contract-v1.json) pins 16 files / 55 functions and
three prepared metadata records. All source symbols below refer to that revision.
This adds offline source/design descriptions, not a production enumerator or an
original-game execution oracle. ZOD-129 and earlier APIs/evidence remain unchanged.

## Correction to historical proximity notes

`include/fft/unit.h` defines **UNIT_SLOT_COUNT=21**, not 32. The ZOD-128 notes and
its 26-foe byte-wrap diagnostic incorrectly treated a larger roster as original
source admission. Self is not an enemy: at most 20 enemy terms, each at most 10,
sum to at most **200**. Thus that 26-foe example is unreachable in the original
roster. The u8 assignment still narrows arithmetically, but does not establish a
reachable cache wrap. Even 21 terms would be below 256.

Historical fixture/evidence files are preserved as recorded; this correction
supersedes their general roster-count/wrap claims. ZOD-129's closed two-opponent
implementation is unaffected. This is a reference fact, not a proposal to impose
21-entry tables on the managed runtime.

## Source entry and ordinary Attack policy

`turn.c::general_select` selects a movement scenario, seeds a move-only baseline,
then selects active targets and evaluates the ability list. `ability_list_evaluate`
loads an entry and calls `evaluation.c::ai_ability_outcome_evaluate`, resuming the
same entry on suspension. This contract isolates **one already selected movement
scenario and one ordinary Attack pass**, ending before ranked-list insertion.
It does not derive scenarios, enumerate an ability roster or select a command.

`abilities.c::usable_abilities_build` initializes the human weapon summary with
HP-affecting, evadeable and physical bits. `ability_weapon_attack_data_store`
merges range, element, trajectory and formula properties; left-hand processing
can override range in the general source. The closed profile certifies one
ordinary non-elemental damaging right-hand striking sword, range 1, empty offhand,
and no effects/alternate weapon formula. An asset ID or Power alone cannot prove
those unrepresented original facts.

For Attack, `ai_ability_entry_load` takes its range and flags from that weapon
summary; its zeroed workspace leaves AoE=0 and CT=0. The fourth flag byte is
replaced by runtime entry usage, not the static ability record's fourth byte.
Canonical flags1..3 are 0x82/0x02/0x01: this is **tile targeting**, with no
FOLLOW_TARGET, map-target, reflect, direct or arc bit. `targets.c::
target_range_set_for_action` also returns TILE_ONLY for Attack.

`ai_unit_abilities_init` clears the low policy bits with 0xf8 and sets exhaustive
origin scan for a short range+AoE (<3) evadeable ability. The prepared Attack
secondary record confirms evasion, so this branch continues before the generic
AoE-zero forced-consume rule. `ability_considerations_set` supplies the usable
bit; the canonical admitted usage is 0x84 and considered word 0x84010282. The
source s32 view is negative; the JSON diagnostic stores the unsigned word. Unknown
or alternate policy bits remain outside this closed certificate. The prepared
secondary Attack **range is 0**, not the sword's effective range 1; do not use that
static byte as the Attack scan range. Attack menu type is 8. All record identities
and decoded facts are in evidence; raw original bytes remain ignored in scratch.

The ability-level usable sign-bit check can skip an unusable original entry.
Random-use gating can draw in other branches, but the admitted ordinary weapon
summary has no random-use bit. Candidate discovery, cache construction and
comparison themselves consume no RNG. A known unsupported policy is a managed
Rejected query, not a guessed successful empty scan.

## Target selection, masks and actual emission order

The caller's `target_active_units_select` applies untargetable/considered and
mode-specific selection. `ai_ability_outcome_evaluate` copies active to targetable,
then `useful_targets_filter` applies `ai_target_classify_ability_effect`.
Under the existing living None/Critical, stable-enemy, no-element/control/status
effect profile, HP damage to the opponent is useful and to self is adverse.
Original active-target selection is still caller context; it cannot be inferred
from two distinct IDs alone.

Ordinary Attack skips reflected, self-target and FOLLOW_TARGET unit/CT-group
passes. `unit_can_move_by_charge_deadline` returns 0 immediately for CT=0, so
`target_enemies_with_move_opportunity_exclude` retains an initially selected
opponent even if it is already ready for its own turn. Then TILE targeting enters
`origin_reachable_evaluate`. This is distinct from proximity's imminent-enemy
filter, which compares baseline delays.

`ai_cast_aim_tiles_build` unions range-1 diamonds around useful targets and
intersects them with the **supplied scenario's** reachable mask. With one target,
that mask contains possible casting origins on adjacent tiles (and, before real
range filtering, potentially the target's own tile). Ordinary movement excludes
the living opponent's tile. For tile targeting and AoE=0 the aim mask is just
the opponent's walkable tile. No broad scan of every reachable map cell is implied.
Water/Geomancy/Monster Skill restrictions occur in mask building; the closed dry
ordinary profile excludes those alternatives.

The exhaustive flag clears the target-setting CONSUME_EVALUATED_AIM_TILES flag
(a different field from the ability usage byte) and leaves ranked_origin_scan
zero. The source visits layer, then y, then x, with bit 15 of a u16 row meaning
x=0. Nonexhaustive code unconditionally sets ranked_origin_scan to 1 after its
product test; do not infer that a small product selects exhaustive order. That
other branch is outside admission here. General aim/cast counts and scan cursors
are bytes; the closed lower-layer scan has at most four cast origins and one aim,
so there is no byte-count overflow.

At each cast origin `origin_destination_finalize` compares its full coordinate
word with the actor's original coordinate word (canonical zero padding required):

- Same tile: ActThenMove, replacing action destination with the scenario's chosen
  destination. It is emitted at this origin's position in map order, not first.
- Other tile: MoveThenAct, with that origin as final destination.

For actor (2,1), target (1,1) and all four neighboring origins, the order is
(1,0), (0,1), (2,1), (1,2). With scenario destination (1,0), the third candidate
is ActThenMove to (1,0); the first is MoveThenAct to (1,0). Keep both. Sorting by
destination, deduplicating destinations or synthesizing both orders at each tile
would change behavior and draws. Stay produces one ActThenMove, not an additional
MoveThenAct. If the actor's tile is absent from the scenario mask, no ActThenMove
candidate is synthesized.

`origin_aim_tiles_evaluate` privately places the actor at the action origin and
intersects the aim mask with actual weapon range. `weapon_range_set` excludes the
caster's own tile. A striking weapon marks four cardinal neighbors with a height
window; the existing dry flat lower profile satisfies that window. The radius-one
prefilter alone is not an attack legality proof. Plain sword Attack has no direct/
arc trajectory branch. Its original action is a TILE command; the managed
candidate's stable TargetId denotes the sole occupant of that original panel,
not an invented follow-unit command. With CT=0 and restored baseline state these
are equivalent for this subset only.

## Evaluation, incumbent and source resume ownership

Each admitted panel calls `ai_action_evaluate` once. Positive accuracy projects
full normal damage and scores; zero accuracy is supported NotValuable and still
consumes the four formula draws. `action_hits_simulate` sets ability_processing_done
before testing valuable accuracy. Thus that zero-accuracy result does not abort
the remaining origin scan. With exhaustive/ranked_origin_scan=0, neither the
policy nor `strikes_simulate`'s ranked-origin self-hit logic consumes the aim.
Every later origin can still evaluate it.

Each evaluation restores the original unit baseline. Projected KO never removes
the original foe from later masks or cache. Earlier hypothetical HP/CT/positions
are not the next candidate's input. RNG is **not** restored: supported losing and
NotValuable candidates all advance the same decision stream. Do not stop early
on hit100, a lethal score, equal scores or a currently good incumbent.

`ai_action_evaluate` replaces best only on strict signed priority, hit then lower
baseline proximity; exact ties preserve the earliest scored candidate. Isolating
this pass requires a freshly reset best record: original priority −32767/hit0/
marker0. Every admitted score (−160..320) beats that sentinel. Managed None is
therefore an equivalent initial incumbent for this closed pass, not a fabricated
score zero. An inherited best from another pass is outside this contract. The
source eventually calls `ai_best_rank_insert`; this query stops before that call,
so returned best does not imply it beats the movement baseline or wins a lottery.

The source is resumable, with distinct saved state at three levels:

- Before origin work's scanline test: resume PREPARE at begin_evaluation without
  rebuilding cast/aim masks.
- Before aim work: saved origin level/y/x re-enters origin_aim_tiles_evaluate.
  Range/intersection work may run again, but completed candidates are not replayed.
- During scoring: saved target panel and search_resume_flag re-enter evaluate_priority;
  action_simulation_phase resumes advance_turns or score_state. Completed formula
  draws and previous candidates' RNG are retained; the formula does not restart.

The outer ability/ability-list cursors retain the same entry. Exhaustive resume
reloads the full row and continues at the saved x, rather than restarting x=0.
The four stage-description vectors illustrate supplied draw prefixes and program
counter ownership; they do not execute original frame scheduling. The proposed
managed follow-up is synchronous and deterministic, with no VSync, wall-clock,
frame suspension API or saved coroutine state.

## Bounded managed follow-up

Proposed additive entry:
`SwordAiCandidateScan.Evaluate(originalState, scenario, weapon, candidateProfile,
scoreProfile, comparisonProfile, scanProfile, decisionRng)`.

Use existing SwordAiComparisonProfile for actor/target IDs and explicit fresh
baseline provenance, and the existing candidate/score profiles. Add an immutable
SwordAiScanProfile with explicit OrdinaryAttackTilePolicy, OriginalScenarioMask,
TargetSelectionMatchesBaseline and FreshAbilityBestReset certificates (default
false), plus a required known boolean TargetActive whose false value means a
supported inactive target. An omitted/unknown active value is not false-by-default
support. These certificates cover the exact ordinary weapon/policy, canonical
coordinate padding, source mask/context and fresh reset described above; asset
IDs cannot prove them.

SwordAiScanScenario snapshots a collection of logical lower-layer origins and a
nullable chosen destination. Validate each unique mask cell against unchanged
FlatOrdinaryMovement AiOrigins from original state. Nonempty masks require a
chosen destination in that mask; empty masks require null. Empty is supported
NoCandidates and never reads a stale destination. Caller certifies that this is
the actual supplied source scenario, rather than a new heuristic subset. Physical
membership alone cannot authenticate that source context. Unsorted input is
canonicalized to source scan order; duplicate, occupied, out-of-bounds or wrong-
layer cells/destination reject before any score call.

Validate existing living/clock/action/weapon/candidate/score admission even for an
empty or inactive scan. Reuse pure profile Supports/ValidateState and movement
queries; do not project a throwaway candidate to validate emptiness. No new MP,
status, roster or general snapshot validation model is authorized. The normalized
description's candidateProfileSupported/scoreProfileClosed booleans summarize
those existing checks; they are not proposed public certificate fields.

Build original baseline proximity privately once, then generate the described
logical candidates and call unchanged SwordAiCandidateScoring once per emitted
candidate, always on original state and with the preceding returned decision RNG.
Keep scores and the private incumbent inside that invocation. No public supplied
score/cache/result input; no repeated ZOD-129 pair calls, which would re-evaluate
incumbents and consume extra draws. Add private source comparison/cache arithmetic
as needed without rewriting historical APIs or executing C.

Return immutable hypothetical emission/evaluation diagnostics (including cast
origin, tile target, candidate/order/destination, score absence, RNG and baseline
proximity), retained index if any and final decision RNG. NoCandidates means no
evaluations; NoScoredCandidate means evaluations all NotValuable; Scanned means
a private best exists, including zero/negative priority. None is never score zero.
The maximum four candidates is a derived boundary, not a copied original table.

All unsupported admission/scenario failures are typed, before score calls. A
rejected emitted evaluation records index and original nested typed reason but
returns **original query RNG and no partial payload/incumbent/cache**. This is the
explicit managed unsupported/exhaustion transaction rule, not original C search
rewind. Successful discards keep every draw. Generation, cache and comparison add
zero draws/ticks/choices; exact-MAX successful and zero-candidate queries succeed.
Replay and independent branches leave original state/profiles/live RNG unchanged.

## Verification and remaining work

The [56 classified vectors](../fixtures/behavior/ai-scan-contract-v1.json) contain
22 isolated source kernels, four source stage descriptions and 30 normalized
managed scan proposals. Twenty-one new Python tests cover literal policy/masks/
order, prepared-data guards, identity and collection permutations, per-candidate
fresh calls, empty/filtered/NotValuable distinctions, source baseline restoration,
signed/zero best, proximity versus accuracy, every prefix draw budget, late tick
failure, resume ownership and the roster erratum. They reuse historical offline
movement, scoring and clock descriptions; they execute neither managed scanning
nor the original. ASN-001..016 remain **unexecuted production cases**.
The full Python suite passes **473 tests**, including those 21 new tests.

Run the full Python discovery suite and
`tools/verification/verify_ai_scan_contract.py --reference C:/repositories/fft_decomp
--check-managed-baseline --executable scratch/all-data-extraction/SCUS_942.21
--output <new scratch report>`. Offline tests need neither media nor the reference
checkout. The audit checks source/function hashes, decoded metadata, 119 preserved
historical artifacts and 33 managed baselines. Final logs/audits are in evidence.
No C# change or .NET rerun; last implementation validation remains ZOD-129's
**4,119 .NET / 452 Python tests**. Owner definitions drafts stay untouched/excluded.

**Next: ZOD-131**, implement only this certified one-scenario scan and execute
ASN-001..016 with actual geometry/formula/clock/profile adapters. Scenario creation,
general ability/roster search, movement baseline/rank-list insertion, lottery,
policy/interface, authoritative RNG/commands and content/save/native/editor
activation remain future work. Editor UI and ZOD-106 remain deferred.
