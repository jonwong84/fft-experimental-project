# Pure fresh post-ACT preparation — ZOD-179

Reference: read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.
Composes [post-ACT refresh](sword-ai-post-act-refresh.md) and the restored-state
policy/reach ordering in the [source contract](../architecture/zod-176-ai-act-first.md).

`SwordAiPreparation.EvaluateAfterAct` owns one fresh post-ACT refresh, then
derives full ordinary reachable origins and NonBlue policy/focus/delays from the
original actual snapshot. It takes no cached refresh or geometry result and
does not fabricate ACT-unused state. Existing moved/unmoved preparation APIs
retain their original choice admission. Source/list/cache/team and ordinary
terrain/profile facts remain explicit, including FreshRefreshEntry: ACT used is
not proof that the early no-progress guard passed.

The additive immutable ActorAttackUsable diagnostic is false after ACT and true
for the existing pre-ACT entries. It describes the closed ordinary Attack list,
not a general ability framework. Source ability preparation computes reach before
clearing actor usable bits, so enemy reach remains raw Move+1, ally reach Move
and random-use threshold0 even though another Attack is unavailable. Current
post-ACT helpers must use this mode rather than infer zero reach or rerun a
pre-ACT Attack scan. No score, ranked action or executable command is produced.

Temporary refresh CT is min(60, liveCT+20), using only unused Move. Live policy
delays/proximity and reachable geometry never use the refresh clock's temporary
units. No additional draw follows refresh, including at MAX. Any rejection hides
the whole owned prefix and returns incoming hypothetical RNG. Live CT, choices,
HP/status, facing, positions, ticks and RNG remain unchanged.

After actual ACT-first hit/miss, preparation returns the same one-draw streams as
refresh: word3535774167/count25 or word1343201072/count30. Live actor/peer CT15/95
give delays17/1, proximity9 and four origins around actor x2, with target x1
excluded. Seed3/power1 produces an actual critical push to target x0: preparation
focuses x0, returns five origins including vacated x1 and proximity8, while reach
remains2 and Attack is unusable. Power8 leaves actual target HP10/Critical; those
HP/status flags feed preparation. The original pre-ACT target/tile data is not
reused. Move0 legitimately yields a singleton origin and enemy reach1.

Five new scenario tests cover these actual outcomes, source reach/usable ordering,
owned refresh equivalence, no extra draws, exact limits, legacy/source/terminal
rejection and both actor/order replay. Twenty-two focused post-ACT/preparation
cases pass. Locked restore, zero-warning/error Release build, **3,958 .NET cases /
516 Python tests** and the clean pinned source audit pass. Logs and seven full
TRX files are ignored under `scratch/ai-post-act-preparation-179/`. Owner content
drafts were included in validation and excluded from these commits. No C/original
execution or UI review; bounded checks do not establish full AI equivalence.

ZOD-180 adds explicit pure post-ACT move-only tile scoring, with no unused-ACT
refund and existing fresh score arithmetic. Scenario generation, empty-ability
selection/lottery and live Move/Wait handoff remain subsequent work. Current
pre-ACT selectors, deferred interfaces and host/editor activation are unchanged.
