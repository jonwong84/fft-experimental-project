using System.Collections.Immutable;

namespace Zodiac.Content.Definitions;

/// <summary>Confirmed high bits of the PS1 primary ability type byte. These do not classify an ability's effects.</summary>
[Flags]
public enum AbilityLearningFlags : byte
{
    None = 0,
    LearnOnHit = 0x20,
    ShowName = 0x40,
    CannotLearnWithJp = 0x80,
}

/// <summary>Immutable learning metadata only; this definition does not supply an executable combat effect.</summary>
public sealed class AbilityDefinition
{
    public AbilityId Id { get; }
    public int JpCost { get; }
    public int LearnOnHitRateRaw { get; }
    public byte TypeFlagsRaw { get; }
    public int SerializedClassRaw => TypeFlagsRaw & 0x0f;
    public byte UnknownTypeFlags => (byte)(TypeFlagsRaw & 0x10);
    public AbilityLearningFlags LearningFlags => (AbilityLearningFlags)(TypeFlagsRaw & 0xe0);
    public bool LearnOnHit => (LearningFlags & AbilityLearningFlags.LearnOnHit) != 0;
    public bool ShowName => (LearningFlags & AbilityLearningFlags.ShowName) != 0;
    public bool CanLearnWithJp => (LearningFlags & AbilityLearningFlags.CannotLearnWithJp) == 0;
    public ImmutableArray<byte> AiFlagsRaw { get; }

    public AbilityDefinition(AbilityId id, int jpCost, int learnOnHitRateRaw, byte typeFlagsRaw,
        IEnumerable<byte> aiFlagsRaw)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        DefinitionValidation.Range(jpCost, ushort.MaxValue, nameof(jpCost));
        DefinitionValidation.Range(learnOnHitRateRaw, byte.MaxValue, nameof(learnOnHitRateRaw));
        ArgumentNullException.ThrowIfNull(aiFlagsRaw);
        ImmutableArray<byte> flags = aiFlagsRaw.ToImmutableArray();
        if (flags.Length != 4)
        {
            throw new ArgumentException($"Ability '{id}' requires exactly four raw AI flag bytes.", nameof(aiFlagsRaw));
        }

        Id = id;
        JpCost = jpCost;
        LearnOnHitRateRaw = learnOnHitRateRaw;
        TypeFlagsRaw = typeFlagsRaw;
        AiFlagsRaw = flags;
    }
}
