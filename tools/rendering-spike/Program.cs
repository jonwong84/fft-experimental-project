using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

// No Game construction, SDL initialization, window, device or input polling occurs in this probe.
Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; OS: {RuntimeInformation.OSDescription}; arch: {RuntimeInformation.ProcessArchitecture}");
Assembly framework = typeof(Game).Assembly;
Console.WriteLine($"MonoGame: {framework.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
Color[] palette = [Color.Transparent, Color.Red, Color.Green, Color.Blue];
byte[] indices = [0, 1, 2, 3];
Color[] syntheticRgba = indices.Select(index => palette[index]).ToArray();
if (syntheticRgba[0].A != 0 || syntheticRgba[1] != Color.Red || syntheticRgba.Length != 4)
    throw new InvalidOperationException("Synthetic CPU palette expansion failed.");
using var nearest = new SamplerState { Filter = TextureFilter.Point, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp };
using var blend = new BlendState { ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, ColorBlendFunction = BlendFunction.Add };
Matrix view = Matrix.CreateLookAt(new Vector3(8, 8, 8), Vector3.Zero, Vector3.Up);
Matrix projection = Matrix.CreateOrthographic(16, 9, 0.1f, 100);
if (!float.IsFinite((view * projection).M11)) throw new InvalidOperationException("Synthetic camera math failed.");
Console.WriteLine("Managed API/CPU probe: palette expansion, point/clamp state, custom blend state and camera matrix passed.");
Console.WriteLine($"Compile-only API anchors: {typeof(RenderTarget2D).Name}, {typeof(DepthStencilState).Name}, {typeof(GameWindow).Name}, {typeof(KeyboardState).Name}, {typeof(MouseState).Name}, {typeof(GamePadState).Name}");
if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
{
    string sdlPath = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "SDL2.dll");
    nint sdl = NativeLibrary.Load(sdlPath);
    try
    {
        var getVersion = Marshal.GetDelegateForFunctionPointer<GetSdlVersion>(NativeLibrary.GetExport(sdl, "SDL_GetVersion"));
        getVersion(out SdlVersion version);
        Console.WriteLine($"Windows x64 SDL native load/version query passed: {version.Major}.{version.Minor}.{version.Patch}; SDL_Init was not called.");
    }
    finally { NativeLibrary.Free(sdl); }
}
Console.WriteLine("No GPU, visible window, controller, DPI or rendering behavior has been tested.");

[StructLayout(LayoutKind.Sequential)]
internal struct SdlVersion { public byte Major; public byte Minor; public byte Patch; }
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void GetSdlVersion(out SdlVersion version);

// Compiled, deliberately never called: verifies the selected texture/render-target API against net10.0.
internal static class TextureApiContract
{
    internal static void Upload(GraphicsDevice device, Color[] rgba)
    {
        using var texture = new Texture2D(device, 2, 2, false, SurfaceFormat.Color);
        texture.SetData(rgba);
        using var target = new RenderTarget2D(device, 1920, 1080, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        device.SetRenderTarget(target);
        device.SamplerStates[0] = SamplerState.PointClamp;
        device.SetRenderTarget(null);
    }
}
