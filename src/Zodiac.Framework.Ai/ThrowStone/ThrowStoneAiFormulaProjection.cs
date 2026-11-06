using Zodiac.Framework.Random;
using Zodiac.Framework.CombatRules;

namespace Zodiac.Framework.Ai;

public sealed record ThrowStoneAiFormulaOutcome(int HitPercent, int NormalDamage, int TargetHp)
{
    public bool HasAccuracy => HitPercent != 0;
}
public sealed record ThrowStoneAiFormulaResult(bool Accepted, AttackCommandRejection Rejection,
    ThrowStoneAiFormulaOutcome? Outcome, RandomState RngState);

/// <summary>Pure non-execution Throw Stone result; separate accuracy, midpoint damage and four real evasion draws.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-185/186. No knockback, live command or clock.</remarks>
internal static class ThrowStoneAiFormulaProjection
{
    public static ThrowStoneAiFormulaResult Evaluate(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, RandomState decisionRng)
    {
        var invalid = TargetedAbilityBattleSimulator.ValidateCommand(state, command, profile);
        if (invalid != AttackCommandRejection.None) return new(false, invalid, null, decisionRng);
        return EvaluateAdmitted(state, command, decisionRng, profile.Abilities[command.AbilityId].Definition);
    }

    // Pair public admission remains in Framework. Roster owners establish their complete admission separately.
    internal static ThrowStoneAiFormulaResult EvaluateAdmitted(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        RandomState decisionRng, PhysicalAbilityDefinition ability)
    {
        var actor = state.Combatants.Single(unit => unit.UnitId == command.ActorId);
        var target = state.Combatants.Single(unit => unit.UnitId == command.TargetId);
        state.TurnBattle.Battle.TryGetUnit(actor.UnitId, out var from);
        state.TurnBattle.Battle.TryGetUnit(target.UnitId, out var to);
        long dx = (long)to!.Position.X - from!.Position.X, dy = (long)to.Position.Y - from.Position.Y;
        var result = PhysicalDamageRules.Calculate(ability, actor.PhysicalAttack, actor.Brave, target.Brave,
            target.ClassPhysicalEvade, dx, dy, target.Facing, PhysicalDamageMode.Ai, decisionRng);
        if (!result.Accepted) return new(false, AttackCommandRejection.RandomDrawCountExhausted, null, decisionRng);
        int hit = result.Outcome!.Accuracy, damage = result.Outcome.Damage;
        return new(true, AttackCommandRejection.None, new(hit, damage, Math.Max(0, target.Hp - damage)), result.RngState);
    }
}
