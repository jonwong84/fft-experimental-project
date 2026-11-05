using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework.Random;
using Xunit;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiDerivedQueryTests;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiContentListTests
{
    private static readonly AbilityId Self = new(SelfData.Self.Value), Rock = new(Stone.Value), Other = new("sample:ability/other");
    private static readonly SkillsetId Basic = new("sample:skillset/basic");
    private static readonly JobId Job = new("sample:job/recruit");
    private static readonly AiHumanAbilityListProfile ListProfile = new(true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);
    private sealed record Fixture(ContentPackage Metadata, ImmediateAbilityContentPackage Immediate,
        TargetedAbilityContentPackage Targeted, BaseJobBattleSetup Setup);

    private static Fixture Content(FacingTurnState state, byte[]? selfFlags = null, byte[]? stoneFlags = null,
        bool reverseSlots = false, bool reverseActions = false)
    {
        SkillsetSlot[] slots = [new(0, reverseActions ? Rock : Self), new(1, null), new(2, reverseActions ? Self : Rock), new(16, Other)];
        var metadata = new ContentPackage("sample:metadata/ai-list", "sample:profile/ai-list", ContentPackageKind.Baseline, null,
            [new JobDefinition(Job, Basic, new(3, 0), false, [])],
            [new SkillsetDefinition(Basic, reverseSlots ? slots.Reverse() : slots)],
            [selfFlags is null ? new AbilityMetadataDefinition(Self, 0, 0, 1,
                new AiAbilityBehavior(TargetsAllies: true, AffectsStats: true, TargetsMapTiles: true), usableByAi: true)
                : AbilityMetadataDefinition.FromOriginalAiFlags(Self, 0, 0, 1, selfFlags),
             stoneFlags is null ? new AbilityMetadataDefinition(Rock, 0, 0, 1,
                new AiAbilityBehavior(TargetsEnemies: true, AffectsHp: true, Evadeable: true, PhysicalAttack: true, DirectTrajectory: true),
                usableByAi: true, aiEvadeWithMotion: true) : AbilityMetadataDefinition.FromOriginalAiFlags(Rock, 0, 0, 1, stoneFlags),
             new AbilityMetadataDefinition(Other, 0, 0, 2, new AiAbilityBehavior(), usableByAi: true)]);
        var setup = BaseJobBattleSetup.Create(new(metadata.Jobs, metadata.Skillsets, metadata.Abilities), state.Battle.TurnBattle.Battle.Map,
            state.Battle.TurnBattle.Battle.Units.Select(u => new BaseJobUnitPlacement(u.Id, Job, u.Position, 6,
                [KeyValuePair.Create(Ground, new TerrainMovementCost(1))])));
        return new(metadata,
            new("sample:effects/self", metadata.ProfileId, metadata.PackageId, [new(Self, ImmediateAbilityEffect.SelfPhysicalAttackIncrease, 1, 0, 0)]),
            new("sample:effects/stone", metadata.ProfileId, metadata.PackageId, [new(Rock, TargetedAbilityEffect.ThrowStoneFlat, 4, 2, 0, 0)]), setup);
    }

    private static AiHumanAbilityLists Lists(Fixture f, bool knowSelf = true, bool knowStone = true, bool secondary = false)
    {
        var known = new List<KnownAiActionSlot>(); if (knowSelf) known.Add(new(Basic, 0)); if (knowStone) known.Add(new(Basic, 2));
        return OrdinaryHumanAiAbilityListSetup.Create(f.Metadata, f.Immediate, f.Targeted, f.Setup,
            new[] { A, B }.Select(id => new OrdinaryHumanAiUnitSelection(id, known, secondary ? Basic : null)), ListProfile);
    }

    private static AccumulateAiPreparationConfiguration Configuration(AiHumanAbilityLists lists, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue)
    {
        var p = SelfData.Configuration(team);
        return p with { AbilityLists = lists, Preparation = p.Preparation with { BothInitializedLists = false } };
    }

    [Fact]
    public void Immutable_metadata_behavior_reaches_owned_entries_and_source_summaries_without_effect_inference()
    {
        var f = Content(Ready()); var list = Lists(f).Units[A];
        var metadata = f.Metadata.Abilities.Single(a => a.Id == Rock);
        var stone = list.Entries.Single(e => e.Kind == AiHumanAbilityKind.ThrowStone);
        Assert.Same(metadata.AiBehavior, stone.Behavior);
        Assert.True(stone.Behavior.AffectsHp); Assert.True(stone.Harmful); Assert.True(stone.Evadeable); Assert.True(stone.HasTrajectory);
        Assert.False(stone.Behavior.AffectedBySilence); Assert.False(stone.Helpful);
        Assert.Equal(2, list.PhysicalHarmfulCount); Assert.Equal(2, list.HarmfulCount); Assert.Equal(1, list.HelpfulCount);
        Assert.Equal(0, list.SilenceQuarterShare); Assert.Equal(2, list.EvadeQuarterShare);
        var disabled = Lists(Content(Ready(), stoneFlags: [130, 2, 65, 1])).Units[A];
        Assert.DoesNotContain(disabled.Entries, e => e.Kind == AiHumanAbilityKind.ThrowStone);
    }

    [Fact]
    public void Changed_AI_targeting_status_trajectory_and_unresolved_static_bits_do_not_silently_use_retail_evaluator()
    {
        foreach (byte[] flags in new byte[][] { [128,2,65,129], [130,0,65,129], [130,3,65,129], [130,2,1,129], [130,2,64,129], [130,2,65,131] })
            Assert.Throws<NotSupportedException>(() => Lists(Content(Ready(), stoneFlags: flags)));
        var behavior = Content(Ready()).Metadata.Abilities.Single(a => a.Id == Rock).AiBehavior;
        AiHumanAbilityLists Build(Zodiac.Framework.Ai.AiAbilityBehavior supplied) => AiHumanAbilityLists.Build(
            [new(A, new(new("sample:semantic"), [new(0, Stone, AiHumanAbilityKind.ThrowStone, true, true, supplied)]))], ListProfile);
        Assert.Same(behavior, Build(behavior).Units[A].Entries[1].Behavior);
        Assert.Throws<NotSupportedException>(() => Build(behavior with { TargetsEnemies = false }));
    }

    [Fact]
    public void Real_metadata_slots_and_resolved_known_state_produce_canonical_lists_without_inferred_learning()
    {
        var state = Ready(); var f = Content(state, reverseSlots: true); var lists = Lists(f);
        Assert.All(lists.Units.Values, list => { Assert.Equal(4, list.InitializedEntryCount); Assert.Equal(2, list.EvadeQuarterShare);
            Assert.Equal(new[] { AiHumanAbilityKind.Attack, AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.FrogAttack }, list.Entries.Select(e => e.Kind)); });
        Assert.All(Lists(f, false, false).Units.Values, list => Assert.Equal(2, list.InitializedEntryCount));
        Assert.All(Lists(f, secondary: true).Units.Values, list => Assert.Equal(6, list.InitializedEntryCount));
        Assert.All(Lists(f, knowSelf: false).Units.Values, list => { Assert.Equal(3, list.InitializedEntryCount); Assert.Equal(2, list.EvadeQuarterShare); });
    }

    [Fact]
    public void Synthetic_zero_AI_bytes_do_not_become_retail_eligibility_and_static_motion_is_not_runtime_consume_policy()
    {
        var state = Ready(); var zero = Lists(Content(state, selfFlags: [0, 0, 0, 0]));
        Assert.DoesNotContain(zero.Units[A].Entries, e => e.Kind == AiHumanAbilityKind.Accumulate);
        var withMotion = Lists(Content(state)); var withoutMotion = Lists(Content(state, stoneFlags: [130, 2, 65, 128]));
        Assert.Equal(Serialize(withMotion.Units[A].Entries), Serialize(withoutMotion.Units[A].Entries));
        Assert.Throws<NotSupportedException>(() => Lists(Content(state, selfFlags: [9, 128, 1, 128])));
    }

    [Fact]
    public void Unknown_units_empty_or_non_action_known_slots_and_foreign_metadata_instances_are_rejected()
    {
        var state = Ready(); var f = Content(state);
        AiHumanAbilityLists Build(IEnumerable<OrdinaryHumanAiUnitSelection> selections, ContentPackage? metadata = null) =>
            OrdinaryHumanAiAbilityListSetup.Create(metadata ?? f.Metadata, f.Immediate, f.Targeted, f.Setup, selections, ListProfile);
        Assert.Throws<ArgumentException>(() => Build([new(A, [])]));
        Assert.Throws<ArgumentException>(() => Build([new(A, []), new(A, [])]));
        foreach (int slot in new[] { 1, 16, 100 })
            Assert.Throws<ArgumentException>(() => Build([new(A, [new(Basic, slot)]), new(B, [])]));
        Assert.Throws<ArgumentException>(() => Build([new(A, []), new(B, [])], Content(state).Metadata));
        Assert.Throws<ArgumentException>(() => new OrdinaryHumanAiUnitSelection(A, [new(Basic, 0), new(Basic, 0)]));
    }

    [Fact]
    public void Content_backed_preparation_matches_certified_streams_and_summaries_for_both_teams_and_actors()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second); var configured = Configuration(Lists(Content(state)), team); var before = Snapshot(state.Battle);
            var actual = AccumulateAiPreparation.Evaluate(state.Battle, SelfData.Request(second), configured, new(1, 7));
            var old = AccumulateAiPreparation.Evaluate(state.Battle, SelfData.Request(second), SelfData.Configuration(team), new(1, 7));
            Assert.True(actual.Accepted, actual.Failure?.ToString()); Assert.Equal(Serialize(old), Serialize(actual));
            Assert.Equal(new RandomState(1103527590, 8), actual.RngState); Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Wrong_order_missing_or_duplicate_actions_and_grant_mismatch_cannot_be_bypassed_by_old_certificate()
    {
        var state = Ready(); var f = Content(state);
        foreach (var list in new[] { Lists(f, knowSelf: false), Lists(f, secondary: true), Lists(Content(state, reverseActions: true)) })
        {
            var configuration = Configuration(list); configuration = configuration with { Preparation = configuration.Preparation with { BothInitializedLists = true } };
            var result = AccumulateAiPreparation.Evaluate(state.Battle, SelfData.Request(), configuration, new(1, 7));
            Assert.False(result.Accepted); Assert.Equal(AccumulateAiPreparationRejection.UnsupportedList, result.Failure!.Reason);
            Assert.Null(result.Outcome); Assert.Equal(new RandomState(1, 7), result.RngState);
        }
        var missingGrant = Configuration(Lists(f)) with { Accumulate = SelfData.Profile(granted: false) };
        Assert.Equal(AccumulateAiPreparationRejection.UnsupportedList,
            AccumulateAiPreparation.Evaluate(state.Battle, SelfData.Request(), missingGrant, new(1, 7)).Failure!.Reason);
    }

    [Fact]
    public void Actual_self_ACT_and_post_ACT_preparation_use_the_same_owned_list_and_preserve_literal_complete_turn()
    {
        var state = Ready(move: 0, word: 7); var configured = Configuration(Lists(Content(state)));
        var profile = DerivedProfile() with { Preparation = configured };
        var command = BasicSkillAiCommandExecution.ExecuteDerived(state, SelfData.Request(), profile, Selection, Dispatch);
        Assert.True(command.Accepted, command.Failure?.ToString()); Assert.Equal(BasicSkillAiCommandKind.Accumulate, command.Outcome!.Kind);
        var rng = command.State.Battle.TurnBattle.Turns.RngState;
        var after = AccumulateAiPreparation.EvaluateAfterAct(command.State.Battle, SelfData.Request(), configured, rng);
        var old = AccumulateAiPreparation.EvaluateAfterAct(command.State.Battle, SelfData.Request(), SelfData.Configuration(), rng);
        Assert.True(after.Accepted); Assert.Equal(Serialize(old), Serialize(after)); Assert.All(after.Outcome!.ActorEntries, e => Assert.False(e.Usable));
        Assert.Equal(1, after.Outcome.Common.EnemyAbilityReach); Assert.Equal(0, after.Outcome.Common.AllyAbilityReach);
        var turn = BasicSkillAiTurnExecution.ExecuteWithWorkspace(state, SelfData.Request(), new(profile, Selection, new(true, true, true)), Dispatch,
            AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map));
        Assert.True(turn.Turn.Completed, turn.Turn.Failure?.ToString()); Assert.Equal(new RandomState(2073554906, 34), turn.Turn.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(6, turn.Turn.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack); Assert.Equal(2, turn.Turn.Decisions.Length);
        Assert.NotEmpty(turn.Workspace.KnownPriorities);
    }

    [Fact]
    public void AI_disabled_self_metadata_can_feed_the_variable_owner_without_removing_gameplay_grants()
    {
        var state = Ready(move: 0, evade: 0); var lists = Lists(Content(state, selfFlags: [0, 0, 0, 0]));
        var configuration = Configuration(lists);
        var result = BasicSkillAiListTurnExecution.Execute(state, SelfData.Request(),
            new(new(configuration, new(true, true, true), true, true, true), new(true, true)), Dispatch);
        Assert.True(result.Completed, result.Failure?.ToString());
        Assert.All(result.Decisions, d => Assert.DoesNotContain(d.Outcome.Selection.Composition.Entries,
            e => e.Entry.Source.Kind == AiHumanAbilityKind.Accumulate));
        Assert.Equal(5, result.State.Battle.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
    }

    [Fact]
    public void Owned_lists_flow_through_retained_priority_recovery_without_new_refresh_or_lost_history()
    {
        var state = Ready(move: 1, evade: 0, target: new(4, 1, 0), count: ulong.MaxValue - 16);
        var history = BasicSkillAiPriorityWorkspaceQueryTests.Seed(state, 1); var profile = DerivedProfile() with { Preparation = Configuration(Lists(Content(state))) };
        var turn = BasicSkillAiTurnExecution.ExecuteWithWorkspace(state, SelfData.Request(), new(profile, Selection, new(true, true, true)), Dispatch, history);
        Assert.False(turn.Turn.Completed); Assert.Equal(BasicSkillAiTurnStage.RetainedCommand, turn.Turn.Failure!.Stage);
        Assert.Single(turn.Turn.Decisions); Assert.Equal((short)0, turn.Workspace.KnownPriorities[new(2, 1, 0)]);
        var retry = BasicSkillAiTurnExecution.ResumeWithWorkspace(turn.Turn.State, turn.Turn.Recovery!);
        Assert.Same(turn.Turn.State, retry.Turn.State); Assert.Same(turn.Workspace, retry.Workspace); Assert.Empty(retry.Turn.Events); Assert.Empty(retry.Turn.Decisions);
    }
}
