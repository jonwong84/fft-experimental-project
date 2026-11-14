using System.Text.Json;
using Xunit;

namespace Fft.Client.Tests;

public sealed class WorldCheckpointCheckTests
{
    [Fact]
    public void Headless_check_proves_slots_continuation_and_explicit_recovery_in_a_fresh_directory()
    {
        string root=Path.Combine(Path.GetTempPath(),"fft-world-check-"+Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0,WorldCheckpointCheck.Run(root));
            using var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"check.json")));
            Assert.Equal(10,report.RootElement.GetProperty("catalogSlots").GetInt32());
            Assert.True(report.RootElement.GetProperty("explicitValidatedBackupRecovery").GetBoolean());
            Assert.Equal("unsupported",report.RootElement.GetProperty("rngCapability").GetString());
            byte[] before=File.ReadAllBytes(Path.Combine(root,"check.json"));Assert.Equal(1,WorldCheckpointCheck.Run(root));
            Assert.Equal(before,File.ReadAllBytes(Path.Combine(root,"check.json")));
        }
        finally
        {
            string target=Path.GetFullPath(root),temp=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))+Path.DirectorySeparatorChar;
            if(!target.StartsWith(temp,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)||!Path.GetFileName(target).StartsWith("fft-world-check-",StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test directory.");
            if(Directory.Exists(target))Directory.Delete(target,true);
        }
    }
}
