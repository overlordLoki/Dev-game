using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Objects
{
    /// <summary>
    /// Shared behaviour for every tree: position, texture, depth sorting and drawing.
    /// The collision box is deliberately left to the subclasses - a sapling and an oak
    /// occupy the same cell but block very different amounts of it, so each one states
    /// its own trunk size below rather than sharing one compromise value.
    /// </summary>
    public abstract class Tree : IObject
    {
        public int Col { get; }
        public int Row { get; }
        public Texture2D Texture { get; }
        public int Rotate { get; }

        // Depth-sort key: the trunk base = bottom edge of the tree's cell.
        public int SortY => (Row + 1) * Layout.CellHeight;

        // The collision box, expressed as fractions of a single cell so it stays
        // correct at any zoom level. Subclasses tune these four numbers and nothing else.
        protected abstract float TrunkW { get; }    // trunk width  as fraction of a cell
        protected abstract float TrunkH { get; }    // trunk height as fraction of a cell
        protected virtual float FootInset => 0.00f; // lift box UP off the cell bottom
        protected virtual float XShift => 0.00f;    // + right, - left (fraction of a cell)

        public Rectangle Bounds
        {
            get
            {
                int cw = Layout.CellWidth, ch = Layout.CellHeight;
                int bw = (int)(cw * TrunkW);
                int bh = (int)(ch * TrunkH);
                int bx = Col * cw + (cw - bw) / 2 + (int)(cw * XShift);
                int by = Row * ch + ch - bh - (int)(ch * FootInset);
                return new Rectangle(bx, by, bw, bh);
            }
        }

        protected Tree(int col, int row, string assetName, int rotate)
        {
            Col = col;
            Row = row;
            Rotate = rotate;
            Texture = Textures.Get(assetName);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            int cellW = Layout.CellWidth;
            int cellH = Layout.CellHeight;
            int size  = (int)(Layout.CellSize * 1.5f);

            int x = Col * cellW + (cellW - size) / 2;
            int y = Row * cellH + (cellH - size);

            float radians = MathHelper.ToRadians(Rotate);
            var origin = new Vector2(Texture.Width / 2f, Texture.Height / 2f);
            var dest   = new Rectangle(x + size / 2, y + size / 2, size, size);

            spriteBatch.Draw(Texture, dest, null, Color.White, radians, origin,
                             SpriteEffects.None, 0f);

            //draw bound box
            Debug.DrawRect(spriteBatch, Bounds, Color.Black);
        }
    }
}
