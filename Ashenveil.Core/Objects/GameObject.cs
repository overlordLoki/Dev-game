using System.Collections.Generic;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

        // Depth-sort key: the base = bottom edge of the object's cell.
        public int SortY => (Row + 1) * Layout.CellHeight;

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

        // The fraction boxes for this concrete class, placed against this object's cell.
        // The base is the SAME square the sprite is drawn over - a CellSize square,
        // centred horizontally in the cell and sat on its bottom edge - not the raw cell.
        // Cells can be non-square (window 1280x720 over a 10x8 grid = 128x90), and the
        // sprite is sized by CellSize; measuring boxes against the raw cell instead would
        // stretch them off the art horizontally. This matches the editor's square preview.
        public IReadOnlyList<Rectangle> Boxes
        {
            get
            {
                int s  = Layout.CellSize;
                int ox = Col * Layout.CellWidth  + (Layout.CellWidth  - s) / 2;
                int oy = Row * Layout.CellHeight + (Layout.CellHeight - s);

                var boxes = BoundsStore.For(GetType().Name);
                var rects = new Rectangle[boxes.Count];
                for (int i = 0; i < boxes.Count; i++)
                    rects[i] = boxes[i].ToRectangle(ox, oy, s, s);
                return rects;
            }
        }

        public Rectangle Bounds => Geometry.Union(Boxes);

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
                Debug.DrawRect(spriteBatch, box, Color.Black);
        }
    }
}
