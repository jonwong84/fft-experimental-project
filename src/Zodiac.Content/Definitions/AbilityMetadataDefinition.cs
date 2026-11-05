using System.Collections.Immutable;
using Zodiac.Framework.Ai;

namespace Zodiac.Content.Definitions;

/// <summary>Immutable learning and semantic AI metadata; this definition does not supply an executable combat effect.</summary>
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
    /// <summary>Derived from the preserved original bytes; not a second editable configuration source.</summary>
    public AiAbilityBehavior AiBehavior { get; }
    public bool UsableByAi => (AiFlagsRaw[3] & 128) != 0;
    /// <summary>Original static bit, deliberately separate from runtime consume-aim policy.</summary>
    public bool AiEvadeWithMotion => (AiFlagsRaw[3] & 1) != 0;
    public bool HasUnresolvedAiFlags => (AiFlagsRaw[3] & ~129) != 0;

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
        AiBehavior = OriginalAbilityAiFlags.Decode(flags);
    }
}
