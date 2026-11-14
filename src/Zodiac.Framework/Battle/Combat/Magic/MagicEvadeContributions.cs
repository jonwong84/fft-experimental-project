using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Immutable magic contributions and independent source blocking-item labels.</summary>
public sealed record MagicEvadeContributions
{
    public MagicEvadeValues Evades { get; }
    public MagicEvadeItems Items { get; }
    internal MagicEvadeContributions(MagicEvadeValues evades, MagicEvadeItems items)
    { Evades = evades; Items = items; }
}
