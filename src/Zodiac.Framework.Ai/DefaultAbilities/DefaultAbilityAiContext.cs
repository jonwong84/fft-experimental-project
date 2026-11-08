namespace Zodiac.Framework.Ai;

/// <summary>Immutable captured battle inputs for a pure ordinary spell query. No prepared work, RNG or command authority.</summary>
/// <remarks>Create a new context for a new battle state. An existing context remains usable for replay of its captured state.</remarks>
public sealed class DefaultAbilityAiContext
{
    public OrdinaryMagicBattleState State { get; }
    public DefaultAbilityAiCatalog Catalog { get; }

    /// <summary>Pair raw inputs. Actor, phase, ownership, current resources and profile are admitted by each query.</summary>
    /// <exception cref="ArgumentNullException">State or catalog is null.</exception>
    /// <exception cref="ArgumentException">The catalog belongs to a different battle definition instance.</exception>
    public DefaultAbilityAiContext(OrdinaryMagicBattleState state, DefaultAbilityAiCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!ReferenceEquals(state.Definition, catalog.Definition))
        {
            throw new ArgumentException("State and catalog must refer to the same battle definition instance.", nameof(catalog));
        }

        State = state;
        Catalog = catalog;
    }
}
