using System.Text.Json;
using Fft.Client.Navigation;
using Fft.Client.Presentation;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Xunit;

namespace Fft.Client.Tests;

public sealed class WorldMenuHostTests
{
    private static WorldMenuSession Session()
    {
        var profile = SyntheticWorldCheckpoint.CreateProfile();
        return new(new WorldSaveSlotStore(profile, Path.Combine(Path.GetTempPath(), "fft-menu-test-" + Guid.NewGuid().ToString("N"))));
    }
    [Theory]
    [InlineData(WorldMenuButtons.Up, WorldMenuInput.Up)]
    [InlineData(WorldMenuButtons.Down, WorldMenuInput.Down)]
    [InlineData(WorldMenuButtons.Confirm, WorldMenuInput.Confirm)]
    [InlineData(WorldMenuButtons.Back, WorldMenuInput.Back)]
    public void Keyboard_and_connected_controller_each_emit_one_edge_until_release(WorldMenuButtons button, WorldMenuInput expected)
    {
        var view = Session().View;
        foreach (bool controller in new[] { false, true })
        {
            var router = new WorldMenuInputRouter(); router.Baseline(new(Connected: true));
            var held = new WorldMenuSample(controller ? 0 : button, controller ? button : 0, true);
            Assert.Equal(expected, router.Poll(held, view, 960, 640, 960, 640)!.Input);
            Assert.Null(router.Poll(held, view, 960, 640, 960, 640));
            Assert.Null(router.Poll(new(Connected: true), view, 960, 640, 960, 640));
            Assert.Equal(expected, router.Poll(held, view, 960, 640, 960, 640)!.Input);
        }
    }
    [Fact]
    public void Focus_and_controller_reconnection_baseline_held_controls()
    {
        var router = new WorldMenuInputRouter(); var view = Session().View;
        var held = new WorldMenuSample(WorldMenuButtons.Confirm, WorldMenuButtons.Back, true, Left: true, Pointer: new(100, 210));
        Assert.Null(router.Poll(held, view, 960, 640, 960, 640));
        Assert.Null(router.Poll(held with { Active = false }, view, 960, 640, 960, 640));
        Assert.Null(router.Poll(held, view, 960, 640, 960, 640));
        Assert.Null(router.Poll(held, view, 960, 640, 960, 640));
        router.Poll(new(), view, 960, 640, 960, 640);
        Assert.Null(router.Poll(new(Controller: WorldMenuButtons.Confirm, Connected: true), view, 960, 640, 960, 640));
        Assert.Null(router.Poll(new(Controller: WorldMenuButtons.Confirm, Connected: true), view, 960, 640, 960, 640));
        router.Poll(new(Connected: true), view, 960, 640, 960, 640);
        Assert.Equal(WorldMenuInput.Confirm, router.Poll(new(Controller: WorldMenuButtons.Confirm, Connected: true), view, 960, 640, 960, 640)!.Input);
    }
    [Fact]
    public void Simultaneous_edges_have_one_safe_priority_and_do_not_leak_after_transition()
    {
        var router = new WorldMenuInputRouter(); var menu = Session(); router.Baseline(new(Connected: true));
        var sample = new WorldMenuSample(WorldMenuButtons.Back | WorldMenuButtons.Confirm, WorldMenuButtons.Down, true, Left: true, Pointer: new(100, 210));
        var action = router.Poll(sample, menu.View, 960, 640, 960, 640)!;
        Assert.Equal(WorldMenuInput.Back, action.Input); action.Apply(menu);
        Assert.Equal(WorldMenuScreen.ConfirmExit, menu.View.Screen);
        Assert.Equal(WorldMenuAction.Cancel, menu.View.Focus);
        Assert.Null(router.Poll(sample, menu.View, 960, 640, 960, 640));
    }
    [Theory]
    [InlineData(960, 640, 960, 640)] [InlineData(1280, 720, 1280, 720)]
    [InlineData(720, 960, 720, 960)] [InlineData(640, 360, 1280, 720)]
    public void Pointer_maps_client_backbuffer_and_bars_to_enabled_entries(int cw, int ch, int bw, int bh)
    {
        var router = new WorldMenuInputRouter(); router.Baseline(new()); var menu = Session();
        PixelPoint Client(int x, int y)
        {
            var fit = SceneLayout.Fit(bw, bh);
            return new((int)(((long)fit.X + (long)x * fit.Width / 960) * cw / bw), (int)(((long)fit.Y + (long)y * fit.Height / 640) * ch / bh));
        }
        var click = new WorldMenuSample(Left: true, Pointer: Client(180, 212));
        Assert.Equal(WorldMenuAction.New, router.Poll(click, menu.View, cw, ch, bw, bh)!.Action);
        Assert.Null(router.Poll(click, menu.View, cw, ch, bw, bh));
        router.Poll(new(), menu.View, cw, ch, bw, bh);
        Assert.Null(router.Poll(click with { Pointer = Client(180, 284) }, menu.View, cw, ch, bw, bh)); // Disabled Resume.
        router.Poll(new(), menu.View, cw, ch, bw, bh);
        Assert.Null(router.Poll(click with { Pointer = new(-1, -1) }, menu.View, cw, ch, bw, bh));
    }
    [Theory]
    [InlineData(0, 0)] [InlineData(1280, 0)] [InlineData(720, 960)]
    public void Minimized_or_letterbox_pointer_cannot_activate(int width, int height)
    {
        var router = new WorldMenuInputRouter(); router.Baseline(new());
        Assert.Null(router.Poll(new(Left: true, Pointer: new(0, 0)), Session().View, width, height, width, height));
    }
    [Fact]
    public void Invalid_dispatches_reject_before_mutating_model()
    {
        var menu = Session(); string before = JsonSerializer.Serialize(menu.View);
        Assert.Throws<ArgumentException>(() => new WorldMenuDispatch().Apply(menu));
        Assert.Throws<ArgumentException>(() => new WorldMenuDispatch(WorldMenuInput.Confirm, WorldMenuAction.New).Apply(menu));
        Assert.Equal(before, JsonSerializer.Serialize(menu.View));
    }
    [Fact]
    public void Finite_plan_passes_expected_screens_and_complete_repeatable_pixel_and_state_checks_headlessly()
    {
        static string Run()
        {
            var menu = Session(); var replay = Session(); var router = new WorldMenuInputRouter(); router.Baseline(new());
            int width = 960, height = 640, events = 0, captures = 0;
            var history = new List<object>();
            foreach (var step in WorldMenuCheckPlan.Steps)
            {
                if (step.Width > 0) (width, height) = (step.Width, step.Height);
                var sample = step.Sample;
                if (sample.Left)
                {
                    var fit = SceneLayout.Fit(width, height);
                    sample = sample with { Pointer = new(fit.X + sample.Pointer.X * fit.Width / 960, fit.Y + sample.Pointer.Y * fit.Height / 640) };
                }
                var command = step.Close ? new WorldMenuDispatch(Close: true) : router.Poll(sample, menu.View, width, height, width, height);
                WorldMenuResult? result = null;
                if (command is not null)
                {
                    result = command.Apply(menu);
                    Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(command.Apply(replay)));
                    events += result.Travel?.Events.Length ?? 0;
                }
                Assert.Equal(JsonSerializer.Serialize(menu.View), JsonSerializer.Serialize(replay.View));
                if (step.Name is "held-confirm" or "connect-held" or "connected-held" or "controller-held" or "inactive" or "reactivated" or "reactivated-held") Assert.Null(command);
                if (step.Name == "party") Assert.Equal(WorldMenuScreen.Party, menu.View.Screen);
                if (step.Name == "home-retained") Assert.Equal(WorldMenuScreen.Home, menu.View.Screen);
                if (step.Name == "confirm-new") Assert.Equal(WorldMenuScreen.ConfirmNew, menu.View.Screen);
                if (step.Name == "close-callback") Assert.Equal(WorldMenuScreen.ConfirmExit, menu.View.Screen);
                if (step.Name == "cancel-close") Assert.Equal(WorldMenuScreen.Party, menu.View.Screen);
                if (step.Capture)
                {
                    string before = JsonSerializer.Serialize(menu.View);
                    uint[] pixels = WorldMenuPresentation.Render(menu.View);
                    Assert.Equal(960 * 640, pixels.Length); Assert.All(pixels, p => Assert.Equal(255u, p >> 24));
                    Assert.Equal(WorldMenuPresentation.Background, pixels[0]); Assert.Equal(WorldMenuPresentation.Accent, pixels[32 * 960 + 32]);
                    Assert.Equal(before, JsonSerializer.Serialize(menu.View)); captures++;
                    history.Add(new { step.Name, menu.View, hash = System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())) });
                }
            }
            Assert.Equal(13, captures); Assert.Equal(2, events);
            Assert.Equal(WorldMenuScreen.Closed, menu.View.Screen);
            Assert.Equal(93, menu.View.Checkpoint!.Funds); Assert.Equal(53, menu.View.Checkpoint.ElapsedSeconds);
            return JsonSerializer.Serialize(history);
        }
        Assert.Equal(Run(), Run());
    }
}
