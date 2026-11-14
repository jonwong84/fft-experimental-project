# One ordinary roster sword candidate — ZOD-294

Source/design, 2026-11-05, managed base
`d284c92f9ccb64fb292b8269bf06acef520d08df`. Read-only fft_decomp checked clean
at `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/roster-sword-source-v1.json) pins source files and
function bodies. No original or C program is executed. Prior
[candidate](zod-124-ai-candidate-contract.md),
[actual roster combat](zod-277-roster-combat.md) and
[roster search](zod-290-roster-action-search.md) studies remain prerequisites.

## Formula and numeric admission

Ordinary Attack uses the equipped range-one, striking, neutral, empty-proc sword
binding. `BoundedSwordWeapon` already carries that validated effect; actor's
equipped ID must match. Attack is the initialized default action, not a separate
learned-action grant. No peer's equipment is invoked by an effect-free clock.
Require ordinary human unchanged-equipment/zero-Golem/normal scoring facts.
Candidate input is a complete initially living compact16 roster with stable
teams; the selected target must be a different team in this ordinary useful
enemy slice. Actual roster commands still permit friendly fire; this candidate
owner does not claim wrong-side strike accounting or arbitrary usefulness masks.

`formula_evade_store_physical` and final-hit helpers retain four evade-source
rolls, even at zero/certain evade. AI `formula_get_random` returns midpoint
0x4000; critical never succeeds and conditional proc is execution-only. Thus
there are exactly four real formula draws, no critical/proc/displacement draws,
and no probability-weighted HP damage. Front class evade subtracts from 100;
side/rear ignores it. Formula origin depends on the supplied action order;
settled target facing and incoming actor facing remain unchanged.

Neutral XA floors PA to one. Preserve the existing execution-safe envelope
`(2*XA-1)*power <= 32767`, then clamp positive normal damage to 999. Signed-16
damage is applied in wider arithmetic and target HP clamps at zero. PA 255/
power 64 is admitted; power 65 rejects before drawing. Zero power still gives
a counted positive-accuracy hit with unchanged HP. Zero accuracy retains four
draws but returns NotValuable before turn-end/clock/scoring.

## One target and all peers

`ai_action_simulate_and_score` saves/restores source units for every candidate.
Move-first places the actor before formula resolution; ACT-first resolves from
the original tile, then places it. Legal destination membership is derived from
the original living roster, before a possible target KO, so a new corpse does
not grant a new move endpoint. Placement preserves facing rather than applying
actual path heading. Hypothetical ACT is consumed; unused Move adds 20 to CT
before the cap of 60. Neither choice nor projected CT is adopted into live state.

Only the selected target receives damage. It becomes Critical at
HP <= floor(maxHP/5), or Dead/counter3 at zero; Dead replaces Critical. Other
allies/enemies retain their own HP/maxHP/status/death counters. Source KO-count
increment is execute-only, so no live or hypothetical KO-count publication is
introduced. The old two-unit `AiCandidateState` implicitly applies target HP to
the non-actor; a roster must use an explicit target ID through a shared private
builder, without weakening old pair admission.

The shared hypothetical clock picks already-ready highest CT then first source
slot before adding CT. A projected corpse still gains CT, takes modulo/upkeep
draws and is skipped without receiving a turn or losing its death counter.
Stop at the first living movement opportunity, which can be any ally or enemy.
One killed target means at most one corpse in this initially living domain;
other peers can shorten the wait but cannot be erased or simulated as pairs.
Absolute scoring visits every source slot with HP/status/seven equipment terms
and each unit's team sign. Ordinary useful enemy strike credit is zero. The
score owner accepts only the freshly owned projection, never a cached public
outcome. A late clock failure rolls back formula and earlier upkeep draws.

## Selected child — ZOD-295

Implement a separate isolated roster sword candidate projection and scoring
owner. Add explicit roster admission to the existing formula query through its
private shared core; preserve the pair formula API and two-unit guard. Generalize
only the private candidate-unit builder to name the changed target; old pair
callers retain their exact behavior. Reuse formula/clock/score owners and expose
diagnostics for all units, score contributions and upkeep order. Preserve both
orders, zero accuracy/damage, PA/width boundaries, Critical/KO/corpse clocks,
ready ties, exact/late budgets and immutable replay. No actual Attack command
may be invoked to obtain this prediction.

This child owns no preparation, usefulness filter, occurrence/target/origin/aim
enumeration, rank insertion, lottery or roster AI command/turn execution.
Full corpse lifecycle, other abilities/policies/statuses/maps, automatic facing,
terminal routing, original frame RNG, owner drafts, host/editor, replacement
interfaces and ZOD-106 remain separate/deferred. ZOD-296 should connect ordinary
roster Attack enumeration to one fresh general prefix through this candidate
owner, preserving source map/aim order and exhaustive scanning's unread keys.

## Implemented follow-up

[ZOD-295](../behavior/roster-sword-ai-candidate.md) implements the selected isolated
query through shared formula/selected-target/clock/score kernels. Thirteen
independent descriptions, eight new managed cases and three new Python tests
cover source arithmetic, corpse upkeep, pair parity and immutable replay. Full
validation passes with 4,363 .NET cases, 560 Python tests, zero-warning/error
Release build and pinned source/prepared/guide/coverage checks. No original/C
execution, full target/aim search or roster AI command activation is claimed.
