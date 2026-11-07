# Verified roster decisions and retained commands — ZOD-311–313

Implemented 2026-11-05 from `8d62bf4`, following
[ZOD-310](../architecture/zod-310-roster-decision-authority.md). Reference remains
clean/read-only at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Source receipt](../evidence/ai/roster-authority-source-v1.json):83 functions/27 files.
The21 [contract vectors](../fixtures/behavior/roster-authority-contract-v1.json)
label original dispatch separately from managed RNG/turn-route contracts; no
original/C execution is claimed.

`RosterDecisionBattle.Execute` owns one synchronous callback and opaque proposal.
`RosterDecisionContext` exposes immutable actual battle/profile/actor/RNG and
concrete Attack/self/Stone/Wait/post-ACT Move factories. Context closes even when
the planner throws. Only the current invocation's proposal can be returned;
null, cached, foreign, wrong-actor or invalid-checkpoint proposals reject.
The shared BIOS affine verifier checks word and nondecreasing ulong count in
logarithmic work, including enormous valid deltas. It establishes continuity,
not whether a policy needed every draw.

Framework privately stages both RNG copies, delegates existing actual commands,
and returns the accepted result without an adoption revision or clock event.
Any command/late settlement failure returns the exact original session, empty
events/path/token and default diagnostic. A post-ACT path and actual receipt-bound
explicit facing commit together; late failure keeps the previously published ACT.
Internal closures freeze concrete immutable commands/profiles; callers cannot
supply a custom command delegate or construct proposal/continuation authority.
No Framework-to-AI reference or production friend access is added.

Planned Move requires both choices unused. Its factories freeze Attack/self/Stone/
Wait and validate structural intent; pure self/Stone setup validators share actual
admission, without running an ACT copy. Stone's original range/ray may be invalid
before Move; they are checked at the actual post-Move execution. Attack retains
structural living-target admission, with actual weapon/range rechecked later.
Failed path publication discards RNG and issues no token. Accepted Move publishes
the real path/heading, one revision and an opaque exact-result continuation.

`RosterMoveDecisionBattle.Continue` accepts only state/actor/token. It requires
the exact session/battle reference, revision, lineage, actor and moved/unused-ACT
choices. There is no replacement payload, new planner or second RNG adoption.
Frozen ACT returns a real completion receipt; frozen Wait settles explicit facing
and refunds unused ACT through the existing owner. Late failure retains the
accepted Move and permits deterministic retry or explicit Wait abandonment on a
newer branch. Same original state/token replay is allowed; receipts cannot cross
replays. No mutable consumed-token registry or save/checkpoint format is added.

Sixteen new Framework cases cover seven exact/large/wrong checkpoint contracts,
closed/throwing/cached/null planners, actual rule parity, wrong actors, late ACT
draw exhaustion, atomic post-ACT facing/revision failure, real Move paths, typed
setup validation, out-of-range pre-Move Stone, four frozen follow-up kinds,
stale/foreign/reconstructed/newer tokens, cancellation/retry, source replay and
post-Move interception. Existing pair and actual effect streams remain unchanged.
Three Python cases check the21 contracts independently of managed execution.

Locked restore, zero-warning/error Release build, **4,429 .NET cases** (1,516
Framework, 1,168 AI), **584 Python tests**, pinned source/guide/coverage/link and
nine unchanged owner-draft checks pass. Logs: `scratch/roster-authority-310-311/`;
[implementation receipt](../evidence/ai/roster-authority-implementation-v1.json).

[ZOD-314/315](roster-ai-command-and-turn.md) now own fresh full-list AI dispatch
inside this boundary and bounded complete turns/recovery. Public cached AI plans
still have no live executor. Explicit fixed/kept facing, ordinary initial living domains and all
documented automatic-facing/corpse/terminal/frame RNG/host/editor/owner-draft/
interface/ZOD-106 deferrals remain.
