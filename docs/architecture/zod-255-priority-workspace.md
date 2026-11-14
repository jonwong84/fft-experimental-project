# Retained cast-priority lifecycle — ZOD-255

Studied 2026-11-04 from managed base `00d0fef`. Read-only fft_decomp was
freshly checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/priority-workspace-source-v1.json) extends
the [ZOD-249 trace](zod-249-three-entry-scenarios.md). No original/C execution.

## Lifetime, writes and reads

`ai_workspace_init` binds the global storage, initializes selected/ranked context,
unit lists and summaries. It does not clear tile priorities. Map initialization
and return from a battle event call it; neither call establishes a numerical
priority initial value. `action_select` resets ranks and refreshes foe proximity,
again without clearing priority. `scenarios_build` writes only filtered S via
`reachable_tiles_score`. The store is the negated absolute rank modulo16 bits.
The source iterates layer/y/x, with 18 rows and16 columns per terrain layer.
Suspension retains unfinished work and stores a cell only after its evaluation.

Priority is shared across actors, turns and repeated decisions, indexed by tile,
not unit or target. Its age does not make it invalid: the original can read a
score from a different actor/target. New scores replace only written cells.
The per-query foe proximity remains fresh and is never retained with that score.
`origin_next_take` reads priority as signed16, multiplies by65536, adds byte
proximity and prefers later layer/y/x on equality. `origin_destination_finalize`
substitutes D1 and sets ACT-first when the chosen origin is the actor. Ordinary
Stone consumes its one evaluated aim even when the candidate is NotValuable.

Event execution aliases the same storage: AI tile priorities occupy workspace
offset `[0x5f4,0xa74)`; event status staging occupies `[0x5f0,0x9e2)`.
Their overlap is1006 bytes, including partial words for individual byte writes.
Event entry clears staging state/mode; save/update/restore paths write further
status, timer, HP and control fields. Returning through `ai_workspace_init`
does not restore earlier scores. A loader, unknown writer or actual event history
is not reconstructed by these traces. A presumed startup zero or previous-score
default remains unjustified.

## Bounded representation and concrete consumer

Use an immutable, map-bound **knowledge** object. Start with all cells unknown;
only an owning accepted fresh scenario/search can add observed signed priority
writes. Keep cells outside S unchanged. The object contains no proximity, ranks,
candidate plans or public numeric seeding/diagnostic-import method. Represent
known cells semantically, without copying original storage capacity into game
content limits. A new map requires a new unknown object. Unmodeled event/status,
loader or other workspace use explicitly forgets all knowledge. Forgetting is
conservative loss of knowledge, not a claim that retail memory was cleared.

This is explicit compatibility state, like query RNG. The caller must thread
the correct branch and invalidate it at unmodeled boundaries. It grants no
Framework authority and cannot discover an omitted external write. Pure queries
publish successor knowledge only on whole-query acceptance. Live commands
publish it only with an accepted command; turn recovery preserves accepted
history alongside actual state and retries only the failed stage. These atomic
failure rules remain managed policy, not a claim of source rollback on suspension.

For the closed three-entry range3/4 consumer, merge current S writes with known
retained cells. Resolve every Stone cast key in final scenario1 before scanning;
unknown readable cells reject. Unread unknown cells remain harmless. The actor
outside S cannot be an Attack range1 origin in this out-of-reach branch. Self
evaluation still runs once. Do not add a clock/draw to rescore the actor.

## Selected children

- **ZOD-256:** immutable priority knowledge and owned scenario successor; map
  binding, conservative forgetting, fresh overwrite and whole-query rollback.
- **ZOD-257:** resolve fresh/retained cast keys and compose the three entries
  through the existing Stone scan/rank owner, preserving signed ordering and aim
  consumption. Keep the stateless API's unknown-key rejection.
- **ZOD-258:** terminal selection with successor knowledge, source lottery and
  late rollback through the same selection owner.
- **ZOD-259:** first actual command with transactional knowledge publication
  through the existing closed Framework callback and typed proposals.
- **ZOD-260:** complete workspace-aware turns and exact recovery; include
  accepted post-ACT movement writes, retained-command/facing no-writes and
  preserved progress on terminal failures.

Use independently checkable vectors with history produced by real managed fresh
queries, not injected priorities. Cover changed actor/target state, fresh
overwrite, actor-versus-moved origin, signed/tie ordering, unknown/invalidation,
literal RNG, exact/late budgets and input isolation. Full memory reconstruction,
general rosters/policies/maps, host/editor activation, replacement interfaces and
frame RNG remain separate. ZOD-106 and owner flag drafts stay deferred.
