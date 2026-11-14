# Maintaining tests as the game grows

Tests protect observable contracts. Refactoring an internal method, adding a
separate rules implementation, or extending the supported domain should not
require rewriting unrelated original-game expectations. Case count is a report,
not a coverage target. Prefer a few readable scenarios with meaningful boundaries
over repeating the same assertion across every available permutation.

## Original rules, shared guarantees, and extensions

Original FFT compatibility regressions exercise the original implementation and
its selected definitions. Keep source-derived literals and documented quirks
explicit. Custom content should supply its own definitions; an alternative AI or
damage implementation should have its own expectations. Do not change original
expectations to make a custom implementation pass, or require custom behavior to
match original formulas. No general rules-selection interface is introduced by
this cleanup; the existing AI helpers are still opt-in, bounded implementations.

Shared guarantees include deterministic replay, input immutability, valid state
transitions and explicit RNG ownership. Test those against each applicable
implementation as alternatives become available. Expectations about the exact
number/order of original draws belong to original-rule coverage; a custom AI can
own a different deterministic draw schedule.

The current `SwordAi*Tests` classes use `Ruleset=OriginalFft`. Selected input
isolation/replay tests also use `Contract=QueryIsolation`. These labels identify
the current AI pilot, not a classification of the whole repository and not an
interface contract already executable against arbitrary implementations.

`Coverage=CurrentAdmission` identifies dedicated checks of current geometry,
weapon, numeric-envelope and certificate limitations. Those are intentional
guards today, not permanent restrictions on future game design. When broadening
an implementation, update the affected guard tests, add supported-domain cases,
and retain meaningful invalid-input and atomic-failure coverage. Mixed fixture
inventories still contain additional admission cases; the label is not exhaustive.

## Current correctness and historical evidence

Routine Python discovery validates current source-description fixtures, literal
arithmetic, proposal consistency, classification and malformed-input rejection.
It does not compare current source/test files with old managed hashes. These
Python descriptions do not execute C# or establish original-game equivalence;
the .NET regressions exercise current managed behavior.

Recorded hashes describe the files present at a particular milestone. The
separate `verify_historical_contracts.py` command exports tracked files from each
recorded Git commit to a temporary directory under ignored `scratch/`, then runs
that checkpoint's unchanged Python auditor with managed-baseline checks enabled.
Each of the nine checkpoints has an explicit full commit ID in the runner. It
uses a separate Python process for each export, so current imported modules
cannot satisfy a historical audit accidentally. The checkout, index and owner
drafts are untouched; temporary exports are removed on success or failure.

This audit requires the recorded commits in local Git history. Missing history
or corrupt recorded files fails the command; there is no working-tree fallback.
It audits historical descriptions and fingerprints, without rebuilding old C#,
consulting the reference checkout, executing C, or reading prepared original
assets. Use a historical verifier's optional reference/prepared-data arguments
from the corresponding snapshot when those additional checks are needed.

Existing verifier CLIs still check the directory passed to them. Running an old
verifier directly against changed current files can fail legitimately. Use the
snapshot runner for historical reproduction; do not refresh old hashes merely
to make a refactor pass. Keep new behavior evidence and any errata separately.
Future evidence should pin its source reference and implementation commit without
making the current test suite freeze all previously observed source files.

## Commands

From the repository root, using the selected .NET SDK and a Python 3 runtime:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
```

For AI iteration, after building:

```powershell
dotnet test tests/Zodiac.Framework.Ai.Tests/Zodiac.Framework.Ai.Tests.csproj -c Release --no-build --no-restore --filter 'Ruleset=OriginalFft'
dotnet test tests/Zodiac.Framework.Ai.Tests/Zodiac.Framework.Ai.Tests.csproj -c Release --no-build --no-restore --filter 'Contract=QueryIsolation'
dotnet test tests/Zodiac.Framework.Ai.Tests/Zodiac.Framework.Ai.Tests.csproj -c Release --no-build --no-restore --filter 'Coverage=CurrentAdmission'
```

Historical verification is explicit and independent of those correctness runs:

```powershell
python tools/verification/verify_historical_contracts.py --output scratch/historical-contracts.json
python tools/verification/verify_historical_contracts.py --checkpoint verify_ai_scan_contract
```

Reports use a new output path and never overwrite an existing report. The normal
CircleCI .NET jobs remain unchanged; neither a shallow checkout nor a routine
test run must fetch old history to test current behavior.

## What changed in ZOD-132

The comparison suite executes all 22 normalized public-query proposals once.
Four focused collection-order tests cover both actor identities and both source
slot orders, comparing complete results and preserving both inputs. They replace
the second execution of every proposal in reversed collection order.

Twenty reflection-based private rank-kernel cases were removed. Public-query
regressions still cover hit/proximity ordering, strict ties and real zero/negative
scores. Source-only signed extrema and priority ordering remain in the Python
literal-kernel suite; those broader values cannot all enter the current closed
same-weapon query. That is a coverage boundary, not evidence that C# implements
the general rank domain. Add public behavioral cases when that domain becomes
reachable. Null-argument checks now call public APIs directly, without asserting
private method names, exact signatures through reflection, or public method count.

The nine existing evidence manifests, fixtures and verifier implementations are
unchanged. Their routine inventory tests now check description consistency;
historical fingerprints run separately. The new runner has executable regressions
for refactored checkout files, changed current fixtures, preserved owner drafts,
corrupt historical fixtures, missing history and export path confinement.

No runtime, rules-selection interface, editor or original behavior changed.
Repository-wide test reorganization, further fixture/setup consolidation and
maintenance of non-AI structural tests remain separate work.

## ZOD-132 validation checkpoint — 2026-10-27 (historical)

Locked restore and Release build passed with zero warnings/errors. The focused
OriginalFft AI group passed 613 cases; the full solution passed 4,082 .NET cases
and Python discovery passed 477 tests. All nine historical audits passed at their
individual recorded commits. Logs, TRX and reports are under ignored
`scratch/test-cleanup-*`. The previous 4,119 .NET count decreased by 37: removing
20 private kernel cases and 22 repeated proposal permutations, adding four
collection-order cases and separating one MP admission check from null handling.
Four new historical-runner tests account for Python's increase from 473 to 477.

The read-only reference checkout remained clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. No new gameplay rule was translated,
no C/original execution or manual presentation review was performed, and owner
untracked definition drafts were preserved. The .NET build included the drafts
present during validation; they are excluded from this maintenance commit.

## ZOD-133 consolidation — 2026-10-27

The owner requested consolidation and removal of tests that would not serve the
completed game. Inventory of the ZOD-132 TRX reports found 4,082 executed cases
from 1,078 test methods in 141 classes. The largest repetitions crossed source
vectors with actor identity, input collection order, unrelated facing and RNG
seeds. The content/save/import/presentation groups contain distinct schema,
corruption, storage and visual-model cases; those were not removed merely to
reduce the total.

The suite now executes **3,548 cases from 1,074 methods in 139 classes**, a net
reduction of 534 cases (13.1%). This primarily removes redundant combinations,
not hundreds of separately written methods. Excluded permutations have not been
moved into loops or marked skipped. Existing scenario replays and independent
graph checks still use their own intentional loops.

The replacements and retirements are:

- Formula projection keeps all 16 target-direction/target-facing combinations,
  plus a separate four-case actor-facing invariance check. Its existing actor/
  collection-order tests and literal seed vectors remain. Critical sword attacks
  keep 32 target-facing/seed cases and a separate four-case actor-facing check.
- All 24 flat route vectors, 12 admitted facing-direction vectors, nine positive
  move proposals, 17 selected command paths and ten materialized score kernels
  remain active once each. Focused identity/order checks cover both actors,
  moved/unmoved queries, moves before/after Act, both candidate action orders,
  and living/dead projections. These checks assert known locations, HP, scores,
  choice flags and RNG counts as well as equality across permutations.
  Path/event equality compares immutable-array
  payloads instead of backing-storage identity; the replay test shares that check.
- Formula and candidate projection retain every draw prefix for their distinct
  schedules. Scoring checks zero, last insufficient, exact and spare budget for
  living/lethal/NotValuable outcomes. Pair comparison checks failure in either
  candidate, their boundary, exact fit and spare budget. Neither forwarding
  wrapper repeats all inner formula prefixes. The explicit long corpse-upkeep,
  tick-overflow, typed-rejection and replay cases remain.
- `SwordSettledFacingTests` no longer repeats 32 legacy cardinal, four damage/
  cap and 20 draw-prefix cases. Both sword overloads call `AttackCore`; current
  arithmetic/facing coverage lives in `FlatSwordKnockbackTests` and
  `AttackBattleSimulatorTests`, with off-map old/new overload equivalence checked
  in the former. Legacy actor identity, both action orders and journal-prefix
  replay remain in `SwordSettledFacingTests`. Its unused setup parameters were
  removed. The old settled-facing fixture remains historical evidence rather
  than a second active C# arithmetic matrix.
- `TilePositionTests` tested generated positional-record accessors/equality,
  without handwritten behavior. It was removed. `EngineIndependenceTests`
  reran a deterministic Relay sample around unrelated FFT calculations; it did
  not establish an assembly boundary. It and the now-unused Relay source link
  were removed from the framework test project. Dedicated engine/sample coverage
  remains in `Tessera.Engine.Tests`.
- Four flat-grid arithmetic/error tests duplicated engine coverage. The two
  Zodiac adapter tests retain parameter/validation-order, upper-layer coordinate
  conversion and immutable-output checks.
- Exact public method/signature/constructor-shape restrictions were removed from
  facing, path, Ready and dark-screen tests. Structural-only DSI-015 is retired
  from active .NET discovery; its original record is unchanged. Behavioral
  missing-input, forged-binding, stale/cross-branch receipt, pending-state and
  rollback tests remain, including reflection used to construct corrupt inputs.

These changes remove overlap in combat, movement and obsolete milestone checks.
They do not establish that all remaining tests are indispensable forever. Retire
temporary admission checks with the APIs/profiles they protect when those change;
do not preemptively stop checking a still-active implementation's safe failure.
Add new rules and content against their own definitions/implementations, and
expand shared contract coverage when a real alternative implementation exists.

Locked restore, zero-warning/error Release build, **3,548 .NET / 477 Python tests**
and all nine historical checkpoint audits pass. Validation caught setup/equality
errors in the new path fixture and a mistaken moved-unit expectation in the new
movement check. A moved unit still has physical geometry; only its AI origins
collapse to the current tile. Those test errors were corrected without changing
runtime behavior, then the full suite passed.
Logs, TRX, the before/after inventory and audit report are under ignored
`scratch/test-consolidation-*`. The reference stayed clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`; no new gameplay rule was translated.
Runtime, old evidence/fixtures/verifiers and owner definition drafts are unchanged.
No original/C execution or presentation review was performed. No AI scan feature
work was started during maintenance.
