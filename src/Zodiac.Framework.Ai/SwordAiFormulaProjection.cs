namespace Zodiac.Framework.Ai;

/// <summary>A formula outcome, before candidate clock advancement or AI scoring.</summary>
/// <param name="NormalDamage">Capped normal damage, before remaining-HP clamping; never probability weighted.</param>
/// <param name="TargetHp">Target HP after applying that damage in the projection.</param>
public sealed record SwordAiFormulaOutcome(int HitPercent, int NormalDamage, int TargetHp)
{
    /// <summary>False for certain evasion; that outcome cannot count as a valuable target hit.</summary>
    public bool HasAccuracy => HitPercent > 0;
}

/// <summary>Successful evaluation may have zero accuracy. Rejection returns no outcome and the incoming RNG.</summary>
/// <remarks>Accepted RNG progress belongs to the decision even if its candidate is later discarded.</remarks>
public sealed record SwordAiFormulaResult(bool Accepted, AttackCommandRejection Rejection,
    SwordAiFormulaOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure ordinary-sword AI formula query over the explicitly selected two-unit flat profile.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-116/117.
/// Uses current combat stats and settled target facing. No execution, choice consumption, battle mutation,
/// critical, proc, knockback, candidate upkeep/clock, scoring or whole-decision RNG claim.
/// </remarks>
public static class SwordAiFormulaProjection
{
    public static SwordAiFormulaResult Evaluate(AttackBattleSnapshot state, AttackUnitCommand command,
        BoundedSwordWeapon? weapon, FlatSwordBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(profile);
        var initialRng = state.TurnBattle.Turns.RngState;
        SwordAiFormulaResult Reject(AttackCommandRejection reason) => new(false, reason, null, initialRng);

        var actor = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.ActorId);
        if (actor is null) return Reject(AttackCommandRejection.UnknownActor);
        var target = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.TargetId);
        if (target is null) return Reject(AttackCommandRejection.UnknownTarget);
        // This first query admits a legal current action, not arbitrary future origins or completed turns.
        if (state.TurnBattle.Turns.ActiveUnitId != actor.UnitId) return Reject(AttackCommandRejection.InactiveActor);
        if (state.TurnBattle.Turns.Units.Single(unit => unit.Id == actor.UnitId).ActionTaken)
            return Reject(AttackCommandRejection.ActionAlreadyTaken);
        if (actor.UnitId == target.UnitId) return Reject(AttackCommandRejection.SelfTarget);
        if (target.Status == AttackUnitStatus.Dead) return Reject(AttackCommandRejection.DeadTarget);
        if (weapon is null || actor.RightHandWeaponId != weapon.Id) return Reject(AttackCommandRejection.UnsupportedSetup);
        try { profile.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return Reject(AttackCommandRejection.UnsupportedSetup); }

        state.TurnBattle.Battle.TryGetUnit(actor.UnitId, out var actorBody);
        state.TurnBattle.Battle.TryGetUnit(target.UnitId, out var targetBody);
        var from = actorBody!.Position; var to = targetBody!.Position;
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) + Math.Abs(dy) != 1 || from.Layer != to.Layer)
            return Reject(AttackCommandRejection.OutOfRange);
        int xa = Math.Max(1, actor.PhysicalAttack); // Neutral ordinary zodiac still floors XA.
        // Retain the execution profile's closed signed-16 domain, even though AI never crits.
        if ((2 * xa - 1) * weapon.Power > short.MaxValue) return Reject(AttackCommandRejection.UnsupportedSetup);
        var front = dx > 0 ? BattleFacing.West : dx < 0 ? BattleFacing.East
            : dy > 0 ? BattleFacing.South : BattleFacing.North;
        int hitPercent = 100 - (target.Facing == front ? target.ClassPhysicalEvade : 0);

        var rng = initialRng;
        try
        {
            // evade_source_roll draws before its execution-state gate, including zero/certain evades.
            var random = new FftBiosRandom();
            for (int source = 0; source < 4; source++) rng = random.Next(rng).State;
        }
        catch (OverflowException) { return Reject(AttackCommandRejection.RandomDrawCountExhausted); }
        // formula_get_random's fixed 0x4000 gives 50%, never a critical (<4%).
        // conditional_status_proc_roll_inner is execution-only: neither step draws here.
        int damage = hitPercent == 0 ? 0 : Math.Min(999, xa * weapon.Power);
        return new(true, AttackCommandRejection.None,
            new(hitPercent, damage, Math.Max(0, target.Hp - damage)), rng);
    }
}
