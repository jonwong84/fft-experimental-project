using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

/// <summary>One command per press; activation baselines held input instead of submitting it.</summary>
public sealed class BattleInputLatch
{
    private readonly HashSet<Keys> held = [];
    private bool leftHeld;
    public bool KeyDown(Keys key) => held.Add(key);
    public void KeyUp(Keys key) => held.Remove(key);
    public bool LeftPress(bool pressed)
    {
        bool fresh = pressed && !leftHeld; leftHeld = pressed; return fresh;
    }
    public void Reset(IEnumerable<Keys> pressedKeys, bool leftPressed)
    {
        held.Clear(); foreach (var key in pressedKeys) held.Add(key); leftHeld = leftPressed;
    }
}
