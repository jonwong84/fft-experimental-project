# SDK and dependency reproducibility (ZOD-006)

The repository targets .NET 10 and selects SDK **10.0.402**, the installed stable Windows SDK inspected on 2026-10-22. `global.json` disables roll-forward and prerelease selection. Installing a newer SDK alongside it does not change this checkout's compiler or bundled NuGet selection. Install that exact SDK from Microsoft's [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0); run commands from the repository root so SDK selection applies.

`Directory.Packages.props` owns package versions: Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, and xunit.runner.visualstudio 3.1.4. Centralizing them preserves the previous versions; no dependency upgrade is included. All projects, including projects with no NuGet dependencies and the new Zodiac engine/test projects, have committed `packages.lock.json` files recording resolved dependencies and content hashes. `Directory.Build.props` enables generation and defaults to locked mode. A normal restore fails for inconsistent dependency declarations instead of silently rewriting locks.

## Windows and Linux commands

With SDK 10.0.402 installed, use the same commands in PowerShell or a Linux shell:

```text
dotnet --version
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln --configuration Release --no-restore
dotnet test Fft.sln --configuration Release --no-build --no-restore
```

The first command must print 10.0.402. Build and tests need no BIN, prepared assets, C toolchain, or container. Restore needs access to NuGet packages or a populated local cache. Locked mode protects package resolution; it does not make downloads available offline.

CircleCI uses `mcr.microsoft.com/dotnet/sdk:10.0.402` and explicitly restores in locked mode before its existing build/test/Sonar stages. The fixed SDK tag aligns compiler selection with Windows; the base image is not pinned by digest, so this is not a claim of byte-for-byte OS/image reproducibility. SonarScanner remains independently versioned at 12.0.0. Apt utilities and external Sonar analysis are not part of the locked NuGet graph.

## Deliberate upgrades and new projects

1. For SDK upgrades, install the proposed stable version, then update both `global.json` and the CircleCI SDK tag in one change. For package upgrades, edit `Directory.Packages.props`; project PackageReference entries remain versionless.
2. Regenerate locks intentionally with `dotnet restore Fft.sln -p:RestoreLockedMode=false --force-evaluate`. Review all dependency/version/content-hash changes, including transitive changes. New projects must be registered in the solution and have their own lock file generated through this step.
3. Run the locked restore/build/test commands above, review the working-tree diff, and commit configuration plus all affected locks together. Run CI to verify Linux and Sonar separately. Never use an unlocked restore in CI to mask drift.

Package removal, target-framework changes and SDK changes can alter lock files even without a direct version change. Treat these as reviewed dependency changes. Retaining multiple local SDK versions is supported; roll-forward remains disabled until an explicit policy change is reviewed.

## Validation and limits

Antigravity began this work by introducing central package versions, SDK configuration and lock files. The coordinating agent repaired duplicate closing tags in two test projects and completed locked-mode defaults, CI alignment, documentation and verification. On Windows with SDK 10.0.402, locked solution restore passed, Release build passed with zero warnings/errors, and all 155 tests passed (zero failures/skips). All 11 projects have lock files and parse as XML; whitespace checks passed. Restore/build/tests ran outside the sandbox after the sandboxed restore exited without diagnostics. Windows execution does not establish a successful Linux CI run; Linux and remote Sonar remain unverified until CI executes.

Policy references: Microsoft's [SDK selection rules](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json), [package lock files and locked mode](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files), and [central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management).
