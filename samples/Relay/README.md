# Relay: a neutral Tessera sample (ZOD-009)

Relay is an invented courier scenario, not FFT content. A courier starts at (0,0,0) on a 4-by-2 board and moves at most two cardinal steps per command. Reaching (3,1,0) increments a delivery counter. The direct shortcut is rejected; moving through (2,0,0) succeeds in two commands. Rejection returns the same immutable state; acceptance creates a new one.

`RelayScenario.cs` uses only `Tessera.Engine` and defines its own tiny state and move outcome. It has no asset, file, clock, RNG, FFT identifier or compatibility dependency. Delivery rules are sample-owned, not added to the engine. This proves the bounded range slice can drive a second scenario, not that Tessera is a complete battle framework.

The source is linked into `Tessera.Engine.Tests` so it compiles and executes without any FFT reference. Run it from the repository root:

```text
dotnet test tests/Tessera.Engine.Tests/Tessera.Engine.Tests.csproj -c Release --filter FullyQualifiedName~RelayScenarioTests
```

`Zodiac.Framework.Tests` separately compiles the same sample source and varies an FFT movement profile from zero to two Move: FFT range changes while Relay's delivery result stays equal. This integration check adds no FFT dependency to the sample or engine test project.

`Directory.Build.targets` enforces the current narrow project-reference allowlist for project names beginning `Tessera.Engine`: only a reference named `Tessera.Engine` is permitted (the engine itself currently has no project references). It runs before reference resolution/compilation, so CircleCI's existing solution build fails on an introduced FFT project reference, including one supplied through imported MSBuild items. It also rejects other project references until a new engine dependency is deliberately reviewed. This is a project-reference boundary check, not a general security boundary or a package/binary dependency scanner. Renaming projects or adding engine projects with other naming conventions requires updating the policy.

Validation on Windows SDK 10.0.402: locked restore passed; Release solution build passed with zero warnings/errors; all 164 tests passed (22 engine, 60 Core, 82 other FFT). An ignored temporary `Tessera.Engine.Probe.proj` under `scratch/boundary-check/` imported the real target and referenced `Zodiac.Framework`: invoking `CheckTesseraProjectBoundary` failed with the expected boundary diagnostic. The ordinary engine/test references passed during the solution build. No original-game source or behavior was translated, no game assets were used, and Linux CI was not executed locally.
