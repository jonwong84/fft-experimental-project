using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Captured strike facts, not a cached score or a certificate for a whole AI candidate.</summary>
public sealed record DefaultAbilityAiStrikeTarget(BattleUnitId UnitId, ushort Accuracy, bool Enemy, bool Useful,
    bool Untargetable = false);
public sealed record DefaultAbilityAiAccountingOutcome(ImmutableArray<BattleUnitId> IncludedTargets,
    short PriorityDelta, byte HitPercent, bool Valuable);

/// <summary>strikes_simulate exclusions/accounting after formula results, before target application.</summary>
public static class DefaultAbilityAiAccounting
{
    public static DefaultAbilityAiAccountingOutcome Evaluate(AiAbilityBehavior behavior,
        ImmutableArray<DefaultAbilityAiStrikeTarget> targets, ImmutableHashSet<BattleUnitId> hadMoveOpportunity,
        BattleUnitId? followedTargetId = null)
    {
        ArgumentNullException.ThrowIfNull(behavior); ArgumentNullException.ThrowIfNull(hadMoveOpportunity);
        if (targets.IsDefault || targets.Length >= 255 || targets.Any(t => t is null || t.Accuracy > 100 || string.IsNullOrWhiteSpace(t.UnitId.Value)) ||
            targets.Select(t => t.UnitId).Distinct().Count() != targets.Length)
            throw new ArgumentException("Distinct ordinary target pairs below the source accounting sentinel are required.", nameof(targets));
        var included = ImmutableArray.CreateBuilder<BattleUnitId>();
        short priority = 0; ushort total = 0; bool valuable = false;
        bool anchorMoved = followedTargetId is { } id && hadMoveOpportunity.Contains(id);
        foreach (var target in targets)
        {
            if (target.Accuracy == 0) continue;
            if (!behavior.TargetsMapTiles)
            {
                if (target.Untargetable) continue;
                if (anchorMoved)
                {
                    if (target.UnitId != followedTargetId) continue;
                }
                else if (!behavior.TargetsAllies && hadMoveOpportunity.Contains(target.UnitId)) continue;
            }
            bool intended = behavior.TargetsEnemies && target.Enemy || behavior.TargetsAllies && !target.Enemy;
            bool contrary = behavior.TargetsEnemies && !target.Enemy || behavior.TargetsAllies && target.Enemy;
            int delta = intended ? behavior.AffectsStats ? 3 : 0 : contrary ? behavior.AffectsStats ? -4 : -1 : 0;
            priority = unchecked((short)(priority + delta));
            total = unchecked((ushort)(total + target.Accuracy));
            included.Add(target.UnitId); valuable |= target.Useful;
        }
        return new(included.ToImmutable(), priority, included.Count == 0 ? (byte)0 : (byte)(total / included.Count),
            valuable && included.Count != 0);
    }
}
