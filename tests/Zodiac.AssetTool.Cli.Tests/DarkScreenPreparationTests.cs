using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class DarkScreenPreparationTests
{
    private static string Workspace()
    {var root=Path.Combine(Path.GetTempPath(),"fft-dark-prep-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"scratch"));return root;}
    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("unsupported")] [InlineData("hold")] [InlineData("outside")]
    public void Invalid_preparation_options_publish_nothing(string kind)
    {
        string root=Workspace(),input=Path.Combine(root,"scratch/input"),dest=Path.Combine(root,"scratch/out");
        string[] args=kind switch
        {
            "missing"=>["prepare-dark-screen"],
            "duplicate"=>["prepare-dark-screen","--output",dest,"--output",dest],
            "unsupported"=>["prepare-dark-screen","--bin",input,"--output",dest],
            "hold"=>["prepare-dark-screen","--executable",input,"--output",dest,"--hold-frames","256"],
            _=>["prepare-dark-screen","--executable",input,"--output",Path.Combine(root,"outside")],
        };
        Assert.Equal(1,new PrepareDarkScreenCommand(root).Run(args,new StringWriter(),new StringWriter()));Assert.False(Directory.Exists(dest));
    }
    [Fact]
    public void Existing_output_and_unverified_original_inputs_are_preserved()
    {
        string root=Workspace(),input=Path.Combine(root,"scratch/input"),dest=Path.Combine(root,"scratch/out");
        Directory.CreateDirectory(dest);File.WriteAllText(Path.Combine(dest,"keep"),"unchanged");File.WriteAllBytes(input,new byte[356352]);
        string[] args=["prepare-dark-screen","--executable",input,"--output",dest];
        Assert.Equal(1,new PrepareDarkScreenCommand(root).Run(args,new StringWriter(),new StringWriter()));
        Assert.Equal("unchanged",File.ReadAllText(Path.Combine(dest,"keep")));
        args[4]=Path.Combine(root,"scratch/fresh");
        Assert.Equal(1,new PrepareDarkScreenCommand(root).Run(args,new StringWriter(),new StringWriter()));Assert.False(Directory.Exists(args[4]));
        Assert.Throws<InvalidDataException>(()=>PrepareDarkScreenCommand.DecodeVerified(new byte[356352],44));
    }
}
