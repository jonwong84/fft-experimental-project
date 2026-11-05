# ZOD-310: verified roster decision and retained Move authority

Reference clean/read-only `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed baseline `8d62bf4`. The source receipt reuses the original command-build,
simulation/restore, selection and actual-effect trace, and records review paths
for existing Framework decision, RNG, receipt and pair continuation owners.
Source dispatch vectors are distinguished from managed authority contracts.

## Source dispatch and retained decisions

`ai_command_build` refreshes/reselects when original choices change, then ends
hypothetical simulation before dispatch. With Move unused, emit Move to a
different tile when ACT was already used or the selected action is Move-first.
Otherwise emit ACT if unused and skillset nonzero, else END. A Move-first action
retains the frozen command for the next invocation; actual Move does not run a
second policy. ACT-first is followed by fresh post-ACT preparation/selection from
actual damage, PA, pushes and RNG. Do not reuse a hypothetical candidate snapshot.

The source's selected/decided and cached choice fields explain retained intent;
they do not authorize externally supplied managed plans. The existing pair
boundary already translates this into invocation-bound proposal factories and
opaque exact-session continuations. Keep its behavior/guards unchanged.

## Managed authority and arithmetic

One Framework entry owns a read-only callback context and opaque proposal.
Only a factory on that open context can issue a proposal; context closes in a
finally block. Reject null/foreign/cached proposals and wrong actors before
publication. Sharing the proposal's immutable input values does not make an
older proposal current. Diagnostics carry no authority. Callback exceptions
publish no returned new session; immutable input remains available.

Reuse `RandomStateGenerator.IsForwardCheckpoint`: unsigned count must not decrease,
and the full32-bit word must equal the BIOS LCG advanced by the reported delta.
Its affine exponentiation uses UInt32 wrap and at most64 bits of work, not one
iteration per draw. This establishes continuity, not proof that a policy needed
every reported draw. A same-count different word is invalid; a large forward
checkpoint is valid when its word matches. No frame RNG equivalence is claimed.

Only after validation stage both RNG copies privately, preserving session profile,
lineage and revision. Delegate actual Attack/self/Stone/Wait to existing roster
commands. Failed command, RNG exhaustion or late receipt settlement returns the
exact original session, empty events/path, no diagnostic/token and no adopted RNG.
For post-ACT turn-ending Move, confirm the actual path, then settle its actual
receipt with explicit fixed/kept facing atomically. A failure here retains the
earlier published ACT, not an earlier pre-ACT state.

## Move-first authority and recovery

Planned Move requires both choices unused and a frozen structural follow-up.
Attack retains its existing structural target checks; actual weapon/range is
revalidated at follow-up. Self/Stone require real typed grants/setup before Move;
Stone must not require pre-Move range or ray clearance, which can legitimately
change from the destination. Reuse a pure setup validator rather than executing
an ACT copy. All actual admission is rechecked after Move.

Publish a token only with an accepted real path/heading and moved session. Bind
exact returned session/battle reference, actor, revision and lineage, plus the
immutable original command/profile/metadata. Continue accepts only state/actor/
token, never replacement commands, RNG or a new planner. Wrong actor, foreign,
reconstructed or newer state rejects. Same original state/token replay is allowed;
there is no mutable consumed registry or save format. A failed retained command
keeps the accepted Move and can be retried on that exact state. Explicit Wait
can abandon it on its returned newer branch.

AI first/post-ACT dispatch owns fresh full-list selection inside this callback.
Match simulation roster facts to the authoritative session, and publish its
successor priority workspace only with accepted command progress. Retained ACT
performs no second query/adoption. A turn driver routes continuation first,
pending actual facing second, active actor to fresh post-ACT selection third,
otherwise finishes without advancing the next clock. Late failure retains events,
decisions, workspace and exact current session in an opaque recovery cursor.
Resume requires that exact session and freezes the original configuration.

## Selected children

- **ZOD-311:** verified roster callback transaction for actual ACT/Wait and
  post-ACT turn-ending Move, reusing current command/receipt/RNG owners.
- **ZOD-312:** planned roster Move with invocation-bound immutable follow-up and
  exact-result token publication; keep pure setup checks separate from range.
- **ZOD-313:** retained Attack/self/Stone/Wait continuation with exact-state binding,
  failure retention, replay and explicit cancellation.
- **ZOD-314:** fresh supported-list AI first/post-ACT dispatcher inside Framework's
  authority boundary, without cached-plan input or new production friend access.
- **ZOD-315:** bounded complete-turn routing and exact-session recovery using those
  accepted commands, continuations and explicit fixed/kept facing.

Initially living compact16/ordinary general-profile selection remains the query
domain. After an actual KO, fresh selection can still reject the deferred corpse
input; preserve that accepted KO/progress for recovery/manual END rather than
inventing terminal routing. Full corpse/terminal/frame RNG, automatic facing,
other abilities/policies/maps, host/editor, owner drafts, broad interfaces and
ZOD-106 stay separate. These children do not activate presentation or establish
full original encounter AI parity.
