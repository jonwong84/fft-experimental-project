using System.Security.Cryptography;
using System.Text;
using Zodiac.AssetTool.Cli;
using Zodiac.Content;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class PrepareAccumulateBattleTests
{
    [Fact]
    public void Repeated_preparations_load_only_prepared_files_and_preserve_existing_output()
    {
        WithRoot(root =>
        {
            string first = Path.Combine(root, "scratch", "a"), second = Path.Combine(root, "scratch", "b");
            using var output = new StringWriter(); using var error = new StringWriter();
            var command = new PrepareAccumulateBattleCommand(root);
            Assert.Equal(0, command.Run(["prepare-accumulate-battle", "--output", first], output, error));
            Assert.Equal(0, command.Run(["prepare-accumulate-battle", "--output", second], output, error));
            foreach (var name in Names) Assert.Equal(File.ReadAllBytes(Path.Combine(first, name)), File.ReadAllBytes(Path.Combine(second, name)));
            var loaded = PreparedAccumulateBattle.Load(LooseAssetStore.Load(first));
            Assert.Equal(5, loaded.Definition.InitialBattle.Combatants[0].PhysicalAttack);
            Assert.Equal(SyntheticAccumulateBattle.AccumulateId.Value, loaded.AbilityId.Value);
            Assert.Equal(2, loaded.Definition.InitialBattle.Combatants.Length);
            string hash = loaded.MetadataSha256;
            Assert.Equal(1, command.Run(["prepare-accumulate-battle", "--output", first], output, error));
            Assert.Equal(hash, PreparedAccumulateBattle.Load(LooseAssetStore.Load(first)).MetadataSha256);
        });
    }

    [Theory] [InlineData("hash")] [InlineData("missing")] [InlineData("utf8")] [InlineData("effect")]
    [InlineData("profile")] [InlineData("budget")]
    public void Prepared_load_fails_before_native_use_for_invalid_files_or_semantics(string kind)
    {
        WithRoot(root =>
        {
            string destination = Path.Combine(root, "scratch", "a");
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(0, new PrepareAccumulateBattleCommand(root).Run(["prepare-accumulate-battle", "--output", destination], output, error));
            string primary = Path.Combine(destination, "content.package.json"), secondary = Path.Combine(destination, "ability.package.json");
            if (kind == "missing") File.Delete(secondary);
            else if (kind == "budget")
            {
                File.Delete(Path.Combine(destination, PreparedAssetManifest.FileName));
                new PreparedAssetManifest([new(PreparedAccumulateBattle.MetadataId, "content.package.json", 9_000_000, new string('0', 64))])
                    .SaveToFile(Path.Combine(destination, PreparedAssetManifest.FileName));
            }
            else
            {
                if (kind == "utf8") File.WriteAllBytes(secondary, [0xff, 0xfe]);
                else if (kind == "profile") File.WriteAllText(primary, File.ReadAllText(primary).Replace(SyntheticAccumulateBattle.ProfileId, "sample:profile/wrong", StringComparison.Ordinal));
                else File.WriteAllText(secondary, File.ReadAllText(secondary).Replace("\"amount\": 1", "\"amount\": 2", StringComparison.Ordinal));
                if (kind != "hash") Manifest(destination);
            }
            Assert.ThrowsAny<Exception>(() => PreparedAccumulateBattle.Load(LooseAssetStore.Load(destination)));
        });
    }

    [Theory] [InlineData("outside")] [InlineData("traversal")] [InlineData("syntax")]
    public void Invalid_preparation_options_create_no_output(string kind)
    {
        WithRoot(root =>
        {
            string path = kind == "outside" ? Path.Combine(root, "outside") : Path.Combine(root, "scratch", "..", "escaped");
            using var output = new StringWriter(); using var error = new StringWriter();
            string[] args = kind == "syntax" ? ["prepare-accumulate-battle"] : ["prepare-accumulate-battle", "--output", path];
            Assert.Equal(1, new PrepareAccumulateBattleCommand(root).Run(args, output, error));
            Assert.Empty(output.ToString()); Assert.Empty(Directory.GetFileSystemEntries(root));
        });
    }

    private static string[] Names => ["content.package.json", "ability.package.json", PreparedAssetManifest.FileName];
    private static void Manifest(string root)
    {
        File.Delete(Path.Combine(root, PreparedAssetManifest.FileName));
        new PreparedAssetManifest(Names.Take(2).Select((name, index) =>
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, name));
            return new PreparedAssetEntry(index == 0 ? PreparedAccumulateBattle.MetadataId : PreparedAccumulateBattle.EffectsId,
                name, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        })).SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
    }
    private static void WithRoot(Action<string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-accumulate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally
        {
            foreach (string child in new[] { "a", "b" })
            {
                string directory = Path.Combine(root, "scratch", child);
                if (!Directory.Exists(directory)) continue;
                foreach (var name in Names) File.Delete(Path.Combine(directory, name));
                Directory.Delete(directory);
            }
            string scratch = Path.Combine(root, "scratch"); if (Directory.Exists(scratch)) Directory.Delete(scratch);
            Directory.Delete(root);
        }
    }
}
