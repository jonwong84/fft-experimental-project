# Pure post-ACT movement-only selection — ZOD-182

Implemented 2026-10-29. Clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`: `turn.c::ranked_select` and
`fallback_select`, following the [ACT-used list contract](../architecture/zod-176-ai-act-first.md).

`SwordAiPostActSelection.EvaluateInReach` and `EvaluateOutOfReach` own
[fresh scenarios](sword-ai-post-act-scenarios.md), then use the existing lottery
tail with eight fresh empty ranks. Ordinary-only preparation has computed reach
before clearing Attack usability. There is no Attack scan, weapon formula,
invented ACT-unused snapshot or public cached-result/list input. Eight slots
represent original `AI_RANKED_ACTION_COUNT`, not a managed content capacity.

Every zero-hit slot still consumes a real BIOS draw. All eight fail; fallback
returns MoveOnly at D0 with no Attack, act-before-move false and deferred
ChooseAtWait facing. `NoDefendSupport` remains an explicit assertion (default
false), because original fallback may otherwise select Defend. Other source
facts remain in preparation/scenario profiles. Actual battle state and both
live RNG copies remain unchanged; rejected queries hide the entire owned prefix
and return incoming decision RNG, including a failure at the eighth roll.

The shared internal lottery tail serves both legacy Attack selection and this
empty-ranking case. Legacy first-roll, failed-positive-first-entry and empty
fallback behavior are unchanged. No caller can supply an opaque composition as
trusted input to the new API.

Actual hit/miss select D0=(2,2) on the existing Move1 fixture. One refresh,
four cell scores and eight rolls finish at word3834121963/count37 or
word3776134388/count42. The hit lottery starts raw20495 and ends raw25736;
the miss starts raw13145 and ends raw24851. An out-of-reach single-cell
case from word1/count7 draws refresh, cell and NonBlue adjustment before its
eight rolls, ending word180171308/count18. Move0 can select staying.

Four scenario tests cover literal streams, owned-prefix equivalence, zero-hit
roll order, adjustment ordering, stay, exact/late exhaustion, no-Defend/branch
admission, actual Critical/knockback, either actor/order replay and isolation.
All 794 AI cases pass. Locked restore, zero-warning/error Release build,
**3,976 .NET cases / 516 Python tests** and the clean pinned source audit pass.
Logs/seven full TRX files: ignored `scratch/ai-post-act-181-183/`. No C/original
execution, host activation or UI review; bounded results do not establish full
AI equivalence. ZOD-183 designs live command publication separately.
