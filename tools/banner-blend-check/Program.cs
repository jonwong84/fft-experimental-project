using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Content;
using Fft.Core.Events;

// Optional finite CPU evidence host. No emulator, original formats, GPU or window are used.
try
{
    if (args.Length != 3) throw new ArgumentException("Expected prepared-root background.rgba fresh-output-root.");
    static string ScratchPath(string value)
    {
        string result = Path.GetFullPath(value), scratch = Path.GetFullPath("scratch") + Path.DirectorySeparatorChar;
        if (!result.StartsWith(scratch, StringComparison.OrdinalIgnoreCase)) throw new IOException("Paths must remain below repository scratch/.");
        for (string? current = result; current is not null; current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse-point paths are unsupported.");
        return result;
    }
    string preparedRoot = ScratchPath(args[0]), backgroundPath = ScratchPath(args[1]), output = ScratchPath(args[2]);
    if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output already exists.");
    if (new FileInfo(backgroundPath).Length != 256 * 240 * 4) throw new InvalidDataException("Expected raw 256x240 RGBA8 background.");
    byte[] background = File.ReadAllBytes(backgroundPath);
    var banner = PreparedGeneralBanner.Load(LooseAssetStore.Load(preparedRoot), GeneralBannerPreview.BannerId);
    var state = GeneralBannerPreview.Start(banner);
    while (state.Event.Tick < 29)
    {
        var result = BannerSceneExecutor.AdvanceTick(state, false, [], 1);
        if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
        state = result.State;
    }
    var geometry = GeneralBannerPresentation.Project(state.Banner!);
    var draws = geometry.Select(g => new BannerRasterDraw(g, new(false, 0, 0))).ToImmutableArray();
    byte[] rgba = FiveBitGeneralBannerPresentation.ComposeOver(banner, draws, background);
    static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    var receipt = new
    {
        format = "fft.banner-background-cpu-check", schemaVersion = 1,
        renderer = "FiveBitGeneralBannerPresentation.ComposeOver", precision = "five-bit",
        backgroundPolicy = "explicit opaque zero-padded RGB5 scanout; no implicit quantization",
        sourceKind = banner.Definition.Source.Kind, referenceRevision = banner.Definition.Source.ReferenceRevision,
        backgroundSha256 = Hash(background), rgbaSha256 = Hash(rgba), logicalTick = state.Event.Tick,
        phase = state.Banner!.Phase.ToString(), revealCompleted = state.Banner.RevealCompleted,
        holdCompleted = state.Banner.HoldCompleted, hideCompleted = state.Banner.HideCompleted,
        ditherEnabled = false, framebufferOrigin = new[] { 0, 0 }, pieces = geometry,
        gpuReadbackPerformed = false, originalComparison = "pending",
    };
    Directory.CreateDirectory(output);
    File.WriteAllBytes(Path.Combine(output, "composed.rgba"), rgba);
    File.WriteAllText(Path.Combine(output, "check.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("CPU banner composition written at logical checkpoint 29; original comparison pending.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("Banner background check failed: " + error.Message);
    return 2;
}
