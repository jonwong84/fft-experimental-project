# Live roster Throw Stone and shared geometry — ZOD-307–309

Implemented 2026-11-05 from `2c4b2e0`, following
[ZOD-306](../architecture/zod-306-roster-live-stone.md). Reference checkout remains
read-only/clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/roster-live-stone-source-v1.json) pins83 functions
across27 files. No original or C execution was used.

`FlatRosterDirectTrajectory` in Framework now owns the exact SDK-normalized sampled
kernel and copied `FlatRosterProjectileHeights`. Between-units excludes actor and
intended target; ToTarget excludes only actor and resolves the source-selected
first hit, including the intended target's box. It validates the complete living
flat slot21 domain and explicit metadata. A supplied free origin is geometry only,
not permission to Move. The AI facade keeps its compact16 and initial legal-origin
checks and delegates to this owner, preserving old results. There is no
Framework-to-AI reference, duplicate sampling algorithm or production friend access.

`TargetedAbilityBattleSimulator.ApplyRoster` admits the real typed effect/actor
grant, exact complete ground/unit/height bindings, living ordinary PA1..99 input,
active unused ACT and intended range1..4. Live geometry can select an intervening
ally or another team. Formula facing, evade, Brave and push direction use that
actual unit. `ThrowStoneTargetInterceptedEvent` identifies the intended/resolved
IDs before the resolved event when they differ. Existing turn/result/HP/status/
KO/push ordering follows, and both RNG copies publish only after success.

The pair API retains exactly-two admission. Both entries share the same actual
formula/choice/result kernel: four real physical evades, then damage and two Brave
rolls on a hit. A miss uses4 draws; a hit uses7 even when KO, occupied or off-map
landing suppresses a push. Damage uses current PA; AI midpoint/score results never
become actual state. Push checks every occupant and preserves peer choices/CT/
facing. Unsupported setup or exhausted draws return exact input and empty events.

`RosterFacingTurnBattle.UseTargetedAbility` reuses the existing private publisher,
opaque lineage/revision and actual completion receipts. Both actual orders work;
foreign receipts cannot settle, pending facing blocks another command, and late
failure retains an earlier accepted Move. Explicit fixed/kept facing stays separate
from deferred automatic facing. Pure AI plans still grant no command authority.

Validation includes13 independently described [live tapes](../fixtures/behavior/roster-live-stone-contract-v1.json),
the14 previous exact geometry tapes,13 new Framework cases, and three Python cases.
Coverage includes interception/facing, real damage/Brave, KO byte wrap, occupied/
diagonal/off-map pushes, tiny sprites, PA99, exact/late draw exhaustion, pair parity,
runtime21 versus AI16, immutable metadata, actual receipts and deterministic replay.
Locked restore, zero-warning/error Release build, **4,413 .NET cases** (1,500
Framework, 1,168 AI), **581 Python tests**, source/prepared SDK/guide/coverage/link
and nine unchanged owner-draft checks pass. Logs: `scratch/roster-live-stone-306-309/`;
[implementation receipt](../evidence/ai/roster-live-stone-implementation-v1.json).

Next ZOD-310 traces verified roster planning/RNG adoption, retained Move authority,
first/post-ACT dispatch and exact-session recovery before their bounded children.
Full corpse/terminal/frame RNG, automatic facing, other abilities/terrain/statuses,
host/editor, owner drafts, broad interfaces and ZOD-106 remain separate/deferred.
