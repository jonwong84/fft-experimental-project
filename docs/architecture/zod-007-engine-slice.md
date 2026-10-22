# ZOD-007: First Zodiac engine slice

Date: 2026-10-22. Managed baseline: `1782ce1555922e16558f85a259a72ff3ed665329`.
Implements the bounds-only calculation selected by the [ZOD-001 audit](zod-001-type-ownership.md), under the independent engine/game naming decision in [ADR 0004](../adr/0004-zodiac-engine-and-fft-game-boundary.md).

## Implemented contract

`Zodiac.Engine` is a headless .NET 10 library with no project or package references.
It contains three types:

- `GridPosition`: immutable signed 32-bit X/Y/layer coordinates. Layer is an index, not height. Position values may be outside a particular bounds instance; the calculation validates its origin.
- `GridBounds`: immutable positive width/depth/layer count and a half-open `Contains` predicate. A sealed record with get-only properties prevents an invalid zero-initialized bounds value; null is rejected by the calculation. Bounds store dimensions only and do not allocate tiles or multiply dimensions into a fixed-size map capacity.
- `FlatGridMovementRange.Calculate(bounds, origin, move)`: immutable set of all other positions reachable within a nonnegative number of cardinal unit-cost steps on the origin layer, clipped to the rectangle. Zero returns empty. There are no layer transitions, obstacles, costs, occupancy, elevation, game identities, state transitions, or path output.

The kernel retains the existing managed breadth-first algorithm and internal west/east/north/south neighbor order. Inputs and arithmetic remain signed 32-bit integers; guarded coordinate increments stay in bounds, and distance increments occur only below the budget. A very large rectangle/range still requires work and memory proportional to the visited positions; this slice adds no arbitrary size cap or cancellation policy.

Determinism means equal inputs produce equal **set membership**. Neither frozen-set enumeration nor queue order is a persistent ordering, path-selection, event, or replay contract. Tests compare membership using independent Manhattan-distance expectations, consistent with [fixture comparison guidance](../behavior/fixture-format.md). No fixture runner is added.

## Boundary and integration status

The engine accepts only its own coordinates, bounds and an integer budget. It has no FFT references, game data, file/input/platform services, clock, RNG, or C-reference dependency. Its tests construct only synthetic engine inputs. No original-game rule is translated or newly traced; this is preservation of the existing managed flat prototype, not evidence of PS1 movement equivalence.

`Fft.Core` and every existing FFT project/type remain unchanged. The engine calculation currently exists alongside the FFT prototype; **ZOD-008** owns replacing that calculation through the existing FFT-owned facade, preserving FFT validations and transition regressions. The separate terrain-aware proof remains untouched. The independent second sample and automated forbidden-reference check remain ZOD-009.

Both new projects are registered in `Fft.sln`; existing CircleCI solution restore/build/test commands include them without CI changes. Engine tests use the same package versions as existing test projects. SDK/dependency policy remains ZOD-006 work.

## Verification

Run from the repository root on Windows:

```powershell
dotnet test tests/Zodiac.Engine.Tests/Zodiac.Engine.Tests.csproj --configuration Release
```

Coverage includes zero budget, single-cell bounds, center/corner clipping, non-square rectangles, same-layer membership, oversized and maximum integer budgets, maximum integer dimensions with a tiny range, invalid dimensions/origins/budget, validation precedence, position equality, and immutable outputs/inputs. All expected memberships are synthetic specifications; tests do not load assets or the reference checkout.

Validated locally with SDK 10.0.402: the new project restored and compiled; all 21 engine tests passed. `dotnet build Fft.sln --configuration Release --no-restore` passed with zero warnings/errors, and `dotnet test Fft.sln --configuration Release --no-build --no-restore` passed all 155 tests (21 engine, 134 existing FFT). The sandboxed test host could not connect; rerunning build/tests outside the sandbox succeeded. Linux CI was not executed locally. Subsequent ZOD-006 integration includes both new projects in central package management and lock-file coverage; the coordinator repeated locked restore, Release build and all 155 tests successfully. See the [SDK and dependency policy](../development/build-reproducibility.md).
