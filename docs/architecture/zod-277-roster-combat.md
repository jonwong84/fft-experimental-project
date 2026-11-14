# Multi-unit ordinary movement and sword commands — ZOD-277

Source/design, 2026-11-04, managed base `ac83f9a978e52fce124fcf68d29a96799f5634f2`.
Read-only fft_decomp checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/combat/roster-combat-source-v1.json) extends existing
[movement](zod-120-flat-ai-movement-contract.md) and
[sword](zod-114-flat-sword-knockback.md) evidence. No original/C execution.

## Occupancy and source extents

`spread_unit_to_scratchpad_store` visits source slots in order, skipping the
mover and Crystal/Dead/Jump/Treasure. It records at most16 other units. It compares
the mover's initial team against each other's current team, unlike AI Enemy
refresh's current-actor/initial-target pairing. The selected domain certifies
stable teams, ordinary unmounted bodies, no restrictions or special flags.
Semantic team indices are explicit; neither IDs nor collection positions imply teams.

`spread_frontier_flags_set` leaves an ally's tile enterable while marking its
occupancy. An enemy clears ENTERABLE and MOUNTABLE; NORMAL is nonzero and cannot
stand on it. On flat equal-height ground, the enemy midpoint also blocks horizontal
jumping; an ally is ignored by that body check. Ally transit therefore costs one
ordinary step. `range_tile_flags_set` and `move_path_build_to_tile` reject occupied
endpoints unless mount/standing rules permit them; those rules are excluded here.
Every other unit excludes its tile as an endpoint, including a same-team ally.

Consequently a living ordinary roster uses a cardinal graph with enemies removed
for traversal, but all other occupied cells removed from destinations. Residual
ranges may include ally transit tiles. AI origins omit other occupied tiles and
add the actor's original tile; MovementTaken leaves only that origin. Reverse
target propagation clears all unit records, so a supplied focus still gives
Manhattan rows in this open cost1 domain, including through occupied cells.

The16-record truncation cannot be silently replaced with all-body blocking when
more than16 other living units exist. This implementation rejects such movement
queries rather than inventing source behavior. Source unit lookup scans21 slots;
the original-compatible battle profile admits at most21 represented units.
Immutable roster/profile storage has no global catalog limit. Original AI also
has16 compact records, a separate constraint not activated by these actual commands.

Dead units are omitted from movement records, but ordinary state currently
forbids overlapping bodies and lacks corpse landing/presence lifecycle. Movement
with any corpse remains explicitly unsupported. Do not treat corpses as living
blockers or automatically empty endpoints. This is separate from knockback below.

## Paths, actual publication and sword resolution

Flat physical distances and carried-backtrace direction ties remain the ZOD-120
kernel: source E/N/W/S spread, East initial predecessor, previous backtrace
direction preferred on a tie, one packed byte per adjacent step. Ally transit does
not authorize stopping midway. Real execution publishes the final step heading;
hypothetical placement still does not rotate facing. No movement RNG is drawn.
Controller `move_confirm` stores settled coordinates/facing before marking Move.
The managed command publishes path, position, heading and choices atomically.

Ordinary sword affects one selected adjacent target; other live combatants add
no formula draw or independent strike. Friendly fire is not prohibited by a
team comparison in this single-target rule. Existing neutral/effect-free stats,
weapon and signed16 product envelope remain mandatory. Accuracy, critical bonus,
empty status proc and damage/KO use the same owners and ordering as the pair.
Miss/normal/critical consume4/6/7 draws, respectively. No per-bystander draws.

`knockback_destination_check` calls `target_find_unit_at_tile`, whose no-status
filter visits every present unit, including a corpse. Any occupant prevents the
push without changing damage or RNG. Otherwise a cardinal critical attempts one
tile beyond the target. Lethal damage cancels displacement after all formula
draws. No Brave contest applies to this sword. Other units retain HP, positions,
facing, CT and choice flags. Target displacement preserves voluntary Move state.

Source marks ACT/possible turn completion before damage finalization; controller
continuation and Wait follow the actual result. A completion receipt must bind
the final HP/KO/displacement/path-facing snapshot, not the intermediate turn event.
Explicit fixed/kept final facing is supported here; roster-aware automatic Wait
facing remains a different nearest/imminent/viability policy. No next-clock,
terminal encounter dispatch or opaque AI planned continuation is added.

## Selected incremental children — ZOD-278–282

- **ZOD-278:** immutable `FlatRosterBattleProfile`, explicit stable teams/source
  order and complete state matching, shared strict ground admission. Preserve
  the old two-unit profile and reject unsupported movement/status/extent facts.
- **ZOD-279:** roster movement range, residuals, AI origins, optional focus rows
  and selected paths through an extracted shared flat kernel. Ally transit,
  occupied endpoints and enemy detours receive independent literal vectors.
- **ZOD-280:** actual roster path Move through shared confirmed-path settlement,
  TurnCycle validation and atomic publication. Preserve bystanders and RNG;
  existing pair path/facing/receipt owners consume the same settlement kernel.
- **ZOD-281:** explicit `AttackRoster` entry through the existing private sword
  resolution kernel and new profile admission. Validate occupied/corpse landings,
  friendly fire, lethal cancellation, exact/late budgets and pair parity.
- **ZOD-282:** opaque roster facing session with fresh Begin, actual command
  publication and exact completion receipts. Share receipt guards with the old
  facing owner; support fixed/kept facing, Move→ACT and ACT→Move/Wait flows.
  Cross-session/stale receipts and late failures preserve published progress.

These are authoritative prerequisites for later AI roster search, not a full
multi-unit AI driver. Keep old APIs/guards/certificates and their RNG streams;
do not build fake pair snapshots or replay effects separately for each target.
No new dependency, production friend access, host/editor activation or owner
definition changes. Full AI refresh/team/ability/scoring/target enumeration,
corpse movement/death scheduling, statuses/maps/other policies, automatic facing,
terminal routing, frame/VSync RNG, broad replacement interfaces and ZOD-106 stay
separate/deferred.

## Implemented follow-up

[ZOD-278–282](../behavior/flat-roster-combat.md) implement the selected authoritative
prerequisites. Eleven independent source descriptions, 16 new Framework cases
and three new Python cases cover literal rules, actual command flows and failure
isolation. Full validation passes with 4,325 .NET cases and 548 Python tests,
zero-warning/error Release build and pinned source/prepared/guide/coverage audits.
Old pair AI remains unchanged; no original/C execution or UI review claim.
