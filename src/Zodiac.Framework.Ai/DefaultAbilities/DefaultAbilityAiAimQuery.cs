namespace Zodiac.Framework.Ai;

/// <summary>Lists potential ordinary aims at the current origin, without simulation, RNG or command authority.</summary>
public static class DefaultAbilityAiAimQuery
{
    public static DefaultAbilityAiAimResult Evaluate(OrdinaryMagicBattleState state, DefaultAbilityAiCatalog catalog,
        DefaultAbilityAiAimRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            DefaultAbilityAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new DefaultAbilityAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            return new(true, null, DefaultAbilityAiAimEnumeration.Build(work));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return new(false, "Unsupported ordinary spell aim enumeration: " + e.Message, null);
        }
    }
}
