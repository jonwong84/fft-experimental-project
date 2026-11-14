# ZOD-122 — Confirmed voluntary Move execution contract

Date: 2026-10-27. Managed base: c358dae; tracked files clean on entry, with the
owner's untracked Common editor classes preserved. Read-only local decomp was
freshly checked clean at 11464943bbdbd2a0af022d90cb7f10455cf911df before study
and after validation. The [evidence](../evidence/ai/voluntary-move-contract-v1.json)
pins 13 files / 33 functions by normalized UTF-8/LF hashes and navigation spans.
See the [flat movement source contract](zod-120-flat-ai-movement-contract.md),
[implemented query](../behavior/flat-ordinary-movement.md) and
[existing facing boundary](../behavior/turn-facing-implementation.md).

This spike specifies one additive adapter. It changes no C# API, movement policy,
scenario, save format or editor. Its [60 vectors](../fixtures/behavior/voluntary-move-contract-v1.json)
have six families: 26 source-derived normalized helper descriptions and 34 managed
design proposals. All 18 VME production implementation cases are unexecuted.
No source helper/proposed adapter runs C, C# or the original game. Logical ordering
labels below describe the source writes; they are not original event objects.

## Original confirmation and completion order

`battle_state_select_move_tile_enter` rebuilds range for the casting unit;
`battle_state_select_move_tile_update` builds and copies its selected path. A bad
path returns to a menu rather than confirming a Move. AI dispatch reaches this
same path through MOVE/MOVE_SELF. The new adapter must rebuild from the command's
current immutable snapshot, never trust an earlier query or caller-built path.

`battle_state_unit_moving_enter` saves tile/facing/mount state for possible undo.
`battle_state_unit_moving_update` advances `move_step_update` until the path and
step animation finish, then opens confirmation. For ordinary adjacent walking,
`move_step_start_walk` sets the runtime facing to each step's heading. The
ACTION_EXECUTE exception preserves facing for knockback. `move_settle_on_tile`
already writes the final tile and facing back to battle stats before confirmation.

`move_confirm` then repeats that tile/facing write, sets TURN_ACTION_MOVED and
calls `turn_move_taken_set`. The latter calls `turn_move_act_taken_set`, which
sets movement_taken and tests `turn_should_end`. With no restrictions, Move-first
leaves the actor active; Act-first then Move calls `turn_end`. Only afterward does
the controller start post-move events. This is the relevant logical order:
tile + real path facing → Move taken → possible turn end → post-move processing.

`turn_end` adds 20 CT for each unused unrestricted choice and caps at60. Both
choices consumed means no refund. Its has_turn check makes a later call a no-op,
including no second cap on a value supplied after the earlier call. `unit->ct`
is stored as u8; this routine uses a u16 local accumulator. The managed active
actor domain is CT0..99; Move-first can retain99, while finishing both choices
caps it to60. Choice/has_turn source fields are bytes.

The continuation still reaches AfterCommand → ContinueTurn → WaitMenu →
WaitDirection when the turn has ended. WaitDirection starts from current runtime
facing, may choose a new direction, calls `turn_end` again and writes tile/facing.
That repeated end must not apply another refund in the adapter's FinishFacing.
Camera/frame/animation waits, including counters checked against0x1f, are
presentation scheduling; they do not become simulation ticks or RNG inputs.

Ordinary NO/UNRESOLVED confirmation calls `move_undo`/`previous_state_restore`,
restoring tile and facing without consuming Move. Teleport's keep-on-cancel
exception and mounts are excluded. The proposed API represents an already
confirmed command; it has no tentative movement state or cancel parameter.
A later presentation may display a query path before submission. Source undo is
documented without claiming the new adapter implements that interactive flow.

Coordinate inputs to `unit_set_tile_position` are u8; stored facing is a cardinal
bitfield. Runtime facing uses signed 16-bit reads with quarter-turn0x400, while
previous_facing is saved as u16. Names must cross three encoding domains:
packed move directions East/West/South/North are0/1/2/3; stats cardinal directions
South/West/North/East are0/1/2/3; managed BattleFacing is East/North/West/South.
ZOD-121 already returns a typed final heading, so do not cast original ordinals,
recompute heading from net displacement, or admit fractional/negative runtime
angles into this closed adapter.

## Post-move and post-KO admission

`move_get_post_events`/`move_get_ability_events` can select crystal/treasure pickup,
mount changes, Move-Find Item or traps, charging cancellation, and Move-HP/MP Up /
Move-Get EXP/JP. `move_start_next_post_event` resolves these before continuation;
some invoke other rules or RNG. No general zero-draw movement claim follows.

The first adapter inherits the ZOD-121 certified open dry flat two-living-opponent
domain, source Move/Jump bytes, explicit cost1 and 1..16 dimensions. Admission
also explicitly certifies **no post-movement effects or scripted/map transitions**,
including the benefits above. The small current model cannot detect unrepresented
original flags; caller certification remains essential. Do not silently treat a
geometry-only certificate as validation of arbitrary imported battle data.
The no-effect path/confirmation/ordinary choice operations themselves draw no
gameplay RNG. Move must work at an exhausted incoming draw count and preserve
the exact RNG word/count. Delegated attacks, readiness and future candidate
upkeep have their own draws.

After a nonlethal action/push, rebuild using settled positions. A previously
occupied panel can become free and the new opponent panel blocks. After a KO,
the living-opponent query rejects; this adapter will not add corpse traversal,
death scheduling or a one-unit movement mode. Guard precedence below means an
already pending/inactive/moved state may reject before reaching that profile test.

The existing TwoUnitBattleScenario first-KO outcome is an authored terminal
policy. Its Apply rejects every later command as AlreadyCompleted; nothing here
bypasses that host. Do not infer a universal original first-KO victory rule from
it: the original controller also handles scenario events/finish operations.
In the separate existing facing wrapper, a fully settled lethal Attack can leave
a pending facing receipt. Existing Fixed/KeepCurrent may complete facing only;
ChooseClosedEnemy rejects a dead enemy. That is not permission to move after KO,
resume a completed scenario, or advance the unsupported death scheduler.

## Proposed public boundary

Add `FacingTurnBattle.MoveAlongPath(state, command, FlatOrdinaryMovementProfile)`
in ZOD-123. Leave all existing Move overloads and BattleMovementPolicy values
unchanged; this is explicit per-command opt-in, not a global original-mode switch.
The existing FacingTurnBattle.Begin lifecycle remains the entry; no attach,
restore, public snapshot injection or externally minted movement receipt.

Return a new PathMoveResult with Accepted, FacingRejection, TurnRejection,
PathRejection (FlatMovementRejection), State, Events and optional Path. This
avoids changing the positional contract of FacingTurnResult. Null arguments are
programmer errors. A rejection returns the identical input FacingTurnState,
empty events and no path, without revision/RNG/CT/receipt changes. Exactly the
applicable rejection field is non-None; accepted results have all None.

Proposed validation precedence:

1. PendingFacing rejects FacingPending, before any command work.
2. Revision==ulong.MaxValue rejects RevisionExhausted.
3. Unknown actor rejects TurnCycleRejection.UnknownUnit.
4. Actor not owning the active turn rejects InactiveUnit.
5. Consumed Move rejects MovementAlreadyTaken; ActionTaken alone permits Move.
6. Run the current ZOD-121 query with destination and explicit profile. Propagate
   UnsupportedProfile or InvalidDestination through PathRejection. Stay remains
   an AI candidate, not a confirmed Move; never consume Move on a zero-length path.

A previously computed path/result is plain data and accepts no authority. The
method accepts only a command/profile, runs the query again and returns its own
selected path. Do not revalidate with the Manhattan default or add an implicit
fallback when certification fails.

Use TurnCycle.Apply(ConfirmMove) for ordinary authority/choice/CT semantics. Its
calculation can be staged privately for validation before geometry; nothing is
published until every check and construction succeeds. Construct the resulting
body position/HasMoved, combat actor's path heading and synchronized turn state
locally. Retain the map, profiles, both HP/status/KO/stat sets, opponent facing,
RNG, ticks and every unrelated field. No new clock or movement policy is needed.

Publish one immutable result with this logical event order:

1. Existing wrapped BattleMovementEvent for from/to.
2. New VoluntaryPathCompletedEvent(actorId, selectedPath, previousFacing), with
   final facing carried by selectedPath.FinalHeading. Emit even if unchanged.
3. Existing wrapped TurnChoiceConsumedEvent(ConfirmMove).
4. Existing wrapped TurnEndedEvent only when this Move consumes the second choice.

These new managed events are an API proposal. Do not reuse FacingSettledEvent
for the walking event; that existing event describes Wait/Finish facing selection.
All observers receive the already reconciled final state, not incremental writes.
Do not call the public legacy Move and patch its returned state after the wrapper
has issued a receipt: that would bind authorization to the wrong snapshot.

Advance wrapper revision once. If TurnEnded occurred, mint the internal existing
FacingCompletionReceipt only after the complete result snapshot includes tile,
path-facing, choice flags and CT. Bind that exact snapshot instance, ended actor,
new revision and unchanged wrapper lineage. With Move-first there is no receipt
and the actor remains active. With Act-first Move, block every further command
until existing FinishFacing succeeds. A later Attack following Move-first uses
the updated actor facing and its own fully settled result before issuing a receipt.

KeepCurrent now means the settled path heading on this explicit command path.
Fixed and ChooseClosedEnemy retain existing semantics, using current positions.
Same-facing completion still consumes the pending receipt and increments revision
once, with no second movement, damage, CT or RNG operation. Identity, snapshot,
actor, revision, lineage and inactive-clock checks remain mandatory. Replaying
an old immutable input can produce another equivalent branch with fresh internal
receipts; those receipts cannot cross branches/runs or later turns. There is no
global consumed-token set or serialized continuation.

Counter boundary follows ZOD-119: a mutation at revision MAX-1 may publish MAX,
including a pending receipt if it ends the turn. Finish at MAX then rejects
RevisionExhausted. Do not silently add revision reservation, wrap counters, or
reuse an earlier receipt to escape this documented limit. Normal tests use the
internal friend boundary for counter setup, not a new public state constructor.

## Preview separation and next milestone

Original `movement_pickup_simulate`/`ai_acting_unit_place` change candidate
coordinates and movement_taken without real path-facing publication. A future
candidate simulation must preserve that distinction; never call MoveAlongPath to
simulate an AI origin. The ZOD-121 query stays phase-independent and side-effect
free. Finishing this execution boundary does not implement original AI search,
roster/team normalization, candidate clock/scoring or decision RNG commitment.

**ZOD-123** should implement only this additive method/result/path event and
local settlement helpers, then execute VME-001..018. Preserve existing public
method bodies/behavior, legacy tests/fixtures and historical evidence; no
scenario/content/save/native/editor activation. Test both actor identities and
orders, real nonlethal pushes, rejection precedence, stale/cross-branch/earlier
receipts, exact event order, immutable replay, every profile/path boundary and
counter/RNG limits. Keep old query/source inventories historical and publish new
implementation mappings. General effects, animation/undo, post-KO movement and
AI preview/candidate simulation remain outside. Editor UI and ZOD-106 stay deferred.

## Validation and reproduction

Eighteen new Python tests exercise literal source heading/confirmation/undo/CT
rules, preview preservation, normalized proposed guards/publication, current
paths, receipt binding, post-KO limits, counter boundaries and repeatability.
All **396 Python tests pass** (18 new). Source-derived and managed-proposal
classifications are checked separately.
They do not execute a production adapter or establish original-game equivalence.
Last implementation validation remains ZOD-121's 3,567 .NET / 378 Python tests;
no C# changes or .NET rerun in this spike. Final Python counts and repeated audit
fingerprints are in the evidence. No new native/headless/original/emulator or C
execution. Owner editor files stay excluded; logs/builders remain ignored under
scratch/voluntary-move-122-* and scratch/build_voluntary_move_122.py.

```powershell
$env:TEMP = Join-Path (Get-Location) 'scratch'
$env:TMP = $env:TEMP
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_voluntary_move_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --output scratch/voluntary-move-122-source-final-a.json
```

Use another output filename for an audit rerun; output creation is exclusive.
The ordinary Python suite validates 60 vectors, 82 preserved artifacts and 24
managed baseline files without prepared assets or a decomp checkout. The optional
source audit additionally checks the clean pinned reference, 13 files / 33 functions.
