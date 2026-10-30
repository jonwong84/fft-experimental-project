using System.Security.Cryptography;
using System.Text.Json;
using Fft.Client.Presentation;
using Zodiac.Content;
using Zodiac.Framework.Events;

// Developer-only finite prepared-content host; never reads original state, code or media.
internal static class ReadySequence
{
    internal static readonly int[] Ticks = [8, 10, 60, 64, 66, 67];
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Scratch(string path)
    {
        string result = Path.GetFullPath(path), root = Path.GetFullPath("scratch") + Path.DirectorySeparatorChar;
        if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Expected scratch path.");
        for (string? p = result; p is not null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse point unsupported.");
        return result;
    }
    private static void Fields(JsonElement node, string expected)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected object.");
        string[] names = node.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != names.Distinct().Count() || !names.ToHashSet().SetEquals(expected.Split(' ')))
            throw new InvalidDataException("Unexpected or duplicate fields.");
    }
    internal static int Run(string artworkRoot, string planPath, string destination, bool birth = false)
    {
        int[] ticks = birth ? [0, 1] : Ticks;
        string output = Scratch(destination), plan = Scratch(planPath);
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output exists.");
        if (new FileInfo(plan).Length > 65536) throw new InvalidDataException("Plan too large.");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(plan), new JsonDocumentOptions { MaxDepth = 8 });
        var root = doc.RootElement;
        Fields(root, "format schemaVersion eventRoot checkpoints");
        if (root.GetProperty("format").GetString() != (birth ? "fft.ready-birth-backgrounds" : "fft.ready-backgrounds") || root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("checkpoints").GetArrayLength() != ticks.Length) throw new InvalidDataException("Invalid Ready plan.");
        var inputs = new List<(int Tick, byte[] Background)>();
        foreach (var item in root.GetProperty("checkpoints").EnumerateArray())
        {
            Fields(item, "logicalTick backgroundPath backgroundSha256");
            int tick = item.GetProperty("logicalTick").GetInt32();
            if (tick != ticks[inputs.Count]) throw new InvalidDataException("Wrong Ready anchors.");
            string path = Scratch(item.GetProperty("backgroundPath").GetString()!);
            if (new FileInfo(path).Length != 256 * 240 * 4) throw new InvalidDataException("Invalid background size.");
            byte[] bytes = File.ReadAllBytes(path);
            if (Hash(bytes) != item.GetProperty("backgroundSha256").GetString()) throw new InvalidDataException("Background digest differs.");
            for (int at = 0; at < bytes.Length; at += 4)
                if (bytes[at + 3] != 255 || ((bytes[at] | bytes[at + 1] | bytes[at + 2]) & 7) != 0)
                    throw new InvalidDataException("Expected opaque zero-padded RGB5 background.");
            inputs.Add((tick, bytes));
        }
        var session = ReadySession.Load(LooseAssetStore.Load(Scratch(root.GetProperty("eventRoot").GetString()!)),
            new("sample:event/ready-preview"), LooseAssetStore.Load(Scratch(artworkRoot)));
        var display = session.Definition.Instructions.Single(i => i.Operation == Zodiac.Content.Events.OrderedScenarioOperation.DisplayConditions);
        if (display.Operand2 != 60) throw new InvalidDataException("Expected authored hold 60.");
        var result = session.Start(new("sample:ready-comparison/scene"), new("sample:ready-comparison/consumer"));
        if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
        var state = result.State;
        var frames = new List<object>(); var pixels = new List<(string Name, byte[] Bytes)>();
        foreach (var input in inputs)
        {
            while (state.Event.Tick < input.Tick)
            {
                result = ReadySceneExecutor.AdvanceTick(state, false, [], ConditionalEventProgram.MaxInstructions);
                if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
                state = result.State;
            }
            var draws = ReadyPresentation.Project(state.Ready, ReadyRasterState.Selected);
            byte[] rgba = ReadyPresentation.ComposeOver(session.Artwork, draws, input.Background);
            string file = $"tick-{input.Tick:D3}.rgba";
            frames.Add(new { logicalTick = input.Tick, file, backgroundSha256 = Hash(input.Background), rgbaSha256 = Hash(rgba),
                drawScale = state.Ready?.DrawScale, child = state.Ready?.ChildPhase.ToString(), draws });
            pixels.Add((file, rgba));
        }
        Directory.CreateDirectory(output);
        foreach (var item in pixels) File.WriteAllBytes(Path.Combine(output, item.Name), item.Bytes);
        File.WriteAllText(Path.Combine(output, "check.json"), JsonSerializer.Serialize(new
        {
            format = "fft.ready-background-cpu-check", schemaVersion = 1, policy = ReadyPresentation.Policy,
            renderer = "ReadyPresentation.ComposeOver", artworkSource = session.Artwork.Definition.Source,
            originalComparison = "pending", gpuReadbackPerformed = false, checkpoints = frames
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"{ticks.Length} accepted Ready snapshots composed over explicit backgrounds.");
        return 0;
    }
}
