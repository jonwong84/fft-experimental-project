# Ordinary roster AI preparation — ZOD-284–286

Implemented 2026-11-04 from managed base `5b25945d09af258d002e8af6c7d665f8fef64397`,
following [ZOD-283](../architecture/zod-283-roster-preparation.md). Read-only
fft_decomp was checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/roster-preparation-source-v1.json) pins 17
functions across seven files. The
[implementation receipt](../evidence/ai/roster-preparation-implementation-v1.json)
records validation. No original/C program was executed.

## Fresh whole-roster refresh — ZOD-284

`RosterAiSimulationProfile` owns the existing strict `FlatRosterBattleProfile`,
one copied baseline per represented unit and an explicit effect-free clock fact.
Storage is not capped, but original-compatible AI queries require at most 16
present compact records, living ordinary None/Critical units, positive Speed,
MP0, stable teams and source-order/TieKey agreement. The Framework's 21 source
slots and 16 other movement records are separate boundaries. Old pair profiles
and their guards remain unchanged.

`RosterAiRefresh.DecisionFlagsRefresh` requires an exact retained-policy row for every unit,
fresh entry, known targetability, zero cached MP modifiers and clean actor delay
cache. It accepts before-Move, after-Move and post-ACT live choices without
inventing unused choices. The actor delay byte is captured from live CT before
the private refresh. HP flags preserve strict half/three-quarter arithmetic.
Stable teams derive every Enemy flag relative to the actor; ConserveCT and
TeamSupportUnavailable remain explicit retained inputs.

Refresh uses the shared private move-only projection with the unchanged actor
tile, equivalent to source ACT-first/skillset0 skipping movement and action work.
The existing `AiSimulationClock` advances all units together, picks highest ready
CT then earliest source order, performs CT modulo and one upkeep draw, and
resets the selected unit's choices. No repeated pair simulations occur. Diagnostic
projected CT never replaces live state. Considered becomes true for all admitted
living units. Every rejection returns incoming RNG and no partial output.

The literal actor CT 15/Speed 5, enemy CT 95/Speed 5 and ally CT 0/Speed 5 starts at
word 1/count 7, ticks 20. Both-unused actor turn-end CT is 55; after one hypothetical
tick, the enemy is selected at CT 0, actor CT 60 and ally CT 5. RNG ends
word 1103527590/count 8. If two peers start at CT 150, the earlier source slot wins
without a tick and retains CT 50; the other stays at CT 150. If neither peer is ready,
the actor can be next after nine ticks. After Move or ACT alone, turn-end CT 35
becomes projected CT 40 in the one-tick case. Odd HP 24/max 49 does not set below-half.

## List summaries and team support — ZOD-285

`RosterAiPreparation.Evaluate` privately owns one refresh, then derives supported
owned-list summaries and support flags on restored live state. Structural Attack/
Frog and missing/repeated self/Stone occurrences retain source order and original
34-entry extent. Initialization and ordinary usability stay distinct. No gameplay
grant is inferred or required by this summary-only entry; it never invokes an
ability. Actual grant/effect binding remains a later entry/command responsibility.

Enemy harmful/physical counts sum occurrences across every non-actor team.
Helpful share uses 128/threshold 77, then clears the first source-order minimum
ally. Equal minimum shares preserve the first minimum; another support-heavy
ally can retain MostlySupport, unlike the earlier single-ally pilot. Enemy
retained MostlySupport remains unknown. Actor policy/reach precedes post-ACT
usability clearing; no self usefulness coins occur. Nonwrapping actor Move<=124
gives hostile reach Move+1 and ally reach Move.

Retained actor TeamSupportUnavailable exits support calculation before counts,
which remain null diagnostics. Otherwise support needs at least two eligible
allies and one non-Critical ally in this admitted domain. Failure propagates
unavailable support to allies for non-Blue; Blue preserves retained flags. Early
exit does not propagate. SingleEnemyRemaining is true only when support succeeds
and exactly one enemy exists. Policies retain ConserveCT independently.

The derived `AiTargetRoster` uses live restored positions, HP/status and delays,
plus the pre-refresh actor byte and freshly owned flags. In the literal example,
target delay remains 1 despite diagnostic target CT 0, which would imply 20 ticks
if incorrectly adopted. Existing targeting/proximity queries consume this data;
no focus, retreat/general-policy admission or movement scenario is selected.

## Isolated movement hypotheses and scores — ZOD-286

`RosterAiMovementBaseline.Evaluate` validates one supplied legal AI-origin tile,
explicit source-score/canonical/normal facts, unchanged human equipment and zero
current/baseline Golem. It owns one fresh whole-roster hypothesis through the
same private projection/clock. Changing destination consumes hypothetical Move
but never ACT or facing. Post-ACT movement refunds neither choice; already-moved
actors can score only their origin. This entry does not refresh, build scenarios,
scan actions, insert ranks, select a plan or authorize a command.

The extracted score owner now accepts a privately derived enemy set; the old pair
entry delegates with its single target and preserves prior results. Every unit
contributes source-ordered HP/status/seven-equipment terms with signed 16-bit running
priority. Two healthy allies and one enemy give priority 485. A Critical ally at
HP 10/max 50 reduces that total to 350; one actor against two different enemy teams
gives -485. Each unit still contributes 357 equipment points before team sign;
they cannot be cancelled as if every roster were one pair. Hit percent is 100.

All literal one-tile movement hypotheses above use one upkeep draw, not an
actual movement path, accuracy or formula draw. Staying both-unused refunds 40
before cap; moving refunds 20; moving after ACT refunds 0. A hypothetical northward
walk preserves an incoming South facing even though actual path heading is North.
No projected choices, position, CT or RNG are adopted into live state.

## Validation and remaining work

Thirteen independently authored source descriptions and three new Python tests
cover clocks/ties, HP thresholds, support propagation/early exit, lowest-share
ordering, asymmetric threats and multi-unit scores. Ten new managed cases also
cover pair parity, ownership/admission, compact/entry extents, exact/late RNG and
tick budgets, missing context/policies/lists, restored target facts, actor/order
replay and live-state isolation. All previous pair/variable-list command/turn
and recovery regressions retain their streams and results.

Locked restore and Release build pass with zero warnings/errors;
**4,335 .NET cases** (1,483 Framework, 1,107 AI) and **551 Python tests** pass.
Pinned source/prepared-data, guide, local-link and coverage checks pass. Seven
full TRX reports and logs are in ignored `scratch/roster-preparation-283-286/`.
Use the [existing pinned Pillow setup](accumulate-ai.md#validation). Historical
checkpoint snapshots and UI were not rerun. Owner drafts, including AbilityToggles,
are unchanged; no Framework/Content/host/editor or project/lock/package changes.

The [ZOD-287–289 follow-up](roster-ai-general-scenarios.md) now derives bounded
general admission/focus and movement scenarios. ZOD-290 next traces roster
cast-origin priorities and supported-action search. Full ability/target/effect
traversal, commands driven by a roster AI selection,
corpse movement/death scheduling, other statuses/maps/policies, automatic facing,
terminal routing and original frame RNG remain open. Existing AI command drivers
stay pair-bounded. Host/editor work, owner flags, broad replacement interfaces
and ZOD-106 remain separate/deferred.
