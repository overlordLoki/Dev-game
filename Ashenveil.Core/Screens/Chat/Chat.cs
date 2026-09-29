using System.Numerics;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Screens.Chat
{
    public class Chat
    {
        public string Text { get; set; }
        public string Portrait { get; set; }
        public string Name { get; set; }

        public Chat(string text, string portrait, string name)
        {
            Text = text;
            Portrait = portrait;
            Name = name;
        }
        public void Draw(SpriteBatch sb, Vector2 pos , string text)
        {
            
        }
    }
}