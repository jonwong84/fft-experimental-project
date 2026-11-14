namespace Zodiac.Framework;

/// <summary>Opaque recomputed candidate bound to one source snapshot. Carries no command, one-use or adoption authority.</summary>
public sealed class MagicProjectionProposal
{
    public MagicProjectionSnapshot Source { get; }
    public MagicProjectionSnapshot Candidate { get; }
    public MagicCastOutcome Cast { get; }
    internal MagicProjectionProposal(MagicProjectionSnapshot source, MagicProjectionSnapshot candidate, MagicCastOutcome cast)
    { Source = source; Candidate = candidate; Cast = cast; }
}
