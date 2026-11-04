# Bounded Blue-team pair AI

Implemented 2026-11-04 from the [ZOD-225 source study](../architecture/zod-225-blue-pair-policy.md),
pinned to read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The existing ordinary pair geometry, lists, status/equipment/scoring assertions
and unknown-priority restrictions remain in force.

## Preparation — ZOD-226

Pair preparation now admits `SwordAiScenarioTeam.Blue` only with explicit
`BlueAiControlled` on `AttackThrowStoneAiPreparationProfile` and source integer
`HP*128/maxHP >= 45`. Direct player-controlled auto-battle and the below-boundary
healer/crystal focus branch remain unsupported. Unknown team and missing control
assertions reject without query RNG consumption. NonBlue behavior is unchanged.

The fresh preparation outcome records the admitted team. Blue preserves the
refreshed retained TeamSupportUnavailable policies; NonBlue still sets the
actor's bit. One ally cannot supply retreat support in either case. Manual
autobattle, focused enemy, ability summaries, minimum reaches and random-use
threshold0 remain shared. Post-ACT still clears actor usability after computing
reach/counts. The shared restored-state helper retains NonBlue as the default
for unchanged Attack-only callers.

Two preparation scenarios check retained clear/set bits and peer policies,
shared initialized summaries, one-draw refresh and actual post-ACT clearing,
control/unknown-team rejection, immutable inputs and the exact 44/128 versus
45/128 threshold (also 17/50 versus18/50). No serialized content/save schema changes.

## Movement — ZOD-227

The existing scenario owner reads its freshly admitted team. In-reach behavior
is shared. Out-of-reach Blue keeps full prepared reach and takes no adjustment
draw; NonBlue still draws after scoring and may halve reach. `ReachAdjustment`
is nullable to represent the source branch accurately. Existing NonBlue vectors
retain non-null values. No fabricated raw/modulo value or replacement Blue
movement/ranking kernel is introduced.

From supplied word20/count7, one refresh and one movement score end at
`(143664818,9)` for Blue. NonBlue then draws12502/mod7=0 and halves reach2 to1,
ending `(2966873603,10)`. Masks, shared scores and null retained actor priority
remain intact. With exactly two draws available, Blue scenarios fit while
NonBlue rejects at reach adjustment; one draw short fails during scoring and
restores the whole query. Post-ACT Blue uses the same no-draw policy and D0=D1.

Preparation and movement support landed together so selection could not admit
Blue with a NonBlue draw schedule. Two scenario families check the source
streams, in-reach equality, pre/post-ACT behavior, budgets and preserved provenance.
