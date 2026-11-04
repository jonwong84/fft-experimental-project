using System.Collections.Immutable;

namespace Zodiac.Content.Definitions;

/// <summary>Immutable learning metadata only; this definition does not supply an executable combat effect.</summary>
public sealed class AbilityMetadataDefinition
{
    public AbilityId Id { get; }
    public int JpCost { get; }
    public int LearnOnHitRateRaw { get; }
    /// <summary>Original serialized classification, not an executable effect or the ID-range classification.</summary>
    public int SerializedClassRaw { get; }
    public bool LearnOnHit { get; }
    public bool ShowName { get; }
    public bool CanLearnWithJp { get; }
    /// <summary>Unresolved original bit 4, preserved without assigning it gameplay meaning.</summary>
    public bool UnknownTypeBit4 { get; }
    public ImmutableArray<byte> AiFlagsRaw { get; }

    public AbilityMetadataDefinition(AbilityId id, int jpCost, int learnOnHitRateRaw, int serializedClassRaw,
        IEnumerable<byte> aiFlagsRaw, bool learnOnHit = false, bool showName = false,
        bool canLearnWithJp = true, bool unknownTypeBit4 = false)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        DefinitionValidation.Range(jpCost, ushort.MaxValue, nameof(jpCost));
        DefinitionValidation.Range(learnOnHitRateRaw, byte.MaxValue, nameof(learnOnHitRateRaw));
        DefinitionValidation.Range(serializedClassRaw, 15, nameof(serializedClassRaw));
        ArgumentNullException.ThrowIfNull(aiFlagsRaw);
        ImmutableArray<byte> flags = aiFlagsRaw.ToImmutableArray();
        if (flags.Length != 4)
        {
            throw new ArgumentException($"Ability '{id}' requires exactly four raw AI flag bytes.", nameof(aiFlagsRaw));
        }

        Id = id;
        JpCost = jpCost;
        LearnOnHitRateRaw = learnOnHitRateRaw;
        SerializedClassRaw = serializedClassRaw;
        LearnOnHit = learnOnHit;
        ShowName = showName;
        CanLearnWithJp = canLearnWithJp;
        UnknownTypeBit4 = unknownTypeBit4;
        AiFlagsRaw = flags;
    }
}
