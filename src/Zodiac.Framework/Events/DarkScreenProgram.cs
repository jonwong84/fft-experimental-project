using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed class DarkScreenProgram
{
    public AssetId Id => Body.Id;
    public ImmutableArray<DarkScreenInstruction> Instructions { get; }
    internal SceneEventProgram Body { get; }
    internal StartDarkScreen? Start { get; }

    public DarkScreenProgram(AssetId id, IEnumerable<DarkScreenInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        Instructions = instructions.Take(ConditionalEventProgram.MaxInstructions + 1).ToImmutableArray();
        if (Instructions.Any(i => i is null)) throw new ArgumentException("Non-null instructions are required.", nameof(instructions));
        var starts = Instructions.Where(i => i.Start is not null).ToArray();
        if (starts.Length > 1) throw new ArgumentException("One syntactic admission is supported.", nameof(instructions));
        Start = starts.SingleOrDefault()?.Start;
        if (Start is { } start && (start.Definition is null || !SceneTaskState.ValidId(start.OwnerId)))
            throw new ArgumentException("A validated definition and owner ID are required.", nameof(instructions));
        Body = new(id, Instructions.Select(i => i.SceneInstruction));
    }

    public DarkScreenSceneState StartScene(ConditionalEventVariables variables, SceneTaskState tasks,
        AssetId consumerId, ReadyDarkScreenBinding? binding)
        => new(this, Body.Start(variables, tasks, consumerId), null, binding);
}
