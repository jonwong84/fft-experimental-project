using System.Security.Cryptography;
using System.Text.Json;
using Fft.Client.Presentation;
using Zodiac.Content;
using Zodiac.Framework.Events;

// Finite developer evidence host. Only prepared data and explicit RGBA backgrounds enter C#.
internal static class DarkScreenSequence
{
    private static readonly int[] Ticks = [2, 6, 14, 26, 42, 46, 163, 165, 167, 171, 179, 211, 213];
    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    private static string Scratch(string path)
    {
        string full = Path.GetFullPath(path), root = Path.GetFullPath("scratch") + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Expected scratch path.");
        for (string? p = full; p is not null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse point unsupported.");
        return full;
    }
    private static void Fields(JsonElement node, string names)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected an object.");
        var actual = node.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Distinct().Count() != actual.Length || !actual.ToHashSet().SetEquals(names.Split(' ')))
            throw new InvalidDataException("Duplicate, missing or unknown fields.");
    }
    internal static int Run(string preparedRoot, string planPath, string destination)
    {
        string output = Scratch(destination), path = Scratch(planPath);
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output exists.");
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Oversized background plan.");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 8 });
        var root = doc.RootElement; Fields(root, "format schemaVersion checkpoints");
        if (root.GetProperty("format").GetString() != "fft.dark-screen-backgrounds" || root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("checkpoints").GetArrayLength() != Ticks.Length) throw new InvalidDataException("Unsupported finite dark-screen plan.");
        var inputs = new List<(int Tick, byte[] Background)>();
        foreach (var item in root.GetProperty("checkpoints").EnumerateArray())
        {
            Fields(item, "logicalTick backgroundPath backgroundSha256");
            int tick = item.GetProperty("logicalTick").GetInt32();
            if (tick != Ticks[inputs.Count]) throw new InvalidDataException("Wrong selected anchor.");
            string background = Scratch(item.GetProperty("backgroundPath").GetString()!);
            if (new FileInfo(background).Length != 256 * 240 * 4) throw new InvalidDataException("Wrong background size.");
            byte[] bytes = File.ReadAllBytes(background);
            if (Hash(bytes) != item.GetProperty("backgroundSha256").GetString()) throw new InvalidDataException("Background hash differs.");
            for (int at = 0; at < bytes.Length; at += 4)
                if (bytes[at + 3] != 255 || ((bytes[at] | bytes[at + 1] | bytes[at + 2]) & 7) != 0)
                    throw new InvalidDataException("Expected opaque zero-padded RGB5.");
            inputs.Add((tick, bytes));
        }
        var session = DarkScreenSession.Load(LooseAssetStore.Load(Scratch(preparedRoot)), new("fft.ps1-us:dark-screen/origin-v1"));
        if (session.Definition.HoldFrames != 108 || session.Definition.Source.Kind != "verified-retail")
            throw new InvalidDataException("Expected verified math and authored release tick 164.");
        var started = session.Start();
        if (!started.Accepted) throw new InvalidOperationException(started.Diagnostic);
        var state = started.State; var captures = new List<object>(); var files = new List<(string Name, byte[] Bytes)>();
        foreach (var input in inputs)
        {
            while (state.Event.Tick < input.Tick)
            {
                var next = DarkScreenExecutor.AdvanceTick(state, [], 8);
                if (!next.Accepted) throw new InvalidOperationException(next.Diagnostic);
                state = next.State;
            }
            var draws = DarkScreenPresentation.Project(session.Definition, state.Worker);
            byte[] pixels = DarkScreenPresentation.Compose(draws, input.Background);
            byte[] geometry = JsonSerializer.SerializeToUtf8Bytes(draws);
            string file = $"tick-{input.Tick:D3}.rgba", geometryFile = $"tick-{input.Tick:D3}.quads.json";
            files.Add((file, pixels)); files.Add((geometryFile, geometry));
            captures.Add(new { logicalTick = input.Tick, file, geometryFile, geometrySha256 = Hash(geometry),
                rgbaSha256 = Hash(pixels), backgroundSha256 = Hash(input.Background), drawCount = draws.Length,
                phase = state.Worker!.Phase.ToString(), drawKind = state.Worker.Draw.Kind.ToString() });
        }
        Directory.CreateDirectory(output);
        foreach (var file in files) File.WriteAllBytes(Path.Combine(output, file.Name), file.Bytes);
        File.WriteAllText(Path.Combine(output, "check.json"), JsonSerializer.Serialize(new
        {
            format = "fft.dark-screen-background-cpu-check", schemaVersion = 1,
            policy = DarkScreenPresentation.Policy, renderer = "DarkScreenPresentation.Compose",
            source = session.Definition.Source, holdFrames = 108, checkpoints = captures,
            originalComparison = "pending", gpuReadbackPerformed = false,
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"{Ticks.Length} accepted dark-screen snapshots composed over explicit backgrounds.");
        return 0;
    }
}
