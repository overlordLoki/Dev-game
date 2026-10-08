using Microsoft.Xna.Framework.Input;

namespace Ashenveil.Core.Utility.Input
{
    /// <summary>Where input comes from. LiveInput asks the OS; ScriptedInput replays a timeline.</summary>
    public interface IInput
    {
        MouseState Mouse { get; }
        KeyboardState Keyboard { get; }
    }

    /// <summary>The real game: straight passthrough to MonoGame's statics.</summary>
    public class LiveInput : IInput
    {
        public MouseState Mouse => Microsoft.Xna.Framework.Input.Mouse.GetState();
        public KeyboardState Keyboard => Microsoft.Xna.Framework.Input.Keyboard.GetState();
    }
}