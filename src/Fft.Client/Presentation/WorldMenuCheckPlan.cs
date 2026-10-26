using System.Collections.Immutable;
using Fft.Client.Navigation;

namespace Fft.Client.Presentation;

public sealed record WorldMenuCheckStep(string Name, WorldMenuSample Sample, bool Capture = false,
    bool Close = false, int Width = 0, int Height = 0, bool Recreate = false);

/// <summary>Finite synthetic device samples; physical device behavior remains a manual check.</summary>
public static class WorldMenuCheckPlan
{
    public static ImmutableArray<WorldMenuCheckStep> Steps { get; } = Build();
    private static ImmutableArray<WorldMenuCheckStep> Build()
    {
        var steps = ImmutableArray.CreateBuilder<WorldMenuCheckStep>();
        void Add(string name, WorldMenuSample sample, bool capture = false, bool close = false, int width = 0, int height = 0, bool recreate = false)
            => steps.Add(new(name, sample, capture, close, width, height, recreate));
        void Release() => Add("release", new(Connected: true));
        void Key(string name, WorldMenuButtons key, bool capture = false)
        { Add(name, new(Keyboard: key, Connected: true), capture); Release(); }
        void Pad(string name, WorldMenuButtons pad, bool capture = false)
        { Add(name, new(Controller: pad, Connected: true), capture); Release(); }
        void Click(string name, int index, bool capture = false)
        {
            var bounds = WorldMenuPresentation.Button(index);
            Add(name, new(Connected: true, Left: true, Pointer: new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)), capture);
            Release();
        }
        Add("home", new(), true);
        Add("new", new(Keyboard: WorldMenuButtons.Confirm), true);
        Add("held-confirm", new(Keyboard: WorldMenuButtons.Confirm));
        Release(); Key("hill", WorldMenuButtons.Confirm, true);
        Key("select-party", WorldMenuButtons.Down); Key("party", WorldMenuButtons.Confirm, true);
        Add("disconnect", new());
        Add("connect-held", new(Controller: WorldMenuButtons.Confirm, Connected: true));
        Add("connected-held", new(Controller: WorldMenuButtons.Confirm, Connected: true)); Release();
        Add("controller-back", new(Controller: WorldMenuButtons.Back, Connected: true));
        Add("controller-held", new(Controller: WorldMenuButtons.Back, Connected: true)); Release();
        var held = new WorldMenuSample(WorldMenuButtons.Confirm, WorldMenuButtons.Confirm, true, false, true, new(182, 356));
        Add("inactive", held); Add("reactivated", held with { Active = true });
        Add("reactivated-held", held with { Active = true }); Release();
        Click("home-retained", 2, true); Click("confirm-new", 0, true);
        Key("cancel-new", WorldMenuButtons.Confirm);
        Key("new-again", WorldMenuButtons.Confirm); Key("accept-new-focus", WorldMenuButtons.Down);
        Key("new-harbor", WorldMenuButtons.Confirm, true);
        Add("wide", new(Connected: true), true, width: 1280, height: 720, recreate: true);
        Click("wide-travel", 0, true); Click("wide-party", 1, true);
        Add("close-callback", new(Connected: true), true, close: true);
        Key("cancel-close", WorldMenuButtons.Confirm);
        Add("tall", new(Connected: true), true, width: 720, height: 960);
        Click("pointer-back", 0); Key("home-back", WorldMenuButtons.Back);
        Key("exit-prompt", WorldMenuButtons.Back); Pad("exit-focus", WorldMenuButtons.Down);
        Add("closed", new(Controller: WorldMenuButtons.Confirm, Connected: true), true);
        return steps.ToImmutable();
    }
}
