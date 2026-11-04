using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void Ai_depends_on_rules_without_introducing_host_content_or_reverse_dependencies()
    {
        var ai = typeof(SwordAiExecution).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.Contains("Zodiac.Framework", ai);
        Assert.DoesNotContain(ai, name => name is "Zodiac.Content" or "Fft.Client" or "Fft.Editor");
        var rules = typeof(FacingDecisionBattle).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain("Zodiac.Framework.Ai", rules);
    }
}
