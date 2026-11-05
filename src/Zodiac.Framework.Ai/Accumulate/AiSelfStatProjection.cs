using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

internal sealed record AiSelfStatProjectionResult(int Before, int After, AiEffectFreeProjectionResult Projection);

/// <summary>Zero-formula-draw, zero-bonus self PA effect and shared private clock after the owner admits a valuable self hit.</summary>
internal static class AiSelfStatProjection
{
    internal static AiSelfStatProjectionResult Evaluate(AttackBattleSnapshot state, BattleUnitId actorId,
        TilePosition destination, ImmutableArray<AiUnitBaseline> baselines, RandomState rng)
    {
        int before = state.Combatants.Single(u => u.UnitId == actorId).PhysicalAttack;
        int after = AccumulateFormula.IncreasePhysicalAttack(before);
        return new(before, after, AiEffectFreeProjection.Evaluate(state, actorId, destination, baselines, rng, consumeAction: true));
    }
}
