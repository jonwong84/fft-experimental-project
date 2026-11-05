# Ordinary roster movement and sword commands — ZOD-278–282

Implemented 2026-11-04 from managed base `ac83f9a978e52fce124fcf68d29a96799f5634f2`,
following [ZOD-277](../architecture/zod-277-roster-combat.md). Read-only
fft_decomp was checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/combat/roster-combat-source-v1.json) pins
28 functions across nine files and inherits the earlier movement/sword studies.
The [implementation receipt](../evidence/combat/roster-combat-implementation-v1.json)
records managed validation. No original or C program was executed.

## Owned profile — ZOD-278

`FlatRosterBattleProfile` owns immutable unit IDs, stable semantic teams and
source-order keys. It validates the complete battle membership and source order
in TurnCycle TieKey, neutral/effect-free ordinary units, explicit cost-one ground,
byte Move/Jump and open flat lower-layer geometry at most16 by16. StableTeam
certifies both initial and current team, excluding Charm/Invite or other team changes.
The profile certifies absent mounts/reactions/importance/immortality, charges,
traps/pickups and other unrepresented effects. No ID implies those facts.

Storage is not capped at original table sizes. Original-compatible battle
admission requires at most21 units; movement requires at most17 living units,
because the source stores only16 **other** movement records. Larger movement
queries reject rather than silently replacing native truncation with all-body
blocking. The separate16 compact AI-record limit is not a rule of actual sword
resolution. Corpse result snapshots are representable, but movement with any
corpse remains unsupported pending overlap/presence lifecycle rules.

`FlatSwordBattleProfile` retains its two-unit guard. Both profiles share strict
ground checking without constructing fake pair certificates or snapshots.

## Roster range and paths — ZOD-279

`FlatRosterMovement.Evaluate` uses an extracted `FlatMovementKernel`, also used
by the original pair query. Ordinary stable enemies block traversal; allies can
be crossed at cost1 but cannot be endpoints. Residual ranges include ally transit
tiles, while physical destinations and AI origins omit occupied tiles. The actor
origin is added; after Move only that AI origin remains. Optional focus rows
describe source reverse propagation with all unit records cleared. No supplied
focus means no distance-row writes; unknown focus rejects explicitly.

The shared source backtrace preserves East initial predecessor, carried previous
direction on equal distance and E/N/W/S fallback. Paths contain one byte per
adjacent step. Literal examples include:

- Corridor actor(0,0), ally(1,0), enemy(4,0), Move3: path to(3,0) is
  (0,0)→(1,0)→(2,0)→(3,0), bytes[0,0,0]. Ally residual is3; the ally cannot be
  the endpoint. Focus rows toward the enemy remain[4,3,2,1,0].
- Actor(0,1), enemy(1,1), ally(0,2), Move4 on3 by3: path to(2,1) is
  (0,1)→(0,2)→(1,2)→(2,2)→(2,1), bytes[192,0,0,128], final heading South.
- A corridor enemy still disconnects the graph at Jump255. No new jump shortcut
  through an equal-height enemy is inferred.

Queries consume no choices/RNG and do not rotate live facing. Old pair outputs
and source streams remain unchanged, including moved/unmoved and cap boundaries.

## Confirmed roster walk — ZOD-280

`FlatRosterPathMovement.Move` stages TurnCycle choice validation and the current
path query before publishing anything. `ConfirmedPathMovement` now owns actual
position, final heading, movement/clock events and choice/CT settlement for both
the roster entry and existing pair facing-path entry. Rejections retain the
identical state, empty events and no path. Ally transit never publishes an
intermediate overlapping battle snapshot. Bystanders retain their state, and
walking consumes no RNG or next-turn ticks.

## Actual sword and occupancy — ZOD-281

`AttackBattleSimulator.AttackRoster` admits the new profile and enters the same
private resolution owner as the old edge-only and two-unit sword APIs. No public
resolver callback or new formula implementation is introduced. Actual selected
targets may be allies; bystanders are not struck or independently simulated.
Critical landing checks every occupied tile, including corpses. Lethal damage
cancels the push; an occupied landing changes neither damage nor draw counts.

At word57/count0, PA5/power5 and evade0, the actual critical consumes seven
draws, deals45 and ends word3448561432/count7. A target at(2,1) survives atHP5:
it moves to(3,1) if empty and stays if an ally/enemy/corpse occupies that tile.
At targetHP45 it dies at the original tile. Word1 normal hit consumes six draws,
deals25 and ends word2516284547/count6; an evade100 miss consumes four and ends
word3295386429/count4. Other units add no draws. Exact and one-draw-short budgets
cover all branches with whole-command rollback.

## Roster facing sessions — ZOD-282

`RosterFacingTurnBattle.Begin` issues a separately typed opaque session from
fresh admitted AddCt state. Advance, selected-path Move and actual Attack publish
complete results with revision/lineage. Existing pair sessions remain unchanged.
Shared `FacingReceiptRules` enforce exact pending receipt, battle, lineage,
revision, actor and completed-turn binding for both owners.

Move→ACT and ACT→Move mint receipts only after actual HP/KO/displacement or
path-facing settlement. `FinishFacing` accepts an explicit cardinal direction
or null to keep the actual heading. Automatic roster enemy-facing policy is
not inferred. Invalid/foreign/wrong-actor/stale receipts and late revision failures
retain accepted state/RNG/events; they do not repeat the preceding command.
Wait settles explicit/kept facing with the usual unused-choice CT refund and cap.

Literal three-unit flows ready at word57/count7 and actorCT15 complete Move+ACT
at word3448561432/count14 and CT15 with no added ticks. ACT→Move west retains
West when facing is kept; Move→ACT can explicitly settle South. An ACT-first
lethal hit leaves Move unused; attempted corpse movement rejects, then Wait can
END with CT35 and the same RNG. A fully unused END leaves CT55. KO scheduling,
next-turn progress with corpses and terminal encounter routing remain unsupported.
There is no new AI planner callback, public cached executable plan or frozen
roster Move→ACT token; callers submit each actual command explicitly.

## Validation and next work

Eleven authored source-only descriptions cover ally transit, occupied endpoints,
enemy detours, moved geometry and actual sword occupancy/draw branches. The offline
checker independently validates them and fingerprints the source; managed tests
consume the same literals. Sixteen new Framework cases also cover immutable
ownership, source extents, invalid facts, optional focus rows, pair parity,
friendly fire, bystanders, exact/late budgets, command orders, receipts, actual
KO progress, explicit facing and replay.

Locked restore and Release build pass with zero warnings/errors;
**4,325 .NET cases** (1,483 Framework,1,097 AI) and **548 Python tests** pass.
Pinned source/prepared-data, guide, local-link and coverage checks pass. Seven
full TRX reports and other logs are under ignored `scratch/roster-combat-277-282/`.
Python uses the [existing pinned Pillow setup](accumulate-ai.md#validation).
Historical checkpoint snapshots and UI were not rerun. All nine owner drafts,
including AbilityToggles, remain unchanged; no project/lock/package/dependency
changes or host/editor activation.

These actual-command prerequisites do not broaden the existing two-opponent AI
queries/turn drivers. The [ZOD-283–286 follow-up](roster-ai-preparation.md) now
adds separate pure roster refresh, list/team preparation and isolated movement
scoring; policy/scenario/action search remains unimplemented. Full AI
ability/target traversal/scoring, corpse movement/death scheduling, statuses/maps/
other policies, automatic facing, terminal routing and frame RNG remain open.
Owner drafts/editor work, broad replacement interfaces and ZOD-106 stay deferred.
