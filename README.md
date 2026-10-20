# fft-experimental-project

Windows-first C#/.NET reimplementation of the North American PS1 release of Final Fantasy Tactics (`SCUS-94221`). See `PROJECT_PLAN.md` for scope and architecture.

This checkout currently contains a headless .NET 10 console bootstrap, a `Fft.Core` library with immutable tile-position and battle-map data types plus namespaced asset IDs, a deliberately simplified flat-grid movement-range prototype, a managed ISO inventory/extraction tool that emits a versioned asset manifest, a separate `Fft.Content` library that loads and verifies files by manifest ID, and a unit `.SPR` parser with a bounded attack-image decoder. The console client is not wired to the content library yet. Asset format coverage is still narrow; SHP/SEQ animation metadata, other sprite formats, full FFT movement/combat rules, campaign logic, and a graphics/UI framework are not implemented.

## Build, run, and test

Requires the .NET 10 SDK.

```powershell
dotnet build Fft.sln -c Release
dotnet run --project src/Fft.Client/Fft.Client.csproj -c Release --no-build
dotnet test Fft.sln -c Release --no-build
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- inventory --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- extract --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue --path MENU/TUTO7.SCR --output scratch/extracted-sample
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- extract --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue --all-data --output scratch/all-data-extraction
```

The client console process prints an identifying line and exits with code 0. The AssetTool inventory command verifies the supported local image profile, reads its ISO primary-volume tree, and compares it with the local reference inventory. The `extract` command accepts repeated `--path` options or `--all-data` for every entry marked `data` in the reference inventory. It validates the complete inventory for Windows path collisions before writing anything, verifies paths and LBAs against the image, checks each extracted SHA-256 against the reference, and writes only to a dedicated directory under ignored `/scratch/`. Mixed XA/STR entries remain unsupported. `dotnet test` runs the Core and AssetTool xUnit projects. 
