using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Content;
using Fft.Core.Events;

// Optional finite CPU evidence host. No emulator, original formats, GPU or window are used.
try
{
    if (args.Length == 4 && args[0] == "--sequence") return Sequence(args[1], args[2], args[3]);
    if (args.Length != 3) throw new ArgumentException("Expected prepared-root background.rgba fresh-output-root.");
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

static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

static string ScratchPath(string value)
{
    string result = Path.GetFullPath(value), scratch = Path.GetFullPath("scratch") + Path.DirectorySeparatorChar;
    if (!result.StartsWith(scratch, StringComparison.OrdinalIgnoreCase)) throw new IOException("Paths must remain below repository scratch/.");
    for (string? current = result; current is not null; current = Path.GetDirectoryName(current))
        if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Reparse-point paths are unsupported.");
    return result;
}

static void Fields(JsonElement node, string expected)
{
    if (node.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected an object.");
    var names = node.EnumerateObject().Select(p => p.Name).ToArray();
    if (names.Length != names.Distinct(StringComparer.Ordinal).Count() ||
        !names.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Split(' ')))
        throw new InvalidDataException("Duplicate, missing or unknown sequence fields.");
}

static int Sequence(string content, string plan, string destination)
{
    string output = ScratchPath(destination), prepared = ScratchPath(content), planPath = ScratchPath(plan);
    if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output already exists.");
    if (new FileInfo(planPath).Length > 65536) throw new InvalidDataException("Sequence plan exceeds budget.");
    using var document = JsonDocument.Parse(File.ReadAllBytes(planPath), new JsonDocumentOptions { MaxDepth = 8 });
    var root = document.RootElement;
    Fields(root, "format schemaVersion checkpoints");
    if (root.GetProperty("format").GetString() != "fft.banner-hide-backgrounds" || root.GetProperty("schemaVersion").GetInt32() != 1 ||
        root.GetProperty("checkpoints").GetArrayLength() != 5) throw new InvalidDataException("Expected the five hide checkpoints.");
    var inputs = new List<(int Tick, byte[] Background)>();
    foreach (var checkpoint in root.GetProperty("checkpoints").EnumerateArray())
    {
        Fields(checkpoint, "logicalTick backgroundPath backgroundSha256");
        int tick = checkpoint.GetProperty("logicalTick").GetInt32();
        if (tick != 81 + inputs.Count) throw new InvalidDataException("Expected ordered hide checkpoints 81..85.");
        string backgroundPath = ScratchPath(checkpoint.GetProperty("backgroundPath").GetString()!);
        if (new FileInfo(backgroundPath).Length != 256 * 240 * 4) throw new InvalidDataException("Invalid sequence background size.");
        byte[] pixels = File.ReadAllBytes(backgroundPath);
        if (Hash(pixels) != checkpoint.GetProperty("backgroundSha256").GetString()) throw new InvalidDataException("Background digest mismatch.");
        // Validate the entire prepared input set before advancing or publishing outputs.
        for (int at = 0; at < pixels.Length; at += 4)
            if (pixels[at + 3] != 255 || ((pixels[at] | pixels[at + 1] | pixels[at + 2]) & 7) != 0)
                throw new InvalidDataException("Sequence background must be opaque zero-padded RGB5.");
        inputs.Add((tick, pixels));
    }
    var banner = PreparedGeneralBanner.Load(LooseAssetStore.Load(prepared), GeneralBannerPreview.BannerId);
    var state = GeneralBannerPreview.Start(banner);
    var frames = new List<object>(); var outputs = new List<(string Name, byte[] Pixels)>();
    foreach (var input in inputs)
    {
        while (state.Event.Tick < input.Tick)
        {
            var result = BannerSceneExecutor.AdvanceTick(state, false, [], 1);
            if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
            state = result.State;
        }
        var geometry = GeneralBannerPresentation.Project(state.Banner!);
        var draws = geometry.Select(g => new BannerRasterDraw(g, new(false, 0, 0))).ToImmutableArray();
        byte[] pixels = FiveBitGeneralBannerPresentation.ComposeOver(banner, draws, input.Background);
        string file = $"tick-{input.Tick:D3}.rgba"; outputs.Add((file, pixels));
        frames.Add(new { logicalTick = input.Tick, phase = state.Banner!.Phase.ToString(),
            revealCompleted = state.Banner.RevealCompleted, holdCompleted = state.Banner.HoldCompleted, hideCompleted = state.Banner.HideCompleted,
            file, backgroundSha256 = Hash(input.Background), rgbaSha256 = Hash(pixels), pieces = geometry });
    }
    var receipt = new { format = "fft.banner-hide-cpu-check", schemaVersion = 1,
        renderer = "FiveBitGeneralBannerPresentation.ComposeOver", sourceKind = banner.Definition.Source.Kind,
        referenceRevision = banner.Definition.Source.ReferenceRevision, precision = "five-bit", ditherEnabled = false,
        framebufferOrigin = new[] { 0, 0 }, gpuReadbackPerformed = false, originalComparison = "pending", checkpoints = frames };
    Directory.CreateDirectory(output);
    foreach (var frame in outputs) File.WriteAllBytes(Path.Combine(output, frame.Name), frame.Pixels);
    File.WriteAllText(Path.Combine(output, "check.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("CPU hide sequence composed at checkpoints 81..85; original comparison pending.");
    return 0;
}
