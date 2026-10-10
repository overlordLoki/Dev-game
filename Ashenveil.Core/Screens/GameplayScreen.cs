using System.Collections.Generic;
using Ashenveil.Core.Utility.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Screens
{
    public class GameplayScreen : IScreen
    {
        public World World { get; private set; }

        public List<Widget> Widgets
        {
            get => World.Widgets;
            set => World.Widgets = value;
        }

        public Matrix Transform => World.Transform;

        public GameplayScreen(World world)
        {
            World = world;
        }

        public void Update(GameTime gameTime)
        {
            World.Update(gameTime);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            World.Draw(spriteBatch, pixel);
        }
    }
}
