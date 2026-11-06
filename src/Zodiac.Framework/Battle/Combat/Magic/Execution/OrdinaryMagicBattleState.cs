using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed class OrdinaryMagicBattleState
{
    public OrdinaryMagicBattleDefinition Definition { get; }
    public ImmutableArray<OrdinaryMagicUnitState> Units { get; }
    public TurnCycleState Turns { get; }
    public ImmutableDictionary<BattleUnitId, PendingMagicCast> Pending { get; }
    public OrdinaryMagicClockPhase ClockPhase { get; }
    public ulong Revision { get; }
    public bool Finished { get; }
    public bool PlayerUnitFallen { get; }
    public OrdinaryMagicLearningReport? LearningReport { get; }

    internal OrdinaryMagicBattleState(OrdinaryMagicBattleDefinition definition,
        ImmutableArray<OrdinaryMagicUnitState> units, TurnCycleState turns,
        ImmutableDictionary<BattleUnitId, PendingMagicCast> pending,
        OrdinaryMagicClockPhase clockPhase, ulong revision, bool finished, bool playerUnitFallen,
        OrdinaryMagicLearningReport? learningReport = null)
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

    internal OrdinaryMagicBattleState With(ImmutableArray<OrdinaryMagicUnitState>? units = null,
        TurnCycleState? turns = null, ImmutableDictionary<BattleUnitId, PendingMagicCast>? pending = null,
        OrdinaryMagicClockPhase? phase = null, ulong? revision = null, bool? finished = null, bool? fallen = null,
        OrdinaryMagicLearningReport? learningReport = null, bool clearLearningReport = false) =>
        new(Definition, units ?? Units, turns ?? Turns, pending ?? Pending, phase ?? ClockPhase,
            revision ?? Revision, finished ?? Finished, fallen ?? PlayerUnitFallen,
            clearLearningReport ? null : learningReport ?? LearningReport);
}
