using Zodiac.AssetTool;
using Xunit;

namespace Zodiac.AssetTool.Tests;

// Synthetic bytes, hand-checked against layouts at reference 611b504d25d4a9b03fab8023d7dbea75fe381d5d.
public sealed class OriginalMetadataRecordsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void Job_raw_minimum_and_maximum_values_are_not_semantically_clamped(byte value)
    {
        byte[] bytes = Enumerable.Repeat(value, 48).ToArray();
        OriginalJobRecord job = OriginalMetadataRecords.DecodeJob(bytes);
        Assert.Equal(value, job.Skillset);
        Assert.Equal(value, job.Move);
        Assert.Equal(value, job.JumpRaw);
        Assert.Equal(Enumerable.Repeat((ushort)(value == 0 ? 0 : ushort.MaxValue), 4), job.Innates);
    }

    [Fact]
    public void Job_uses_four_little_endian_words_and_exact_movement_offsets_with_immutable_copy()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xcc, 48).ToArray();
        bytes[0] = 5;
        new byte[] { 0x34, 0x12, 0xff, 0x01, 0x00, 0x80, 0x34, 0x12 }.CopyTo(bytes, 1);
        bytes[0x17] = 200;
        bytes[0x18] = 0xff;
        OriginalJobRecord job = OriginalMetadataRecords.DecodeJob(bytes);
        Array.Clear(bytes);
        Assert.Equal(5, job.Skillset);
        Assert.Equal(new ushort[] { 0x1234, 0x01ff, 0x8000, 0x1234 }, job.Innates);
        Assert.Equal(200, job.Move);
        Assert.Equal(255, job.JumpRaw);
    }

    [Theory]
    [InlineData(0, 0, 0x80)]
    [InlineData(7, 0, 0x01)]
    [InlineData(8, 1, 0x80)]
    [InlineData(15, 1, 0x01)]
    [InlineData(16, 2, 0x80)]
    [InlineData(21, 2, 0x04)]
    public void Skillset_high_bits_are_msb_first_across_packed_boundaries(int selectedSlot, int highByte, byte mask)
    {
        var bytes = new byte[25];
        bytes[highByte] = mask;
        for (int slot = 0; slot < 22; slot++) bytes[3 + slot] = (byte)(slot + 1);
        var decoded = OriginalMetadataRecords.DecodeHumanSkillset(bytes);
        Assert.Equal(22, decoded.Length);
        for (int slot = 0; slot < 22; slot++)
            Assert.Equal((ushort)(slot + 1 + (slot == selectedSlot ? 256 : 0)), decoded[slot]);
    }

    [Fact]
    public void Skillset_retains_empty_and_duplicate_slots_and_ignores_unused_high_bits()
    {
        var bytes = new byte[25];
        bytes[3 + 1] = 42;
        bytes[3 + 2] = 42;
        var before = OriginalMetadataRecords.DecodeHumanSkillset(bytes);
        bytes[2] = 3;
        var after = OriginalMetadataRecords.DecodeHumanSkillset(bytes);
        Array.Fill(bytes, (byte)255);
        Assert.Equal(before.ToArray(), after.ToArray());
        Assert.Equal(0, after[0]);
        Assert.Equal(42, after[1]);
        Assert.Equal(42, after[2]);
        Assert.Equal(0, after[21]);
        Assert.All(OriginalMetadataRecords.DecodeHumanSkillset(bytes), id => Assert.Equal(511, id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void Ability_preserves_all_raw_metadata_and_copies_ai_bytes(byte value)
    {
        byte[] bytes = Enumerable.Repeat(value, 8).ToArray();
        OriginalAbilityRecord ability = OriginalMetadataRecords.DecodeAbility(bytes);
        Array.Clear(bytes);
        Assert.Equal((ushort)(value == 0 ? 0 : ushort.MaxValue), ability.JpCost);
        Assert.Equal(value, ability.LearnRate);
        Assert.Equal(value, ability.TypeFlags);
        Assert.Equal(Enumerable.Repeat(value, 4), ability.AiFlags);
    }

    [Fact]
    public void Ability_layout_uses_little_endian_jp_and_four_distinct_ai_bytes()
    {
        OriginalAbilityRecord ability = OriginalMetadataRecords.DecodeAbility(new byte[] { 0x34, 0x12, 201, 0x1f, 1, 128, 255, 17 });
        Assert.Equal(0x1234, ability.JpCost);
        Assert.Equal(201, ability.LearnRate);
        Assert.Equal(0x1f, ability.TypeFlags);
        Assert.Equal(new byte[] { 1, 128, 255, 17 }, ability.AiFlags);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(47)]
    [InlineData(49)]
    public void Job_requires_exact_record_length(int length) =>
        Assert.Contains("exactly 48 bytes", Assert.Throws<InvalidDataException>(() =>
            OriginalMetadataRecords.DecodeJob(new byte[length])).Message);

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(26)]
    public void Human_skillset_requires_exact_record_length(int length) =>
        Assert.Contains("exactly 25 bytes", Assert.Throws<InvalidDataException>(() =>
            OriginalMetadataRecords.DecodeHumanSkillset(new byte[length])).Message);

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    public void Ability_requires_exact_record_length(int length) =>
        Assert.Contains("exactly 8 bytes", Assert.Throws<InvalidDataException>(() =>
            OriginalMetadataRecords.DecodeAbility(new byte[length])).Message);
}
