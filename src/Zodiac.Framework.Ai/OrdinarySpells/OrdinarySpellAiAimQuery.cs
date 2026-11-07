namespace Zodiac.Framework.Ai;

/// <summary>Lists potential ordinary aims at the current origin, without simulation, RNG or command authority.</summary>
public static class OrdinarySpellAiAimQuery
{
    public static OrdinarySpellAiAimResult Evaluate(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiAimRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            OrdinarySpellAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new OrdinarySpellAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            return new(true, null, OrdinarySpellAiAimEnumeration.Build(work));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return new(false, "Unsupported ordinary spell aim enumeration: " + e.Message, null);
        }
    }
}
