# ZOD-034 — Windows CI and launch validation

The automated Windows lane builds/tests without a desktop or original assets and
checks a private **win-x64 self-contained folder** of the synthetic client. A separate
manual check establishes graphical launch on a clean Windows machine. This does
not add an installer, public release, original game content or campaign support.

## CI paths and reports

`.circleci/config.yml` runs two independent jobs on `main`, `feature/*` and `codex/*`:

- Linux retains the pinned SDK container, locked solution restore, Release build,
  headless tests and Sonar analysis. Sonar still uses the `global-v1` context.
- Windows uses CircleCI's `windows.medium` machine executor and
  `windows-server-2022-gui:current`, with Windows PowerShell 5.1. It installs the
  exact x64 SDK from `global.json` using Microsoft's installer, then locked-restores
  the portable solution, explicitly builds Core/Engine without Client/MonoGame,
  builds the solution and runs its headless tests. The job needs no Sonar secrets.
  It then restores the separate Windows publish graph in locked mode, publishes,
  and invokes the published executable from a new empty working directory.

Both jobs retain TRX and JUnit XML under ignored `scratch/ci`; Windows also keeps
build binary logs. Linux build diagnostics remain in the CircleCI step log, avoiding
archiving a Sonar-instrumented build's configuration and credentials in a binary log.
JUnit is uploaded to CircleCI's Tests view; raw reports and logs are downloadable
artifacts. Report conversion runs even after test failure and never turns a failed
test command into success. `trx2junit` is pinned at **2.1.0**, installed with
`--allow-roll-forward` so the tool can use .NET 10. Like SonarScanner, this tool is
independently versioned rather than covered by application NuGet locks.

Windows artifacts additionally contain `windows-launch/bundle`, a per-file SHA-256
inventory, publish logs and `launch-check.json`. The bundle is private build output,
not a release. Download **the entire bundle folder**, retaining all DLLs and notices.
Do not transfer only `Fft.Client.exe`. The SDK bootstrap/tool folders are build tools,
not part of the client bundle. Artifacts contain no local BIN/prepared assets.

The Windows image's `gui` name does not imply a display check: automation never
starts a game window. Image tags and the Microsoft installer endpoint can change;
SDK and application dependency selection are pinned, OS bytes are not.

Sources checked on 2026-10-23: [CircleCI Windows executor](https://circleci.com/docs/guides/execute/using-windows/),
[test results](https://circleci.com/docs/guides/test/collect-test-data/),
[Microsoft SDK installer](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script).

## Reproduce the bundle check locally

Install the exact SDK from `global.json`; use x64 Windows PowerShell 5.1 or PowerShell 7
from the repository root. The output must be a fresh directory inside ignored `scratch`:

```powershell
./tools/ci/Publish-WindowsClient.ps1 -OutputRoot scratch/ci/local-034-001
```

An alternate SDK executable can be supplied with `-DotNet <path-to-dotnet.exe>`.
The script refuses to overwrite output and propagates failed restore/publish/launch
checks. It imposes a 30-second limit on the child client. No test requires the
reference checkout, BIN, BIOS, importer, prepared files, GPU or audio device.

The script requires the published runtime DLLs, managed dependencies, SDL/OpenAL
DLLs and notices, then runs `Fft.Client.exe --scenario two-unit-battle`. It verifies
twelve accepted commands, first-unit victory, HP `25/0`, tick `20` and RNG
`2473755763/22`. The child has no shared-runtime root configured and runs outside
the bundle/repository working directory. This demonstrates the published managed
headless path and bundled runtime on the executing host. Native DLL presence is
checked; native loading, GPU, audio, input and a virgin OS are **not** established.

Ordinary builds keep `packages.lock.json`. Publish restore and publish both pass
`-p:FftWindowsBundle=true`, selecting `packages.win-x64.lock.json` for Client and its
three project dependencies. This avoids turning Core/Engine into Windows-targeted
assemblies or replacing the portable locks. An intentional dependency/SDK update
must also regenerate this graph with:

```powershell
dotnet restore src/Fft.Client/Fft.Client.csproj --runtime win-x64 -p:SelfContained=true -p:FftWindowsBundle=true -p:RestoreLockedMode=false --force-evaluate
```

Review both graphs; CI uses locked mode for each. Run a normal locked solution
restore again before normal `--no-restore` builds, because the RID restore shares
the projects' `obj/project.assets.json`. The script explicitly disables trimming,
AOT and single-file bundling. Application packages are unchanged. See
[NuGet lock-path properties](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets)
and [.NET deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/).

## Clean-machine prerequisites and manual check

Use a clean **Windows 11 x64** VM or second machine with an interactive desktop,
current Windows updates and a graphics driver exposing OpenGL 2.0 plus
`ARB_framebuffer_object`, or OpenGL 3.0. A VM must expose suitable accelerated
graphics; a generic display adapter is not sufficient evidence. This is the first
validation target, not a tested support promise for every Windows release.
The self-contained bundle needs **no installed .NET SDK/runtime, C toolchain,
MonoGame installation, checkout, original media or network at launch**. Native DLLs
remain beside the executable; driver/system-library prerequisites still matter.
Do not preinstall a broad dependency pack to hide a missing native dependency;
record any missing DLL and the precise remediation before changing the baseline.
[.NET Windows requirements](https://learn.microsoft.com/en-us/dotnet/core/install/windows),
[MonoGame graphics requirements](https://docs.monogame.net/articles/getting_started/platforms.html).

Copy the full bundle to a user-writable folder on that machine. In PowerShell,
from that folder, use a fresh check directory:

```powershell
./Fft.Client.exe --scenario two-unit-battle
./Fft.Client.exe --battle-check ./clean-check-001
./Fft.Client.exe --battle
```

1. Record the commit/job URL, OS edition/build, architecture, GPU/driver, display
   scale and whether .NET/VC runtimes were already installed. Record the bundle
   inventory hash. Disconnect networking and leave original media unavailable.
2. Confirm the headless result above and finite graphical check exit with code 0.
   Keep `clean-check-001/check.json` and six captures locally. Copy any game-derived
   captures only to ignored `scratch` when bringing evidence back to this checkout.
3. Inspect the invented battle's labels, highlights, HP and winner state. Play the
   [documented victory sequence](playable-battle.md#controls), then Restart and close.
   Check physical keyboard/mouse, resize, minimize/restore and available DPI scales;
   note observed limits. A finite programmed-input check alone does not establish
   physical input timing or mixed-monitor DPI behavior.
4. Record failure exit codes/messages, missing libraries or graphics errors, and
   any added prerequisite. Keep original assets unavailable throughout. Audio,
   controllers and original prepared-scene fidelity remain separate checks.

The bundle includes this guide as `WINDOWS-LAUNCH.md`; repository-relative guide
links are for source readers. No original assets are required for these commands.
Dependency notice provenance is in `THIRD-PARTY/README.md` in the bundle and
[`licenses/README.md`](../../licenses/README.md) in the checkout.

## Evidence and outstanding gates

Local validation on 2026-10-23 used SDK 10.0.402 and Windows PowerShell 5.1:

- Portable locked solution restore and Release build passed with zero warnings/errors.
  All **868 tests** passed (Core 293, Content 213, AssetTool 151, CLI 91, Engine 24,
  Client 96). Six actual TRX reports converted to JUnit with exactly 868 cases.
- Core/Engine built independently without Client or a rendering package dependency.
  CI YAML parsed locally; this is not CircleCI server schema/execution validation.
- The final publish script passed locked RID restore, self-contained publishing,
  dependency/notice inventory and headless launch under Windows PowerShell 5.1.
  Local receipts are in ignored `scratch/ci/local-034-001` and `local-034-002`.
  The corrected inventory contains 211 files with normalized paths. Existing-output
  and outside-scratch guards failed safely; an injected restore exit code 17 stopped
  the script before publishing and produced a nonzero script exit.
- `local-034-002/published-battle-check` ran the **published executable**, without
  original inputs, on the developer workstation: AMD Radeon RX 6600, Windows
  10.0.26200, bundled .NET 10.0.13. It completed 280 draws, 12 accepted commands and
  2 rejections, matched both headless and journal replay, and preserved state after
  resize to 1280x720. Final digest:
  `9f441423bd0533074796128bf249e313178275797933a63d0adaab7757711ffb`.
  The finished and terminal-rejected PNG hashes matched the earlier ZOD-032 observations.
  This verifies native/GPU launch on this workstation, not a virgin OS or hardware input.

A developer workstation run is not a clean-machine result. A CircleCI run must be observed
after the commit is pushed; configured jobs alone do not establish remote success.
ZOD-034 stays validation-pending until remote Windows/Linux jobs and the clean
interactive machine check above have recorded outcomes.
