using System.Collections.Immutable;
using Fft.Client.Navigation;

namespace Fft.Client.Presentation;

public enum WorldSlotCheckMutation { None, ExternalOverwrite01, Corrupt01, Delete04, Hold03, Release03 }

/// <summary>Authored UI/storage failure plan, restricted to fresh check directories.</summary>
public static class WorldSlotsCheckPlan
{
    public static ImmutableArray<WorldMenuCheckStep> Steps { get; } = Build();
    private static ImmutableArray<WorldMenuCheckStep> Build()
    {
        var steps = ImmutableArray.CreateBuilder<WorldMenuCheckStep>();
        void Release() => steps.Add(new("release", new(Connected: true)));
        void Key(string name, WorldMenuButtons key, bool capture = false)
        { steps.Add(new(name, new(Keyboard: key, Connected: true), capture)); Release(); }
        void Click(string name, int index, bool capture = false, bool list = false)
        {
            var bounds = list ? new PixelRect(32, 184 + index * 36, 300, 32) : WorldMenuPresentation.Button(index);
            steps.Add(new(name, new(Connected: true, Left: true, Pointer: new(bounds.X + 150, bounds.Y + bounds.Height / 2)), capture)); Release();
        }
        void Mutation(string name, WorldSlotCheckMutation mutation) => steps.Add(new(name, new(Connected: true), Mutation: mutation));
        void Confirm(string name, bool capture = true) { Key(name + "-focus", WorldMenuButtons.Down); Key(name, WorldMenuButtons.Confirm, capture); }
        steps.Add(new("home", new(), true)); Key("new", WorldMenuButtons.Confirm);
        Click("empty-slots", 3, true); Click("page-two", 6, true, true); Click("page-one", 5, list: true);
        Click("empty-details", 0, true, true); Click("saved-harbor", 0, true); Click("continue", 0);
        Click("hill", 0); Click("save-slots", 3); Click("save-details", 0, list: true);
        Click("overwrite-prompt", 0, true); Key("cancel-overwrite", WorldMenuButtons.Confirm);
        Click("overwrite-again", 0); Confirm("saved-hill"); Click("continue", 0);
        Click("travel-after-save", 0); Click("load-slots", 4); Click("load-details", 0, list: true);
        Click("load-prompt", 1, true); Key("cancel-load", WorldMenuButtons.Confirm);
        Click("load-again", 1); Confirm("loaded-hill"); Click("continue", 0);
        Click("stale-save-slots", 3); Click("stale-details", 0, list: true); Click("stale-prompt", 0);
        Mutation("external-overwrite", WorldSlotCheckMutation.ExternalOverwrite01); Confirm("stale-error");
        Click("stale-refresh", 1, true); Click("back-list", 4); Click("back-world", 8, list: true);
        Mutation("corrupt-primary", WorldSlotCheckMutation.Corrupt01);
        Click("recovery-slots", 4); Click("corrupt-details", 0, true, true);
        Click("recovery-prompt", 2, true); Key("cancel-recovery", WorldMenuButtons.Confirm);
        Click("recovery-again", 2); Confirm("recovered"); Click("continue", 0);
        Click("recover-save-slots", 3); Click("slot-two", 1, list: true); Click("saved-recovered", 0, true);
        Click("back-details", 1); Click("back-list", 4); Click("slot-four", 3, list: true); Click("saved-four", 0); Click("continue", 0);
        Click("missing-load-slots", 4); Click("missing-details", 3, list: true); Click("missing-prompt", 1);
        Mutation("delete-four", WorldSlotCheckMutation.Delete04); Confirm("missing-error");
        Click("missing-refresh", 1); Click("back-list", 4); Click("slot-three", 2, list: true);
        Mutation("lock-three", WorldSlotCheckMutation.Hold03); Click("busy-error", 0, true);
        Mutation("unlock-three", WorldSlotCheckMutation.Release03); Click("busy-refresh", 1, true);
        steps.Add(new("wide-details", new(Connected: true), true, Width: 1280, Height: 720, Recreate: true));
        steps.Add(new("close-prompt", new(Connected: true), true, Close: true)); Key("cancel-close", WorldMenuButtons.Confirm);
        Click("back-list", 4); steps.Add(new("tall-slots", new(Connected: true), true, Width: 720, Height: 960));
        Click("back-world", 8, list: true); Key("home-back", WorldMenuButtons.Back); Key("exit-prompt", WorldMenuButtons.Back);
        Key("exit-focus", WorldMenuButtons.Down);
        steps.Add(new("closed", new(Controller: WorldMenuButtons.Confirm, Connected: true), true));
        return steps.ToImmutable();
    }
}
