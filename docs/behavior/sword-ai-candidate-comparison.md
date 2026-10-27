# Supplied sword candidate pair comparison

ZOD-129, 2026-10-27. Opt-in managed companion implementing the
[ZOD-128 contract](../architecture/zod-128-ai-comparison-contract.md). Fresh clean
read-only decomp reference: **11464943bbdbd2a0af022d90cb7f10455cf911df**.
The [implementation evidence](../evidence/ai/comparison-implementation-v1.json)
records source/history audits, test coverage and artifact fingerprints. The old
source-contract vectors/evidence remain historical and unchanged.

## Entry and admission

`SwordAiCandidateComparison.Evaluate(state, incumbent, challenger, weapon,
candidateProfile, scoreProfile, comparisonProfile, decisionRng)` accepts one
original snapshot, two supplied candidates for the same actor/opponent, a shared
nullable weapon, shared ZOD-125/127 profiles, a comparison certificate and an
explicit decision stream. Required null arguments throw ArgumentNullException;
a null weapon reaches the existing typed score/projection rejection.

`SwordAiComparisonProfile(ActorId, TargetId, FreshDecisionBaseline=false)` is an
immutable caller certificate. Omitted/default/false, invalid/self IDs or a binding
different from the incumbent gives UnsupportedComparisonProfile before scoring.
It asserts that original initial decision flags, targetability/relation and cached
actor delay belong to this settled state at fresh selection, after refresh and
reachability, before candidate simulation. These original fields are not all
represented in the managed snapshot; IDs alone cannot establish them. General
stale/resumed workspace caches are not admitted. Existing sword/movement/clock/
human/equipment/team/MP/Golem certificates continue to apply.

Different actor/target pairings or IDs missing from the exact two-unit state give
InvalidCandidatePair. Unchanged FlatOrdinaryMovement derives scenario-0-equivalent
AiOrigins on the original living state. Unsupported movement/geometry, including
initial Dead, gives UnsupportedMovementProfile. Either destination outside that
membership gives InvalidCacheDestination plus index 0/1. This preflight precedes
either score and never treats an unqueried source cache cell as zero. The closed
map remains dry, open, flat cost-1 lower ground, with each dimension 1..16.

The shared scoring certificate is validated inside the first unchanged score
query. Its rejection retains UnsupportedScoreProfile with failed index 0. Candidate
order/action-origin/weapon/choice checks likewise remain with the score/projection
query and retain their typed nested reasons. The companion does not duplicate a
general snapshot validator or silently infer omitted original facts.

## Baseline cache and fresh evaluations

The private cache records byte ActorDelay, signed-int TargetDelay, nullable
EligibleEnemyId and two destination/value pairs. For initial living None/Critical
units, delay is ceil(max(100−CT,0)/Speed). The actor's source field is a byte; all
admitted delays fit 0..100. An active actor has already reduced CT modulo 100,
so CT15/Speed5 means 17 ticks, not zero. The sole enemy contributes when target
delay <= actor delay; excluded statuses/control/team changes remain outside the
existing certified profile. Current original HP/status/CT/position is used.

For each validated destination the contribution is
`max(10 − abs(targetX−x) − abs(targetY−y), 0)`, or zero for an ineligible enemy.
No public cache, arbitrary row mask or distance claim is accepted. Values are
built once before scoring; target Dead/clock/choice changes in a projection do
not rebuild them. ActThenMove compares its final movement destination even
though its action formula originated from the actor's old position. Original
unwritten cells remain outside admission, rather than becoming zero-filled data.

Both evaluations call unchanged SwordAiCandidateScoring.Evaluate exactly once on
the **same original state** and shared inputs. The incumbent's returned decision
RNG feeds the challenger. The second candidate never starts from the first one's
hypothetical HP, coordinates or turn choices. Each score still performs its own
existing geometry/projection work; only cache/comparison adds zero random draws
and zero ticks. No external cached, copied or forged score/result is accepted.

## Comparison, outcomes and rollback

Every SwordAiComparedCandidate exposes computed DecidedMarker 0. Source selected
marker 255 and rank-reset sentinel −32767 are not pair inputs or absence values.
Two Scored evaluations compare signed priority descending, independent hit percent
descending, then baseline proximity ascending. A strict improvement replaces the
incumbent; an exact tie retains index 0. The result includes a typed Priority,
HitPercent, Proximity or Equal reason.

Only one score gives IncumbentOnly or ChallengerOnly, retaining that index even
for legitimate zero/negative priority. Both NotValuable give NoScoredCandidate,
NeitherScored, no retained index, and both fresh evaluation diagnostics. An absent
score is never assigned zero. In the admitted same-weapon, effect-free pair,
positive-hit projections share normal damage and absolute priority. The private
rank kernel nevertheless retains the traced signed-priority precedence; tests
exercise it separately with isolated source literals, not a broader public input.

SwordAiComparisonResult carries disposition/rejection, optional failed candidate
index, original typed scoring/projection rejection, nullable Outcome and returned
RNG. A successful Outcome has two immutable candidate/evaluation records, the
private cache, retained index and typed comparison reason. These are diagnostics,
not commands, active turns, receipts or execution authority.

Any failure returns the **original pair RNG** with no partial Outcome/cache or
successful evaluation payload. A first evaluation failure skips the second; a
late second failure retains index 1 and its nested reason, even after a private
successful incumbent score. This is the documented managed unsupported/exhaustion
transaction rule, not a claim that original C search refunds draws.

Successful losing and NotValuable evaluations keep their RNG progress. Cache and
comparison add none, including when the second score ends exactly at ulong.MAX
draw count. Live battle/turn RNG, fields, inputs and independent branches remain
unchanged. Typical normal pairs use 10 draws, two lethal projections here use 12,
one NotValuable plus a normal projection uses 9, and two NotValuable use 8; other
supported clock configurations may consume more upkeep draws.

## Coverage and reproduction

All **ACM-001..014** have coverage in **162 new managed cases across 18 methods**.
All 22 normalized pair proposals execute the actual entry with both collection
orders (44 cases). They use real 3x2 geometry, Move4, shared damage/weapon and
front/back target facing with matching class evade. Supplied normalized formula
results and membership booleans are not production inputs. A false membership
proposal is materialized as an occupied enemy destination; false pair identity
certification is materialized as a mismatched comparison binding. Reversed IDs,
source TieKeys, both action orders, critical/negative/zero scores, lethal cache,
NotValuable, replay and late draw/tick failure are exercised without editing old
fixtures or evidence.

Ten candidate-marker source rank rows execute the private kernel in both argument
orders (20 cases), including signed extremes and sentinel literals outside the
public pair's score range. The two chosen-marker rows remain source-only:
the pair has no marker input. Source multi-foe wrap, other layers/status timers
and unmarked-cell retention kernels also remain diagnostic descriptions, not
expanded production admission. Delay/cutoff tests and actual movement preflight
check the admitted equivalents. Reflection invokes only the private arithmetic
kernel for these isolated rank tests; it creates no public result entry point.

Locked restore, Release build with **zero warnings/errors**, **4,119 .NET tests**
and **452 Python tests** pass. All focused and full executable
runs passed with no implementation/test correction. SDK builds included the owner
drafts in Common/Ability/Item, which were neither edited nor committed. Before and
repeated final audits match the pinned 12 files / 27 functions, 110 historical
files and 31 managed baselines; new evidence fingerprints 115 preserved artifacts.
The old score audit also passes both prepared metadata records. No original/C/
emulator/native or additional manual presentation execution was performed.
An additional Status draft observed after validation is preserved and excluded;
the test results describe the compiled snapshot before that observation.

```powershell
dotnet restore Fft.sln --locked-mode -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/ai-comparison-129-artifacts
dotnet build Fft.sln -c Release --no-restore -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/ai-comparison-129-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/ai-comparison-129-artifacts --logger trx --results-directory scratch/<new-results>
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_comparison_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --output scratch/<new-audit>.json
```

The historical auditor intentionally still reports ZOD-128's unexecuted
production inventory; this new implementation evidence owns the completed ACM
mapping. Runtime has no C/reference/media dependency. No scene/content/save/editor,
full AI policy/interface, ranked list, lottery or authoritative RNG commit is
activated. Editor UI and ZOD-106 remain deferred.

**Next: ZOD-130**, source/design the ordinary sword candidate scan within one
supplied movement scenario. Trace emission order and evaluation RNG ownership
before adding enumeration. Do not repeatedly call this pair query as a tournament:
that would re-evaluate incumbents and consume extra draws. A future scan must
evaluate each emitted candidate once and retain its private incumbent score with
provenance; that boundary needs its own traced contract.
