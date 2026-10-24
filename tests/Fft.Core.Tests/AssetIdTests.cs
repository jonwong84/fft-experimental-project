using Fft.Core;
using Xunit;

namespace Fft.Core.Tests;

public sealed class AssetIdTests
{
    [Fact]
    public void AssetId_preserves_valid_namespaced_value()
    {
        var id = new AssetId("base:battle/cyoko.spr");

        Assert.Equal("base:battle/cyoko.spr", id.Value);
        Assert.Equal("base:battle/cyoko.spr", id.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("BATTLE/CYOKO.SPR")]
    [InlineData("base:../outside")]
    [InlineData("base:Upper/Case")]
    public void AssetId_rejects_unqualified_or_unsafe_value(string value)
    {
        Assert.Throws<ArgumentException>(() => new AssetId(value));
    }
}
