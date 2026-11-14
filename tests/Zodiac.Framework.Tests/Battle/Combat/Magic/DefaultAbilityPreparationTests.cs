using Xunit;
using Zodiac.Framework.CombatRules;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class DefaultAbilityPreparationTests
{
    [Theory]
    [InlineData(131, false, false, 3)]
    [InlineData(9, true, false, 5)]
    [InlineData(9, true, true, 0)]
    public void Primary_preparation_proposes_timing_and_action_statuses_without_spending_resources(
        byte raw, bool shortCharge, bool nonCharge, byte expected)
    {
        var request = MagicChargePlanTests.Request();
        var definition = request.Selection.Definition with { RawChargeTime = raw };
        request = request with
        {
            Selection = SpellCastPhaseTestData.Confirm(definition),
            Caster = request.Caster with
            {
                Mp = 0,
                Supports = new(ShortCharge: shortCharge, NonCharge: nonCharge),
                Statuses = new(Shell: true, Charging: true, Performing: true, Defending: true)
            }
        };
        var result = DefaultAbilityPreparation.Prepare(request);
        Assert.True(result.Accepted);
        var prepared = result.Outcome!;
        Assert.Same(definition, prepared.Plan.Definition);
        Assert.Equal(request.Selection.Aim, prepared.Plan.Aim);
        Assert.Equal(expected, prepared.Plan.Counter);
        Assert.Equal(expected == 0, prepared.Immediate);
        Assert.Equal(new MagicCurrentStatuses(Shell: true, Charging: expected != 0), prepared.Caster.Statuses);
        Assert.Equal(request.Caster with { Statuses = prepared.Caster.Statuses }, prepared.Caster);
        Assert.True(request.Caster.Statuses.Defending);
        Assert.True(request.Caster.Statuses.Performing);
        Assert.Equal((ushort)0, prepared.Caster.Mp);
        // The existing plan-only API remains a compatible projection of this preparation.
        var planOnly = MagicChargePlanQuery.Prepare(request);
        Assert.True(planOnly.Accepted);
        Assert.Equal(prepared.Plan.Counter, planOnly.Outcome!.Counter);
        Assert.Equal(prepared.Plan.Aim, planOnly.Outcome.Aim);
    }

    [Fact]
    public void Inactive_followed_anchor_and_unsupported_performance_return_no_partial_proposal()
    {
        var request = MagicChargePlanTests.Request();
        var anchor = request.Roster.Single(u => u.UnitId == request.Selection.Aim.TargetUnitId);
        var inactive = DefaultAbilityPreparation.Prepare(request with
        {
            Roster = request.Roster.Replace(anchor, anchor with { Active = false })
        });
        Assert.False(inactive.Accepted);
        Assert.Null(inactive.Outcome);
        var performance = DefaultAbilityPreparation.Prepare(request with
        {
            Selection = SpellCastPhaseTestData.Confirm(request.Selection.Definition with { Persevere = true })
        });
        Assert.False(performance.Accepted);
        Assert.Null(performance.Outcome);
    }
}
