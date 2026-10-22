# Deterministic movement fixture runner (ZOD-011)

The runner lives in `tests/Fft.Core.Tests/Fixtures/MovementFixtureRunner.cs`. It is test infrastructure for [fixture v1](fixture-format.md), not a simulation/file dependency or public runtime API. Committed JSON is copied into test output. Run from the repository root:

```text
dotnet test tests/Fft.Core.Tests/Fft.Core.Tests.csproj -c Release --filter FullyQualifiedName~MovementFixtureRunnerTests
```

The accepted-step fixture and the three-command rejection replay execute `BattleSimulator.Apply` sequentially. Each expected command compares acceptance/rejection first, then state identity, the immutable map's dimensions and all tile fields, ordered units and their complete movement profiles, explicit null RNG state, and ordered events. The input state/map are projected before and after each command to detect mutation. Flag sets and terrain-cost maps are normalized by symbolic name/stable surface ID; unit and event arrays are never sorted. Unknown schema fields, duplicates, invalid IDs, enum values, integer widths, evidence links and inconsistent assertion shapes are rejected before executing any command.

Reports distinguish invalid fixture, failed assertion, not executed and passed assertion. Each report retains fixture/policy identity, implementation commit (from the build's assembly informational version), reference commit and a JSON copy of target/evidence/preconditions/exclusions/unknowns/capture metadata. The first mismatch includes command index, assertion evidence kind/confidence, field path and expected/actual value. Repository commit identity does not certify an uncommitted worktree; inspect the working tree alongside local results. No persistent hash/fingerprint is defined.

`map-boundary.source-description.json` is validated but explicitly not executed. Source cases are research descriptions; the runner never calls C, silently converts descriptions to tests, or reports them as original observations. The new rejection replay is separately synthetic. It tests rejection before acceptance and a subsequent rejection using the previously returned state, without changing historical source evidence.

Capture metadata is checked for safe relative scratch paths and basic hash/length shape, but captures are never opened by this baseline runner. Their availability and any claimed observed/executable provenance are not independently verified here. No fixture needs a BIN, prepared media, emulator, network, C toolchain or graphics backend. Fixture confidence remains an author-supplied evidence claim; a managed pass does not upgrade it to original-game equivalence.

The implementation deliberately supports only the flat prototype policy, move-unit commands, movement events and null RNG state. More rules, commands, event kinds, RNG state or differential capture execution require an explicit contract extension. The existing sprite fixture is not consumed.

Validation on Windows SDK 10.0.402: locked restore passed; Release solution build passed with zero warnings/errors; all **182** tests passed, including 18 fixture-runner cases. Coverage includes repeat replay reports, retained provenance, description-only status, deliberate state/event changes, extra events, unsupported versions, missing/extra/duplicate fields, invalid numeric/enum/ID data, missing/unresolved evidence, unsafe capture paths, duplicate flags and order-independent flag comparison. Linux CI was not executed locally. No production gameplay behavior changed.
