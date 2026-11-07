# ZOD-024 no-window compatibility probe

This standalone investigation is outside `Fft.sln` and adds no rendering dependency to the game, simulation or engine projects. It uses the repository's pinned .NET SDK and its own checked-in package lock. The project explicitly opts out of central package versions; it does not change the solution's dependency policy.

From the repository root:

```powershell
dotnet restore tools/rendering-spike/RenderingSpike.csproj --locked-mode
dotnet run --project tools/rendering-spike/RenderingSpike.csproj -c Release --no-restore
```

The probe loads MonoGame's managed assembly, creates unbound sampler/blend state objects, performs synthetic CPU palette expansion and camera math, and compiles a never-called texture/render-target upload method. On Windows x64 it loads the supplied SDL native library and queries its version using `SDL_GetVersion`, documented as safe before initialization. It does not call `SDL_Init`, construct `Game`, create a window/graphics device, poll real input, play audio or open any game asset.

Passing this probe establishes managed API/runtime compatibility and that the selected Windows x64 SDL DLL loads on the tested machine. It does not establish GPU rendering, window/DPI behavior, performance, texture upload execution, controller support or PS1 presentation equivalence. The printed transparent-index example is synthetic, not a universal rule for imported FFT palettes.

See [ADR 0005](../../docs/adr/0005-windows-rendering-and-input-stack.md) for the recommendation, exact tested versions, primary sources and remaining host-validation gate. Generated `bin/` and `obj/` files remain ignored.
