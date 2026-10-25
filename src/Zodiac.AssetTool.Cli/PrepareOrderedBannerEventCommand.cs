using System.Security.Cryptography;
using System.Text.Json;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

/// <summary>Explicit authored-only preparation. Raw/processed bytes stay offline beneath scratch.</summary>
public sealed class PrepareOrderedBannerEventCommand(string workspaceRoot)
{
    public const string Usage = "prepare-ordered-banner-event --input <scratch/block> --encoding raw|processed [--text-offset <n>] --id <id> --artwork-id <id> --owner-id <id> --provenance authored --output <fresh-scratch-directory>";
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            if (args.Length % 2 != 1 || args[0] != "prepare-ordered-banner-event") throw new ArgumentException(Usage);
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i += 2)
                if (args[i] is not ("--input" or "--encoding" or "--text-offset" or "--id" or "--artwork-id" or "--owner-id" or "--provenance" or "--output") ||
                    string.IsNullOrWhiteSpace(args[i + 1]) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Usage);
            foreach (var key in new[] { "--input", "--encoding", "--id", "--artwork-id", "--owner-id", "--provenance", "--output" })
                if (!options.ContainsKey(key)) throw new ArgumentException(Usage);
            if (options["--provenance"] != "authored" || options["--encoding"] is not ("raw" or "processed")) throw new ArgumentException(Usage);
            int? textOffset = null;
            if (options.TryGetValue("--text-offset", out var text))
            {
                if (!int.TryParse(text, out int count)) throw new ArgumentException(Usage);
                textOffset = count;
            }
            string input = ScratchPath(options["--input"]), destination = ScratchPath(options["--output"]);
            if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Output already exists; choose a fresh directory.");
            using var stream = File.OpenRead(input);
            if (stream.Length != ScenarioBlockInspector.BlockBytes) throw new InvalidDataException("An authored 8192-byte block is required.");
            var bytes = new byte[ScenarioBlockInspector.BlockBytes]; stream.ReadExactly(bytes);
            var inspection = ScenarioBlockInspector.Inspect(bytes, options["--encoding"] == "raw" ? ScenarioBlockEncoding.Raw : ScenarioBlockEncoding.Processed, textOffset);
            var definition = ScenarioEventExporter.CreateSyntheticOrderedBanner(inspection, new(options["--id"]), new(options["--artwork-id"]), new(options["--owner-id"]));
            byte[] metadata = definition.ToUtf8();
            string parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".ordered-banner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            File.WriteAllBytes(Path.Combine(staging, "event.json"), metadata);
            new PreparedAssetManifest([new(definition.Id, "event.json", metadata.Length, Convert.ToHexStringLower(SHA256.HashData(metadata)))])
                .SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedOrderedBannerEvent.Load(LooseAssetStore.Load(staging), definition.Id).ToExecutionDefinition();
            File.WriteAllText(Path.Combine(staging, "provenance.json"), JsonSerializer.Serialize(new
            {
                kind = "authored", source = definition.Source, profile = PreparedOrderedBannerEvent.Profile,
                limitations = "preloaded post-readiness domain; no original event playback, startup duration or display latency claim",
            }) + "\n");
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared ordered banner event {definition.Id}: {definition.Source.BlockSha256} at {destination}.");
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
        { error.WriteLine("Ordered banner preparation failed: " + e.Message); return 1; }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                foreach (var name in new[] { "event.json", "provenance.json", PreparedAssetManifest.FileName }) File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }

    private string ScratchPath(string path)
    {
        string scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch")) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(scratch, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Input/output must stay beneath workspace scratch/.");
        for (string? at = full; at is not null; at = Path.GetDirectoryName(at))
            if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse-point paths are unsupported.");
        return full;
    }
}
