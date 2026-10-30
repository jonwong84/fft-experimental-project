# Ordinary AI Wait-facing settlement

ZOD-153, 2026-10-28. Source: read-only clean fft_decomp
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[source contract](../architecture/zod-152-ai-wait-settlement.md) and
[source receipt](../evidence/ai/wait-settlement-contract-v1.json) trace the caller,
turn-end guards, CT widths/refunds and battle-stat facing publication.

## Supported behavior

`SwordAiWaitSettlement` is an opt-in adapter around the existing facing session:

- `Wait` requires its active actor and no pending completion. It queries current
  settled positions, then delegates to `FacingTurnBattle.WaitWithFacing` using
  the freshly resolved Fixed choice. Ordinary unused choices refund 20 each,
  capped at 60; the existing end events precede FacingSettledEvent.
- `Finish` requires the session's exact pending receipt and actor. It resolves
  against the fully settled Move/Attack result, then delegates to FinishFacing.
  Only actor facing, revision and pending authority change. CT, choice flags,
  HP/KO, positions, ticks and RNG retain their post-action values. It never repeats
  Wait or damage. Both Attack->Move and Move->Attack are supported.

Both paths require false-default `OrdinaryTurnEndContext` in addition to explicit
original ChooseAtWait/eligible-opponent facts and the existing ordinary flat
movement certificate. These facts certify unrepresented control, targetability,
status/restriction and event behavior. The domain remains two living opponents,
clear dry one-layer flat ground, coordinates 0..15 and ordinary movement. A dead
opponent rejects and leaves completion pending; no KeepCurrent guess is added.

Existing public facing APIs retain their guard order and behavior. Guards are
shared internally; only a validated session receipt permits the private query on
an ended turn. Public `SwordAiWaitFacing.Evaluate` stays active-only. Cardinal and
diagonal arithmetic is shared; managed direction ordinals are never source casts.

Results carry typed outer stage/reason plus the existing query/transition reasons.
Every rejected operation returns the identical session, empty events and no facing
diagnostic. Success increments the existing revision once and clears pending.
Replaying an old immutable input is valid; its receipt cannot authorize a newer
state or another session. No external plan, facing, decision RNG, fabricated
active snapshot or public receipt constructor is accepted.

Both entries use current session RNG with zero draws/ticks, including draw count
at MAX. Full AI selection/command execution, decision-RNG commitment and native
presentation remain separate. The source's earlier runtime-facing animation and
0x1f frame pacing are not simulated by the post-end battle-stat event order.

## Validation

Thirteen focused new cases exercise real session transitions, ordinary CT 15->55/
35 refunds and cap 60, both choice orders, final critical knockback coordinates,
same-facing completion, authority/context rejection, dead target, both RNG copies
at MAX and identity/order/replay. No private state or receipt fabrication is needed.
Existing facing-session and pure-query tests also pass (132 focused cases total).
Locked restore and a zero-warning/error Release build passed. All **3,821 .NET /
496 Python** tests passed; the [implementation receipt](../evidence/ai/wait-settlement-implementation-v1.json)
records reproduction paths. The owner's pending definition refactor/drafts were
included in validation and excluded from these commits. No original/C execution
or UI review occurred.
Source audit includes 65,280 directional pairs as an offline arithmetic proof;
these are not additional unit tests or original/C execution.
