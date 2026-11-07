using System.Text.Json;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

/// <summary>Complete gameplay receipt for local check/replay comparison; not a save-file format.</summary>
public static class OrdinaryMagicSnapshot
{
    public static string Capture(OrdinaryMagicBattleState state) => JsonSerializer.Serialize(new
    {
        state.Revision, state.Finished, state.PlayerUnitFallen, state.ClockPhase, state.LearningReport,
        state.Turns.Ticks, state.Turns.RngState, state.Turns.Phase, state.Turns.ActiveUnitId,
        turns = state.Turns.Units,
        units = state.Units.Select(u => new
        {
            u.Id, u.Definition.PlayerControlled, u.Position, u.Facing, u.Current, u.Vitals, u.RawStats, u.Level, u.Experience,
            u.KoCount, u.DeathCounter, u.OutcomeMarker,
            career = u.JobPoints.OrderBy(p => p.Key.Value, StringComparer.Ordinal).Select(p => new { id = p.Key.Value, p.Value }),
            unlocked = u.UnlockedJobs.OrderBy(id => id.Value, StringComparer.Ordinal).Select(id => id.Value),
            learned = u.LearnedAbilitySlots.OrderBy(p => p.Key.Value, StringComparer.Ordinal)
                .Select(p => new { job = p.Key.Value, slots = p.Value.Order() }),
            knownSpells = OrdinaryMagicBattle.KnownSpells(state, u.Id).OrderBy(id => id.Value, StringComparer.Ordinal).Select(id => id.Value)
        }),
        pending = state.Units.Where(u => state.Pending.ContainsKey(u.Id)).Select(u =>
        {
            var cast = state.Pending[u.Id]; return new { u.Id, spell = cast.Definition.Id, cast.Counter, cast.Aim };
        })
    });

    public static OrdinaryMagicBattleState Replay(OrdinaryMagicBattleDefinition definition, IEnumerable<OrdinaryMagicCommand> commands)
    {
        var state = definition.Start();
        foreach (var command in commands) state = OrdinaryMagicBattle.Apply(state, command).State;
        return state;
    }
}
