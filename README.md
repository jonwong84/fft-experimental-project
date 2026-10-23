# Zodiac — FFT native reimplementation

Zodiac is a Windows-first C#/.NET game engine, with the North American PS1 release of Final Fantasy Tactics (`SCUS-94221`) as its first planned content set. See [`PROJECT_PLAN.md`](PROJECT_PLAN.md) and [`docs/architecture/overview.md`](docs/architecture/overview.md) for scope and architecture.

The proposed dependency boundaries and initial architecture decisions are documented in [`docs/architecture/overview.md`](docs/architecture/overview.md) and [`docs/adr/`](docs/adr/). A detailed restart point for future contributors is in [`docs/PROJECT_HANDOFF.md`](docs/PROJECT_HANDOFF.md).

The prioritized development tickets, dependencies, and acceptance criteria are maintained in [`docs/BACKLOG.md`](docs/BACKLOG.md).

This checkout contains a .NET 10 deterministic, bounded headless battle and a MonoGame Windows host displaying its synthetic starting snapshot. The graphical view supports resize and exit; gameplay input and original map/sprite rendering remain future work. Core and Zodiac.Engine stay independent of graphics/input. The offline asset tools and Fft.Content provide a separate prepared-file pipeline; asset coverage and supported simulation rules remain narrow. Full FFT combat, campaign, animation and authoring UI are not implemented.

## Build, run, and test

Requires the .NET SDK version selected by `global.json` (currently 10.0.402). See [SDK and dependency policy](docs/development/build-reproducibility.md) for installation, locked restoration, and deliberate upgrades.

On Windows, `--window` opens the synthetic battle view. See [launch and graphical validation](docs/development/windows-synthetic-host.md)
for resize/exit controls, the finite `--window-check` capture, and input/DPI limits. Rendering
does not advance the battle; use `--scenario two-unit-battle` to execute its headless script.

The [selected sprite-pose preparation guide](docs/development/prepared-sprite-pose.md) describes
`prepare-sprite-pose` and the client's `--sprite-pose` load check. The prepared Chocobo pose
is static render input; the Windows window continues to use its synthetic scene.

Content metadata validation uses the shared `Fft.Content.ContentPackages` API. It reads data only; it does not load authoring programs, the BIN or assets:

```powershell
dotnet run --project src/Fft.AssetTool.Cli -c Release -- validate-content --package docs/fixtures/content/metadata-synthetic.json
```

Use repeated `--addon <path>` arguments to validate explicitly selected additions against that baseline. Exit codes are 0 for valid metadata, 1 for invalid content or file errors, and 2 for command usage errors. See [production content packages](docs/architecture/zod-016-content-packages.md) for C# authoring, deterministic export, diagnostics and limits. This validates structural metadata, not battle readiness or original-game equivalence.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet run --project src/Fft.Client/Fft.Client.csproj -c Release --no-build
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --scenario two-unit-battle
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --window
dotnet run --project src/Fft.Client/Fft.Client.csproj -c Release -- --content-root scratch/prepared-chocobo-content --asset-id base:battle/cyoko.spr
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- verify-sprite --content-root scratch/prepared-chocobo-content
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- verify-sprite --content-root scratch/prepared-chocobo-content --preview-output scratch/chocobo-palettes.png
dotnet test Fft.sln -c Release --no-build
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- inventory --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- extract --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue --path MENU/TUTO7.SCR --output scratch/extracted-sample
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- extract --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue --all-data --output scratch/all-data-extraction
```

With no arguments, the client prints an identifying line and exits with code 0. With `--content-root` and `--asset-id`, it loads the prepared manifest, reads the selected file, and verifies its size and SHA-256. The `verify-sprite` AssetTool command checks the local Chocobo `.SPR` input and its decoded 256×200 packed-4bpp output against a hash fixture in `docs/fixtures/`; optional `--preview-output` writes a 4×4 PNG contact sheet for all 16 palette banks under ignored `/scratch/`. The preview is a diagnostic approximation, not an emulation of PS1 blending or final display output. The fixture includes hashes and source provenance, not image data. The AssetTool inventory command verifies the supported local image profile, reads its ISO primary-volume tree, and compares it with the local reference inventory. The `extract` command accepts repeated `--path` options or `--all-data` for every entry marked `data` in the reference inventory. It validates the complete inventory for Windows path collisions before writing anything, verifies paths and LBAs against the image, checks each extracted SHA-256 against the reference, and writes only to a dedicated directory under ignored `/scratch/`. Mixed XA/STR entries remain unsupported. `dotnet test` runs the Core and AssetTool xUnit projects.
