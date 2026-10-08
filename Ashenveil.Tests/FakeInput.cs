using Ashenveil.Core.Utility.Input;
using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Tests;

/// <summary>
/// An IInput the test drives by hand: whatever was last passed to Hold / MoveMouse is
/// what the game sees, frame after frame, until the test changes it.
/// </summary>
public class FakeInput : IInput
{
    public MouseState Mouse { get; private set; }
    public KeyboardState Keyboard { get; private set; }

    /// <summary>These keys are down and every other key is up. Hold() with nothing releases them all.</summary>
    public void Hold(params Keys[] keys) => Keyboard = new KeyboardState(keys);

    public void MoveMouse(int x, int y, bool leftDown = false) =>
        Mouse = new MouseState(x, y, 0,
            leftDown ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
}
