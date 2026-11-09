using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiContextTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard");
    private static readonly DefaultAbilityAiAbilitySearchRequest Request = new(Mage, 0, new(true));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Null_constructor_inputs_are_programmer_errors(bool nullState)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup();
        if (nullState)
        {
            Assert.Equal("state", Assert.Throws<ArgumentNullException>(() => new DefaultAbilityAiContext(null!, catalog)).ParamName);
        }
        else
        {
            Assert.Equal("catalog", Assert.Throws<ArgumentNullException>(() => new DefaultAbilityAiContext(state, null!)).ParamName);
        }
    }

    [Fact]
    public void Equivalent_content_ids_do_not_allow_a_catalog_from_another_definition_instance()
    {
        var setup = DefaultAbilityAiAimTests.Setup();
        var foreign = DefaultAbilityAiAimTests.Setup();
        Assert.Equal(setup.State.Definition.Spells.Keys.Single(), foreign.State.Definition.Spells.Keys.Single());
        Assert.NotSame(setup.State.Definition, foreign.Catalog.Definition);
        var exception = Assert.Throws<ArgumentException>(() => new DefaultAbilityAiContext(setup.State, foreign.Catalog));
        Assert.Equal("catalog", exception.ParamName);
        var context = new DefaultAbilityAiContext(setup.State, setup.Catalog);
        Assert.Same(setup.State, context.State);
        Assert.Same(setup.Catalog, context.Catalog);
    }

    [Theory]
    [InlineData(0, "context")]
    [InlineData(1, "request")]
    [InlineData(2, "Profile")]
    public void Null_query_context_request_or_nested_profile_throws_before_evaluation(int change, string parameter)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup();
        DefaultAbilityAiContext context = new(state, catalog);
        var request = Request;
        if (change == 0)
        {
            context = null!;
        }
        else if (change == 1)
        {
            request = null!;
        }
        else
        {
            request = request with { Profile = null! };
        }
        var entry = new RandomState(57, 11);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var exception = Assert.Throws<ArgumentNullException>(() => DefaultAbilityAiAbilitySearchQuery.Evaluate(context, request, entry));
        Assert.Equal(parameter, exception.ParamName);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
        Assert.Equal(new RandomState(57, 11), entry);
    }

    [Fact]
    public void Rejected_requests_do_not_poison_a_reusable_raw_context_or_rng()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2, targetX: 2);
        var context = new DefaultAbilityAiContext(state, catalog);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var entry = new RandomState(57, 11);
        var requests = new[] { Request with { Profile = new() }, Request with { Scenario = (DefaultAbilityAiOriginScenario)999 },
            Request with { ActorId = Guard }, Request with { PrimarySlot = 1 } };
        foreach (var request in requests)
        {
            var rejected = DefaultAbilityAiAbilitySearchQuery.Evaluate(context, request, entry);
            Assert.False(rejected.Accepted);
            Assert.Null(rejected.Outcome);
            Assert.Equal(entry, rejected.RngState);
            Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(context.State));
        }
        var accepted = DefaultAbilityAiAbilitySearchQuery.Evaluate(context, Request, entry);
        Assert.True(accepted.Accepted, accepted.Rejection);
        Assert.Equal(new RandomState(399997557, 143), accepted.RngState); // Original source vector132 plus entry count11.
        Assert.Equal((short)49, accepted.Outcome!.Best!.Candidate.Evaluation.Candidate.Outcome!.Score!.Priority);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(context.State));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Matching_definition_context_does_not_pre_admit_current_mp_action_phase_or_learning(int change)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup();
        int actor = state.Units.IndexOf(state.Units.Single(u => u.Id == Mage));
        if (change == 0)
        {
            state = state.With(units: state.Units.SetItem(actor, state.Units[actor] with {
                Current = state.Units[actor].Current with { Mp = 5 } }));
        }
        else if (change == 1)
        {
            var turns = state.Turns;
            state = state.With(turns: new(turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed, u.Ct,
                u.TieKey, u.HasTurn, u.MovementTaken, u.Id == Mage || u.ActionTaken)), turns.RngState, turns.Ticks, turns.Phase));
        }
        else if (change == 2)
        {
            state = state.Definition.Start();
        }
        else
        {
            var unit = state.Units[actor];
            state = state.With(units: state.Units.SetItem(actor, unit with {
                LearnedAbilitySlots = unit.LearnedAbilitySlots.ToImmutableDictionary(p => p.Key, p => p.Value.Clear()) }));
        }
        var context = new DefaultAbilityAiContext(state, catalog); // Construction validates only the pair.
        var entry = new RandomState(57, 11);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = DefaultAbilityAiAbilitySearchQuery.Evaluate(context, Request, entry);
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.Equal(entry, result.RngState);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Actual_turn_command_creates_a_new_context_while_the_old_snapshot_remains_replayable()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2, targetX: 2);
        var captured = new DefaultAbilityAiContext(state, catalog);
        var entry = new RandomState(57, 0);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var first = DefaultAbilityAiAbilitySearchQuery.Evaluate(captured, Request, entry);
        Assert.True(first.Accepted, first.Rejection);
        var waited = DefaultAbilityBattle.Apply(state, new DefaultAbilityCommand.Wait(Mage));
        Assert.True(waited.Accepted, waited.Rejection);
        var current = new DefaultAbilityAiContext(waited.State, catalog);
        Assert.NotSame(captured.State, current.State);
        Assert.Same(catalog, current.Catalog);
        Assert.Equal(state.Revision + 1, current.State.Revision);
        var noTurn = DefaultAbilityAiAbilitySearchQuery.Evaluate(current, Request, entry);
        Assert.False(noTurn.Accepted);
        Assert.Null(noTurn.Outcome);
        Assert.Equal(entry, noTurn.RngState);
        var replay = DefaultAbilityAiAbilitySearchQuery.Evaluate(captured, Request, entry);
        Assert.True(replay.Accepted, replay.Rejection);
        Assert.Equal(new RandomState(399997557, 132), replay.RngState);
        Assert.Equal(first.RngState, replay.RngState);
        Assert.Equal(JsonSerializer.Serialize(first.Outcome!.Passes), JsonSerializer.Serialize(replay.Outcome!.Passes));
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(captured.State));
    }

    [Fact]
    public void Late_failed_search_retains_no_partial_preparation_in_the_context()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2, targetX: 2);
        var context = new DefaultAbilityAiContext(state, catalog);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var exhausted = new RandomState(57, ulong.MaxValue - 18); // Prefix13 and unit5 fit; tile formula cannot finish.
        var rejected = DefaultAbilityAiAbilitySearchQuery.Evaluate(context, Request, exhausted);
        Assert.False(rejected.Accepted);
        Assert.Null(rejected.Outcome);
        Assert.Equal(exhausted, rejected.RngState);
        var accepted = DefaultAbilityAiAbilitySearchQuery.Evaluate(context, Request, new(57, 0));
        Assert.True(accepted.Accepted, accepted.Rejection);
        Assert.Equal(new RandomState(399997557, 132), accepted.RngState);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(context.State));
    }
}
