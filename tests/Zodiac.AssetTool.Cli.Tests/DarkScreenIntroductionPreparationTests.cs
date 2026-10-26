using Zodiac.Content;
using Zodiac.Content.Events;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class DarkScreenIntroductionPreparationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "fft-dark-intro-cli-" + Guid.NewGuid().ToString("N"));
    private static string[] Args(string output, string g = "60", string r = "60") => ["prepare-dark-screen-introduction", "--id", "sample:intro/test",
        "--dark-screen-id", "sample:dark/test", "--general-artwork-id", "sample:art/general", "--ready-artwork-id", "sample:art/ready",
        "--owner-id", "sample:owner/test", "--general-hold", g, "--ready-hold", r, "--provenance", "authored", "--output", output];
    private int Run(string[] args) => new PrepareDarkScreenIntroductionCommand(root).Run(args, new StringWriter(), new StringWriter());
    [Theory] [InlineData("0", "255")] [InlineData("60", "60")] [InlineData("255", "0")]
    public void Preparation_repeats_both_files_without_original_inputs(string g, string r)
    {
        Assert.Equal(0, Run(Args("scratch/a", g, r))); Assert.Equal(0, Run(Args("scratch/b", g, r)));
        foreach (string file in new[] { "introduction.json", PreparedAssetManifest.FileName })
            Assert.Equal(File.ReadAllBytes(Path.Combine(root, "scratch/a", file)), File.ReadAllBytes(Path.Combine(root, "scratch/b", file)));
        var d = PreparedDarkScreenIntroduction.Load(LooseAssetStore.Load(Path.Combine(root, "scratch/a")), new("sample:intro/test"));
        Assert.Equal(int.Parse(g), d.GeneralHold); Assert.Equal(int.Parse(r), d.ReadyHold);
    }
    [Theory] [InlineData("-1", "0")] [InlineData("256", "0")] [InlineData("0", "-1")] [InlineData("0", "256")] [InlineData("x", "60")]
    public void Invalid_holds_publish_nothing(string g, string r)
    { Assert.Equal(1, Run(Args("scratch/rejected", g, r))); Assert.False(Directory.Exists(Path.Combine(root, "scratch/rejected"))); }
    [Fact]
    public void Existing_output_escapes_unsupported_provenance_and_duplicate_options_preserve_files()
    {
        Directory.CreateDirectory(Path.Combine(root, "scratch/existing")); File.WriteAllText(Path.Combine(root, "scratch/existing/marker"), "preserve");
        Assert.Equal(1, Run(Args("scratch/existing"))); Assert.Equal("preserve", File.ReadAllText(Path.Combine(root, "scratch/existing/marker")));
        Assert.Equal(1, Run(Args("outside"))); Assert.False(Directory.Exists(Path.Combine(root, "outside")));
        var args = Args("scratch/rejected"); args[16] = "original-event"; Assert.Equal(1, Run(args));
        args = Args("scratch/rejected"); args[15] = "--id"; Assert.Equal(1, Run(args));
        Assert.False(Directory.Exists(Path.Combine(root, "scratch/rejected")));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
