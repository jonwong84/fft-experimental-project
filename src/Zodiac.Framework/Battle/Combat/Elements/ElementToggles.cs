using System.Text.Json.Serialization;

namespace Zodiac.Framework;

/// <summary>Immutable shared element switches; use a with expression to create an edited copy.</summary>
public sealed record ElementToggles
{
    public static ElementToggles None { get; } = new();

    public bool Fire { get; init; }
    public bool Ice { get; init; }
    public bool Lightning { get; init; }
    public bool Wind { get; init; }
    public bool Earth { get; init; }
    public bool Water { get; init; }
    public bool Holy { get; init; }
    public bool Dark { get; init; }

    // Reserved custom elements; the original-game rules do not admit them yet.
    public bool Gravity { get; init; }
    public bool Poison { get; init; }
    public bool Restore { get; init; }

    [JsonIgnore]
    public bool HasCustomElements => Gravity || Poison || Restore;

    /// <summary>Neutral means no element is enabled, rather than a separate element.</summary>
    [JsonIgnore]
    public bool Neutral => !(Fire || Ice || Lightning || Wind || Earth || Water || Holy || Dark || HasCustomElements);

    public bool Overlaps(ElementToggles other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Fire && other.Fire || Ice && other.Ice || Lightning && other.Lightning ||
            Wind && other.Wind || Earth && other.Earth || Water && other.Water ||
            Holy && other.Holy || Dark && other.Dark || Gravity && other.Gravity ||
            Poison && other.Poison || Restore && other.Restore;
    }
}
