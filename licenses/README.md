# Windows client dependency notices

These unmodified upstream notices accompany the private ZOD-034 CI folder bundle.
They describe dependencies, not a license grant for this repository or original game media.
The client remains a folder deployment with replaceable native DLLs. No original
assets are included. Public release, source/relinking delivery and installer policy
are separate work; this inventory does not establish public distribution readiness.

Resolved versions come from `src/Fft.Client/packages.win-x64.lock.json`:

- MonoGame.Framework.DesktopGL **3.8.6**: `MonoGame-LICENSE.txt`, Ms-PL and MIT portions.
  Copied from [source commit c610a529](https://raw.githubusercontent.com/MonoGame/MonoGame/c610a529667336f63da323c6057274258727664b/LICENSE.txt).
- MonoGame.Library.SDL **2.32.10.2**, native SDL **2.32.10**:
  `SDL-LICENSE.txt`, zlib; copied from the locked NuGet package's `LICENSE.txt`.
  [Versioned upstream notice](https://github.com/libsdl-org/SDL/blob/release-2.32.10/LICENSE.txt).
- MonoGame.Library.OpenAL **1.24.3.4**, native OpenAL Soft **1.24.3**:
  `OpenAL-Soft-BSD-LICENSE.txt` is the locked NuGet package's `LICENSE`, covering BSD portions.
  `OpenAL-Soft-COPYING.txt` is the full
  [OpenAL Soft 1.24.3 COPYING](https://raw.githubusercontent.com/kcat/openal-soft/1.24.3/COPYING),
  GNU Library GPL v2; the package's BSD notice alone is insufficient.
- NVorbis **0.10.4**: `NVorbis-LICENSE.txt`, MIT; copied from the locked NuGet package's `LICENSE`.
  [Versioned upstream notice](https://raw.githubusercontent.com/NVorbis/NVorbis/v0.10.4/LICENSE).
- Microsoft.NETCore.App.Runtime.win-x64: the packaging script reads the actual
  published runtime version from `Fft.Client.deps.json` and copies that restored
  runtime pack's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` as `DotNet-LICENSE.txt`
  and `DotNet-THIRD-PARTY-NOTICES.txt`. They are generated bundle files, not vendored here.
  [Runtime source/license](https://github.com/dotnet/runtime).

On a dependency upgrade, review the publish locks and refresh the applicable texts
and this inventory. The accompanying `bundle-inventory.json` records each actual
published file's SHA-256 and size, including these notices.
