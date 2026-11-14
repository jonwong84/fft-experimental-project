using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiAccountingTests
{
    private static readonly BattleUnitId Enemy = new("enemy"), Ally = new("ally"), Other = new("other");
    private static ImmutableArray<DefaultAbilityAiStrikeTarget> Targets => [new(Enemy,100,true,true),new(Ally,75,false,false),new(Other,0,true,true)];

    [Fact]
    public void Collateral_penalty_and_accuracy_average_include_only_positive_accuracy_pairs()
    {
        var r=DefaultAbilityAiAccounting.Evaluate(DefaultAbilityAiListTests.FireBehavior,Targets,[]);
        Assert.Equal(new[] {Enemy,Ally},r.IncludedTargets); Assert.Equal((short)-1,r.PriorityDelta);
        Assert.Equal((byte)87,r.HitPercent); Assert.True(r.Valuable);
    }

    [Fact]
    public void Moved_follow_anchor_excludes_every_collateral_even_healing_allies()
    {
        var b=DefaultAbilityAiListTests.FireBehavior with {TargetsAllies=true};
        var r=DefaultAbilityAiAccounting.Evaluate(b,Targets,[Enemy],Enemy);
        Assert.Equal(Enemy,Assert.Single(r.IncludedTargets)); Assert.Equal((byte)100,r.HitPercent);
    }

    [Fact]
    public void Tile_damage_excludes_moved_targets_but_ally_preferences_keep_them()
    {
        var damage=DefaultAbilityAiAccounting.Evaluate(DefaultAbilityAiListTests.FireBehavior,Targets,[Enemy]);
        Assert.Equal(Ally,Assert.Single(damage.IncludedTargets)); Assert.False(damage.Valuable);
        var healing=DefaultAbilityAiAccounting.Evaluate(new(TargetsAllies:true),Targets,[Enemy,Ally]);
        Assert.Equal(2,healing.IncludedTargets.Length);
    }

    [Fact]
    public void Untargetable_is_skipped_but_map_target_behavior_bypasses_motion_and_targetability()
    {
        var targets=Targets.SetItem(0,Targets[0] with {Untargetable=true});
        Assert.False(DefaultAbilityAiAccounting.Evaluate(DefaultAbilityAiListTests.FireBehavior,targets,[]).Valuable);
        var map=DefaultAbilityAiAccounting.Evaluate(DefaultAbilityAiListTests.FireBehavior with {TargetsMapTiles=true},targets,[Enemy,Ally]);
        Assert.Equal(2,map.IncludedTargets.Length); Assert.True(map.Valuable);
    }

    [Fact]
    public void Stat_credit_and_no_valuable_target_follow_separate_source_terms()
    {
        var r=DefaultAbilityAiAccounting.Evaluate(DefaultAbilityAiListTests.FireBehavior with {AffectsStats=true},Targets,[]);
        Assert.Equal((short)-1,r.PriorityDelta); // +3 intended, -4 collateral.
        var empty=DefaultAbilityAiAccounting.Evaluate(DefaultAbilityAiListTests.FireBehavior,[],[]);
        Assert.False(empty.Valuable); Assert.Equal((byte)0,empty.HitPercent);
        var collateral=DefaultAbilityAiAccounting.Evaluate(DefaultAbilityAiListTests.FireBehavior,[Targets[1]],[]);
        Assert.False(collateral.Valuable); Assert.Equal((short)-1,collateral.PriorityDelta);
    }
}
