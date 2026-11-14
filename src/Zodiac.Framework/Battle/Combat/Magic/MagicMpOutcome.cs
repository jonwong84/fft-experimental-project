namespace Zodiac.Framework;

public sealed record MagicMpOutcome(MagicMpBlock Block, int RecordedCost, ushort CandidateMp, bool SubtractionPerformed)
{
    public bool Usable => Block == MagicMpBlock.None;
}
