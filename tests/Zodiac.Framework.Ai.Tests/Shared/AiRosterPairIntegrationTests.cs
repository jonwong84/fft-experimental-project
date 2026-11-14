using Xunit;
using Zodiac.Framework.Random;
using Data = Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AiRosterPairIntegrationTests
{
    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Shared_pair_preparation_uses_restored_CT_in_all_three_entry_modes_and_both_actor_orders()
    {
        foreach (bool second in new[] { false, true })
        foreach (var mode in new[] { (Moved: true, Acted: false), (Moved: false, Acted: false), (Moved: false, Acted: true) })
        {
            var state = Data.State(second: second, reverse: second, moved: mode.Moved, acted: mode.Acted);
            var before = Data.Snapshot(state); var request = Data.Request(second, reverse: second);
            var result = mode.Acted
                ? SwordAiPreparation.EvaluateAfterAct(state, request, Data.Candidate, Data.Score, Data.Refresh, Data.Preparation, SwordAiScenarioTeam.NonBlue, Data.Rng)
                : mode.Moved
                ? SwordAiPreparation.Evaluate(state, request, Data.Candidate, Data.Score, Data.Refresh, Data.Preparation, SwordAiScenarioTeam.NonBlue, Data.Rng)
                : SwordAiPreparation.EvaluateUnmoved(state, request, Data.Candidate, Data.Score, Data.Refresh, Data.Preparation, SwordAiScenarioTeam.NonBlue, Data.Rng);
            Assert.True(result.Accepted, result.Rejection.ToString()); var o = result.Outcome!;
            Assert.Equal(new RandomState(1103527590, 8), result.RngState);
            Assert.Equal((17, 1, (byte)9), (o.ActorDelay, o.TargetDelay, o.FoeProximity));
            Assert.True(o.TargetActive); Assert.Equal(new TilePosition(2, 1, 0), o.FocusPosition);
            Assert.Equal(mode.Moved || mode.Acted ? 40 : 60, o.Refresh.Units.Single(u => u.UnitId == request.ActorId).Ct);
            Assert.Equal(0, o.Refresh.Units.Single(u => u.UnitId == request.TargetId).Ct);
            Assert.Equal(before, Data.Snapshot(state));
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Wider_read_only_roster_queries_do_not_authorize_broader_battle_movement_or_combat()
    {
        var state = Data.State(moved: false); var old = state.TurnBattle; var extra = new BattleUnitId("third-unit");
        var body = old.Battle.Units[0]; var combat = state.Combatants[0];
        var wider = new AttackBattleSnapshot(new(new(old.Battle.Map,
            old.Battle.Units.Append(new BattleUnitState(extra, new(4, 4, 0), body.MovementProfile)), old.Battle.RngState),
            new(old.Turns.Units.Append(new TurnUnitState(extra, 5, 0, 99)), old.Turns.RngState, old.Turns.Ticks, old.Turns.Phase)),
            state.Combatants.Append(new AttackUnitState(extra, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, combat.ZodiacSign,
                BattleFacing.East, AttackUnitStatus.None, 0, Data.Weapon)));
        var roster = new AiTargetRoster(Data.A, 17, [
            new(Data.A, 2, new(1, 1, 0), new(), new(Considered: true), 17, false),
            new(Data.B, 17, new(2, 1, 0), new(), new(Enemy: true, Considered: true), 1, false),
            new(extra, 99, new(4, 4, 0), new(), new(Enemy: true, Considered: true), 20, false)]);
        Assert.Equal(Data.B, AiRosterNearest.FindEligibleEnemy(roster, roster.ById.Keys).TargetId);
        Assert.Equal(3, AiRosterActiveTargets.Select(roster, AiRosterTargetFilter.Any).SelectedUnits.Length);
        Assert.Single(AiRosterProximity.Evaluate(roster, [new(1, 1, 0)]).TargetableEnemies);
        Assert.Throws<NotSupportedException>(() => Data.Candidate.Sword.ValidateState(wider));
        Assert.False(FlatOrdinaryMovement.Evaluate(wider, Data.A, Data.Candidate.Movement).Accepted);
        var prepared = SwordAiPreparation.EvaluateUnmoved(wider, Data.Request(), Data.Candidate, Data.Score, Data.Refresh, Data.Preparation,
            SwordAiScenarioTeam.NonBlue, Data.Rng);
        Assert.False(prepared.Accepted); Assert.Null(prepared.Outcome); Assert.Equal(Data.Rng, prepared.RngState);
    }
}
