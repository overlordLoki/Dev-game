using System;
using System.Collections.Generic;
using Ashenveil.Core.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Entities
{
    public class NPC : IEntity
    {
        public int id { get;}
        public Vector2 Position { get; set; }
        private Vector2 direction { get; set; }
        public Texture2D Texture { get; private set; }

        public float Speed { get; set; }
        public int Width  => (int)(Layout.CellSize * 0.9f);
        public int Height => (int)(Layout.CellSize * 0.9f);
        // Depth-sort key: the feet = bottom of the NPC's box.
        public int SortY => (int)(Position.Y + Height);

        // Tight collision box(es) around the body/feet. Tuned in the editor against the
        // debug outline and stored under "NPC" in Content/bounds.json (basis = the
        // sprite's own size).
        public IReadOnlyList<Rectangle> Boxes
        {
            get
            {
                var boxes = BoundsStore.For(nameof(NPC));
                var rects = new Rectangle[boxes.Count];
                for (int i = 0; i < boxes.Count; i++)
                    rects[i] = boxes[i].ToRectangle((int)Position.X, (int)Position.Y, Width, Height);
                return rects;
            }
        }

        public Rectangle Bounds => Geometry.Union(Boxes);
        public Color color { get; set; } = Color.Blue;
        Vector2 IEntity.direction { get => direction; set => direction = value; }

        public NPC(Texture2D texture2D, Vector2 pos, int id)
        {
            this.Texture = texture2D;
            this.Position = pos;
            Speed = 100f;
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            this.id = id;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Rectangle shape = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
            //(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
            spriteBatch.Draw(Texture, Position, shape, color);
        }

        public void Update(GameTime gameTime)
        {
            // TODO: NPC AI logic
        }

        // CheckEntityCollision and ResolveCollision come from IEntity, which resolves
        // against Bounds — the tight box above. This class used to carry its own copy
        // that collided on the full sprite rectangle instead, so the tuned box had no
        // effect on entity-vs-entity hits.
    }
}
