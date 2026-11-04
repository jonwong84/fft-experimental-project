# Complete supplied-scenario three-entry turns — ZOD-248

Implemented 2026-11-04 from managed base `68b108a`, following the
[ZOD-247 study](../architecture/zod-247-three-entry-turn.md). Read-only fft_decomp
was freshly checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/three-entry-turn-source-v1.json) pins 15
functions across seven files; the
[implementation receipt](../evidence/ai/three-entry-turn-implementation-v1.json)
records validation. No original/C program or UI was executed.

## Automatic post-ACT reach

`BasicSkillAiPostActSelection.Evaluate` extends the existing private post-ACT
core with a derived-reach mode. It owns one actual-state preparation, reads
target distance through the existing pure geometry and compares it with the
prepared enemy reach before any later NonBlue halving draw. It then continues
exactly one existing movement/empty-lottery branch from the progressed refresh
checkpoint. It does not call the public explicit entry and repeat preparation.
`BasicSkillAiPostActExecution.Execute` owns this automatic query inside the same
Framework callback as the previous explicit in/out-of-reach executors.

Existing explicit APIs, types, admission, diagnostics and streams remain
unchanged. Tests compare automatic and explicit outputs in both reach branches
and both teams, including literal checkpoints that would expose an extra refresh.
An actual Stone push at Move0 changes distance1→2 with prepared reach1 and routes
out-of-reach. Target distance equal to reach remains in-reach; the complete
ACT-first Stone witness pushes to exactly that boundary.

## Complete turn

`BasicSkillAiTurnExecution.Execute` takes the current session, request, supplied
pre-ACT scenario, `BasicSkillAiTurnConfiguration` and dispatch assertions. The
configuration shares the pre-ACT preparation owner with post-ACT selection and
adds the existing post-ACT scenario assertions. General-entry, fresh-ranking and
NoDefend facts feed the existing post-ACT configuration. There is no second copy
of grants/content or public cached-plan input.

The driver composes existing authoritative APIs:

- FirstCommand calls the existing fresh `BasicSkillAiCommandExecution` once.
- A returned Move-first token enters RetainedCommand, calling Framework's frozen
  Attack/Stone/Wait continuation without another selection or RNG adoption.
- An ACT-first result enters PostActCommand, making one automatically routed
  fresh movement decision from actual PA, HP/status, displacement and RNG.
- A real pending completion receipt enters FinishFacing. Normal post-ACT Move
  and Wait already settle facing atomically through their current owner.

Each accepted step consumes a choice, ends the turn or settles the actual
receipt. Once the actor is inactive with no pending continuation/facing, the
driver returns Completed. It adds no next-turn clock, presentation pause, extra
ACT, self-Move token, refund or RNG draw. Events retain publication order, and
successful fresh decisions are typed BeforeAct/AfterAct diagnostics. Actual PA6
or capped PA99 survives the complete flow; projected pre-ACT D1 grants no Move.

## Failure and recovery

A rejection stops immediately, retaining every earlier accepted command and
only its published events/diagnostics. Failure identifies FirstCommand,
RetainedCommand, PostActCommand or FinishFacing, with the underlying typed reason.
Failed private selection/command work contributes no partial diagnostic or RNG
adoption. There is no speculative retry or whole-turn rollback.

`BasicSkillAiTurnRecovery` is an opaque in-memory cursor containing frozen
inputs, exact returned partial session, next stage and any existing retained
Framework token. `Resume(state, recovery)` requires that exact state instance;
equivalent replays or foreign sessions return RecoveryBinding with no effects or
new cursor. The immutable session already carries its lineage/revision/battle
and any pending receipt; the cursor creates no new Framework authority.

Resume invokes only the failed stage and returns only newly published events
and decisions. It does not repeat earlier Move, damage, PA boost or selection.
Unchanged exhausted budgets or terminal conditions remain deterministic failures.
Changing inputs requires a newly admitted flow rather than editing the cursor.
This cursor is not a persisted save/checkpoint representation.

A late post-ACT Move/facing failure keeps the prior actual self ACT and its
checkpoint, while the existing Framework transaction rolls back that remaining
command. A late retained-ACT facing failure keeps the completed actual Move/ACT
and exact pending receipt, retrying only FinishFacing. Lethal ACT-first remains
at the unsupported terminal post-ACT boundary; lethal Move-first remains at
NoLivingEnemy facing with its receipt. The driver invents no living target,
final heading or encounter outcome.

## Literal complete streams and validation

Standard vectors begin at word1/count7, origin(1,1), PA5, CT15 and Speed5/5.
The source-supplied pre-ACT scenarios retain their original provenance:

- Self ACT-first finishes at word995405759/count49, PA6, CT15, after two fresh
  decisions. Post-ACT derives destination(1,4) from the actual boosted state.
- Attack ACT-first finishes at word2058258651/count53, target HP40 and CT15,
  also with two fresh decisions.
- Stone ACT-first uses only origin in the supplied scenario, deals5 and pushes
  target(4,1)→(5,1), then finishes at word995405759/count49, HP45 and CT15.
- Move-first Stone finishes at word3791773014/count24, target HP40 and CT15;
  Move-first Attack at word1343201072/count30, target HP40 and CT15. Each owns
  one selection and retains its real follow-up.
- Empty different-tile Move/Wait and same-tile END both finish at
  word836760821/count19, respectively CT35 and CT55.
- A PA99/Move0 self case begins at word5/count7 and finishes at
  word736722860/count22, PA99 and CT35. It resolves once without another PA gain.

Ten new AI cases cover those families, automatic/explicit reach equivalence,
actual push routing, manual command composition/event order, exact full-turn
budgets, first/retained/post-ACT/facing failures, exact cursor binding, terminal
boundaries and both teams/identities/collection-policy orders. Existing pair and
all prior three-entry tests stay active. Locked restore and full Release build
pass with zero warnings/errors; **4,221 .NET cases** (1,467 Framework, 1,009 AI),
**539 Python tests**, pinned-source/prepared-data, algorithm-guide and coverage
checks pass. Logs and seven full TRX reports are under ignored
`scratch/three-entry-turn-247-248/`. Unchanged historical checkpoint audits were
not rerun. No host/editor or original-program review was performed.

Reproduce with the pinned SDK and [Python/Pillow setup](accumulate-ai.md#validation):

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_algorithm_guide.py --reference C:/repositories/fft_decomp
python tools/verification/verify_ai_accumulate_contract.py --reference C:/repositories/fft_decomp --prepared scratch/all-data-extraction/SCUS_942.21
```

The next ZOD-249 study traces pre-ACT scenario derivation and retained score-cell
provenance before selecting another bounded implementation. Full original
lists/rosters/maps/policies, terminal encounter routing, replacement contracts,
host/editor activation and original frame/VSync RNG remain separate. ZOD-106
stays deferred; PA remains battle-only. Owner definition drafts, including
AbilityToggles, remain unchanged.
