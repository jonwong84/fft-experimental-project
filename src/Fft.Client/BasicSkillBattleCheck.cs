using System.Text.Json;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Samples;
using Zodiac.Framework;

namespace Fft.Client;

/// <summary>Finite headless content/lifecycle proof; diagnostic output is not a save or persistent replay format.</summary>
public static class BasicSkillBattleCheck
{
    public static int Run(string outputDirectory)
    {
        try
        {
            string output = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Choose a fresh check directory.");
            var metadata = SyntheticBasicSkillBattle.Metadata();
            string primary = metadata.ToJson(), self = SyntheticBasicSkillBattle.SelfEffects(metadata).ToJson(),
                targeted = SyntheticBasicSkillBattle.TargetedEffects(metadata).ToJson();
            var definition = Compose();
            var state = definition.Start();
            var commands = SyntheticBasicSkillBattle.Journal();
            var receipts = new List<object>();
            foreach (var command in commands)
            {
                var result = TwoUnitBattleScenario.Apply(state, command); state = result.State;
                receipts.Add(new { command = JsonSerializer.SerializeToElement(command, command.GetType()),
                    kind = command.GetType().Name, result.Accepted, result.ScenarioRejection, result.AttackRejection,
                    state = Snapshot(state), events = result.Events.Select(ev => ev.ToString()).ToArray() });
            }
            // Recompose immutable definitions for every prefix, replay that prefix, then compare every suffix.
            for (int checkpoint = 0; checkpoint <= commands.Length; checkpoint++)
            {
                var left = definition.Start(); var right = Compose().Start();
                foreach (var command in commands.Take(checkpoint))
                { left = TwoUnitBattleScenario.Apply(left, command).State; right = TwoUnitBattleScenario.Apply(right, command).State; }
                if (Snapshot(left) != Snapshot(right)) throw new InvalidOperationException("Fresh-definition prefix differs.");
                foreach (var command in commands.Skip(checkpoint))
                {
                    var a = TwoUnitBattleScenario.Apply(left, command); var b = TwoUnitBattleScenario.Apply(right, command);
                    if (a.Accepted != b.Accepted || a.ScenarioRejection != b.ScenarioRejection || a.AttackRejection != b.AttackRejection ||
                        a.TurnRejection != b.TurnRejection || a.MovementRejection != b.MovementRejection ||
                        Snapshot(a.State) != Snapshot(b.State) || !a.Events.SequenceEqual(b.Events))
                        throw new InvalidOperationException("Fresh-definition continuation differs.");
                    left = a.State; right = b.State;
                }
            }
            if (state.Outcome != TwoUnitBattleOutcome.FirstUnitWon || Snapshot(definition.Start()) != Snapshot(Compose().Start()))
                throw new InvalidOperationException("Basic Skill outcome/restart differs.");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "content.package.json"), primary);
            File.WriteAllText(Path.Combine(output, "ability.package.json"), self);
            File.WriteAllText(Path.Combine(output, "targeted.package.json"), targeted);
            File.WriteAllText(Path.Combine(output, "check.json"), JsonSerializer.Serialize(new
            { format = "fft.basic-skill-headless-check", schemaVersion = 1, checkpointCount = commands.Length + 1,
              freshDefinitionContinuation = true, restartMatched = true, receipts, final = Snapshot(state),
              evidence = "authored command/content proof; no save, native input or original execution" }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine($"Basic Skill headless check passed: {commands.Length} commands, {commands.Length + 1} fresh-definition continuations; {output}");
            return 0;

            TwoUnitBattleDefinition Compose()
            {
                var loaded = ContentPackage.ReadJson(primary);
                return SyntheticBasicSkillBattle.Create(loaded, ImmediateAbilityContentPackage.ReadJson(self), TargetedAbilityContentPackage.ReadJson(targeted));
            }
        }
        catch (Exception exception) { Console.Error.WriteLine("Basic Skill check failed: " + exception.Message); return 1; }
    }

    public static string Snapshot(TwoUnitBattleState state) => JsonSerializer.Serialize(new
    { state.Outcome, state.WinnerId, state.Battle.Combatants, state.Battle.TurnBattle.Turns,
      units = state.Battle.TurnBattle.Battle.Units.Select(unit => new { unit.Id, unit.Position, unit.HasMoved }),
      state.Battle.TurnBattle.Battle.RngState });
}
