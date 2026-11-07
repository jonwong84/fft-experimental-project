# ZOD-150 — Deferred wait-facing on settled ordinary positions

Source/design checkpoint, 2026-10-28. Read-only reference:
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[receipt](../evidence/ai/wait-facing-contract-v1.json) pins the caller, chooser,
direction/target helpers, layout/offset data and documented quirk. No production
facing resolver, C/original execution or native behavior changes here. ZOD-151
is the bounded pure-query implementation follow-up.

## Caller, timing and control

`battle_state_wait_direction_enter` starts from the source unit's current facing,
handles rider/mount alignment, then calls `ai_wait_facing_choose` only when
**UNIT_TEAM_FLAG_PLAYER_CONTROLLED (0x08) is clear**. This is a different condition
from the out-of-reach scenario's Blue/NonBlue team-mask test. Neither actor ID
nor original team class proves the Wait control flag. Mount behavior is excluded.

The chooser returns any selected hint other than ChooseAtWait (5) unchanged.
Otherwise it recopies **current settled coordinates** and searches. Existing
selected plans deliberately retain a semantic ChooseAtWait hint; their destination
is not an already-executed position and their projected enemy HP is not live HP.
Do not run this resolver on a candidate's predicted outcome or apply it during
selection. A future caller must supply an actual settled state at the Wait boundary.

`battle_state_wait_direction_update` applies hints 0..3 using a 0x400 quarter turn,
leaves >=4 un-applied, and owns its presentation frame counter/turn-ending work.
Those updates, visual pacing, turn_end, input and authoritative facing mutation
are outside the proposed pure query. Source AI ordinals South0/West1/North2/East3
differ from managed BattleFacing East/North/West/South; map names explicitly.

## Viability and original search

The source checks neighbors in South, West, North, East order using unsigned-byte
offsets (0,255),(255,0),(0,1),(1,0). Assignment wraps at map edges; x/y>=map bound
rejects. It tests the destination's lower layer, blocked flag and raw height
rise>=3; descents are unrestricted. It does **not** reject occupied tiles, so an
adjacent enemy does not make that facing invalid. Slope, water, body height and
ordinary movement destination eligibility are not this viability test.

When no direction is viable it enables all four. Otherwise it remembers the last
viable direction for a later fallback. It resets targetable flags, repeatedly
tries nearest imminent enemies, disables one whose direction is not viable, then
tries the nearest enemy of any kind. If that direction is not viable, it keeps
current facing when viable or returns the remembered last viable direction.
Nearest distance ties keep first original slot; facing viability ties are a
separate concern from movement/Attack ranking.

Imminence uses cached acting-unit remaining clockticks, current target delay,
status admission and an adjacent height check. Do not assert that cache was
refreshed from a projected candidate. On the bounded single-enemy profile below,
the final facing does not depend on whether the one enemy is imminent: either
search returns that same living eligible enemy, whose direction is viable.

## Exact two-opponent reduction and diagonal ties

Require two distinct living positive-Speed ordinary units, None/Critical status,
one clear dry flat lower-layer rectangle, dimensions<=16 and the existing stable
opponent/movement certificate. Require active actor/UNIT_TURN at the settled Wait
boundary; allow either movement/action choice already used. Do not reuse the
candidate/baseline validator, which rejects an already-used ACT.

New false-default context facts must certify **OriginalAiChooseAtWaitContext**
(actual AI-controlled Wait boundary and selected ChooseAtWait hint) and
**EligibleOpponent** (the other unit is an original enemy admitted by the ordinary
any-enemy search, including unrepresented targetability flags). These are not
derived from IDs, team class or HP alone. The query takes the settled snapshot,
actor/target IDs, ordinary movement profile, these facts and explicit RNG.
No selected plan/cache/score is accepted as command authority.

With only this eligible enemy, choose `ai_target_facing_find(target,actor)`.
For distinct points, its dominant-axis direction advances one step toward the
enemy within their coordinate bounding box, hence inside any rectangle containing
both. Flat unblocked terrain makes that neighbor viable even if occupied. At
least one viable direction exists, so the no-direction and keep-current/last
fallbacks are unreachable in this slice. No delay cache or nearest-enemy mode
needs to be invented as a result diagnostic.

Let dx=target.x-actor.x, dy=target.y-actor.y. Source evaluates
`a=dy-dx`, `b=dy+dx` with signed s32 branches after s8 coordinate loads:

- a<=0 and b<0: South.
- Otherwise a>0 and b<=0: West.
- Otherwise a>=0 and b>0: North.
- Otherwise a<0 and b>=0: East.
- Otherwise: Overlap (same coordinates).

Thus exact diagonal ties are southwest->South, northwest->West,
northeast->North and southeast->East. This is not an arbitrary atan2 rounding
policy. Coordinates 0..15 have no signed-byte truncation. Preserve the source
branch order and map semantic directions explicitly instead of casting ordinals.

## Quirks and unsupported cases

When no enemy qualifies, nearest search returns the actor. Coincident source/
target yields Overlap (4); `ai_wait_facing_choose` reads viability[4], the
never-written byte after its four-byte array. The result depends on leftover
stack contents, as recorded in reference QUIRKS.md. Reject no eligible enemy,
overlap, dead target and unsupported maps/context rather than silently choosing
KeepCurrent or defining an invented fifth viability bit. No bug fix is selected.

Fixed hints, mounts, multiple enemies, blocked/elevated/layered maps, hidden or
disabled targets, full cached-threat fallback and authoritative Wait execution
need separate source-traced children. Existing selection output stays deferred.

## Acceptance for ZOD-151

1. Four cardinals and four exact diagonal ties use the source directions, with
   explicit managed enum mapping. Non-equal dominant axes remain consistent.
2. Edge/corner and 1x2/2x1 rectangles face an adjacent enemy despite occupancy;
   no movement query destination test should reject that direction.
3. Use settled positions changed from an earlier hypothetical destination; face
   the actual enemy. Allow ACT already used and unchanged choice flags. Both
   imminent/non-imminent ordinary target timings give the same direction.
4. Unknown context/eligibility, wrong active actor/phase, dead/missing/same target,
   overlap or unsupported terrain reject atomically with no facing/state mutation.
5. Replay/order/both actor identities preserve snapshot, CT/HP/choices and explicit
   RNG. The query consumes zero draws/ticks, including counters already at MAX.

## Validation

The read-only source auditor also checks eight authored directional literals and
all 65,280 ordered distinct position pairs on a 16x16 grid: the chosen neighbor
lies within each pair's bounding box and reduces Manhattan distance by one.
This is an offline arithmetic check, not C/original or managed query execution,
and does not add 65,280 unit-test cases. Managed expectations remain unexecuted.

```powershell
python tools/verification/verify_ai_wait_facing_contract.py --reference C:/repositories/fft_decomp --output <new scratch report>
```

Report: `scratch/ai-wait-facing-150/source.json`. No C# changed; last complete
.NET validation remains ZOD-149's 3,780 cases. Live host/facing application stays
outside this milestone. All **496 Python tests** passed after the auditor was
added; log: `scratch/ai-wait-facing-150/python.txt`.
