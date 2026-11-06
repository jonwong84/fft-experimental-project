using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Admitted magic preparation. Base hit can only be 100, 50 or 25; class evade remains zero.</summary>
public sealed record MagicEvadePrepared
{
    public byte BaseHit { get; }
    public MagicEvadeValues Evades { get; }
    public MagicEvadeItems Items { get; }
    public bool AbandonTriggered { get; }
    internal MagicEvadePrepared(byte baseHit, MagicEvadeValues evades, MagicEvadeItems items, bool abandonTriggered)
    { BaseHit = baseHit; Evades = evades; Items = items; AbandonTriggered = abandonTriggered; }
}
