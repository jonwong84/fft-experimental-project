# Fresh roster AI commands and bounded turns — ZOD-314/315

Current managed ownership: [ZOD-331](../development/ai-refactor-331.md) uses
`AiDecisionMaker`, `AiCommandExecutor` and `AiTurnController` with shared immutable
configuration. Older names below identify the historical source-traced slices;
lower-level pilot APIs are now internal. Original rules and evidence remain unchanged.

Implemented 2026-11-05 from managed baseline `dc660e9`, following
[the authority design](../architecture/zod-310-roster-decision-authority.md) and
[Framework transactions/continuations](roster-decision-and-continuation.md).
The reference remains clean/read-only at
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source audit](../evidence/ai/roster-turn-source-v1.json) rechecks the existing
[83-function/27-file trace](../evidence/ai/roster-authority-source-v1.json).
The twelve [independent compositions](../fixtures/behavior/roster-turn-contract-v1.json)
combine original selection/dispatch, confirmed routes, actual formulas, RNG and
choice settlement. Managed corpse-input rejection and exact-session recovery are
explicit boundaries, not original terminal behavior. No original/C execution.

## Command publication

`RosterBasicSkillAiCommandExecution.Execute` takes an actual roster session,
request and immutable supported profiles. It accepts neither caller RNG nor a
cached plan. Inside `RosterDecisionBattle.Execute`, it checks explicit ordinary
dispatch facts and semantic equality of stable teams/source order/ground with
the authoritative profile. Equivalent separately allocated profiles are allowed;
foreign team/order facts are rejected before selection.

One fresh full supported-list query reads the callback's current battle and RNG.
The source `ai_command_build` branches derive from actual current choices:

- Move unused, different destination, and ACT used or selected Move-first: Move.
- Otherwise ACT unused with a selected action: actual Attack, Accumulate or Stone.
- Otherwise: END.

Post-ACT still owns fresh preparation and the empty-ranking lottery, using real
damage, push, PA and resolution RNG. It never invokes action entries again.
The private selection result maps directly to concrete Framework factories.
Move-first freezes the selected Attack/self/Stone/END payload and issues an exact
continuation only with an accepted actual path/heading. A post-ACT Move and its
actual receipt-bound fixed/kept facing publish atomically. Retained ACT performs
no second query, lottery or RNG adoption.

Only accepted actual progress publishes the successor priority workspace.
Unknown cast keys, invalid ordinary facts, RNG exhaustion, actual admission or
late settlement failure return the exact input session/workspace, empty events,
no path/token/diagnostic and no private RNG adoption. A late post-ACT failure
preserves the earlier accepted ACT. Pure public plans remain diagnostics.

## Complete turns and recovery

`RosterBasicSkillAiTurnExecution` composes these actual owners using the same
private routing kernel as the older pair drivers: retained command first,
pending actual facing next, active actor to fresh post-ACT selection, otherwise
complete. It also admits a current post-ACT state or an actual pending receipt.
It stops before the next AddCt step. `Facing = null` keeps the actual heading;
a cardinal value is explicit, with no automatic enemy-facing policy.

Failure returns all accepted state/events/decisions/priority knowledge and an
opaque recovery cursor. Resume accepts only the exact returned session object,
freezes configuration/workspace/stage/continuation and emits only newly accepted
events/decisions. Reconstructed, foreign, older or newer branches reject binding.
Same-state retry is deterministic; no mutable consumed registry or save token.
An invalid fixed-facing configuration can therefore fail again until the caller
explicitly finishes the real receipt or abandons a remaining choice on a new
manual branch; Resume cannot silently replace that configuration.

The fresh query domain remains initially living compact16 general-policy units
on ordinary flat ground. An ACT-first actual KO may make fresh post-ACT selection
reject its deferred corpse domain. The driver retains that real KO, resolution
RNG, ACT choice and priority writes for recovery/manual END. A Move-first KO can
finish via its actual completion receipt without another query. Neither case
implements full corpse clocks, terminal policy or frame RNG equivalence.

## Verification

Twenty AI cases include the twelve independent source scenarios, deterministic
replay/permuted storage, actual public AddCt bootstrap, both orders, Self PA,
empty fallback, retained Stone, explicit/kept facing, both KO paths, exact recovery,
foreign/stale binding, late actual RNG exhaustion, and rollback of private
post-ACT path/priority/facing. Public tests construct no internal session or
receipt and add no production friend access. Three Python cases validate source
compositions and reject tampered results. Actual lower-layer Attack/self/Stone/
Move/END and frozen four-kind continuation suites remain in the full regression.

Locked restore, zero-warning/error Release build, **4,449 .NET cases** (1,516
Framework, 1,188 AI), **587 Python tests**, pinned source/prepared SDK, algorithm
guide, coverage and local-link audits pass. All nine owner drafts are unchanged.
See the [implementation receipt](../evidence/ai/roster-turn-implementation-v1.json);
ignored logs are under `scratch/roster-turn-314-315/`.

[ZOD-316](roster-ai-replay.md) proves several real turns through explicit clock
advancement and shared state/priority knowledge. [ZOD-321–325](dash-ai.md) now
extend the supported roster lane with Dash. Other policies/abilities/maps, full corpse/
terminal/frame RNG, automatic facing, host/editor, owner drafts, broad replacement
interfaces and ZOD-106 remain separate/deferred. This is bounded integration,
not full original encounter or campaign AI parity.
