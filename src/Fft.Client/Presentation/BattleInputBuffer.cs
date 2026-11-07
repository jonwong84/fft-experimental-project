using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

/// <summary>Queued press edges; an ownership/focus boundary discards old presses and baselines held buttons.</summary>
public sealed class BattleInputBuffer
{
    private readonly BattleInputLatch latch = new();
    private readonly Queue<Keys> keys = [];
    public void KeyDown(Keys key) { if (latch.KeyDown(key)) keys.Enqueue(key); }
    public void KeyUp(Keys key) => latch.KeyUp(key);
    public bool TryTake(out Keys key) => keys.TryDequeue(out key);
    public bool LeftPress(bool pressed) => latch.LeftPress(pressed);
    public void ClearQueued() => keys.Clear();
    public void Baseline(IEnumerable<Keys> held, bool leftPressed) { keys.Clear(); latch.Reset(held, leftPressed); }
}
