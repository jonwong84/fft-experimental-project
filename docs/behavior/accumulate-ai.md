# Bounded Accumulate AI — ZOD-230–232

Implementation follows the [ZOD-229 source study](../architecture/zod-229-accumulate-ai.md),
with read-only fft_decomp HEAD rechecked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Source fingerprints, record identity,
call order and arithmetic descriptions are in that study's
[manifest](../evidence/ai/accumulate-contract-v1.json). No C/original code executes
in the managed runtime. Owner drafts remain separate.

Implemented 2026-11-04 from managed base `525974b`. These three completed tickets
extend pure evaluation without activating an authored host or adding command
authority. Existing Attack/Stone and both admitted team paths remain supported.
The [implementation receipt](../evidence/ai/accumulate-implementation-v1.json)
records the new entry points, tests, literal stream and validation boundaries.

## Supplied projection and scoring — ZOD-230

`AccumulateAiCandidateProjection` accepts a supplied post-usefulness candidate,
explicit self-active/useful assertion, zero-bonus immediate effect/grants,
ordinary movement and effect-free clock certificate. Actor and opponent are
distinct; the effect targets actor, and opponent identifies the negative absolute
score contribution. Both orderings and an already-used Move are admitted when
the destination is an admitted AI origin. PA must be 1..99 for both units.

It owns a private projection, calls `AccumulateFormula.IncreasePhysicalAttack`,
preserves HP/status, consumes hypothetical Act, and reuses the existing candidate
state and simulation clock. Before/after PA are diagnostics in the self outcome,
not a live snapshot or a mutation of immutable definitions. Candidate resolution
and scoring add no random draws; only the hypothetical clock consumes RNG.
Unsupported input or exhausted ticks/draws return the incoming decision stream
and no partial outcome. Live RNG, choices, PA, position and events are untouched.

`AccumulateAiCandidateScoring` owns a fresh projection and supplies the source
allied-stat-hit credit 3 and hit100 to shared absolute scoring. Existing damage
and move-only callers keep initial priority zero. No public method accepts a
cached score or result as trusted input. PA99 still scores a stat hit, and no
direct PA or future damage term is invented. Full/full maxHP50 scores 3;
Critical actor HP10 versus full enemy scores −132, the reverse scores 138.

## Fresh closed-list preparation and usefulness — ZOD-231

`AccumulateAiPreparation` derives preparation for two ordinary human lists in source
order **Attack, Accumulate, Throw Stone, unusable non-Frog Frog Attack, terminator**.
Require explicit original-list/retained-policy assertions and actual grants for
both abilities on both units. Reuse the shared refresh/restored-state preparation;
do not falsely assert the existing Attack/Stone-only list certificate.

Initialization counts four entries, two evadeable: quarter share 2, MP/Silence
shares 0. Each living unit has two harmful and one helpful usable abilities,
ranged true, heal-allies false and helpful share floor(128/3)=42 below 77.
The actor's Mostly Support flag is derived false; the hostile peer's retained
flag is unrepresented (nullable diagnostics), since source updates allies only.
Enemy harmful/physical totals remain 2. Enemy reach is Move+1, ally reach Move.
Accumulate is force-consume and consumes evaluated aim tiles, is not exhaustive,
has range/area/CT/cost0, and causes no preparation classifier draw. The ordinary
NonBlue and certified Blue AI-controlled HP*128/maxHP>=45 branches share this
derivation. Unknown/low-HP/player-controlled Blue policies remain unsupported.

`AccumulateAiUsefulness` is a separate entry-stage operation, because movement scoring
and earlier list entries precede it in a full search. In the certified active
living pair it visits source slots in order: the enemy is Adverse without a
draw; eligible self consumes exactly one parity coin (even Useful, odd None),
even at PA99. No random-use draw, per-origin coin, general classifier or cached
once-per-turn coin is introduced. Later orchestration must place this operation
at the actual entry position, after earlier entries have retained their RNG.

## One map-target entry against a supplied movement baseline — ZOD-232

`AccumulateAiMapEntry` privately owns fresh preparation, movement baseline,
usefulness, ACT-first and distinct-destination Move-first self scoring in that
order. It compares strict ranks with the existing source rank helper and reports the
best entry/admission against the movement baseline. No cached preparation,
baseline, usefulness or score can enter as authority. A supplied reachable
destination is a source-scenario boundary, not a claim to derive D0/D1 for a
three-action decision. A destination equal to the actor's origin skips the
second origin. Full search must retain earlier Attack evaluation draws before
the usefulness coin; this isolated component query does not execute that prefix.

An odd usefulness coin stops the entry before candidate clocks. Both candidate
orders start from the original PA and live state; they share only the continuing
decision RNG. Complete score/proximity ties retain ACT-first. Failure at any
stage rolls the whole query back, including preparation/baseline/coin draws.
Use existing movement and rank machinery; do not create another lottery.

This entry is independently checkable integration of the map-target path, not
the entire three-action search. Ranking alongside Attack/Stone requires source
order Attack → Accumulate → Throw Stone and privately retained ranks/RNG; that
composition is now implemented by [ZOD-234–236](basic-skill-ai-selection.md) for
supplied known-score scenarios. [ZOD-238/239](immediate-self-act.md) add the real
self-ACT boundary and fresh ACT-first adapter. Move-first/post-ACT integration
remains separate work. ZOD-106, editor
and host activation stay deferred; boosted PA remains battle-only.

## Validation

Locked restore and full Release build pass with zero warnings/errors. All
**4,156 .NET cases** (952 AI, including 24 new cases) and **539 Python tests**
pass. Source/prepared-data and complete algorithm-guide audits pass at the pinned
revision. Seven full TRX reports and logs are under ignored
`scratch/accumulate-ai-230-232/`. Owner drafts were included in validation and
remain excluded from this change's commit. Historical checkpoint audits were
not repeated; their unchanged snapshots last passed at ZOD-158.

Reproduce with the pinned SDK and a Python runtime with repository-pinned
Pillow 12.3.0 (`tools/verification/requirements-visual-comparison.txt`). On this
workstation, PYTHONPATH points to the existing ignored
`scratch/accumulate-ai-229/python-deps` and TEMP/TMP to its `python-temp` folder.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_accumulate_contract.py --reference C:/repositories/fft_decomp --prepared scratch/all-data-extraction/SCUS_942.21
python tools/verification/verify_ai_algorithm_guide.py --reference C:/repositories/fft_decomp
```

Tests use literal source/RNG/clock vectors, cap and
negative-score cases, query isolation, exact-budget/late-failure rollback and
existing Attack/Stone/Blue regressions. The original-frame RNG stream and retail
observations remain outside these source-derived managed proofs.
