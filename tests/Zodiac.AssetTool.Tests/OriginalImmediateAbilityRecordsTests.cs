using Zodiac.AssetTool;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class OriginalImmediateAbilityRecordsTests
{
    [Theory] [InlineData(0)] [InlineData(13)] [InlineData(15)]
    public void Secondary_record_has_an_exact_width(int length) =>
        Assert.Throws<InvalidDataException>(() => OriginalImmediateAbilityRecords.Decode(new byte[length]));

    [Fact]
    public void Raw_decoder_preserves_every_unsigned_byte_without_claiming_support()
    {
        byte[] raw = Enumerable.Range(242, 14).Select(value => (byte)value).ToArray();
        var record = OriginalImmediateAbilityRecords.Decode(raw);
        Assert.Equal(242, record.Range); Assert.Equal(249, record.Element); Assert.Equal(255, record.MpCost);
        Array.Clear(raw); Assert.Equal(252, record.Y);
        Assert.Throws<InvalidDataException>(() => OriginalImmediateAbilityRecords.ValidateAccumulate(record));
    }
}
