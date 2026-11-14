# Pure ordinary command dispatch — ZOD-166

Reference: read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed base `c882977018ee685c3303a6f6ced4073b788a026a`. The
[source contract](../architecture/zod-165-ai-command-continuation.md) and its
[receipt](../evidence/ai/command-continuation-contract-v1.json) record command
ordering, invalidation, controller callers and the exact decided-byte distinction.

`SwordAiCommandDispatch` exposes two pure entry points over immutable battle data:
InspectEntry checks the original pre-selection guard; DispatchFreshSelection
classifies a newly completed selection inside that invocation, skipping the earlier
guard. False-default profiles certify initialized same-actor context, present
compact records, canonical coordinates and ordinary effect-free continuation.
The fresh entry additionally requires FreshSelectionCompleted. Saved choices have
an explicit actor ID; unknown/inactive actor, mismatched cache owner, unsupported
geometry/context or decision marker rejects without a partial output.

Entry inspection returns no-progress EndTurn when decided and saved/current choice
flags match, before checking ACT-triggered reselection. Otherwise undecided or
ACT-used input returns SelectionRequired. These branches do not inspect stale
selected payloads or record a command. SelectionRequired is a descriptive stop
before refresh/search, not a new original command or executed selection.

Normal dispatch validates semantic Attack/MoveOnly data, matching actor/target,
destination/order and lower-layer unoccupied map coordinates. It returns Move
when movement is unused, the selected order permits it and the destination differs;
otherwise EndTurn if ACT is used or the selection is move-only, and Attack otherwise.
Outputs include only the relevant command data and current pre-execution choices.
RecordCommand describes the source's saved-choice/history update; no fixed history
array or unused END_TURN union payload is copied into the managed API.

The decision marker admits only Undecided=0 and Decided=0xff. Source byte1's
different branch behavior is recorded in the fixture and explicitly rejects here;
it is not coerced into an ordinary decided boolean. Other independent context and
choice flags use named booleans.

The query never scores, selects again, runs a path, changes a revision/receipt or
consumes RNG/ticks. It returns the snapshot's original RNG, including MAX counters.
Command descriptions do not certify path/Attack legality or selection provenance.
Caller-supplied data cannot authorize a live continuation; eventual execution must
revalidate through Framework's own command/session authority. Existing selectors,
prepared executors and Framework APIs remain unchanged.

Fifteen managed cases execute all ten source vectors (including the excluded
noncanonical marker), source guard/fresh-stage and ACT ordering, malformed payloads,
context/actor rejection, both-identity/order replay and MAX counter isolation.
One scenario uses actual confirmed-path movement: fresh dispatch describes Move,
the existing Framework path transition spends Move once, and entry inspection
describes the retained ACT without new selection/RNG work. Repeated unchanged
choices then describe no-progress EndTurn. Query calls preserve both sessions.

ZOD-167 designs the Framework-owned real Move and opaque continuation boundary
before adding live Move-first execution. Fresh unmoved preparation, post-ACT
reselection, broader policies, saveable tokens and host activation remain separate.

Validation: locked restore, zero-warning/error Release build and **3,906 .NET /
510 Python tests** pass. The pinned continuation audit checks nine files/25
functions and ten source branch vectors plus inherited receipts. Seven full TRX
files and build/Python/source logs are ignored under `scratch/ai-command-dispatch-166/`.
Owner content drafts were included in validation and excluded from this AI commit.
No C/original execution or UI review.
