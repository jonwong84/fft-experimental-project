using Zodiac.AssetTool;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class OriginalSwordRecordsTests
{
    [Fact]
    public void Item_and_weapon_fields_keep_exact_offsets_unsigned_widths_and_unused_bytes()
    {
        byte[] item = [1, 2, 3, 128, 19, 3, 254, 7, 52, 18, 255, 253];
        var decoded = OriginalSwordRecords.DecodeItem(item);
        Assert.Equal(new OriginalItemRecord(1, 2, 3, 128, 19, 3, 254, 7, 0x1234, 255, 253), decoded);
        Assert.Equal(new OriginalWeaponRecord(1, 142, 1, 255, 4, 5, 254, 127),
            OriginalSwordRecords.DecodeWeapon(new byte[] { 1, 142, 1, 255, 4, 5, 254, 127 }));
        item[8] = item[9] = 255;
        Assert.Equal(ushort.MaxValue, OriginalSwordRecords.DecodeItem(item).Price);
    }

    [Fact]
    public void Proc_preserves_zero_type_and_unknown_masks_without_replacing_them()
    {
        byte[] bytes = [0, 1, 128, 255, 2, 3];
        var proc = OriginalSwordRecords.DecodeProc(bytes);
        Array.Clear(bytes);
        Assert.Equal(0, proc.Type);
        Assert.Equal(new byte[] { 1, 128, 255, 2, 3 }, proc.Statuses);
        Assert.Equal(255, OriginalSwordRecords.DecodeProc(new byte[] { 255, 0, 0, 0, 0, 0 }).Type);
    }

    [Theory]
    [InlineData("item", 11)] [InlineData("item", 13)]
    [InlineData("weapon", 7)] [InlineData("weapon", 9)]
    [InlineData("proc", 5)] [InlineData("proc", 7)]
    public void Truncated_or_oversized_records_fail(string kind, int size)
    {
        byte[] bytes = new byte[size];
        Assert.Throws<InvalidDataException>(() =>
        {
            if (kind == "item") OriginalSwordRecords.DecodeItem(bytes);
            else if (kind == "weapon") OriginalSwordRecords.DecodeWeapon(bytes);
            else OriginalSwordRecords.DecodeProc(bytes);
        });
    }
}
