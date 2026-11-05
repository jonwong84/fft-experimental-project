using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Remaining source assertions; masks, baseline cells and prepared scan policies are privately derived.</summary>
public sealed record BasicSkillAiDerivedProfile(AccumulateAiPreparationConfiguration Preparation,
    AttackThrowStoneAiScenarioProfile Scenarios, bool SourceGeneralEntry = false, bool FreshEmptyRankedList = false,
    bool ClearDirectTrajectories = false);
public enum BasicSkillAiDerivedStage { Admission, Scenarios, Provenance, Composition, Lottery }
public sealed record BasicSkillAiDerivedFailure(BasicSkillAiDerivedStage Stage, BasicSkillAiScenarioFailure? Scenarios = null,
    BasicSkillAiFailure? Composition = null, int? LotteryIndex = null);
public sealed record BasicSkillAiDerivedCompositionOutcome(BasicSkillAiScenarioOutcome Scenarios,
    ImmutableArray<TilePosition> ScanOrigins, BasicSkillAiCompositionOutcome Composition);
public sealed record BasicSkillAiDerivedCompositionResult(bool Accepted, BasicSkillAiDerivedFailure? Failure,
    BasicSkillAiDerivedCompositionOutcome? Outcome, RandomState RngState);
public sealed record BasicSkillAiWorkspaceCompositionResult(BasicSkillAiDerivedCompositionResult Query,
    AiPriorityWorkspace Workspace, ImmutableArray<AiCastOriginPriority> CastOrigins);

/// <summary>Own fresh preparation/scenarios and provenance-safe three-entry composition through the existing entry/rank owner.</summary>
/// <remarks>ZOD-249/251. No public scenario, score, cache or ranked input; never rescore the inserted actor or guess an unwritten key.</remarks>
internal static class BasicSkillAiDerivedComposition
{
    public static BasicSkillAiDerivedCompositionResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiDerivedProfile profile, RandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        return EvaluateWithWorkspace(state, request, profile, AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map), decisionRng).Query;
    }

    public static BasicSkillAiWorkspaceCompositionResult EvaluateWithWorkspace(AttackBattleSnapshot state,
        AccumulateAiPreparationRequest request, BasicSkillAiDerivedProfile profile, AiPriorityWorkspace workspace, RandomState decisionRng)
    {
        ValidateArguments(state, request, profile);
        ArgumentNullException.ThrowIfNull(workspace); workspace.ValidateMap(state);
        BasicSkillAiWorkspaceCompositionResult Reject(BasicSkillAiDerivedFailure failure) => new(new(false, failure, null, decisionRng), workspace, []);
        if (!profile.SourceGeneralEntry || !profile.FreshEmptyRankedList || !profile.ClearDirectTrajectories)
            return Reject(new(BasicSkillAiDerivedStage.Admission));
        var scenarios = BasicSkillAiScenarios.EvaluateFresh(state, request, profile.Preparation, profile.Scenarios, decisionRng);
        if (!scenarios.Result.Accepted) return Reject(new(BasicSkillAiDerivedStage.Scenarios, Scenarios: scenarios.Result.Failure));
        var ready = scenarios.Result.Outcome!; var m = ready.Movement;
        int Distance(TilePosition p) => Math.Abs(p.X - ready.Preparation.Common.FocusPosition.X) + Math.Abs(p.Y - ready.Preparation.Common.FocusPosition.Y);
        int range = ready.Preparation.ActorEntries.Single(e => e.Kind == AccumulateAiEntryKind.ThrowStone).Range;
        // Cast-range filtering precedes ranked key reads. Self map-target evaluation reads no such key and is not pruned.
        var keys = ImmutableArray.CreateBuilder<AiCastOriginPriority>();
        foreach (var cell in m.Cells.Where(c => m.Scenario1.Contains(c.Tile) && Distance(c.Tile) <= range))
        {
            short priority;
            if (cell.WrittenPriority is { } fresh) priority = fresh;
            else if (!workspace.KnownPriorities.TryGetValue(cell.Tile, out priority))
                return Reject(new(BasicSkillAiDerivedStage.Provenance));
            keys.Add(new(cell.Tile, priority, cell.Proximity, cell.PriorityWritten ? AiCastPriorityProvenance.Fresh : AiCastPriorityProvenance.Retained));
        }
        var castOrigins = keys.ToImmutable();
        var written = m.ScoreMask.ToHashSet();
        var known = castOrigins.Select(c => c.Tile).ToHashSet();
        var scanOrigins = m.Scenario1.Where(p => written.Contains(p) || known.Contains(p)).ToImmutableArray();
        var scenario = new ThrowStoneAiScanScenario(m.ScoreMask, scanOrigins, m.BaselineDestination, m.ActFirstDestination);
        var entries = CreateEntryProfile(profile, ready.Preparation);
        if (!BasicSkillAiComposition.SupportsProfile(entries)) return Reject(new(BasicSkillAiDerivedStage.Admission));
        var admission = BasicSkillAiComposition.ValidateScans(state, request, scenario, entries, decisionRng, castOrigins);
        if (admission is not null) return Reject(new(BasicSkillAiDerivedStage.Composition, Composition: admission));
        var composition = BasicSkillAiComposition.ContinueFromMovement(state, request, scenario, entries, ready.Preparation,
            scenarios.Prefix!, scenarios.Result.RngState, decisionRng, castOrigins);
        if (!composition.Accepted) return Reject(new(BasicSkillAiDerivedStage.Composition, Composition: composition.Failure));
        return new(new(true, null, new(ready, scanOrigins, composition.Outcome!), composition.RngState), workspace.Write(m.Movement), castOrigins);
    }

    private static BasicSkillAiProfile CreateEntryProfile(BasicSkillAiDerivedProfile profile, AccumulateAiPreparationOutcome preparation)
    {
        var attack = preparation.ActorEntries[0]; var self = preparation.ActorEntries[1]; var stone = preparation.ActorEntries[2];
        bool order = attack.Kind == AccumulateAiEntryKind.Attack && self.Kind == AccumulateAiEntryKind.Accumulate && stone.Kind == AccumulateAiEntryKind.ThrowStone;
        bool attackPolicy = attack.Usable && attack.Range == 1 && attack.ExhaustiveOriginScan;
        bool stonePolicy = stone.Usable && stone.Range == 4 && !stone.ExhaustiveOriginScan && stone.ConsumeEvaluatedAimTiles;
        bool active = preparation.Common.TargetActive;
        return new(profile.Preparation, new(active, attackPolicy, true, true, true),
            new(active, true, true, stonePolicy, true, true, true, profile.ClearDirectTrajectories),
            new(true, true, profile.Scenarios.NormalScoring, profile.Scenarios.CanonicalCoordinates),
            profile.SourceGeneralEntry, order, profile.FreshEmptyRankedList);
    }

    internal static void ValidateArguments(AttackBattleSnapshot state, AccumulateAiPreparationRequest request, BasicSkillAiDerivedProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(profile.Scenarios);
        AccumulateAiPreparation.ValidateArguments(state, request, profile.Preparation);
    }
}
