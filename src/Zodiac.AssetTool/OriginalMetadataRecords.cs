using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Zodiac.AssetTool;

public sealed record OriginalJobRecord(byte Skillset, ImmutableArray<ushort> Innates, byte Move, byte JumpRaw);

public sealed record OriginalAbilityRecord(ushort JpCost, byte LearnRate, byte TypeFlags, ImmutableArray<byte> AiFlags);

/// <summary>Decodes fixed records without resolving game identities or disc offsets.</summary>
/// <remarks>
/// Layouts traced to job.h, ability.h and main/party.c at decomp revision
/// 611b504d25d4a9b03fab8023d7dbea75fe381d5d. Values remain raw; decoding is not semantic validation.
/// </remarks>
public static class OriginalMetadataRecords
{
    public static OriginalJobRecord DecodeJob(ReadOnlySpan<byte> bytes)
    {
        RequireLength(bytes, 48, "job");
        var innates = ImmutableArray.CreateBuilder<ushort>(4);
        for (int i = 0; i < 4; i++)
            innates.Add(BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(1 + i * 2, 2)));
        return new(bytes[0], innates.MoveToImmutable(), bytes[0x17], bytes[0x18]);
    }

    public static ImmutableArray<ushort> DecodeHumanSkillset(ReadOnlySpan<byte> bytes)
    {
        RequireLength(bytes, 25, "human skillset");
        var abilities = ImmutableArray.CreateBuilder<ushort>(22);
        for (int slot = 0; slot < 22; slot++)
        {
            int high = (bytes[slot / 8] & (0x80 >> (slot % 8))) != 0 ? 0x100 : 0;
            abilities.Add((ushort)(bytes[3 + slot] | high));
        }
        return abilities.MoveToImmutable();
    }

    public static OriginalAbilityRecord DecodeAbility(ReadOnlySpan<byte> bytes)
    {
        RequireLength(bytes, 8, "ability");
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes), bytes[2], bytes[3],
            ImmutableArray.CreateRange(bytes.Slice(4, 4).ToArray()));
    }

    private static void RequireLength(ReadOnlySpan<byte> bytes, int expected, string record)
    {
        if (bytes.Length != expected)
            throw new InvalidDataException($"Original {record} record requires exactly {expected} bytes; received {bytes.Length}.");
    }
}
