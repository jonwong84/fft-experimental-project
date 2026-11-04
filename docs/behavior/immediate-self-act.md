# Actual immediate self ACT — ZOD-238–239

Implemented 2026-11-04 from managed base `e51c1fa`, following the
[ZOD-237 source contract](../architecture/zod-237-self-act-command.md). Read-only
fft_decomp HEAD was freshly checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Source functions and the actual-draw
boundary are pinned in the [study receipt](../evidence/ai/self-act-source-v1.json);
the [implementation receipt](../evidence/ai/self-act-implementation-v1.json)
records entry points, checks and validation. No original/C program was executed.

## Framework transaction — ZOD-238

`FacingDecisionContext.PrepareImmediateAbility` issues a typed invocation-bound
self-command proposal carrying `UseImmediateAbilityCommand`, immutable effect/
grant profile, progressed RNG and diagnostic. The context closes in the existing
finally path. Foreign/cached proposals, factories called after closure, wrong
actor and invalid RNG progress cannot commit.

The existing `FacingDecisionBattle.Execute` privately verifies forward RNG and
stages only the accepted decision stream into both snapshots. It then delegates
through `FacingTurnBattle.UseImmediateAbility` to the unchanged actual
`ImmediateAbilityBattleSimulator.Apply`. That simulator revalidates actor, Act,
known effect, explicit grants, zero-bonus PA1..99 and the supported living-unit
domain; it consumes Act and applies the shared capped PA+1 arithmetic.
Neither hypothetical units nor predicted PA enter the actual transition.

Actual resolution adds **zero draws**, including at PA99. Reaching draw-count
MAX during planning does not prevent a supported self ACT. Success increments
revision once, emits the existing turn/effect/change events and preserves all
other combat inputs, geometry, facing and ticks. At cap, resolution occurs with
no PA-changed event. Failed current command/profile/grant validation discards
staged RNG adoption, events and success diagnostics, returning the identical
incoming session. Existing Attack/targeted ACT/Wait/Move APIs remain supported.

With Move unused the actor stays active, CT is unchanged and no completion
receipt exists. With Move already used, the turn ends without another refund;
the receipt is minted after final PA publication and binds the complete battle,
revision and lineage. A replay branch cannot use it. Explicit FinishFacing adds
its own revision, preserves boosted PA and draws/refunds/ticks nothing. Revision
exhaustion or pending facing reject before the callback. No public session,
proposal or receipt constructors or production friend access were added.

## Fresh three-entry ACT-first execution — ZOD-239

`BasicSkillAiActFirstExecution.Execute` runs fresh supplied-scenario
`BasicSkillAiSelection` inside the Framework callback. Both choices must be
unused; initialized actor context, present compact records, canonical coordinates
and ordinary continuation remain explicit source assertions. Existing S/D0/D1
known-score and selection/default/team restrictions still apply.

Only a nonempty ACT-first selection is admitted. Attack and Throw Stone use
existing actual command branches; Accumulate uses the new self branch with the
fresh effect ID/profile. The selected plan is never accepted as public input.
Framework checks current authority and commands again. Move-first and empty
plans reject atomically; no planned path, retained continuation, automatic END,
post-ACT reselection or receipt is invented from predicted D1.

The literal source-order case starts at word1/count7. Its selection ends at
word3794901700/count26, chooses Accumulate and projects D1=(0,1). The actual ACT
remains at origin(1,1), publishes PA5→6, leaves CT15 and Move unused, and retains
that same RNG without extra draws. A separate PA99 case resolves at cap. Tests
then Wait, advance both turns and turn the opponent away from its high front
evade; a later real sword Attack deals12 from PA6 instead of10 from PA5.

Real damage branches continue to resolve actual hit/miss/damage/displacement,
rather than copying AI predictions. A lethal actual Attack publishes the Dead
result with unused Move, no completion receipt and no invented living enemy or
post-ACT move. Broader terminal routing remains unsupported. Failures in fresh
selection or late actual resolution restore the entire command transaction;
already published earlier commands are not rewound.

## Validation and remaining scope

The eight new Framework cases cover actual PA, zero draws/cap, full-result
receipts, grant/domain/authority rejection, expired contexts, counter guards and
replay. Eight new AI cases cover fresh actual self ACT, cap, real damage and KO,
later boosted damage, exact/late budgets, explicit dispatch guards and both
teams/actor identities. Existing pair and actual command regressions stay active.
Locked restore, a zero-warning/error full Release build, **4,185 .NET cases**
(1,467 Framework, 973 AI), **539 Python tests**, and pinned-source/prepared-data
audits pass. Seven full TRX files and logs are under ignored
`scratch/self-act-237-239/`. Owner drafts were included in validation and remain
excluded from this change. Unchanged historical checkpoint audits were not rerun.

Use the pinned SDK and Python/Pillow setup in
[Accumulate validation](accumulate-ai.md#validation):

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_algorithm_guide.py --reference C:/repositories/fft_decomp
python tools/verification/verify_ai_accumulate_contract.py --reference C:/repositories/fft_decomp --prepared scratch/all-data-extraction/SCUS_942.21
```

[ZOD-240–242](basic-skill-ai-command-execution.md) now reuse the existing tokens
for three-entry Move-first, empty END and unified fresh dispatch.
[ZOD-243–246](basic-skill-ai-post-act.md) add post-ACT preparation, derived explicit
reach branches and actual settlement. Pre-ACT scenario derivation and complete
three-entry turns remain separate. The current
effect-free Accumulate entry retains ACT-first on its complete tie; adding a
frozen self-Move token needs a concrete supported consumer. Reuse existing
Attack/targeted ACT/Wait continuations for Move-first adapters. No host/editor activation or battle-save
feature is selected. ZOD-106 stays deferred and PA remains battle-only. Original
frame RNG and broader retail-equivalence claims remain unresolved.
