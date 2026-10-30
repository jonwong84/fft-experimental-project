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

The optional `--sequence` mode accepts a closed `fft.banner-hide-backgrounds` v1
plan containing exactly checkpoints 81..85 with background paths and SHA-256s.
It preloads/validates all backgrounds, replays in order and writes five RGBA files
plus a separate `fft.banner-hide-cpu-check` receipt. The
[hide verifier](../verification/compare_banner_hide.py) prepares this plan and
invokes the mode; source-based display correspondence remains outside this host.

The separate `--reveal-sequence` mode accepts `fft.banner-reveal-backgrounds` v1
with exactly ticks 1, 2, 3, 4, 5, 9, 10, 11, 12, 13 and 14 in that order. It writes
an `fft.banner-reveal-cpu-check` receipt. The
[early-reveal verifier](../verification/compare_banner_reveal.py) supplies isolated
backgrounds and compares the actual C# pixels. A valid difference exits that Python
tool with code 1 and retains strict diagnostic evidence; invalid input exits 2.
The original single-anchor and hide-sequence modes retain their contracts.

The [triangle color verifier](../verification/verify_banner_triangle_colors.py)
reuses hash-bound original/control reveal captures and invokes this host after
rebuilding Client. It launches no emulator. The
[color trace](../../docs/development/banner-triangle-colors.md) documents the
corrected per-triangle seed and preservation of the older exact checkpoints.
