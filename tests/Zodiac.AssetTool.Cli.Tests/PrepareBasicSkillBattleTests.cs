using System.Security.Cryptography;
using System.Text;
using Zodiac.AssetTool.Cli;
using Zodiac.Content;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class PrepareBasicSkillBattleTests
{
    [Fact]
    public void Repeated_preparations_load_only_prepared_files_and_preserve_existing_output()
    {
        WithRoot(root =>
        {
            string first = Path.Combine(root, "scratch", "a"), second = Path.Combine(root, "scratch", "b");
            using var output = new StringWriter(); using var error = new StringWriter();
            var command = new PrepareBasicSkillBattleCommand(root);
            Assert.Equal(0, command.Run(["prepare-basic-skill-battle", "--output", first], output, error));
            Assert.Equal(0, command.Run(["prepare-basic-skill-battle", "--output", second], output, error));
            foreach (var name in Names) Assert.Equal(File.ReadAllBytes(Path.Combine(first, name)), File.ReadAllBytes(Path.Combine(second, name)));
            var loaded = PreparedBasicSkillBattle.Load(LooseAssetStore.Load(first));
            Assert.Equal(5, loaded.Definition.InitialBattle.Combatants[0].PhysicalAttack);
            Assert.Equal(SyntheticBasicSkillBattle.Accumulate.Value, loaded.SelfAbilityId.Value);
            Assert.Equal(2, loaded.Definition.InitialBattle.Combatants.Length);
            string hash = loaded.MetadataSha256;
            Assert.Equal(1, command.Run(["prepare-basic-skill-battle", "--output", first], output, error));
            Assert.Equal(hash, PreparedBasicSkillBattle.Load(LooseAssetStore.Load(first)).MetadataSha256);
        });
    }

    [Theory] [InlineData("hash")] [InlineData("missing")] [InlineData("utf8")] [InlineData("effect")]
    [InlineData("profile")] [InlineData("budget")] [InlineData("targeted-hash")] [InlineData("targeted-missing")] [InlineData("targeted-effect")]
    public void Prepared_load_fails_before_native_use_for_invalid_files_or_semantics(string kind)
    {
        WithRoot(root =>
        {
            string destination = Path.Combine(root, "scratch", "a");
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(0, new PrepareBasicSkillBattleCommand(root).Run(["prepare-basic-skill-battle", "--output", destination], output, error));
            string primary = Path.Combine(destination, "content.package.json"), secondary = Path.Combine(destination, "ability.package.json");
            if (kind.StartsWith("targeted-", StringComparison.Ordinal))
            {
                string target = Path.Combine(destination, "targeted.package.json");
                if (kind == "targeted-missing") File.Delete(target);
                else { File.WriteAllText(target, File.ReadAllText(target).Replace("\"range\": 4", "\"range\": 5", StringComparison.Ordinal)); if (kind == "targeted-effect") Manifest(destination); }
            }
            else if (kind == "missing") File.Delete(secondary);
            else if (kind == "budget")
            {
                File.Delete(Path.Combine(destination, PreparedAssetManifest.FileName));
                new PreparedAssetManifest([new(PreparedBasicSkillBattle.MetadataId, "content.package.json", 9_000_000, new string('0', 64))])
                    .SaveToFile(Path.Combine(destination, PreparedAssetManifest.FileName));
            }
            else
            {
                if (kind == "utf8") File.WriteAllBytes(secondary, [0xff, 0xfe]);
                else if (kind == "profile") File.WriteAllText(primary, File.ReadAllText(primary).Replace(SyntheticBasicSkillBattle.ProfileId, "sample:profile/wrong", StringComparison.Ordinal));
                else File.WriteAllText(secondary, File.ReadAllText(secondary).Replace("\"amount\": 1", "\"amount\": 2", StringComparison.Ordinal));
                if (kind != "hash") Manifest(destination);
            }
            Assert.ThrowsAny<Exception>(() => PreparedBasicSkillBattle.Load(LooseAssetStore.Load(destination)));
        });
    }

    [Theory] [InlineData("outside")] [InlineData("traversal")] [InlineData("syntax")]
    public void Invalid_preparation_options_create_no_output(string kind)
    {
        WithRoot(root =>
        {
            string path = kind == "outside" ? Path.Combine(root, "outside") : Path.Combine(root, "scratch", "..", "escaped");
            using var output = new StringWriter(); using var error = new StringWriter();
            string[] args = kind == "syntax" ? ["prepare-basic-skill-battle"] : ["prepare-basic-skill-battle", "--output", path];
            Assert.Equal(1, new PrepareBasicSkillBattleCommand(root).Run(args, output, error));
            Assert.Empty(output.ToString()); Assert.Empty(Directory.GetFileSystemEntries(root));
        });
    }

    private static string[] Names => ["content.package.json", "ability.package.json", "targeted.package.json", PreparedAssetManifest.FileName];
    private static void Manifest(string root)
    {
        File.Delete(Path.Combine(root, PreparedAssetManifest.FileName));
        new PreparedAssetManifest(Names.Take(3).Select((name, index) =>
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, name));
            return new PreparedAssetEntry(index == 0 ? PreparedBasicSkillBattle.MetadataId : index == 1 ? PreparedBasicSkillBattle.EffectsId : PreparedBasicSkillBattle.TargetedEffectsId,
                name, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        })).SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
    }
    private static void WithRoot(Action<string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-basic-skill-" + Guid.NewGuid().ToString("N"));
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
