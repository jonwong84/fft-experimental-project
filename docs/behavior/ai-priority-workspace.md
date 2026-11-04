# Retained AI priority knowledge — ZOD-256–260

Implemented 2026-11-04 from managed base `00d0fef`, following the
[ZOD-255 study](../architecture/zod-255-priority-workspace.md).
Read-only fft_decomp was checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/priority-workspace-source-v1.json) pins the
lifecycle and consumer; the
[implementation receipt](../evidence/ai/priority-workspace-implementation-v1.json)
records managed behavior and validation. No original/C execution or UI review.

## Knowledge and owned scenario writes — ZOD-256

`AiPriorityWorkspace.StartUnknown(map)` creates an immutable knowledge object
for that exact immutable map instance. No priority value is implied. Only
owning AI queries can write numeric cells; constructors and write methods are
private/internal. The public immutable dictionary is diagnostic and cannot be
imported back as trusted writes. No proximity, candidate, rank or plan is cached.

`BasicSkillAiScenarios.EvaluateWithWorkspace` owns its usual preparation and
movement scoring, then returns the old query result with successor workspace.
Accepted writes are unchecked signed16 negated absolute scores, only at S.
Other known cells remain unchanged. Rejection returns the original workspace
instance and query RNG with no partial diagnostics. Each branch owns its
successor explicitly; there is no mutable global store or implicit live commit.

The caller threads this compatibility state with battle state and RNG, starts
unknown for each battle/map and calls `Forget()` after event/status staging,
loader use or any other unmodeled workspace writer. Forgetting discards all
knowledge while leaving the input untouched; it does not claim that the source
cleared memory. A different map instance is an argument error. Source priority
can survive a change of actor, target or stats: values are indexed by tile,
whereas proximity is always recomputed from current input.

The object does not detect omitted external writes or bind itself to a live
session lineage. It is explicit AI state supplied by its owner, not Framework
authority. The managed host has not been activated to maintain this state.
Event field-by-field replay, byte aliases and loader history remain unmodeled;
those boundaries must forget knowledge. General maps/rosters remain restricted
by existing admission, not by a new fixed-size priority array.

## Resolved cast keys and composition — ZOD-257

`BasicSkillAiDerivedComposition.EvaluateWithWorkspace` derives one fresh
scenario, then resolves every range4 Stone origin in final scenario1. A current
write wins over history; otherwise a known retained signed priority is required.
Unknown readable cells reject at Provenance. Unknown unread cells need no value.
Returned `AiCastOriginPriority` diagnostics distinguish Fresh/Retained and use
fresh byte proximity. The old scenario cell's `WrittenPriority` still means a
write in this decision, not a historical value.

The existing private composition/Stone scan owner accepts resolved keys only
from closed owning calls. Public supplied scans retain fresh-only admission.
Signed priority×65536 plus proximity orders origins, with later coordinates on
a full tie. Selecting the actor sets ACT-first and substitutes fresh D1; other
origins are Move-first. The sole evaluated aim is consumed even after
NotValuable, so the scan never retries another origin. Attack and self keep
their existing source order, actual-state candidates, clocks and ranks. No actor
rescore or extra refresh/draw is added.

Stateless `Evaluate` starts with unknown knowledge and returns its original
result shape. It retains the same unknown-key rejection and previous streams.
The workspace variant admits range3/4 only when prior owned writes resolve the
otherwise unwritten actor. It does not accept arbitrary numeric initial memory.

## Terminal selection — ZOD-258

`BasicSkillAiDerivedSelection.EvaluateWithWorkspace` feeds the same resolved
composition into the existing terminal lottery. Its wrapper returns the query,
successor workspace and resolved cast diagnostics. Terminal/default/NoDefend
assertions remain explicit. A late lottery failure returns incoming RNG and
the exact original workspace, hiding every speculative write and cast key.
The existing stateless selector remains a thin unknown-start entry to this core.

## Actual first command — ZOD-259

`BasicSkillAiCommandExecution.ExecuteWithWorkspace` performs selection inside
the same closed Framework callback as existing execution, using current actual
state/RNG and the supplied knowledge. It reuses all context guards, typed
Move/ACT/END proposals, RNG adoption and real effects. Only an accepted actual
command publishes successor knowledge. A late actual-resolution rejection
returns the input workspace along with the existing atomic command failure.
There is no public selector callback, cached executable plan, new friend access
or session/receipt constructor. The wrapper exposes the unchanged command result.

## Complete turns and recovery — ZOD-260

`BasicSkillAiTurnExecution.ExecuteWithWorkspace` uses the existing derived turn
configuration and shared stage machine. The successful first command commits
its priority writes. An accepted post-ACT movement decision adds its own fresh
writes from actual HP/PA and movement state. Retained ACT and facing settlement
perform no new AI scoring and add no priority writes. They never recompute a
priority merely because actual damage changed HP.

The wrapper returns the existing turn result and current workspace, including
partial progress on failure. The opaque recovery cursor freezes that workspace
with the exact partial session, input mode and failed stage.
`ResumeWithWorkspace` retries only that stage, retaining prior actual effects,
writes and RNG and returning only new events/decisions. A different session is
RecoveryBinding; a cursor from a stateless entry cannot manufacture a workspace.
No whole-turn rollback, additional clock advance, terminal policy or persisted
save format is introduced. Existing supplied/stateless APIs remain supported.

## Independent vectors and validation

Tests produce prior values through real managed fresh queries, rather than
injecting numbers. Prior HP50 versus40 yields a movement score+26 and stored
priority-26; reversing that HP difference gives+26, equal HP gives0. These are
source-traced managed scenario witnesses, not observed retail memory histories.

For current actor(1,1), target(4,1), Move1, PA5, CT15, zero evade and word1/count7:

- Current S contains only(2,1), freshly stored priority0. The actor remains
  unwritten with fresh proximity7; the scored tile's proximity is8.
- Retained actor-26 or0 selects actor/ACT-first; +26 selects(2,1)/Move-first.
  Both query schedules end word267834847/count17. No actor score was inserted.
- With both proximities0 and priority0, later x=2 wins the exact key tie.
- The first ACT ends word3791773014/count24 and targetHP40. Subsequent post-ACT
  scoring writes-26 at(2,1), then the complete ACT-first turn ends
  word3008955749/count35, CT15 with two fresh decisions.
- Move-first retains one real Stone and finishes word3791773014/count24, CT15,
  HP40 with one fresh decision. Its stored(2,1) priority remains0 until a later
  actual AI search writes it, despite changed target HP.

Eighteen new AI cases cover owned writes/unknowns, retained sign/proximity/ties,
NotValuable aim consumption, overwrite/nonwrite preservation, forgetting/map
binding, whole-query and late-command rollback, exact budgets, actual effects,
all recovery stages, terminal boundaries, old API parity and both teams/actors.
Locked restore and full Release build pass with zero warnings/errors;
**4,260 .NET cases** (1,467 Framework, 1,048 AI), **539 Python tests**, pinned
source/prepared-data, guide and coverage checks pass. Logs and seven full TRX
reports are under ignored `scratch/priority-workspace-255-260/`. Follow the
[existing Python/Pillow setup](accumulate-ai.md#validation). Historical checkpoint
snapshots were not rerun; no original behavioral equivalence claim is added.

Next ZOD-261 studies content-derived usable lists for the supported actions.
Arbitrary event/loader memory remains unknown. Full policies/rosters/maps,
terminal encounter routing, frame RNG and host/editor activation remain
separate. ZOD-106 and owner definition drafts, including AbilityToggles, remain
deferred and unchanged.
