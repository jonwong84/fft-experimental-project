using Zodiac.Content;
using Zodiac.Content.Events;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class BannerReadyPreparationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "fft-sequence-cli-" + Guid.NewGuid().ToString("N"));
    private string[] Args(string output, string general = "60", string ready = "60") => ["prepare-banner-ready-sequence", "--id", "sample:sequence/test",
        "--general-artwork-id", "sample:art/general", "--ready-artwork-id", "sample:art/ready", "--owner-id", "sample:owner/sequence",
        "--general-hold", general, "--ready-hold", ready, "--provenance", "authored", "--output", output];
    private int Run(string[] args) => new PrepareBannerReadySequenceCommand(root).Run(args, new StringWriter(), new StringWriter());
    [Theory]
    [InlineData("0", "255")] [InlineData("60", "60")] [InlineData("255", "0")]
    public void Preparation_repeats_manifest_and_descriptor_without_original_input(string g, string r)
    {
        Assert.Equal(0, Run(Args("scratch/a", g, r))); Assert.Equal(0, Run(Args("scratch/b", g, r)));
        foreach (string name in new[] { "sequence.json", PreparedAssetManifest.FileName })
            Assert.Equal(File.ReadAllBytes(Path.Combine(root, "scratch/a", name)), File.ReadAllBytes(Path.Combine(root, "scratch/b", name)));
        var d = PreparedBannerReadySequence.Load(LooseAssetStore.Load(Path.Combine(root, "scratch/a")), new("sample:sequence/test"));
        Assert.Equal(int.Parse(g), d.GeneralHold); Assert.Equal(int.Parse(r), d.ReadyHold);
    }
    [Theory]
    [InlineData("-1", "60")] [InlineData("256", "60")] [InlineData("60", "-1")] [InlineData("60", "256")] [InlineData("no", "0")]
    public void Invalid_holds_publish_nothing(string g, string r)
    { Assert.Equal(1, Run(Args("scratch/rejected", g, r))); Assert.False(Directory.Exists(Path.Combine(root, "scratch/rejected"))); }
    [Fact]
    public void Existing_output_and_escape_paths_are_preserved()
    {
        Directory.CreateDirectory(Path.Combine(root, "scratch/existing")); File.WriteAllText(Path.Combine(root, "scratch/existing/sentinel"), "preserve");
        Assert.Equal(1, Run(Args("scratch/existing"))); Assert.Equal("preserve", File.ReadAllText(Path.Combine(root, "scratch/existing/sentinel")));
        Assert.Equal(1, Run(Args("outside"))); Assert.False(Directory.Exists(Path.Combine(root, "outside")));
    }
    [Fact]
    public void Unsupported_provenance_duplicates_and_options_reject_before_output()
    {
        var args = Args("scratch/rejected"); args[14] = "retail"; Assert.Equal(1, Run(args));
        args = Args("scratch/rejected"); args[13] = "--id"; Assert.Equal(1, Run(args));
        args = Args("scratch/rejected"); args[13] = "--input"; Assert.Equal(1, Run(args));
        Assert.False(Directory.Exists(Path.Combine(root, "scratch/rejected")));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
