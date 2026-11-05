# Computed flat roster direct trajectory — ZOD-300

Implemented 2026-11-05 from managed base `5df5cc0`, following
[ZOD-299](../architecture/zod-299-roster-stone-trajectory.md). The read-only
reference remains clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Source receipt](../evidence/ai/roster-trajectory-source-v1.json): eleven files,
fourteen functions, independently checked resident SDK table identity.

`RosterAiDirectTrajectory.Evaluate` owns a pure computed range1..4 query from
one supplied legal actor origin to one other unit. `RosterAiProjectileHeights`
copies distinct actual byte sprite heights with exact whole-roster membership;
missing/extra/unknown bindings reject. Height0 is meaningful, not unknown.
The existing simulation/movement profiles admit initially living compact16,
ordinary ground height10, no water/slopes/mounts/Jump/unrepresented effects and
an initial legal unoccupied endpoint. The target need not be an enemy: collision
is geometry, independent of AI usefulness. Every other unit is an obstacle
regardless of team, in source order, with its initial live position and height.

The query computes the source's SDK-normalized distance and discrete Q12 samples,
retaining negative arithmetic shifts, strict +/-8 horizontal and vertical box
boundaries, zero-height exclusions and the target's zero-raise fallback12.
It tests terrain first and reports the first blocking unit/sample/point, total
sample count and approximate Q12 distance. Accepted blocked differs from an
unsupported query. Input state, choices, RNG and all occupants remain unchanged;
there is no effect, candidate score or live command authority.

SDK factors are calculated using integer root only **after** source normalization.
The offline prepared audit verifies all192 factors against the pinned resident
table, including its SHA-256. No raw table, prepared file, C code, SDK or I/O is
required at runtime. A sqrt of the original distance would not preserve samples:
the oblique delta84/28 gives source distance361920, not the exact root distance.

Fourteen independent [source tapes](../fixtures/behavior/roster-trajectory-contract-v1.json)
cover ally/other-team blockers, height0/20/21/255 boundaries, reverse direction,
diagonal blocked/clear, oblique near-corner samples, unequal endpoint heights,
zero-target fallback, actor origin substitution and endpoint exclusions.
Six managed cases also cover copied metadata/exact membership, legal origins,
range/context failures, replay/reordered storage and compact16 versus uncapped
profile storage. Three Python cases check source tapes and approximation limits.

Validation: locked restore, zero-warning/error Release build, **4,384 .NET cases**
(1,156 AI, 1,483 Framework), **569 Python tests**, source/prepared table/guide,
coverage, links and unchanged owner-draft checks. Logs are under ignored
`scratch/roster-trajectory-299-300/`;
[implementation receipt](../evidence/ai/roster-trajectory-implementation-v1.json).

This corrects ZOD-297's mistaken claim that the SDK implementation was absent;
the capitalized filename is present at the same pinned revision. ZOD-298's
supplied-clear candidate API remains valid and unchanged. ZOD-301 next composes
computed geometry with one fresh isolated ranked Stone occurrence. Full-list
ranking/lottery, roster commands/turns, other terrain/status/ability policies,
complete corpse lifecycle, facing, terminal/frame RNG and all documented
host/editor/owner-draft/interface/ZOD-106 deferrals remain separate.
