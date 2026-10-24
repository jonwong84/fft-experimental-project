# Finite CPU banner blend check

Optional developer evidence host for the
[controlled original-background comparison](../../docs/development/general-banner-background-blending.md).
This project is outside the main solution. It uses the existing Client compositor
and package versions; no original-format parser, emulator or GPU is used here.

From the repository root, restore/build with the committed lock file:

```powershell
dotnet restore tools/banner-blend-check/BannerBlendCheck.csproj --locked-mode
dotnet build tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-restore
```

The Python comparison tool normally invokes the host. For a manual CPU check:

```powershell
dotnet run --project tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-build --no-restore -- scratch/banner-078-a scratch/banner-background-c/background.rgba scratch/banner-blend-manual-check
```

All three paths must be under repository scratch; output must be fresh. The
background is exactly 256x240 raw RGBA8, opaque alpha, RGB channels divisible by
eight. The host performs 29 explicit no-Circle logical steps and writes
`composed.rgba` plus a hash-bound `check.json`. It performs no original comparison;
the Python tool validates isolation, provenance and pixel equality separately.
