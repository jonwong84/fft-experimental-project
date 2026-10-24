using Fft.AssetTool.Cli;
using Fft.Content.ContentPackages;
using Fft.Content.Definitions;
using Xunit;

namespace Fft.AssetTool.Cli.Tests;

public sealed class ValidateContentCommandTests
{
    [Theory]
    [InlineData("validate-content")]
    [InlineData("validate-content", "--package")]
    [InlineData("validate-content", "--unknown", "file.json")]
    [InlineData("validate-content", "--package", "one.json", "--package", "two.json")]
    public void Invalid_arguments_return_usage_exit_code(params string[] args)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(2, new ValidateContentCommand().Run(args, output, error));
        Assert.Contains("Usage:", error.ToString());
        Assert.Empty(output.ToString());
    }

    [Fact]
    public void File_authored_validation_returns_success_and_never_changes_input()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "content.package.json");
        try
        {
            Synthetic().Save(path);
            byte[] before = File.ReadAllBytes(path);
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(0, new ValidateContentCommand().Run(["validate-content", "--package", path], output, error));
            Assert.Contains("sample:package/recruits", output.ToString());
            Assert.Contains("1 jobs", output.ToString());
            Assert.Empty(error.ToString());
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    [Fact]
    public void Invalid_content_returns_nonzero_with_package_record_and_field()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Synthetic().ToJson().Replace("\"baseMove\": 3", "\"baseMove\": -1", StringComparison.Ordinal));
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(1, new ValidateContentCommand().Run(["validate-content", "--package", path], output, error));
            Assert.Contains("package=sample:package/recruits", error.ToString());
            Assert.Contains("record=sample:job/recruit", error.ToString());
            Assert.Contains("field=$.jobs[0].baseMove", error.ToString());
            Assert.Empty(output.ToString());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Missing_file_returns_nonzero()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(1, new ValidateContentCommand().Run(["validate-content", "--package",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")], output, error));
        Assert.Contains("file error", error.ToString());
    }

    private static ContentPackage Synthetic() => new("sample:package/recruits", "sample:profile/metadata", ContentPackageKind.Baseline, null,
        new[] { new JobDefinition(new("sample:job/recruit"), new("sample:skillset/practice"), 3, 2, false, []) },
        new[] { new SkillsetDefinition(new("sample:skillset/practice"), [new(0, new AbilityId("sample:ability/focus"))]) },
        new[] { new AbilityDefinition(new("sample:ability/focus"), 25, 0, 0x41, [0, 0, 0, 0]) });
}
