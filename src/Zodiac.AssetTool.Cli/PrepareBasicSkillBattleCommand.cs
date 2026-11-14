using System.Security.Cryptography;
using System.Text;
using Zodiac.Content;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.Samples;

namespace Zodiac.AssetTool.Cli;

/// <summary>Prepares invented content only, with shared package validation and a manifest; no original input.</summary>
public sealed class PrepareBasicSkillBattleCommand(string workspaceRoot)
{
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? staging = null;
        try
        {
            if (args.Length != 3 || args[0] != "prepare-basic-skill-battle" || args[1] != "--output" || string.IsNullOrWhiteSpace(args[2]))
                throw new ArgumentException("Usage: prepare-basic-skill-battle --output <fresh-scratch-directory>");
            string scratch = Path.GetFullPath(Path.Combine(workspaceRoot, "scratch"));
            string destination = Path.GetFullPath(args[2]);
            if (!destination.StartsWith(scratch + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new IOException("Preparation output must stay under repository scratch.");
            for (string? path = destination; path is not null; path = Path.GetDirectoryName(path))
                if ((Directory.Exists(path) || File.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse-point preparation paths are unsupported.");
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Output already exists; choose a fresh directory.");
            var metadata = SyntheticBasicSkillBattle.Metadata();
            var effects = SyntheticBasicSkillBattle.SelfEffects(metadata);
            effects.ValidateAgainst(metadata);
            var targeted = SyntheticBasicSkillBattle.TargetedEffects(metadata);
            targeted.ValidateAgainst(metadata);
            byte[] tertiary = Encoding.UTF8.GetBytes(targeted.ToJson());
            byte[] primary = Encoding.UTF8.GetBytes(metadata.ToJson()), secondary = Encoding.UTF8.GetBytes(effects.ToJson());
            string parent = Path.GetDirectoryName(destination)!; Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".basic-skill-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            File.WriteAllBytes(Path.Combine(staging, "content.package.json"), primary);
            File.WriteAllBytes(Path.Combine(staging, "ability.package.json"), secondary);
            File.WriteAllBytes(Path.Combine(staging, "targeted.package.json"), tertiary);
            new PreparedAssetManifest([
                new(PreparedBasicSkillBattle.MetadataId, "content.package.json", primary.Length, Convert.ToHexStringLower(SHA256.HashData(primary))),
                new(PreparedBasicSkillBattle.EffectsId, "ability.package.json", secondary.Length, Convert.ToHexStringLower(SHA256.HashData(secondary))),
                new(PreparedBasicSkillBattle.TargetedEffectsId, "targeted.package.json", tertiary.Length, Convert.ToHexStringLower(SHA256.HashData(tertiary)))])
                .SaveToFile(Path.Combine(staging, PreparedAssetManifest.FileName));
            _ = PreparedBasicSkillBattle.Load(LooseAssetStore.Load(staging));
            Directory.Move(staging, destination);
            output.WriteLine($"Prepared authored Basic Skill battle at {destination}.");
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or NotSupportedException or
            UnauthorizedAccessException or KeyNotFoundException or Zodiac.Content.ContentPackages.ContentPackageValidationException)
        { error.WriteLine("Basic Skill preparation failed: " + exception.Message); return 1; }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                foreach (string name in new[] { "content.package.json", "ability.package.json", "targeted.package.json", PreparedAssetManifest.FileName }) File.Delete(Path.Combine(staging, name));
                Directory.Delete(staging);
            }
        }
    }
}
