using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed class DefaultAbilityBattleState
{
    public DefaultAbilityBattleDefinition Definition { get; }
    public ImmutableArray<DefaultAbilityUnitState> Units { get; }
    public TurnCycleState Turns { get; }
    public ImmutableDictionary<BattleUnitId, PendingMagicCast> Pending { get; }
    public DefaultAbilityClockPhase ClockPhase { get; }
    public ulong Revision { get; }
    public bool Finished { get; }
    public bool PlayerUnitFallen { get; }
    public DefaultAbilityLearningReport? LearningReport { get; }

    internal DefaultAbilityBattleState(DefaultAbilityBattleDefinition definition,
        ImmutableArray<DefaultAbilityUnitState> units, TurnCycleState turns,
        ImmutableDictionary<BattleUnitId, PendingMagicCast> pending,
        DefaultAbilityClockPhase clockPhase, ulong revision, bool finished, bool playerUnitFallen,
        DefaultAbilityLearningReport? learningReport = null)
    {
        Definition = definition;
        Units = units;
        Turns = turns;
        Pending = pending;
        ClockPhase = clockPhase;
        Revision = revision;
        Finished = finished;
        PlayerUnitFallen = playerUnitFallen;
        LearningReport = learningReport;
    }

    internal DefaultAbilityBattleState With(
        ImmutableArray<DefaultAbilityUnitState>? units = null,
        TurnCycleState? turns = null,
        ImmutableDictionary<BattleUnitId,PendingMagicCast>? pending = null,
        DefaultAbilityClockPhase? phase = null,
        ulong? revision = null,
        bool? finished = null,
        bool? fallen = null,
        DefaultAbilityLearningReport? learningReport = null,
        bool clearLearningReport = false) =>
        new(Definition, units ?? Units, turns ?? Turns, pending ?? Pending, phase ?? ClockPhase,
            revision ?? Revision, finished ?? Finished, fallen ?? PlayerUnitFallen,
            clearLearningReport ? null : learningReport ?? LearningReport);
}
