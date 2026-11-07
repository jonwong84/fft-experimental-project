# Fresh supported roster list decision — ZOD-303

Implemented 2026-11-05 from `87fc753`, following
[ZOD-302](../architecture/zod-302-roster-list-selection.md). The read-only
reference remains clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Source receipt](../evidence/ai/roster-list-source-v1.json): 73 functions/26 files.
No original or C program was executed.

`RosterBasicSkillAiSelection.Evaluate` owns one fresh general prefix, structural
Attack/self/Stone/Frog traversal, an initially empty eight-slot ranked list and
one final source lottery. Repeated IDs retain indexes and distinct entry RNG.
Only entered actions require their real effect/grant/metadata/policy bindings;
unusable entries record equal entry/exit RNG. Post-ACT still performs the owned
preparation, then skips all effect work and runs the empty-ranking lottery.

The isolated Attack, self and Stone owners now expose private fresh continuations
inside AI. Their public APIs retain fresh-prefix ownership and transactional
failure behavior. No caller can supply a cached prefix/rank/result as trusted
executable input. Shared entry continuations preserve exhaustive Attack, each
self occurrence's real allied coin pass, computed Stone blockers, independent
consumed aims, source-order clocks, peer/PA restoration and current key provenance.
The full-list owner never invokes independently refreshed public entry queries.

One best per entered occurrence attempts shared `AiRankedInsertion`: strictly
above D0, then rank/hit/lower proximity, stable complete ties, eight slots. It
does not deduplicate actions or retain a hypothetical PA gain. `AiRankedLottery`
draws once per visited slot, including empty slots, selecting the first passing
slot, slot0 after all failures with any nonzero hit, or D0 Move-only under the
explicit no-Defend profile. Plan retains ChooseAtWait. Selected occurrence,
target, ability, order and destination are diagnostics, not live command authority.

Success publishes only fresh movement writes after the lottery. Missing entered
profiles, actual entry/key/clock failures and lottery exhaustion discard every
earlier entry/rank/plan and return the original workspace/RNG. Existing pair
commands, receipt/session boundaries and profiles retain their guards.

Twelve independent [source tapes](../fixtures/behavior/roster-list-contract-v1.json)
cover Attack-only, reordered self/Stone, duplicate passes, ten Stone occurrences
competing for eight stable slots, zero/partial-hit lotteries, post-ACT empty
ranking, late unknown priorities, PA99 coins and multiple-enemy restoration.
Four managed cases check every entry boundary/best/insertion/rank/roll/selection,
entered-only bindings, late lottery rollback, storage reorder/replay and map identity.
Three Python cases verify those tapes independently of managed execution.

Validation: locked restore, zero-warning/error Release build, **4,396 .NET cases**
(1,168 AI, 1,483 Framework), **575 Python tests**, source/prepared SDK/guide,
coverage, links and all nine owner drafts unchanged. Logs:
`scratch/roster-list-302-303/`;
[implementation receipt](../evidence/ai/roster-list-implementation-v1.json).

ZOD-304 next traces authoritative roster command/turn integration and chooses
bounded missing prerequisites. The pure decision does not yet run a roster AI
turn. Other abilities/policies/statuses/maps, complete corpse lifecycle, facing,
terminal/frame RNG and documented host/editor/owner-draft/interface/ZOD-106
deferrals remain separate. This is a supported list milestone, not full AI parity.
