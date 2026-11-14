# ZOD-152 — Source-certified AI Wait settlement through existing receipts

Source/design, 2026-10-28. Read-only clean reference:
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[receipt](../evidence/ai/wait-settlement-contract-v1.json) extends ZOD-150 with
ordinary turn completion and stat publication. No production settlement adapter,
C/original execution or native changes in this checkpoint. ZOD-153 implements it.

## Original ordering and widths

`battle_state_wait_direction_enter` chooses the AI hint on settled coordinates.
During `battle_state_wait_direction_update`, a cardinal hint sets runtime facing
(quarter turn 0x400). Once its presentation counter reaches 0x1f, the source calls
`turn_end`, then `tile_position_with_partner_set`; the latter writes current tile
and facing into battle stats through `unit_set_tile_position`. Mount/animation/
camera/frame timing remain excluded. Managed FacingSettledEvent after TurnEndedEvent
models battle-stat publication, not the earlier renderer's runtime-facing update.

`turn_end` immediately returns for absent units or has_turn==0. Otherwise it reads
u8 CT into u16, suppresses movement refund for moved/Don't Move and action refund
for acted/Don't Act/Performing, adds 20 per unused unrestricted choice, caps at60,
writes u8 CT and clears has_turn. It does not clear choice flags, advance global
ticks, change other CT or draw RNG. The closed ordinary context excludes those
restrictions/status/charge and intervening event effects; do not infer it from
visible None/Critical state alone.

`turn_act_taken_set` and `turn_move_taken_set` share `turn_move_act_taken_set`.
After the second choice, `turn_should_end` calls turn_end before later Wait-facing.
The later turn_end is therefore a no-op, not another refund. This agrees with
the current TurnCycle auto-completion; no fake active-turn snapshot is needed.

## Reuse the existing authority boundary

`FacingTurnState` is issued only by Begin and accepted transitions. Its automatic
completion receipt binds actor, revision, exact fully settled battle and private
lineage after the complete action result. This already handles damage/forced
displacement settling after an earlier internal clock event. Reuse
`FacingTurnBattle.WaitWithFacing` and `FinishFacing`; do not mint receipts from
public AI diagnostics, copy an ended snapshot into UNIT_TURN, or add a parallel
facing mutation/clock implementation.

Add two explicit opt-in settlement entries:

- **Wait:** require no pending receipt and the active actor. Resolve fresh AI
  facing on the current session Battle, then pass an internally created Fixed
  choice to WaitWithFacing. The original ChooseAtWait context remains explicit;
  Fixed is the already-resolved value at this boundary. Publish one Wait/refund,
  one FacingSettledEvent, revision+1 and no pending receipt.
- **Finish:** require the exact pending completion receipt and actor. Resolve
  against its fully settled Battle without requiring a now-ended active turn.
  Pass the fresh Fixed choice and the same receipt to FinishFacing. Publish one
  FacingSettledEvent and revision+1, clear pending, and preserve CT/flags/HP/KO,
  positions and both RNG copies. Never call Wait or replay the original action.

Extract existing authority guards into internal helpers, preserving their public
validation order and rejection reasons. Share an internal pending-facing query
entry that validates the session receipt before relaxing the active-turn check;
keep the public raw-snapshot ZOD-151 query active-only. No public skip-validation
flag, cached-facing executor or attach/restore entry is introduced.

Both entries require the existing ordinary movement and false-default original
AI ChooseAtWait/eligible-opponent facts plus **OrdinaryTurnEndContext**, false by
default. That fact certifies the source's ordinary unrestricted completion path.
Use the session's current RNG for the internal query; accept no external decision
RNG or selection result to commit. Candidate simulation progress remains private.
The 16x16 two-living-unit restriction stays; a dead/no eligible opponent rejects
without guessing the original overlap stack byte, leaving pending authority intact.

## Atomic result and exclusions

Return immutable fresh-facing diagnostics alongside the existing transition
result/state/events only on success. On unsupported context, authority guard,
query or transition rejection, return the identical input FacingTurnState, empty
events and no partial facing diagnostic. Preserve typed outer stage/reason and
existing nested guard/query/transition reasons. Immutable-input replay remains
valid; a consumed receipt cannot authorize the returned newer state. No global
consumed-token registry is selected.

RNG draw/tick cost is zero for both paths, including current draw count at MAX.
Revision is the existing managed command-boundary counter, not a source frame
counter. Preserve its exhaustion handling. Full AI selection/command dispatch,
decision-RNG commitment, path legality, save tokens, native host/pauses and original
presentation timing remain outside this milestone.

## Acceptance for ZOD-153

1. Fresh active Wait at CT15 with no choices used yields CT55; Move-only or
   ACT-only yields CT35. Face actual settled coordinates, retain source direction
   ties and old command events before FacingSettledEvent; no choice is invented.
2. Both Move->Attack and Attack->Move completion use their actual receipt and
   settled positions/HP. Finish changes only actor facing/revision/pending state;
   CT15 stays15, no second TurnEndedEvent, no RNG/tick/damage replay.
3. High active CT clamps to60 on real completion; pending CT60 stays60. A same-facing
   result still emits the existing completion event and consumes the local revision.
4. Missing/foreign/stale/consumed receipt, wrong actor and pending active-Wait
   misuse reject atomically. Default ordinary-end or Wait/eligibility facts and
   dead-opponent query rejection preserve input and pending receipt.
5. Current RNG at MAX does not prevent active Wait. Both actor identities,
   collection order, repeated immutable input and returned-state replay preserve
   source results. Reuse the legacy facing-session guards instead of duplicating
   every counter or receipt test through private reflection.

No original/C execution is used for these source-derived proposals. Full last
.NET validation remains ZOD-151's 3,808 cases; the new adapter cases are unexecuted
until ZOD-153. Audit reproduction:

The extended receipt audit passed against the clean pinned checkout; all 496
Python verification tests passed. No .NET code changed in this contract ticket.

```powershell
python tools/verification/verify_ai_wait_facing_contract.py --reference C:/repositories/fft_decomp --settlement --output <new scratch report>
```
