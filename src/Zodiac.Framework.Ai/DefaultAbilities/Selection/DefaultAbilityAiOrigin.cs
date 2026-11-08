using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Potential cast origin, final movement destination and independent selectable aims. No aim is consumed here.</summary>
public sealed record DefaultAbilityAiOrigin(TilePosition CastOrigin, TilePosition MovementDestination, bool ActBeforeMove,
    AiCastOriginPriority? Priority, ImmutableArray<DefaultAbilityAiAim> Aims);
