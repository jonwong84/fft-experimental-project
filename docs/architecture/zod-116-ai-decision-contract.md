# ZOD-116 — Original AI map and proposed C# decision boundary

Date: 2026-10-26. Managed base: 96bc879, clean on entry. Read-only reference
C:/repositories/fft_decomp was freshly checked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df. Every source symbol below refers to
that revision. [Evidence](../evidence/ai/decision-contract-v1.json) fingerprints
16 files/71 functions; line anchors are supplemental to those identities.

The original algorithm can be translated behind a replaceable Zodiac Framework
interface. It is a resumable search that projects outcomes, ranks candidates,
consumes decision RNG and emits one command at a time. It cannot be faithfully
replaced with a stateless nearest-enemy heuristic or by calling the current
execution-mode Attack API on a discarded snapshot.

This completes a stage/helper map and proposed interface, **not a complete
original-AI port or executable whole-turn oracle**. Fifty
[description vectors](../fixtures/behavior/ai-decision-contract-v1.json) exercise
eight isolated helper/stage families. AIC-001..020 remain unexecuted production
cases. No C#, runtime interface, scene, content, save or editor changes are made.

## Decision stages and ordering

All AI source paths in this section are under src/battle/ai unless qualified.

1. abilities.c::ai_workspace_init visits present original unit slots in ascending
   order, assigns compact records and builds ability lists. Average present maxHP
   is capped at255. usable_abilities_build starts an ordinary human with Attack,
   appends known/AI-usable primary then secondary abilities in their source order,
   then Frog Attack and a terminator if room remains. ai_unit_abilities_init
   derives weapon/effect summaries and copies ENTD AI policy bytes. For the first
   subset, only ordinary Attack is usable; the nonfrog Frog entry is disabled.
   Do not invent a learned-ability grant from a job name or silently omit a
   usable ability in an original compatibility encounter.
2. turn.c::ai_command_build checks the scanline budget, then resumes refresh,
   movement or action selection when needed. It ends instead of repeating a
   selected command if the observed Move/Act flags did not change. A missing
   compact record for any present unit also ends the turn. These are source
   failure behaviors, not evidence that an arbitrary managed roster is valid.
3. acting_unit_init records current coordinates, current team, Move and estimated
   ticks until the actor's next turn. decision_flags_refresh compares each other
   unit's **initial team** against the actor's **current team**, rebuilds target
   flags, and performs a move/act-free simulation pass to refresh transient
   status/considered flags. Current and initial teams cannot be collapsed into
   one field for general Invite/Charm behavior. That behavior is excluded here.
4. ability_considerations_set filters both sides' abilities by current and
   predicted statuses, MP, equipment/learning and inventory/level restrictions.
   It derives support shares, usable ranges and random-use thresholds. Sword
   enemy minimum reach becomes sword range1 + Move. After the actor has acted,
   its usable actions are disabled for the next movement decision.
5. reachable_tiles_build calls original movement range generation, excludes other
   occupied living-unit destinations, includes the actor's current tile, and
   handles moved/Don't Move and known traps. Movement allowance and target-distance
   propagation are distinct calculations. Ordinary target_distance_grid_fill
   calls map/move_range.c::ai_move_propagate_from_target; only the Teleport branch
   directly uses Manhattan distance. Do not assume all original path propagation
   equals the current managed movement prototype.
6. decision_target_consideration_init chooses the source policy and focus target.
   AI_AUTOBATTLE_MANUAL=0 also selects the general algorithm for an enemy; the name
   does not mean no AI. Special ENTD behavior, statuses, team support and player
   autobattle settings can select other branches. Normal nonplayer, nonsupport
   enemy behavior focuses the nearest eligible enemy. Tied nearest targets keep
   the first original slot because the comparison is strict distance less-than.
7. action_select resets ranks, fills foe proximity, then tries status-imposed
   behavior, conditional retreat, special autobattle policy, and finally the
   general search. In a two-unit, no-support, no-control-status encounter there
   is no extra ally enabling the support-dependent retreat branch. Other policies
   remain separate unresolved implementation families, not aliases of general AI.
8. general_select builds three movement scenarios, seeds a move-only baseline,
   evaluates keeping a charge (excluded here), enumerates the ability list,
   then runs the ranked lottery or movement/Defend fallback. Scenario0 contains
   all reachable tiles. Scenario1 is a target-distance subset with a selected
   destination; scenario2 is its reachable complement. Original nonplayer units
   outside the reach threshold consume rand()%7, halving reach on zero. There is
   no draw on the player-team or in-reach branch. Original stay-near/focus rules
   and low-HP crystal handling are not general movement preferences to invent.
9. evaluation.c::ai_ability_outcome_evaluate filters useful targets and builds
   origin/aim masks. A short evadeable sword receives exhaustive-origin scanning.
   origin_reachable_evaluate visits layer, y, then x; origin_aim_tiles_evaluate
   visits aim tiles in the same ordering. Some other abilities instead consume
   score-ranked origins/aims. A zero-area ability inserts its best result into
   the ranked list; it does not necessarily retain one slot per movement tile.
10. An origin at the actor becomes Act-before-Move plus the scenario's destination;
    another origin becomes Move-before-Act. ai_move_origins_evaluate explicitly
    evaluates both orders where applicable. ai_action_evaluate projects the
    candidate and keeps it only when ai_rank_is_higher says it improves the best.
11. ai_command_build publishes Move first when permitted and required, otherwise
    Act or EndTurn. It preserves observed choice flags and a two-entry command
    history. A successfully executed Move can reuse the selected action for Act;
    after Act the source decides again to choose subsequent movement. A complete
    turn must not be frozen into an irrevocable precomputed plan.

## Candidate simulation, score and RNG

The prepared SCUS_942.21 was verified as 356,352 bytes with SHA-256
287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562.
The PS-X EXE header gives load0x80010000/payload0x56800; target/main.yaml maps
g_ability_data to0x8005ebf0, corresponding to file offset0x4f3f0. Its eight-byte
Attack record confirms enemy/HP, evadeable and physical flags, with no stat-effect
or random-use flag. include/fft/ability.h establishes the stride and flag layout.
The evidence stores the record fingerprint and decoded facts; raw bytes stay under
ignored scratch. This validates the zero stat-effect bonus used below without
treating the weapon-summary flags as identical to the primary ability record.

evaluation.c::ai_action_simulate_and_score snapshots selected unit/action/equipment
fields, applies candidate Act and movement in the specified order, ends the
simulated turn, and advances the source clock. With no charges/performers it still
processes a turn event before checking that none remain. Otherwise it continues
up to the source event limit256. Refresh passes update flags without a scored
candidate. Saved between-turn globals and unit fields are restored afterward.

The score includes floor(HP*128/maxHP), signed status weights, 51 per unchanged
human equipment slot (including unchanged empty slots), MP share/modifiers and
team Golem. Enemies contribute the negative of their unit score. Ordinary sword
damage to an intended enemy adds no stat-effect priority bonus. Base hit is a
separate mean across counted target/strike pairs, not a multiplier of the final
HP score. The closed no-MP/no-Golem, unchanged-equipment slice cancels the seven
equipment bonuses between two ordinary humans. Given actorHP50/max50:

- EnemyHP50/max50, no status: priority0.
- EnemyHP25, no status: priority64.
- EnemyHP10 with Critical weight-32: priority135.
- EnemyHP0 with Dead weight-192: priority320.

These are **post-simulation score-component vectors**, not complete search
results. The helper takes statuses explicitly; it does not establish the entire
status-transition or simulated-clock lifecycle. General MP, equipment changes,
other statuses, reactions, summons and pickups need separate contracts.

The preview/execution distinction is essential:

- combat/outcomes.c::formula_get_random returns fixed0x4000 outside execution.
  Ordinary sword critical's fixed percentage is50, so no critical/bonus/knockback
  is projected. conditional_status_proc_roll_inner short-circuits its draw outside
  execution. The empty proc adds no AI status choice or proc draw.
- formula_core.c::evade_source_roll calls random_roll_pass_fail **before** its
  execution-state check. All four physical evade sources therefore draw from the
  real RNG during AI simulation. A partial evade changes displayed accuracy, not
  a random AI miss. Front class evade10 projects full normal sword damage25 and
  hit90, not expected damage22 or an executed seed-specific result. Certain evade
  sets accuracy/hit to0, still consumes four rolls, and rejects that target outcome.
- Thus the bounded **formula-level** projection uses four real draws on either
  hit-capable or certain-evade inputs. Seed57, PA5/power5, HP50 and front evade10
  projects HP25, hit90, no critical/push, word3587663605/count4. Current execution
  on that seed instead crits45 and consumes seven draws. These are different APIs.
- turn/clock.c::upkeep_action_build starts with rand() even when nothing visible
  changes, including simulated readiness. Search refresh and every move-only and
  action candidate can therefore advance the decision RNG. There are also direct
  draws in ranked_select, scenario reach randomization and other ability/policy
  branches. The baseline snapshot and ai_simulation_end do **not** restore the
  RNG. Returning projected HP to its baseline does not return the random stream.

Do not use a separate unrecorded random stream or discard the actual decision's
draws. A pure C# function can return an explicit advanced RNG without directly
mutating the input. Hypothetical inspector probes may discard their returned
progress, but a real decision must commit its RNG progress through an authoritative
transition. Exact whole-search draw totals remain unproven until enumeration,
clock and resume behavior execute together. Formula vectors are not those totals.

## Ranking, widths and original quirks

rank_compare compares decided:u8, priority:s16, then base-hit:u8, all descending.
Candidates have decided0; chosen cache entries get255. ai_rank_is_higher then
prefers **lower** foe proximity on a rank tie; exact equality keeps the incumbent.
The proximity grid sums max(10-Manhattan distance,0) for imminent eligible enemies,
so lower values can prefer a tile farther from danger. It is not a shortest-path
or always-approach score.

ai_best_rank_insert requires a strict rank improvement over the move-only baseline
(which has hit100), then inserts into the eight-entry list using rank/proximity.
destination_select and origin_next_take have a different tie rule: the last
scanned equal-key tile wins. Destination keys combine the stored signed16 tile
priority in the high half of a signed32 key and unsigned8 proximity below it;
priority-first then compares target distance, while distance-first reverses
those comparisons. Preserve the different local rules and source scan order.

ranked_select consumes rand()%100 for each visited slot, including zero-hit slots.
The first passing roll selects that entry. If all eight fail but any chance was
nonzero, it selects entry0; all-zero returns failure and invokes the fallback.
For seed1 first raw16838, modulo is38, unlike multiply/divide scaling51. Do not
reuse execution evasion's percentage scaling for this lottery.

Rank resets use priority-32767, not short.MinValue, and do not clear the entire
payload. Priority accumulation stores into s16 after each unit contribution.
Coordinates are u8; ai_target_facing_find reads coordinate bytes as s8. The
proposed first small map stays below that signed wrap. Grid capacities, 34 ability
slots, 16 compact records and eight rank slots are distinct: keep observed
selection/failure behavior, without imposing all original allocation sizes on a
generic public engine API. This is Zodiac-owned, not a new Tessera AI abstraction.

## Wait facing and pacing

evaluation.c::ai_wait_facing_choose runs at Wait-direction entry, using current
coordinates after actions/movement. A stored hint0..3 names South/West/North/East;
hint4 keeps current and hint5 requests selection. Selection prefers the nearest
imminent enemy with a viable direction, then an enemy of any kind. A direction's
adjacent lower tile must be in bounds, unblocked and less than three heights
above the source; all four become viable if none pass. If the enemy direction
fails, retain a viable current facing, else the last viable direction.

Exact diagonals choose SW=South, NW=West, NE=North, SE=East. Map by name to managed
BattleFacing (East0/North1/West2/South3), not by numeric cast. Coincident/no-enemy
fallback can index a leftover stack byte beyond the four-direction array. Record
that uncertainty; the first managed profile should explicitly reject that chooser
case rather than silently invent its original result. The authored first-KO
objective may stop before it, but that is not an original victory proof.

Controller EndTurn sets runtime presentation turn_action_flags and calls turn_end;
it does not set both stats movement_taken/action_taken, so do not erase the source
unused-choice CT refund. ContinueTurn still routes a finished turn through the
Wait menu/direction path; unit_set_tile_position publishes final facing even when
Move+Act already ended the clock turn. A future facing operation must handle this
completion boundary without refunding CT or consuming RNG twice. Current Wait
accepts only unit ID and cannot express this. Current Attack may automatically
complete the turn, so merely adding a facing argument to active-unit Wait would
not cover every path.

The original controller waits until frame31 before building a command and also
has a Wait-facing delay. Search stages yield at scanline limits441 (decision),
313 (simulated clock) and325 (pathfinding), saving nested counters/phase state.
The owner's brief enemy movement/action pauses belong to presentation. Proposed
managed work-unit budgeting must preserve logical order and RNG across yields;
it need not read wall-clock/VSync from deterministic rules. Whether original
frame interleavings affect broader random behavior remains an explicit limit.

## Proposed replaceable interface — no production types yet

The following names are a contract sketch; no interface assembly or plugin loader
is introduced by this spike:

~~~csharp
public interface IBattleDecisionPolicy
{
    DecisionPolicyId Id { get; }
    DecisionStepResult Step(DecisionRequest request,
        DecisionContinuation? continuation, DecisionWorkBudget budget);
}
~~~

DecisionRequest should contain immutable battle/definition inputs, actor ID,
selected combat and movement capabilities, normalized AI roster/policy inputs,
source slot ordering, the snapshot revision, requested phase (NextCommand or
FinishTurnFacing), and the prior authoritative command receipt when continuing.
RNG comes from the validated battle snapshot, with both existing copies agreeing.
Roster includes initial/current teams, control mode, policy flags/focus/stay-near,
known ordered abilities and the explicitly supported equipment/status projection.
Stable managed IDs do not by themselves establish original slot order.

DecisionStepResult should distinguish Pending, Ready and Unsupported. It returns
an immutable implementation-owned continuation, explicit RNG word/draw-count
progress, and optional trace facts. Ready contains one typed Move, Attack, Wait
or turn-facing request appropriate to its phase. The continuation records policy
ID/version, stage/counters, cached candidate/rank state, expected choice flags and
the revision/command receipt it belongs to. This is an in-memory continuation,
not a battle save feature. Unsupported setup must fail before publishing progress;
stale/mismatched continuations must not be silently reused.

An authoritative runner validates and commits decision progress (including both
RNG copies) before executing a ready command through the existing battle rules.
Pending progress may also require an explicit RNG commit. It must never repeat a
committed chunk on retry. The runner then feeds the actual accepted/rejected
receipt and fresh snapshot into the next step; the policy cannot directly assign
live HP/position or bypass legality. A separate turn-facing transition/capability
is needed before FinishTurnFacing can be admitted. Specify revision/progress
receipts and atomic failure in implementation tests, not as loose host callbacks.

Keep the source-derived original policy as the default. Refined scoring or a
different implementation supplies another explicit policy ID and continuation;
do not patch the original silently. Diagnostic traces should expose stage,
candidate order/origin/aim, projected damage/hit/score, tie reason, RNG range and
selected command. They can support a future inspector without requiring editor
UI work now. Select identical fixtures/seeds to compare implementations; using
different decision algorithms can legitimately change later RNG consumption.

## Concrete gaps and next bounded milestone

Existing managed code supplies execution-mode sword/flat knockback, CT, movement
subsets, immutable state and replay. It lacks the AI projection above, normalized
AI roster/teams/slot order, original search sessions/RNG commit, full original
candidate clock evaluation and end-turn facing. TwoUnitBattleScenario also does
not select FlatSwordBattleProfile yet. No playable original AI is ready to wire
into the window simply by choosing an interface implementation.

Recommend **ZOD-117: pure formula-level ordinary-sword AI projection**. Implement
the seven projection vectors and boundary/atomic RNG tests with explicitly
admitted current stats/geometry and returned projected damage/hit/HP/RNG. Preserve
execution Attack APIs and share arithmetic where behavior actually agrees. Keep
the four real evade draws, fixed midpoint critical check and skipped execution
proc; never reuse the seeded execution result as the projection. This milestone
does not implement search scoring, turn advancement, interface/session runner,
AI policy, scenario/native/save activation or editor UI. Recheck pinned source
and relevant callers before translation, including zero-accuracy handling.

Then select a bounded turn-facing transition and original movement/search
composition proof before the first complete selector. The full AIC inventory
is a roadmap, not a claim that ZOD-117 alone will complete it. General policy
branches, status tables/quirks, original full encounters and scheduling effects
remain explicit unresolved work. Do not replace missing dependencies with a
heuristic in compatibility mode. ZOD-106 and editor work remain deferred.

## Reproduction and validation boundary

~~~powershell
python -m unittest discover -s tools/verification -p test_ai_decision_contract.py -v
python tools/verification/verify_ai_decision_contract.py --reference C:/repositories/fft_decomp --prepared-exe scratch/all-data-extraction/SCUS_942.21 --check-managed-baseline --output scratch/ai-decision-116-audit-final-a.json
python tools/verification/verify_ai_decision_contract.py --reference C:/repositories/fft_decomp --prepared-exe scratch/all-data-extraction/SCUS_942.21 --check-managed-baseline --output scratch/ai-decision-116-audit-final-b.json
~~~

Use fresh output paths. The prepared-exe argument is also optional; ordinary tests
need neither original data nor a C checkout. It reads and verifies metadata only,
without executing the prepared file. Without --reference, the source check is skipped.
History fingerprints use explicit UTF-8/LF; the optional managed-baseline check
is for this checkpoint and should be omitted after intentional later code changes.
Generated reports and the one-off description builder stay ignored under scratch.
Both final audit reports are byte-identical, SHA-256
ecbaf56880f437255567b1e705b6d841828a06ced1d6840528e00751b3b6d40e.
They verify 50 helper/stage vectors, 16 source files/71 functions, the one retail
metadata record and all 37 historical/seven managed baseline files.
All **342 Python tests pass** (13 new). Last implementation validation
remains 3,044 .NET / 329 Python from ZOD-115; no .NET rerun or new native/original
execution is required for this source-only contract. Offline helper checks are
not an independent executable reference or full AI correctness proof.
