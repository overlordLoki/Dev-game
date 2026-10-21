using System.Collections.Generic;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Ashenveil.Core.Graphics;

namespace Ashenveil.Core.Objects
{
    /// <summary>
    /// Shared behaviour for every placed object - tree, bush, well: grid position,
    /// texture, depth sorting, drawing and collision. A concrete subclass usually adds
    /// nothing but a constructor that names its sprite; its collision boxes come from
    /// Content/bounds.json keyed by the class name and are tuned in the editor, so the
    /// shape lives in data rather than a wall of per-class fraction overrides.
    /// </summary>
    public abstract class GameObject : IObject
    {
        public int Col { get; protected set; }
        public int Row { get; protected set; }
        public Texture2D Texture { get; protected set; }
        public int Rotate { get; protected set; }

        // Depth-sort key: the base = bottom edge of the object's cell, in cells.
        public float SortY => Row + 1;

        // How big the sprite is drawn, in multiples of a cell. Trees have always sat at
        // 1.5; a subclass whose art wants a different footprint overrides just this.
        protected virtual float DrawScale => 1.5f;

        protected GameObject(int col, int row, string assetName, int rotate)
        {
            Col = col;
            Row = row;
            Rotate = rotate;
            Texture = Textures.Get(assetName);
        }

        // The fraction boxes for this concrete class, placed against this object's cell: in
        // world units that's simply the 1x1 square at (Col, Row). (Cells are square now, so
        // the old centring-in-a-stretched-cell sum always came out as exactly this.)
        public IReadOnlyList<RectF> Boxes
        {
            get
            {
                var boxes = BoundsStore.For(GetType().Name);
                var rects = new RectF[boxes.Count];
                for (int i = 0; i < boxes.Count; i++)
                    rects[i] = boxes[i].ToRect(Col, Row, 1f, 1f);
                return rects;
            }
        }

        public RectF Bounds => Geometry.Union(Boxes);

        public void Draw(SpriteBatch spriteBatch)
        {
            int cellW = Layout.CellWidth;
            int cellH = Layout.CellHeight;
            int size  = (int)(Layout.CellSize * DrawScale);

            int x = Col * cellW + (cellW - size) / 2;
            int y = Row * cellH + (cellH - size);

            float radians = MathHelper.ToRadians(Rotate);
            var origin = new Vector2(Texture.Width / 2f, Texture.Height / 2f);
            var dest   = new Rectangle(x + size / 2, y + size / 2, size, size);

            spriteBatch.Draw(Texture, dest, null, Color.White, radians, origin,
                             SpriteEffects.None, 0f);

            // Debug: outline every collision box.
            foreach (var box in Boxes)
                Debug.DrawRect(spriteBatch, Layout.ToScreen(box), Color.Black);
        }
    }
}
