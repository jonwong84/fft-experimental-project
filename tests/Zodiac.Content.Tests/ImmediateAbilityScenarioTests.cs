using System.Text.Json;
using System.Text.Json.Nodes;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class ImmediateAbilityScenarioTests
{
    private static BattleUnitId A => SyntheticTwoUnitBattle.FirstUnitId;
    private static BattleUnitId B => SyntheticTwoUnitBattle.SecondUnitId;
    private static AssetId Ability => new(SyntheticAccumulateBattle.AccumulateId.Value);
    private static TwoUnitBattleCommand.UseAbility Use => new(A, Ability);

    [Fact]
    public void Boost_survives_turns_and_changes_later_sword_damage_with_the_original_draw_order()
    {
        var state = SyntheticAccumulateBattle.Create().Start();
        state = Accepted(state, new TwoUnitBattleCommand.Advance());
        state = Accepted(state, Use);
        state = Accepted(state, new TwoUnitBattleCommand.Wait(A));
        Assert.Equal(20, state.Battle.TurnBattle.Turns.Units[0].Ct);
        state = Accepted(state, new TwoUnitBattleCommand.Advance());
        Assert.Equal(B, state.Battle.TurnBattle.Turns.ActiveUnitId);
        state = Accepted(state, new TwoUnitBattleCommand.Wait(B));
        state = Accepted(state, new TwoUnitBattleCommand.Advance());
        // B waited with both choices unused (40 CT), so it becomes ready again before A's 20-CT remainder.
        Assert.Equal(B, state.Battle.TurnBattle.Turns.ActiveUnitId);
        state = Accepted(state, new TwoUnitBattleCommand.Wait(B));
        state = Accepted(state, new TwoUnitBattleCommand.Advance());
        Assert.Equal(A, state.Battle.TurnBattle.Turns.ActiveUnitId);
        state = Accepted(state, new TwoUnitBattleCommand.Move(A, new(0, 1, 0)));
        var rng = state.Battle.TurnBattle.Turns.RngState;
        var result = TwoUnitBattleScenario.Apply(state, new TwoUnitBattleCommand.Attack(A, B));
        Assert.True(result.Accepted);
        var hit = Assert.Single(result.Events.OfType<TwoUnitBattleTransitionEvent>().Select(ev => ev.Event).OfType<AttackResolvedEvent>());
        Assert.Equal(new AttackResolvedEvent(A, B, true, false, 24), hit);
        Assert.Equal(26, result.State.Battle.Combatants[1].Hp);
        Assert.Equal(rng.DrawCount + 6, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        var independent = rng;
        for (int i = 0; i < 6; i++) independent = new FftBiosRandom().Next(independent).State;
        Assert.Equal(independent, result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(6, result.State.Battle.Combatants[0].PhysicalAttack);
        // A fresh unboosted actor with the same RNG/turn/geometry uses PA5 => damage20.
        var unit = state.Battle.Combatants[0];
        var unboosted = new AttackUnitState(A, unit.Hp, unit.MaxHp, 0, 0, 5, 0, 0, 0, 0, unit.Brave, unit.ZodiacSign,
            unit.Facing, unit.Status, unit.KoCount, unit.RightHandWeaponId);
        var baseline = new AttackBattleSnapshot(state.Battle.TurnBattle, [unboosted, state.Battle.Combatants[1]]);
        var attack = AttackBattleSimulator.Attack(baseline, new(A, B), state.Definition.Weapons.Values.Single());
        Assert.Equal(20, Assert.Single(attack.Events.OfType<AttackResolvedEvent>()).Damage);
        Assert.Equal(attack.State.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Turns.RngState);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Move_and_ability_can_be_taken_in_either_order(bool moveFirst)
    {
        var state = Accepted(SyntheticAccumulateBattle.Create().Start(), new TwoUnitBattleCommand.Advance());
        TwoUnitBattleCommand move = new TwoUnitBattleCommand.Move(A, new(0, 1, 0));
        var rng = state.Battle.TurnBattle.Turns.RngState;
        state = Accepted(state, moveFirst ? move : Use);
        Assert.Equal(A, state.Battle.TurnBattle.Turns.ActiveUnitId);
        state = Accepted(state, moveFirst ? Use : move);
        Assert.Null(state.Battle.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(0, state.Battle.TurnBattle.Turns.Units[0].Ct);
        Assert.Equal(6, state.Battle.Combatants[0].PhysicalAttack);
        Assert.Equal(rng, state.Battle.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Many_legal_turns_saturate_and_replay_including_rejected_actions_and_restart()
    {
        var definition = SyntheticAccumulateBattle.Create();
        var state = definition.Start();
        var journal = new List<TwoUnitBattleCommand>();
        var results = new List<string>();
        int uses = 0;
        while (uses < 100)
        {
            Submit(new TwoUnitBattleCommand.Advance());
            var active = state.Battle.TurnBattle.Turns.ActiveUnitId!.Value;
            if (active == A)
            {
                Submit(Use); uses++;
                var duplicate = TwoUnitBattleScenario.Apply(state, Use);
                Assert.False(duplicate.Accepted); Assert.Same(state, duplicate.State);
                Submit(Use);
            }
            Submit(new TwoUnitBattleCommand.Wait(active));
        }
        Assert.Equal(99, state.Battle.Combatants[0].PhysicalAttack);
        Assert.Equal(5, state.Battle.Combatants[1].PhysicalAttack);
        Assert.Equal((ulong)journal.OfType<TwoUnitBattleCommand.Advance>().Count(), state.Battle.TurnBattle.Turns.RngState.DrawCount);
        var replay = SyntheticAccumulateBattle.Create().Start();
        for (int i = 0; i < journal.Count; i++)
        {
            var result = TwoUnitBattleScenario.Apply(replay, journal[i]);
            Assert.Equal(results[i], Receipt(result)); replay = result.State;
        }
        Assert.Equal(5, definition.Start().Battle.Combatants[0].PhysicalAttack);
        Assert.Contains(Ability, definition.Start().Definition.ImmediateAbilities!.Units[A].Abilities);

        void Submit(TwoUnitBattleCommand command)
        {
            journal.Add(command);
            var result = TwoUnitBattleScenario.Apply(state, command);
            if (command is not TwoUnitBattleCommand.UseAbility || !state.Battle.TurnBattle.Turns.Units[0].ActionTaken)
                Assert.True(result.Accepted);
            results.Add(Receipt(result)); state = result.State;
        }
    }

    [Theory] [InlineData(0)] [InlineData(100)] [InlineData(255)]
    public void New_profile_restricts_initial_PA_without_changing_the_old_sword_profile(int pa)
    {
        Assert.Throws<NotSupportedException>(() => SyntheticAccumulateBattle.Create(pa));
        var source = SyntheticTwoUnitBattle.Create();
        var unit = source.InitialBattle.Combatants[0];
        var changed = new AttackUnitState(A, 50, 50, 0, 0, pa, 10, 0, 0, 0, 70, 0, unit.Facing, AttackUnitStatus.None, 0, unit.RightHandWeaponId);
        var old = new TwoUnitBattleDefinition(new("sample:scenario/old-pa"), A, B,
            new(source.InitialBattle.TurnBattle, [changed, source.InitialBattle.Combatants[1]]), source.Weapons.Values);
        Assert.Null(old.ImmediateAbilities);
    }

    [Fact]
    public void Setup_checks_worst_reachable_PA_and_rejects_missing_or_ungranted_actions()
    {
        _ = SyntheticAccumulateBattle.Create(1, 166);
        Assert.Throws<NotSupportedException>(() => SyntheticAccumulateBattle.Create(1, 167));
        var state = Accepted(SyntheticAccumulateBattle.Create(grantFirstUnit: false).Start(), new TwoUnitBattleCommand.Advance());
        var noGrant = TwoUnitBattleScenario.Apply(state, Use);
        Assert.Equal(AttackCommandRejection.AbilityNotGranted, noGrant.AttackRejection);
        Assert.Same(state, noGrant.State); Assert.Empty(noGrant.Events);
        var unknown = TwoUnitBattleScenario.Apply(state, new TwoUnitBattleCommand.UseAbility(A, new("sample:ability/unknown")));
        Assert.Equal(AttackCommandRejection.UnknownAbility, unknown.AttackRejection);
        var old = SyntheticTwoUnitBattle.Create().Start();
        Assert.Equal(TwoUnitBattleRejection.UnsupportedCommand, TwoUnitBattleScenario.Apply(old, Use).ScenarioRejection);
        Assert.Null(SyntheticTwoUnitBattle.Create().ImmediateAbilities);
    }

    [Fact]
    public void First_KO_still_finishes_the_scenario_and_all_later_ability_commands_reject_atomically()
    {
        var state = Accepted(SyntheticAccumulateBattle.Create(99).Start(), new TwoUnitBattleCommand.Advance());
        state = Accepted(state, new TwoUnitBattleCommand.Move(A, new(0, 1, 0)));
        state = Accepted(state, new TwoUnitBattleCommand.Attack(A, B));
        Assert.Equal(TwoUnitBattleOutcome.FirstUnitWon, state.Outcome);
        var result = TwoUnitBattleScenario.Apply(state, Use);
        Assert.Equal(TwoUnitBattleRejection.AlreadyCompleted, result.ScenarioRejection);
        Assert.Same(state, result.State); Assert.Empty(result.Events);
    }

    [Theory] [InlineData(5, false)] [InlineData(5, true)] [InlineData(99, true)]
    public void Ability_profiles_cannot_save_or_load_even_before_boost_or_at_no_change_cap(int pa, bool use)
    {
        var definition = SyntheticAccumulateBattle.Create(pa);
        var state = definition.Start();
        if (use) state = Accepted(Accepted(state, new TwoUnitBattleCommand.Advance()), Use);
        var profile = new TwoUnitBattleSaveProfile(SyntheticAccumulateBattle.ProfileId, 1, 1, definition);
        Assert.Contains("ZOD-106", Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.ToJson(profile, state)).Message);
        var legacy = SyntheticTwoUnitBattle.CreateSaveProfile();
        string old = TwoUnitBattleSave.ToJson(legacy, legacy.Definition.Start());
        Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.ReadJson(profile, old));
        Assert.Throws<NotSupportedException>(() => definition.Restore(state.Battle));
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "preserve me");
            Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.Save(profile, state, file));
            Assert.Equal("preserve me", File.ReadAllText(file));
            Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.Load(profile, file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Content_round_trip_is_ordered_and_does_not_change_learning_metadata()
    {
        var metadata = SyntheticAccumulateBattle.Metadata(); string before = metadata.ToJson();
        var original = SyntheticAccumulateBattle.Effects(metadata);
        var package = ImmediateAbilityContentPackage.ReadJson(original.ToJson());
        package.ValidateAgainst(metadata);
        Assert.Equal(original.ToJson(), package.ToJson()); Assert.DoesNotContain("\r", package.ToJson());
        Assert.Equal(before, metadata.ToJson());
        var ability = Assert.Single(package.Abilities); Assert.Equal(1, ability.Amount);
        var list = new List<ImmediateAbilityDefinition> { ability };
        var snapshot = new ImmediateAbilityContentPackage(original.PackageId, original.ProfileId, original.RequiredMetadataPackageId, list);
        list.Clear(); Assert.Single(snapshot.Abilities);
        var other = new ImmediateAbilityDefinition(new("sample:ability/z-other"), ImmediateAbilityEffect.SelfPhysicalAttackIncrease, 1, 0, 0);
        var forward = new ImmediateAbilityContentPackage(original.PackageId, original.ProfileId, original.RequiredMetadataPackageId, [ability, other]);
        var reverse = new ImmediateAbilityContentPackage(original.PackageId, original.ProfileId, original.RequiredMetadataPackageId, [other, ability]);
        Assert.Equal(forward.ToJson(), reverse.ToJson());
    }

    [Theory]
    [InlineData("format")] [InlineData("version")] [InlineData("unknown-root")] [InlineData("unknown-effect")]
    [InlineData("target")] [InlineData("amount")] [InlineData("charge")] [InlineData("mp")]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("foreign")]
    [InlineData("fraction")] [InlineData("null-record")] [InlineData("array-root")]
    public void Unsupported_or_malformed_content_fails_with_a_field_diagnostic(string kind)
    {
        var root = JsonNode.Parse(SyntheticAccumulateBattle.Effects(SyntheticAccumulateBattle.Metadata()).ToJson())!;
        var effect = root["abilities"]![0]!;
        switch (kind)
        {
            case "format": root["format"] = "other"; break;
            case "version": root["schemaVersion"] = 2; break;
            case "unknown-root": root["extra"] = 1; break;
            case "unknown-effect": effect["effect"] = "damage"; break;
            case "target": effect["target"] = "enemy"; break;
            case "amount": effect["amount"] = 2; break;
            case "charge": effect["chargeTicks"] = 1; break;
            case "mp": effect["mpCost"] = 1; break;
            case "missing": effect.AsObject().Remove("amount"); break;
            case "duplicate": root["abilities"]!.AsArray().Add(effect.DeepClone()); break;
            case "foreign": effect["id"] = "foreign:ability/other"; break;
            case "fraction": effect["amount"] = 1.5; break;
            case "null-record": root["abilities"]![0] = null; break;
            case "array-root": root = new JsonArray(); break;
        }
        Assert.NotEmpty(Assert.Throws<ContentPackageValidationException>(() => ImmediateAbilityContentPackage.ReadJson(root.ToJsonString())).Message);
        string duplicate = SyntheticAccumulateBattle.Effects(SyntheticAccumulateBattle.Metadata()).ToJson().Replace("\"amount\": 1", "\"amount\": 1, \"amount\": 1", StringComparison.Ordinal);
        Assert.Throws<ContentPackageValidationException>(() => ImmediateAbilityContentPackage.ReadJson(duplicate));
    }

    [Theory] [InlineData("unit")] [InlineData("slot")] [InlineData("ability")] [InlineData("duplicate")] [InlineData("bonus")]
    public void Learned_grants_require_the_resolved_action_slot_and_supported_domain(string kind)
    {
        var metadata = SyntheticAccumulateBattle.Metadata();
        var setup = Setup(metadata);
        var grant = new LearnedImmediateAbilityGrant(kind == "unit" ? new("sample:unit/unknown") : A,
            kind == "slot" ? 1 : 0, kind == "ability" ? new("sample:ability/unknown") : SyntheticAccumulateBattle.AccumulateId);
        void Create() => ImmediateAbilityBattleSetup.CreateProfile(metadata, SyntheticAccumulateBattle.Effects(metadata), setup,
            kind == "duplicate" ? [grant, grant] : [grant], kind == "bonus" ? 1 : 0);
        if (kind == "bonus") Assert.Throws<NotSupportedException>(Create);
        else Assert.Throws<ArgumentException>(Create);
    }

    [Fact]
    public void Authored_action_slots_do_not_inherit_the_original_table_capacity()
    {
        var original = SyntheticAccumulateBattle.Metadata();
        var metadata = new ContentPackage(original.PackageId, original.ProfileId, ContentPackageKind.Baseline, null, original.Jobs,
            [new(original.Skillsets[0].Id, [new(2048, SyntheticAccumulateBattle.AccumulateId)])], original.Abilities);
        var profile = ImmediateAbilityBattleSetup.CreateProfile(metadata, SyntheticAccumulateBattle.Effects(metadata), Setup(metadata),
            [new(A, 2048, SyntheticAccumulateBattle.AccumulateId)], 0);
        Assert.Contains(Ability, profile.Units[A].Abilities);
    }

    [Theory] [InlineData("dependency")] [InlineData("profile")] [InlineData("missing")]
    [InlineData("reaction")]
    public void Companion_requires_the_selected_ordinary_action_metadata(string kind)
    {
        var source = SyntheticAccumulateBattle.Metadata();
        var original = SyntheticAccumulateBattle.Effects(source);
        var metadata = new ContentPackage(source.PackageId, source.ProfileId, ContentPackageKind.Baseline, null,
            kind == "missing" ? [] : source.Jobs, kind == "missing" ? [] : source.Skillsets,
            kind == "missing" ? [] : kind == "reaction" ? [new(SyntheticAccumulateBattle.AccumulateId, 0, 0, 7, new byte[4])] : source.Abilities);
        var package = new ImmediateAbilityContentPackage(original.PackageId,
            kind == "profile" ? "sample:profile/wrong" : original.ProfileId,
            kind == "dependency" ? "sample:package/wrong" : original.RequiredMetadataPackageId, original.Abilities);
        Assert.Throws<ContentPackageValidationException>(() => package.ValidateAgainst(metadata));
    }

    [Theory] [InlineData("effect")] [InlineData("amount")] [InlineData("charge")] [InlineData("mp")]
    public void Typed_effects_also_reject_unimplemented_variants(string kind) =>
        Assert.Throws<NotSupportedException>(() => new ImmediateAbilityDefinition(SyntheticAccumulateBattle.AccumulateId,
            kind == "effect" ? (ImmediateAbilityEffect)99 : ImmediateAbilityEffect.SelfPhysicalAttackIncrease,
            kind == "amount" ? 128 : 1, kind == "charge" ? 1 : 0, kind == "mp" ? 1 : 0));

    [Fact]
    public void Json_limits_and_required_collections_are_enforced_before_projection()
    {
        var metadata = SyntheticAccumulateBattle.Metadata(); var source = SyntheticAccumulateBattle.Effects(metadata);
        Assert.Throws<ContentPackageValidationException>(() => ImmediateAbilityContentPackage.ReadJson(new string(' ', ContentPackage.MaxJsonBytes + 1)));
        Assert.Throws<ContentPackageValidationException>(() => ImmediateAbilityContentPackage.ReadJson("{"));
        Assert.Throws<ContentPackageValidationException>(() => ImmediateAbilityContentPackage.ReadJson(null!));
        Assert.Throws<ContentPackageValidationException>(() => new ImmediateAbilityContentPackage(source.PackageId, source.ProfileId, source.RequiredMetadataPackageId, null!));
        Assert.Throws<ContentPackageValidationException>(() => new ImmediateAbilityContentPackage(metadata.PackageId, source.ProfileId, metadata.PackageId, source.Abilities));
        Assert.Throws<ContentPackageValidationException>(() => new ImmediateAbilityContentPackage(source.PackageId, source.ProfileId, source.RequiredMetadataPackageId, [null!]));
        Assert.Throws<ContentPackageValidationException>(() => new ImmediateAbilityContentPackage(source.PackageId, source.ProfileId, source.RequiredMetadataPackageId,
            Enumerable.Range(0, ContentPackage.MaxDefinitionsPerKind + 1).Select(index =>
                new ImmediateAbilityDefinition(new($"sample:ability/limit-{index}"), ImmediateAbilityEffect.SelfPhysicalAttackIncrease, 1, 0, 0))));
    }

    private static BaseJobBattleSetup Setup(ContentPackage metadata)
    {
        var surface = new TerrainSurfaceId("sample:terrain/ground");
        var map = new BattleMapDefinition(2, 2, 1, Enumerable.Repeat(new BattleTile(surface, 10), 4).ToArray());
        return BaseJobBattleSetup.Create(new(metadata.Jobs, metadata.Skillsets, metadata.Abilities), map,
            [new(A, metadata.Jobs[0].Id, new(0, 0, 0), 6, [new(surface, new(1))]),
             new(B, metadata.Jobs[0].Id, new(1, 1, 0), 6, [new(surface, new(1))])], FftBiosRandom.Seed(1));
    }

    private static TwoUnitBattleState Accepted(TwoUnitBattleState state, TwoUnitBattleCommand command)
    { var result = TwoUnitBattleScenario.Apply(state, command); Assert.True(result.Accepted, Receipt(result)); return result.State; }

    private static string Receipt(TwoUnitBattleResult result) => JsonSerializer.Serialize(new
    {
        result.Accepted, result.ScenarioRejection, result.AttackRejection, result.TurnRejection, result.MovementRejection,
        units = result.State.Battle.Combatants, turn = result.State.Battle.TurnBattle.Turns,
        positions = result.State.Battle.TurnBattle.Battle.Units.Select(unit => new { unit.Id, unit.Position, unit.HasMoved }),
        events = result.Events.Select(ev => ev.ToString()),
    });
}
