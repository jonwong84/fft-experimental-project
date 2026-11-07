# Ordinary spell aim search — ZOD-418 source study

This study closes the fixed-origin source-tracing milestone and selects ZOD-424
as the next implementation child. It does not implement candidate search.
Managed baseline is `230faf2`; the read-only fft_decomp revision is
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/spell-aim-search-source-v1.json) pins nine files
and twenty-four functions. No original game or reference C was executed.

## Callers and stages

`ai_ability_outcome_evaluate`,0x8019AF18, prepares the considered command and
resets its valuable-target mask from initialized active targets. Unsupported
ability/random-use gates precede search. Map-wide/Math and reflected searches
are distinct branches. For an ordinary ability it filters useful targets,
evaluates useful self-target alternatives, then performs unit and tile searches.

With primary FollowTarget set, the command uses target type6. ConsumeAim selects
one unit-target pass; otherwise the source evaluates complementary target groups:
first those which can move by the considered charge deadline, then those which
cannot. The saved valuable-target mask is restored afterward. A zero-area
following ability stops after the unit alternatives. Otherwise tile-target
type5 follows, after `target_enemies_with_move_opportunity_exclude` removes
valuable enemies that can move by that deadline for an enemy-targeting ability.
It does not apply that exclusion universally to allied targets.

`unit_can_move_by_charge_deadline` uses the considered entry's CT, not a newly
prepared actual counter. Instant abilities return false. Otherwise a unit's
predicted turn must be due by that CT and it must satisfy movement eligibility
at the deadline. Considered CT and actual CT retain the already-documented loader
and high-bit distinction. Broader statuses, Reraise, Reflect, self-origin retries
and multiple movement origins remain separate from the selected runtime child.

## Masks and one fixed origin

`ai_cast_aim_tiles_build`,0x8019C3D4, produces cast-origin and aim masks. For
unit targeting, possible aims start at useful units' coordinates. For tile
targeting, aims include centers whose AoE can cover those units. Masks are
initially copied to both levels and intersected with walkable panels.
For a FollowTarget ability on a tile pass, the source XOR-removes each valuable
unit's own-layer tile; jumping units receive the same treatment. Coincident
positions/flag combinations must not be silently simplified to ordinary sets.

`origin_targetable_mask_build` uses 16-bit rows with bit0x8000 representing X0.
Radii0..7 have explicit row templates. Radii8..15 read beyond that table into
status-priority values; radius16 or more fills both masks and bypasses the linear
filter. These are observed source quirks, not permission to widen managed masks
with a normal diamond. The first implementation child admits only certified
dimensions and row-template radii; custom wider targeting remains a separate rule.

`origin_aim_tiles_evaluate`,0x8019CD9C, selects the actual cast origin according
to action/move order and intersects aims with selectable ability range. A raw
considered range of zero seeds only the origin's actual layer. For nonzero range
it calls `target_range_set_for_action`; it explicitly tests only result3
(NoPanels), rather than converting every nonzero result to failure. The target
choice mode does not decide the AI command's unit-versus-tile type. Existing
managed supplied-aim capture is stricter, so the next runtime child must certify
consistent primary FollowTarget and secondary choice flags instead of treating
inconsistent combinations as equivalent.

Enumeration is layer0 then layer1, increasing Y, then increasing X. Unit-target
or trajectory candidates look up the first present source unit slot on that
panel (`target_unit_on_panel_find`); an empty panel skips evaluation. Direct/arc
checks have their own paths and Item's adjacent exception. They are outside the
ordinary spell search child. Initial selectable range belongs here; delayed
readiness resolves the stored aim and affected area without repeating it.

## Simulation, RNG and early exits

Each evaluated aim calls `ai_action_evaluate`,0x8019D308, which invokes
`ai_action_simulate_and_score`. Candidate snapshots restore selected unit state
while the shared decision RNG continues. A nonvaluable result can consume real
formula/evasion draws and must not restart the RNG for the next aim. The current
`OrdinarySpellAiCandidateQuery` already covers that bounded simulation contract,
including private state, ready formula work and atomic unsupported-context exits.
Reusing returned diagnostics as a later trusted candidate is not permitted.

The source may consume an aim after evaluation, including a self-outcome change
from strike processing, so later origins skip it. Zero remaining aims stops the
origin loops. `ability_processing_done == 0` also abandons the origin after the
evaluation call. An unresolved charge at the255-call cap can still update the
best rank with hit255 before this early exit; it is not simply an invalid or
zero-hit candidate. Resume flags re-enter the suspended evaluation without
reinitializing it. Frame suspension and its RNG remain deferred.

## Ranking reuse

`rank_compare` compares decided marker, signed16 priority, then byte hit.
Fresh candidates share marker0. `ai_rank_is_higher` breaks equal ranks with lower
foe proximity; exact rank/proximity ties retain the first candidate. At the
same origin/destination, proximity is equal, so stable source aim order matters.
`ai_action_evaluate` retains a candidate only on a strict improvement.

`ai_best_rank_insert` requires a strict rank improvement over the movement
baseline before considering the list. Better proximity alone cannot pass an
equal baseline. Candidate search is not maximum damage or expected damage.
The existing `AiCandidateRank`, `AiRankedInsertion` and ranked/lottery owners
already implement these distinctions for the admitted roster. They should be
reused by a later composed magic search, not duplicated as a spell-specific rank.

## Independently checkable descriptions and next child

[The fixture](../fixtures/behavior/spell-aim-search-v1.json) contains four literal
aim-mask examples, four unit/tile stage-order examples and seven rank/baseline
examples, including exact ties, signed priority and unresolved hit255.
`verify_spell_aim_search.py` reconstructs the bounded source row-template masks,
walkable/range intersections and scan order, stage dispatch and rank keys.
Six Python tests exercise the literals, mutation detection, input isolation and
rejection of the table-overread radius. This is a source description model;
it neither runs C nor establishes that C# already performs full aim search.
Existing managed candidate/clock tests protect RNG, NotValuable and unresolved
outcomes through real public queries.

ZOD-424 will implement a fresh fixed-origin ordinary aim-list query using a
definition-bound catalog, current usefulness and certified source masks/choice
flags. It must preserve layer/Y/X order, unit-before-tile stages and deadline
groups, and return diagnostics without command authority. Begin with ordinary
neutral HP abilities, no self/Reflect/trajectory expansion, safe row-template
radii and current identity/geometry guards. Unsupported combinations reject.
Actual ranking, multiple origins, full ability dispatch, lottery, automatic
turns, statuses and frame work remain later children. The
[ZOD-423 pathway guide](../development/action-pipelines.md) now makes the existing
entry points and the future search boundary easy to find.
