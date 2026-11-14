using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class PrepareGeneralBannerCommandTests
{
    [Theory]
    [InlineData("syntax")][InlineData("existing-output")][InlineData("bad-image")][InlineData("outside-scratch")]
    public void FailureNeverOverwritesOrPublishesPartialRoot(string variant)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "banner-cli-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(workspace, "scratch"), output = Path.Combine(scratch, "output");
        Directory.CreateDirectory(scratch);
        string input = Path.Combine(scratch, "input.bin"); File.WriteAllBytes(input, [0]);
        try
        {
            if (variant == "existing-output") { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "sentinel"), "keep"); }
            string[] args = variant == "syntax" ? ["prepare-general-banner"] :
                ["prepare-general-banner", "--bin", input, "--output", variant == "outside-scratch" ? Path.Combine(workspace, "outside") : output];
            int result = new PrepareGeneralBannerCommand(workspace).Run(args, new StringWriter(), new StringWriter());
            Assert.NotEqual(0, result);
            if (variant == "existing-output") Assert.Equal("keep", File.ReadAllText(Path.Combine(output, "sentinel")));
            else Assert.False(Directory.Exists(output));
            Assert.Empty(Directory.GetDirectories(scratch, ".general-banner-*"));
        }
        finally
        {
            File.Delete(input);
            if (Directory.Exists(output)) { File.Delete(Path.Combine(output, "sentinel")); Directory.Delete(output); }
            Directory.Delete(scratch); Directory.Delete(workspace);
        }
    }

    [Theory]
    [InlineData(958464, 127684)][InlineData(1, 127684)][InlineData(958464, 1)]
    public void SyntheticInputsCannotClaimRetailIdentity(int bonusLength, int requireLength)
        => Assert.Throws<InvalidDataException>(() => PrepareGeneralBannerCommand.DecodeVerified(new byte[bonusLength], new byte[requireLength]));
}
