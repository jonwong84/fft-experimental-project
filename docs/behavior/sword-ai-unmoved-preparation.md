# Fresh unmoved AI refresh and preparation — ZOD-172

Implements the [ZOD-171 source/design](../architecture/zod-171-ai-unmoved-preparation.md)
against read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed base `e51948354e75e9c6de88f8ae1430e3fd613c1ae3` (ZOD-171). Historical source receipts/fixtures remain
unchanged; the managed scenarios now exercise the previously proposed behavior.

`SwordAiRefresh.EvaluateUnmoved` admits an active ordinary actor with both Move
and ACT unused. It shares refresh admission, flags and private simulation with
the existing Evaluate entry, which still requires Move used/ACT unused. The new
entry temporarily refunds both choices: min(60, liveCT+40), with addition before
the source byte CT store. It performs no hypothetical move or action. The closed
clock pass retains exactly one upkeep draw, including when the actor itself is
the first ready unit. All results remain pure diagnostics and hypothetical RNG.

`SwordAiPreparation.EvaluateUnmoved` owns that fresh refresh and the existing
ordinary movement query on the original snapshot. It returns immutable
ReachableOrigins containing the actor tile and all ordinary reachable destinations
in layer/y/x order. Consistent body/turn movement flags are enforced by
TurnBattleState. Move0 gives the actor tile alone; it does not imply a Move command.
The existing already-moved preparation leaves this additive diagnostic empty and
keeps its prior admission/behavior. Neither entry accepts cached query outcomes.

Policy/focus, delay, reach and proximity use restored live units, never the
refresh clock's temporary units. Preparation remains NonBlue and retains all
explicit entry, list, cached policy, delay and ordinary profile facts. Raw
Move<=124 remains this compatibility adapter's envelope. No extra draw follows
refresh; preparation can finish at MAX draw count. Rejection returns the original
hypothetical RNG and no partial refresh/origins/policy. A geometry failure also
discards the owned refresh result. Shared flag/terrain guards remain active.

Five scenario tests cover the seven source clock vectors, the five-origin and
zero-Move reach vectors, policy derivation after the hypothetical actor gets a
turn, opposite entry rejection, current certificates, exact limits and both-ID/
collection-order replay with unchanged live snapshots/RNG. CT15/95 Speed5/5 ends
temporary actor CT55, picks peer after one addition and leaves actor60/peer0.
Entry word1/count7 consumes raw16838 and returns word1103527590/count8. CT15/40
Speed10/10 picks actor after five additions, but policy delays stay actor9/peer6
from live CT15/40. On the 5x3 actor(3,1)/target(1,1) Move1 board, origins are
(3,0), (2,1), (3,1), (4,1), (3,2), with no movement or geometry RNG draw.

All 31 focused new/existing refresh/preparation cases pass. Locked restore,
zero-warning/error Release build, **3,929 .NET cases / 513 Python tests** and the
pinned inherited/unmoved source audit pass. Logs and seven full TRX files are
ignored under `scratch/ai-unmoved-preparation-172/`. Owner content drafts were
included in validation and excluded from this commit. No C/original execution or
UI review was performed; bounded scenarios do not establish full AI equivalence.

No selection, trusted session, command receipt or live RNG is published. Existing
post-prefix Move-first adapters still require GeneralSelectionEntryContext.
ZOD-173 will compose the fresh unmoved prefix privately with explicit in/out-of-
reach pure selection. Live integration, ACT-first/post-ACT reselection, broader
policies/search and host activation remain subsequent work.
