# ZOD-148 — Out-of-reach selection and ability-prefix ownership

Source/design, 2026-10-28. Clean read-only reference:
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[receipt](../evidence/ai/out-of-reach-selection-contract-v1.json) reuses the
audited out-of-reach movement and original composition/selection functions.
No C/original execution or production selection change here. ZOD-149 implements
this continuation and the admission correction below.

## Ability-prefix correction to ZOD-144

`turn.c::ability_list_evaluate` starts at entry 0 and evaluates each entry before
the terminator. `evaluation.c::ai_ability_outcome_evaluate` can consume random-use
RNG and return without ranking anything; losing/NotValuable candidates also retain
their draws. A fresh empty ranked list therefore does **not** prove that earlier
ability work was absent. TerminalAttackEntry proves only that no later entry
will execute, and FreshAbilityBestReset is per-entry, not a prefix certificate.

ZOD-144 derived the absence of the in-reach scenario adjustment correctly, but
its stated derived NoInterveningRandomWork fact was too broad for a potentially
non-first Attack entry. Add **FirstAttackEntry**, false by default, to the shared
derived-selection profile and require it before movement in both derived entry
points. Together with the terminal-entry fact this bounds the modeled ability
loop to this first/last ordinary Attack. Inactive-target input does not bypass
admission. This is an explicit supported-domain tightening, not an original
behavior change; prior evidence remains historical. Current tests will certify
the stronger context, plus test omitted/false rejection. Legacy supplied-scenario
APIs keep their existing NoInterveningRandomWork certificate and signatures.

## Private continuation and result

Add an explicit out-of-reach derived-selection entry, with the existing state,
weapon, profiles and hypothetical RNG plus known original Blue/NonBlue team.
No masks, destinations, score arrays or cached outcomes are accepted.

Share pure weapon/policy/state preflight and the fresh composition/lottery tail
with the in-reach entry. Privately invoke the out-of-reach movement query once,
retain all its S evaluations and its **post-adjustment** RNG, then build
A=S union actor without scoring the newly added actor. Read D0's actual S score,
scan the ordinary Attack once and run the existing lottery. Preserve the wrapper's
team/effective-reach/raw-adjustment/D1-fallback diagnostics alongside selection.
Do not pass a public scenario result into an executor, call the old composition
to repeat movement, or substitute pre-adjustment RNG into the Attack stage.

For this focused flat ordinary-sword branch, d>Move+1 implies every reachable
origin has target distance >=d-Move>1; the added actor is also non-adjacent.
Thus the single Attack emits **no candidates** even with TargetActive=true.
An implementation must still run the validated scan, preserving its diagnostic
absence and source context rather than synthesizing an Attack or drawing formula
RNG. Empty ranking visits all eight lottery slots, then chooses MoveOnly at
D0=D1. NoDefendSupport remains required: the original fallback otherwise selects
Defend. Full broader ability search could reach differently and is excluded.

Success cost is |S| + (Blue:0, NonBlue:1) + 8 actual RNG draws. Destination
selection, D0 baseline lookup/copy, insertion and fallback add none; original
per-simulation ticks remain unchanged. Do not read or invent actor movement
priority outside S. Its proximity is based on original R/state, not movement
scores or a hypothetical KO. Deferred ChooseAtWait is retained; no command,
facing resolution, damage or live RNG commitment occurs.

On admission, movement, adjustment, scan or lottery rejection, publish no partial
scenario/rolls/plan and restore **whole-query input RNG**. Keep typed outer stage
and existing nested movement/adjustment/lottery failure index. Check actual draws:
Blue needs no reserved adjustment and NonBlue needs exactly one.

## Acceptance for ZOD-149

- 7x1 source vectors from ZOD-146, actor x6/target x1/Move3, first+terminal
  Attack, seed1/count7: Blue chooses x5, three movement draws + eight rolls,
  final word180171308/count18. NonBlue retains raw17515 adjustment, chooses x5,
  final word836760821/count19. Seed19 Blue chooses x5 at word1844253734/count18;
  NonBlue raw224 halves reach4 to2, chooses x3, final word1165580391/count19.
- Exactly S=x3,x4,x5 is scored; A additionally contains x6. Composition reuses
  those same three entries. Scan candidates/ranked actions are empty; all eight
  lottery rolls have hit0. Live HP/CT/choices/RNG remain unchanged.
- TargetActive=false and singleton/no-match movement keep their real movement,
  adjustment and lottery schedules. No speculative formula draws or extra actor
  scoring is allowed.
- Missing FirstAttackEntry rejects **both** derived APIs before movement, even
  with inactive target and exhausted RNG. Update existing derived test contexts
  explicitly; do not change literal expected original results. Existing legacy
  supplied-scenario regression results stay unchanged.
- Unknown team, wrong branch, invalid weapon and false selection/context facts
  reject before scoring. Late movement/adjustment and late lottery failures return
  original whole-query RNG/no output. Exact Blue/nonBlue budgets complete at MAX.
- Source-order/replay, both actor IDs and collection order remain deterministic.
  Reuse standalone movement coverage; do not duplicate every admission permutation.

The next useful source boundary is deferred wait-facing resolution on settled
positions. Full ability/roster search, a replaceable general AI interface, live
command execution and native/editor activation remain separate.
