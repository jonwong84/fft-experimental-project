using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class DashAbilityTests
{
    private static readonly AssetId Dash = new("test:dash");
    private static DashAbilityBattleProfile Profile(AttackBattleSnapshot s, bool grant = true) =>
        new(Ground, [new(Dash)], s.Combatants.Select(u => new TargetedAbilityUnitGrant(u.UnitId, grant ? [Dash] : [], 0)));
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "dash-contract-v1.json")));

    [Fact]
    public void Twenty_four_source_quartile_boundaries_share_exact_neutral_PA_arithmetic()
    {
        using var doc = Fixture(); int count = 0;
        foreach (var row in doc.RootElement.GetProperty("formula").EnumerateArray())
        {
            var d = row.GetProperty("input"); Assert.Equal(row.GetProperty("expected").GetInt32(), DashFormula.Damage(d.GetProperty("pa").GetInt32(), d.GetProperty("raw").GetUInt32())); count++;
        }
        Assert.Equal(24, count); Assert.Throws<ArgumentOutOfRangeException>(() => DashFormula.Damage(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DashFormula.Damage(100, 0)); Assert.Throws<ArgumentOutOfRangeException>(() => DashFormula.Damage(1, 32768));
    }

    [Fact]
    public void Eight_actual_source_tapes_ignore_evade_and_preserve_three_draw_damage_Brave_push_and_KO_order()
    {
        using var doc = Fixture(); int count = 0;
        foreach (var row in doc.RootElement.GetProperty("actual").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected");
            TilePosition Tile(JsonElement t) => new(t[0].GetInt32(), t[1].GetInt32(), 0);
            var from = Tile(d.GetProperty("actor")); var to = Tile(d.GetProperty("target")); int hp = d.GetProperty("hp").GetInt32();
            var specs = new List<Spec> { new("a", from), new("b", to, 1, Math.Min(50, hp)) };
            foreach (var p in d.GetProperty("occupied").EnumerateArray().Select(Tile).Where(p => p != from && p != to).Distinct())
                specs.Add(new("other" + specs.Count, p));
            var s = Make(specs.ToArray(), width: d.GetProperty("width").GetInt32(), depth: d.GetProperty("depth").GetInt32(),
                seed: d.GetProperty("word").GetUInt32(), count: d.GetProperty("count").GetUInt64(), pa: d.GetProperty("pa").GetInt32(), evade: 100);
            var state = new AttackBattleSnapshot(s.State.TurnBattle, s.State.Combatants.Select(u => u.UnitId == B ? new AttackUnitState(B, hp, Math.Max(50, hp), 0, 0, u.PhysicalAttack,
                100, 0, 0, 0, d.GetProperty("targetBrave").GetInt32(), 0, BattleFacing.West, hp <= Math.Max(50, hp) / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None,
                d.GetProperty("ko").GetInt32(), null) : u));
            var before = Snapshot(state); var result = DashAbilityBattleSimulator.ApplyRoster(state, new(A, Dash, B), Profile(state), s.Profile);
            Assert.True(result.Accepted, row.GetProperty("id").GetString());
            var resolved = Assert.Single(result.Events.OfType<DashResolvedEvent>()); Assert.Equal(e.GetProperty("damage").GetInt32(), resolved.Damage);
            Assert.Equal(e.GetProperty("attempted").GetBoolean(), resolved.KnockbackAttempted);
            var target = result.State.Combatants.Single(u => u.UnitId == B); Assert.Equal(e.GetProperty("hp").GetInt32(), target.Hp); Assert.Equal(e.GetProperty("ko").GetInt32(), target.KoCount);
            Assert.Equal(Tile(e.GetProperty("destination")), Position(result.State, B));
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), result.State.TurnBattle.Turns.RngState);
            Assert.Equal(result.State.TurnBattle.Turns.RngState, result.State.TurnBattle.Battle.RngState);
            Assert.True(result.State.TurnBattle.Turns.Units.Single(u => u.Id == A).ActionTaken); Assert.Equal(before, Snapshot(state));
            var replay = DashAbilityBattleSimulator.ApplyRoster(state, new(A, Dash, B), Profile(state), s.Profile);
            Assert.Equal(Snapshot(result.State), Snapshot(replay.State)); Assert.Equal(SerializeEvents(result.Events), SerializeEvents(replay.Events)); count++;
        }
        Assert.Equal(8, count);
    }

    [Fact]
    public void Setup_range_grants_current_choices_and_late_draw_exhaustion_reject_atomically()
    {
        var s = Make(); var command = new UseTargetedAbilityCommand(A, Dash, B);
        void Reject(AttackBattleResult r, AttackBattleSnapshot state, AttackCommandRejection reason)
        { Assert.False(r.Accepted); Assert.Equal(reason, r.AttackRejection); Assert.Same(state, r.State); Assert.Empty(r.Events); }
        Reject(DashAbilityBattleSimulator.ApplyRoster(s.State, command, Profile(s.State, false), s.Profile), s.State, AttackCommandRejection.AbilityNotGranted);
        Reject(DashAbilityBattleSimulator.ApplyRoster(s.State, command with { TargetId = A }, Profile(s.State), s.Profile), s.State, AttackCommandRejection.SelfTarget);
        Reject(DashAbilityBattleSimulator.ApplyRoster(s.State, command with { TargetId = C }, Profile(s.State), s.Profile), s.State, AttackCommandRejection.OutOfRange);
        var used = Make(acted: true); Reject(DashAbilityBattleSimulator.ApplyRoster(used.State, command, Profile(used.State), used.Profile), used.State, AttackCommandRejection.ActionAlreadyTaken);
        foreach (ulong remaining in new ulong[] { 0, 1, 2 })
        {
            var late = Make(count: ulong.MaxValue - remaining);
            Reject(DashAbilityBattleSimulator.ApplyRoster(late.State, command, Profile(late.State), late.Profile), late.State, AttackCommandRejection.RandomDrawCountExhausted);
        }
    }

    [Fact]
    public void Direct_verified_Dash_and_frozen_Move_Dash_share_actual_receipts_and_explicit_settlement()
    {
        var ready = Ready(); var profile = Profile(ready.Battle);
        var direct = RosterDecisionBattle.Execute(ready, A, c => c.PrepareDashAbility(new(A, Dash, B), profile, c.RngState, "Dash"));
        Assert.True(direct.Accepted); Assert.Equal("Dash", direct.Diagnostic); Assert.Null(direct.State.PendingFacing);
        var end = RosterFacingTurnBattle.WaitWithFacing(direct.State, A, BattleFacing.North); Assert.True(end.Accepted);
        var start = Ready(true); var p = Profile(start.Battle);
        var move = RosterDecisionBattle.Execute(start, A, c => c.PrepareMoveThenDashAbility(new(A, new(1, 1, 0)), new(A, Dash, B), p, c.RngState, "frozen"));
        Assert.True(move.Accepted); Assert.NotNull(move.Continuation); Assert.NotNull(move.Path); Assert.Empty(move.Events.OfType<DashResolvedEvent>());
        var act = RosterMoveDecisionBattle.Continue(move.State, A, move.Continuation); Assert.True(act.Accepted); Assert.NotNull(act.State.PendingFacing);
        var finish = RosterFacingTurnBattle.FinishFacing(act.State, A, act.State.PendingFacing, BattleFacing.North); Assert.True(finish.Accepted);
        Assert.Null(finish.State.PendingFacing); Assert.Null(finish.State.Battle.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(direct.State.Battle.Combatants.Single(u => u.UnitId == B).Hp, act.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(direct.State.Battle.TurnBattle.Turns.RngState, act.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(FacingMoveRejection.StaleContinuation, RosterMoveDecisionBattle.Continue(finish.State, A, move.Continuation).Rejection);
    }

    [Fact]
    public void Frozen_Dash_validates_setup_before_Move_but_actual_range_after_Move_and_keeps_failed_progress()
    {
        var ready = Ready(true); var p = Profile(ready.Battle);
        var denied = RosterDecisionBattle.Execute(ready, A, c => c.PrepareMoveThenDashAbility(new(A, new(1, 1, 0)), new(A, Dash, B), Profile(c.Battle, false), c.RngState, 1));
        Assert.False(denied.Accepted); Assert.Same(ready, denied.State); Assert.Null(denied.Continuation);
        var moved = RosterDecisionBattle.Execute(ready, A, c => c.PrepareMoveThenDashAbility(new(A, new(0, 2, 0)), new(A, Dash, B), p, c.RngState, 1));
        Assert.True(moved.Accepted);
        var failed = RosterMoveDecisionBattle.Continue(moved.State, A, moved.Continuation);
        Assert.False(failed.Accepted); Assert.Equal(AttackCommandRejection.OutOfRange, failed.Transition.AttackRejection); Assert.Same(moved.State, failed.State); Assert.Empty(failed.Events);
        Assert.Same(moved.State, RosterMoveDecisionBattle.Continue(moved.State, A, moved.Continuation).State);
        var ended = RosterFacingTurnBattle.WaitWithFacing(moved.State, A); Assert.True(ended.Accepted);
        Assert.Equal(FacingMoveRejection.StaleContinuation, RosterMoveDecisionBattle.Continue(ended.State, A, moved.Continuation).Rejection);
    }
}
