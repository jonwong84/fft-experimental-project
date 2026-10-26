using System.Text.Json.Nodes;
using Fft.Client;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BasicSkillBattleCheckTests
{
    [Fact]
    public void Fresh_headless_outputs_repeat_and_existing_output_is_preserved()
    {
        string root=Path.Combine(Path.GetTempPath(),"fft-basic-check-"+Guid.NewGuid().ToString("N"));
        string a=Path.Combine(root,"a"),b=Path.Combine(root,"b");
        try
        {
            Assert.Equal(0,BasicSkillBattleCheck.Run(a)); Assert.Equal(0,BasicSkillBattleCheck.Run(b));
            Assert.Equal(4,Directory.GetFiles(a).Length);
            foreach(string path in Directory.GetFiles(a)) Assert.Equal(File.ReadAllBytes(path),File.ReadAllBytes(Path.Combine(b,Path.GetFileName(path))));
            var report=JsonNode.Parse(File.ReadAllText(Path.Combine(a,"check.json")))!;
            Assert.True(report["freshDefinitionContinuation"]!.GetValue<bool>()); Assert.True(report["restartMatched"]!.GetValue<bool>());
            Assert.Equal(report["receipts"]!.AsArray().Count+1,report["checkpointCount"]!.GetValue<int>());
            Assert.Equal(1,BasicSkillBattleCheck.Run(a)); Assert.Equal(4,Directory.GetFiles(a).Length);
        }
        finally
        {
            foreach(string directory in new[]{a,b}) if(Directory.Exists(directory))
            { foreach(string name in new[]{"content.package.json","ability.package.json","targeted.package.json","check.json"}) File.Delete(Path.Combine(directory,name)); Directory.Delete(directory); }
            if(Directory.Exists(root)) Directory.Delete(root);
        }
    }
}
