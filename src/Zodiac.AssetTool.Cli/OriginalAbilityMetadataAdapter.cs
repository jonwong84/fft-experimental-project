using Zodiac.AssetTool;
using Zodiac.Content.Definitions;

namespace Zodiac.AssetTool.Cli;

/// <summary>Translates the original packed ability type byte at the offline import boundary.</summary>
/// <remarks>
/// ability.h::ability_data_t and ability_type_flag_e at reference revision
/// 11464943bbdbd2a0af022d90cb7f10455cf911df. The low nibble is not the ID-range enum.
/// </remarks>
internal static class OriginalAbilityMetadataAdapter
{
    internal static AbilityMetadataDefinition ToDefinition(AbilityId id, OriginalAbilityRecord record) =>
        new(id, record.JpCost, record.LearnRate, record.TypeFlags & 0x0f, record.AiFlags,
            learnOnHit: (record.TypeFlags & 0x20) != 0,
            showName: (record.TypeFlags & 0x40) != 0,
            canLearnWithJp: (record.TypeFlags & 0x80) == 0,
            unknownTypeBit4: (record.TypeFlags & 0x10) != 0);

    // Compare imported metadata to the original row when importing companion effects.
    internal static byte EncodeTypeFlags(AbilityMetadataDefinition definition) => (byte)(
        definition.SerializedClassRaw |
        (definition.UnknownTypeBit4 ? 0x10 : 0) |
        (definition.LearnOnHit ? 0x20 : 0) |
        (definition.ShowName ? 0x40 : 0) |
        (definition.CanLearnWithJp ? 0 : 0x80));
}
