namespace Zodiac.Framework.Ai;

public sealed record ThrowStoneAiFormulaOutcome(int HitPercent, int NormalDamage, int TargetHp)
{
    public bool HasAccuracy => HitPercent != 0;
}
public sealed record ThrowStoneAiFormulaResult(bool Accepted, AttackCommandRejection Rejection,
    ThrowStoneAiFormulaOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure non-execution Throw Stone result; separate accuracy, midpoint damage and four real evasion draws.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-185/186. No knockback, live command or clock.</remarks>
public static class ThrowStoneAiFormulaProjection
{
    public static ThrowStoneAiFormulaResult Evaluate(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, FftRandomState decisionRng)
    {
        var invalid = TargetedAbilityBattleSimulator.ValidateCommand(state, command, profile);
        if (invalid != AttackCommandRejection.None) return new(false, invalid, null, decisionRng);
        var actor = state.Combatants.Single(unit => unit.UnitId == command.ActorId);
        var target = state.Combatants.Single(unit => unit.UnitId == command.TargetId);
        state.TurnBattle.Battle.TryGetUnit(actor.UnitId, out var from);
        state.TurnBattle.Battle.TryGetUnit(target.UnitId, out var to);
        long dx = (long)to!.Position.X - from!.Position.X, dy = (long)to.Position.Y - from.Position.Y;
        int hit = 100 - (ThrowStoneFormula.IsFront(dx, dy, target.Facing) ? target.ClassPhysicalEvade : 0);
        var rng = decisionRng;
        try
        {
            var random = new FftBiosRandom();
            for (int source = 0; source < 4; source++) rng = random.Next(rng).State;
        }
        catch (OverflowException) { return new(false, AttackCommandRejection.RandomDrawCountExhausted, null, decisionRng); }
        // formula_get_random is the fixed midpoint in AI; execution-only Brave/knockback work is skipped.
        int damage = hit == 0 ? 0 : ThrowStoneFormula.Damage(actor.PhysicalAttack, 16384);
        return new(true, AttackCommandRejection.None, new(hit, damage, Math.Max(0, target.Hp - damage)), rng);
    }
}
