# Shared magic calculation — ZOD-343

Owner-approved consolidation, 2026-11-05, from managed base
`f41ad2c2362096b71272ddbf01fe744b52691f7c`. The read-only reference remains clean
at `11464943bbdbd2a0af022d90cb7f10455cf911df`. This refactors already-traced
behavior; it adds no new original rules or source-derived expectations.

## One calculation entry

Call `FaithMagicFormula.ResolveAfterHit(request, rng)` for the supported shared
0x08/0x0C calculation. `FaithMagicFormulaRequest` groups the immutable amount
inputs, element context, target Undead fact, accuracy, evaluation mode and existing
finalization profile. The result remains `MagicResultCompositionResult`: bounded
HP result data, proc diagnostics and a candidate RNG continuation, or atomic
rejection. The caller must already have resolved earlier admission/evasion.

The entry composes the existing amount/modifier/element/Faith owner and one ordered
tail for HP routing, conditional proc, empty-status preservation and finalization.
The tail moved from `MagicResultFinalization` into `FaithMagicFormula`.
`MagicResultFinalization.EvaluateAfterHit` retains its signature and delegates to
that same tail for callers with an existing opaque projection. Raw finalization
and its admission predicate keep one owner. The arithmetic, routing and proc
helpers remain separately readable and testable; none are duplicated in the new
entry. The earlier strict neutral projection also remains unchanged.

All current limits remain: explicit living/non-Petrify/Wall/no-Golem/empty-status/
no-pending work, already-supplied MA/Faith and modifier facts, correct element
knowledge, source integer stores/order and evaluation-mode draws. Argument errors
still throw; unsupported finalization or counter exhaustion returns the incoming
RNG without partial result data. Pure amount projection before admission consumes
no RNG. No geometry, HP/status publication, resource use, charging, commands or
magic AI activation is added.

## Formula and targeting extension direction

An ability should select its calculation and targeting independently, supplying
validated parameters and separate presentation references. A different spell
power or targeting shape can reuse this shared formula. A new algorithm belongs
in readable C# with its own tested semantics. Introduce an explicit stable
formula/profile identity and dispatch when a concrete additional algorithm needs
it; this change adds no universal interface, executable JSON, plugin loader or
per-spell coordinator classes. Original compatibility behavior and custom variants
must retain separate expectations and explicit selection.

The owner clarified the desired future cone using Abyssal Blade in War of the
Lions as an example. The agreed geometry is **nine tiles**, excluding the actor:
depth1 has one tile directly ahead, depth2 has three centered tiles, and depth3
has five centered tiles. For depth `d` in1..3, lateral offsets are `-(d-1)..d-1`.
Rotate the pattern with the attack direction. This is an owner-specified future
shape, not a claim that War of the Lions behavior has been traced or implemented.
Height, obstacles, map edges, occupancy, target eligibility and targeting
orientation/facing effects need explicit rules when this targeting work is selected.
Existing imported AI shape bits do not establish actual cone geometry support.

Future geometry should supply eligible targets in a defined deterministic order.
The calculation handles one target and its explicit RNG checkpoint at a time;
the caller owns target order and any transaction that adopts returned RNG/results.
AI evaluation and execution should reuse the appropriate geometry/formula owners
while preserving their mode-specific behavior. A cone does not need its own copy
of Faith damage arithmetic. This refactor implements no target collection or
multi-target publisher.

## Validation and continuation

Five new Framework cases run the20 existing composed literal descriptions through
raw formula inputs, preserve required input guards, verify healing without damage
element context, check deterministic request isolation, and resolve two ordered
target calculations around a rejected target with exact RNG checkpoints. All
existing expectations and fixtures remain unchanged.

Locked restore and Release build pass with zero warnings/errors. Full validation:
**4,618 .NET tests** (1,577 Framework) and **635 Python tests**. Pinned finalization,
Faith, element and prepared-data audits, guide, coverage, local links and nine
owner-draft checks pass. [Implementation receipt](../evidence/combat/magic-consolidation-implementation-v1.json);
ignored logs: `scratch/magic-consolidation-343/`.

ZOD-342 remains next: trace magical evasion and pre-formula admission before
expanding the supported entry boundary. Owner drafts, JobStats, host/editor,
automatic facing, full corpse/Golem/terminal/status/frame RNG, broad interfaces,
resource/target/command integration and ZOD-106 remain separate/deferred.
