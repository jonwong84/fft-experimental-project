# ZOD-167 — Planned Move and retained follow-up authority

Design, 2026-10-28; managed base `0e38a59d5d04c67faa7749c01f2e6ceb12de1ce1`.
Behavior reference: read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source contract](zod-165-ai-command-continuation.md) and
[pure dispatch query](../behavior/sword-ai-command-dispatch.md) establish selected
action reuse after ordinary Move. The [existing handoff](ai-command-boundary.md)
owns fresh planning/RNG adoption and session construction. This milestone adds
no production API, gameplay behavior or executable continuation.

## Selected boundary

Keep Framework independent of AI. Add an opt-in Framework-owned two-step protocol
beside the existing Attack/Wait transaction. Proposed type names are
FacingMoveDecisionContext, FacingMoveDecision<T>, FacingMoveContinuation and
FacingMoveDecisionBattle; exact names may follow implementation conventions.
The first child, **ZOD-168**, implements this Framework boundary with a synthetic
synchronous planner. AI selection/dispatch integration is a later child.

The admitted actor is active with both Move and ACT unused, no pending facing,
and an ordinary confirmed-path profile. The selected destination differs from
the actor origin. Mounts, traps, pickups, benefits, scripts, charge cancellation,
restrictions and control changes remain excluded by the existing path certificate.
ACT-first, same-tile and post-ACT movement do not enter this first protocol.

The new context exposes only the current immutable battle, actor and RNG. It has
invocation-bound factories for **MoveThenAttack**, **MoveThenWait** and rejection.
MoveThenAttack freezes a Move command, path profile, ordinary Attack command and
weapon. MoveThenWait freezes a Move command, path profile and semantic FacingChoice.
Both carry the planner's final RNG and optional immutable diagnostics. There is
no public decision constructor, token constructor, raw snapshot adoption or cached
plan executor. Context closes on callback exit/throw; an older invocation's proposal
rejects even on the same input. Choosing a trusted planner remains the application's
responsibility; RNG continuity does not prove its policy calculations truthful.
Structural validation must not execute Attack from the pre-Move origin or resolve
deferred Wait facing there; those rules run against the settled moved state.

## Step 1: plan once, publish actual Move

1. Check current actor/session authority, unused choices and revision room before
   invoking the planner once. Validate proposal ownership and structural command
   actor/target consistency, then the existing forward RNG-checkpoint invariant.
2. Stage both RNG copies privately, preserving every other field, profile,
   revision and lineage. Invoke **FacingTurnPathMovement.MoveAlongPath** on this
   staged session. Its rebuilt physical path and final heading are authoritative;
   neither a preview path nor the older Manhattan Move API substitutes for it.
3. Reject the whole step on invalid selection/proposal/RNG/path/choice. Return the
   identical pre-Move session, empty events and no diagnostics, path or continuation.
   No RNG-only state is published.
4. On success publish exactly that settled Move, its events/path and one revision.
   The actor remains active with Move used/ACT unused and no completion receipt.
   Only now mint an opaque continuation containing the frozen follow-up. Bind it
   to the **exact returned FacingTurnState instance**, battle reference, actor,
   revision and lineage. Minting the token adds no revision or draw.

Confirmed ordinary movement consumes no RNG/ticks; all planner draws become live
only with successful Move. Presentation may pause between steps without becoming
simulation time or adding gameplay RNG draws.

## Step 2: reuse, without another planner

Continue accepts the current session, actor and opaque continuation only. It does
not accept a replacement command, weapon, facing, RNG, public dispatch outcome or
new planner callback. Missing/wrong-actor/stale/foreign tokens reject before command
work. Check exact state/battle binding, revision/lineage, active actor, Move-used/
ACT-unused choices, no pending facing and current revision room.

Execute the frozen Attack through FacingTurnBattle.Attack, or the frozen Wait
through FacingTurnBattle.WaitWithFacing. Use **current post-Move RNG**, already
adopted by step 1. No refresh, movement scoring, lottery or second RNG adoption
runs. Attack consumes its real conditional combat draws and returns its existing
settled completion receipt. FinishFacing remains a separate existing operation.
Wait adds no draws, applies the unused-ACT refund once and settles facing.

The original AI wrapper must retain ChooseAtWait as
FacingChoice.ChooseClosedEnemy(target), resolving it on settled post-Move positions.
Framework need not reference AI hint/score types. Fixed facing can remain an explicit
non-AI policy choice; the later original-AI wrapper must not precompute its deferred
facing at selection time. Lethal Attack keeps the existing living-enemy AI-facing
limitation; it never undoes an accepted Attack.

Success increments revision once. A failed follow-up returns the identical **moved**
session and empty events, with the same continuation still valid for that input.
It cannot refund decision draws or rewind the already-published Move. These atomic
failure rules are managed transaction policy, not a claim of original C rewind.
Counters are checked per actual step: do not reserve worst-case Attack draws or
future revisions. A Move may consume the last revision slot; later Continue then
rejects revision exhaustion while preserving that Move, matching the existing
per-transition revision policy. Wait can complete at MAX draw count.

## Replay and cancellation

Use the immutable-session convention already used by completion receipts:
repeating Continue on the **same original moved session/token pair** is a valid
deterministic replay, including retry after failure. On its returned newer session
the old token is stale. A replay of step 1 creates a distinct moved wrapper/token;
mixing tokens across those equal-valued wrappers rejects. Numeric equality or
matching lineage/revision alone is insufficient. No mutable global consumed-token
registry or save serialization is introduced.

There is no separate cancel-to-unmoved or token-reset operation in the first child.
Discarding a token does not undo Move or invalidate other copies. To abandon the
retained intent, the owner can explicitly Wait through the existing authority
with a valid facing choice. That publishes a newer session, ends the remaining
turn and makes the old continuation stale on the current branch. Any other legal
intervening command similarly changes the bound session; cancellation is never
silent reselection or implicit rollback.

## Implementation child and evidence limits

ZOD-168 must check: fresh/cached/closed proposal ownership; failed Move fully rolls
back planner RNG; successful path heading/choices and both RNG copies settle once;
only a successful Move issues a bound continuation; equal-valued/cross-session/
wrong-actor tokens reject; follow-up invokes no planner; actual Attack and Wait
use retained intent/current RNG; later command failure preserves Move and retries;
explicit Wait cancellation, receipt settlement, replay and actual counter limits.
Use a few behavioral scenarios, not a combinatorial API-shape matrix.

The later AI adapter must privately run existing fresh selection and
DispatchFreshSelection once, require a Move-first command and freeze the matching
follow-up. It must never accept caller-populated query outputs as authority.
Start at certified **post-prefix general-selection entry**: current fresh
refresh/preparation APIs require already-moved actors, so they cannot be relabeled
as fresh unmoved preparation. All remaining scoring/first-terminal-Attack/no-Defend,
canonical, stable-target and ordinary-continuation facts remain explicit until
derived. Fresh unmoved preparation, post-ACT reselection, broad search, replacement
interfaces, saved tokens and host activation remain separate.

Validation: the inherited pinned audit passes for nine continuation files/25
functions, ten branch vectors and preceding receipts
(`scratch/ai-move-continuation-167/source.json`). No production code or tests
changed; the last full validation remains ZOD-166's **3,906 .NET / 510 Python**
tests with a warning-free Release build, not rerun for this design-only milestone.
All new protocol acceptance cases above remain unexecuted until ZOD-168.
