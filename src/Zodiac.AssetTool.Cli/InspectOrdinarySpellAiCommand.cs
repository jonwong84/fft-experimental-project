using System.Text.Json;
using Zodiac.Content;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Zodiac.Framework.Ai;

namespace Zodiac.AssetTool.Cli;

/// <summary>Read-only prepared-data diagnostics; no original media/reference checkout or command adoption.</summary>
public sealed class InspectOrdinarySpellAiCommand
{
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length != 3 || args[0] is not ("inspect-magic-ai" or "inspect-delayed-magic-ai") || args[1] != "--content-root" || string.IsNullOrWhiteSpace(args[2]))
                throw new ArgumentException("Usage: (inspect-magic-ai|inspect-delayed-magic-ai) --content-root <prepared-directory>");
            bool delayed = args[0] == "inspect-delayed-magic-ai";
            var prepared = PreparedOrdinaryMagicBattle.Load(LooseAssetStore.Load(args[2]));
            var catalog = PreparedOrdinarySpellAi.CreateCatalog(prepared);
            var advance = OrdinaryMagicBattle.Apply(prepared.Definition.Start(), new OrdinaryMagicCommand.Advance());
            if (!advance.Accepted || advance.State.Turns.ActiveUnitId is not { } actorId)
                throw new NotSupportedException("The prepared scene did not reach an ordinary initial unit turn.");
            var state = advance.State; var actor = state.Units.Single(u => u.Id == actorId);
            var target = state.Units.FirstOrDefault(u => u.Occupant.InitialTeam != actor.Occupant.InitialTeam)
                ?? throw new NotSupportedException("This diagnostic selects one explicit hostile recipient.");
            var cases = new List<object>();
            foreach (var slot in catalog.PrimaryByJob[actor.Definition.RewardJobId].Slots.OrderBy(s => s.Index))
            {
                if (!actor.LearnedAbilitySlots[actor.Definition.RewardJobId].Contains(slot.Index) || !slot.UsableByAi) continue;
                if (delayed)
                {
                    if (slot.Behavior.FollowTarget) cases.Add(DelayedCase(state, catalog, actorId, slot, target.Position, true));
                    cases.Add(DelayedCase(state, catalog, actorId, slot, target.Position, false));
                    continue;
                }
                var r = OrdinarySpellAiReadyQuery.Evaluate(state, catalog, new(actorId, slot.Index, target.Position, []), state.Turns.RngState);
                if (!r.Accepted) throw new NotSupportedException(r.Rejection);
                cases.Add(new
                {
                    sourceSlot = slot.Index, spellId = slot.Spell.Id.Value, slot.Spell.OriginalAbilityId,
                    disposition = r.Disposition.ToString(), r.Outcome!.Score,
                    units = r.Outcome.Units.Select(u => new { id = u.Id.Value, u.Vitals.Hp, u.Current.Mp }),
                    frames = r.Outcome.Frames.Select(f => new { targetId = f.TargetId.Value, f.Result }),
                    entryRng = state.Turns.RngState, exitRng = r.RngState
                });
            }
            if (cases.Count == 0) throw new NotSupportedException("No known AI-usable primary spell is selected.");
            output.WriteLine(JsonSerializer.Serialize(new
            {
                scope = delayed
                    ? "Independent fixed-origin single-charge candidates; no move/aim enumeration, ranked selection or execution"
                    : "Independent initial-aim/ready-hit hypotheses on current facts; no charge-clock forecast or AI decision authority",
                eachCaseRestartsFromSameStateAndRng = true, prepared.Ruleset, actorId = actorId.Value, cases,
                referenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df",
                sourceOriginalExecuted = false, cExecuted = false
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or KeyNotFoundException)
        {
            error.WriteLine("Ordinary spell AI inspection failed: " + e.Message); return 1;
        }
    }

    private static object DelayedCase(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog, BattleUnitId actorId,
        OrdinarySpellAiSlot slot, TilePosition aim, bool follow)
    {
        var r = OrdinarySpellAiCandidateQuery.Evaluate(state, catalog, new(actorId, slot.Index, aim, follow), state.Turns.RngState);
        if (!r.Accepted) throw new NotSupportedException(r.Rejection);
        var o = r.Outcome!;
        return new
        {
            sourceSlot = slot.Index, spellId = slot.Spell.Id.Value, slot.Spell.OriginalAbilityId,
            aimMode = follow ? "Unit" : "Tile", disposition = r.Disposition.ToString(),
            consideredDelay = o.Ability.ChargeTime, actualChargeTime = o.ActualChargeTime, o.Score,
            units = o.Units.Select(u => new { id = u.Id.Value, u.Vitals.Hp, u.Current.Mp }),
            frames = o.Frames.Select(f => new { targetId = f.TargetId.Value, f.Result }),
            clock = o.Clock is not { } clock ? null : new
            {
                clock.Ticks, clock.ElapsedTicks, clock.RemainingCharge, clock.EventLimitReached,
                units = clock.Units.Select(u => new { id = u.UnitId.Value, u.Ct, u.MovementTaken, u.ActionTaken }),
                opportunities = clock.Units.Where(u => clock.HadMoveOpportunity.Contains(u.UnitId)).Select(u => u.UnitId.Value),
                events = clock.Events.Select(e => new { kind = e.Kind.ToString(), id = e.UnitId.Value, e.Tick }),
                upkeep = clock.UpkeepPicks.Select(p => new { id = p.UnitId.Value, p.CtBefore, p.CtAfter, p.Dead, p.RawRoll })
            },
            formulaEntryRng = o.FormulaEntryRng, entryRng = state.Turns.RngState, exitRng = r.RngState
        };
    }
}
