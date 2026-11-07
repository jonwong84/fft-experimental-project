# Fresh prepared in-reach pair selection — ZOD-198

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, using the
[ZOD-195 source trace](../architecture/zod-195-pair-preparation.md) and
[fresh pair scenarios](attack-throw-stone-ai-scenarios.md).

`AttackThrowStoneAiPreparedSelection.Evaluate` owns fresh both-unused NonBlue
preparation, in-reach scenarios, both candidate scans/ranking and one lottery.
Normal scoring, canonical coordinates, no other scenario overrides, clear direct
trajectories and no Defend support remain explicit caller assertions. The refresh,
exact initialized-list, no-special-behavior and clean-delay-cache certificates
still apply. No public mask, destination, cached score/rank or projected outcome
is an input to this prepared entry.

The owned preparation establishes active target, usable source entry order,
Attack exhaustive policy and Throw Stone consume-aim policy. Its exact lists
also establish that unusable Frog Attack adds no later searchable action. Fresh
scenarios establish masks/D0/D1 and baseline-cell provenance. The owning fresh
entry supplies working best/list/default resets, preserving required unrepresented
assertions rather than inferring them from stable IDs.

`AttackThrowStoneAiComposition` factors pure admission and an internal fresh-prefix
continuation. `AttackThrowStoneAiSelection` factors its fresh lottery tail. Their
existing public supplied-scenario methods retain admission, results and RNG
semantics. The new entry privately passes the already-produced prefix and actual
RNG through those continuations: it does not refresh twice, rescore movement/D0,
restart RNG or adopt any hypothetical unit state. No trusted session or command
receipt is produced; the returned typed plan still defers facing until Wait.

Failure exposes stage/reasons only and restores incoming whole-query RNG, even
after complete preparation/scenarios, earlier ranks or a late lottery roll.
No partial masks/ranks/plan escape. This managed atomic policy does not claim
original C rewind. The in-reach entry rejects an out-of-reach request; no reach-
halving work or out-of-reach original-origin cache assumption is introduced.

Default actor(1,1), target(2,1), PA5, HP50, Move3, CT15/95, Speed5 and ticks20:
one refresh draw,14 movement draws, three normal Attack evaluations costing15,
one Throw Stone costing5 and one lottery draw total36. D0=(1,3), D1=(1,4).
Attack's best is ACT-first toward D1; Throw Stone is Move-first toward D1.
Power2 gives equal score26/hit100/proximity6, retaining Attack first. Lottery
entry RNG is `(3776134388,42)` and raw19067/mod67 selects it; incoming `(1,7)`
ends `(3397090333,43)`. Power1 instead selects stronger Throw Stone with the
same destination and different order. HP10 KO candidates need two upkeep draws
each, yielding40 total draws and `(1775811865,47)`.

Move0 cases retain the actor tile. Seed10/evade75/power4 rejects Attack's first
lottery roll then selects Throw Stone; seed1 makes both real rolls fail and keeps
Attack after all eight rolls. At evade100 both are NotValuable and the empty
lottery moves only at D0. These preserve the prior shared lottery arithmetic,
including draws for empty slots.

Six new scenarios cover those literal plans/costs, normal/KO/second/last-slot
exact and short budgets, late Attack/Throw Stone failures, remaining source
admission, both-actor/order replay and live-state isolation. The fully prepared
result also matches a fresh public scenario plus supplied-selection chain with
the independently retained refresh RNG. Existing supplied/legacy regressions
cover the internal refactor.

Locked restore, zero-warning/error Release build, **4,042 .NET cases** across
seven projects (855 AI), **526 Python tests** and clean source/prepared-data audit
pass. Logs, `source-final.json` and seven full TRX files are in ignored
`scratch/ai-pair-scenarios-197-198/`. Owner content drafts participate in validation
and remain excluded from these commits. No original/C execution or UI review;
frame/VSync RNG equivalence remains unresolved.

ZOD-199 traces out-of-reach pair mask/cache provenance before implementation.
General lists/statuses, blocked paths, live pair authority, replacement interfaces
and presentation activation remain separate work.
