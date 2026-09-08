using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Objects
{
    public abstract class Bush : IObject
    {
        public int Col { get; }
        public int Row { get; }
        public Texture2D Texture { get; }
        public int Rotate { get; }

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
        public Bush(int col, int row, string assetName, int rotate)
        {
            Col = col;
            Row = row;
            Texture = Textures.Get(assetName);
            Rotate = rotate;
        }
        public void Draw(SpriteBatch spriteBatch)
        {
            int cellW = Layout.CellWidth;
            int cellH = Layout.CellHeight;
            int size  = (int)(Layout.CellSize * 1.5f);

            int x = Col * cellW + (cellW - size) / 2;
            int y = Row * cellH + (cellH - size);

            float radians = MathHelper.ToRadians(Rotate);
            spriteBatch.Draw(Texture, new Vector2(x, y), null, Color.White, radians, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
        }
    }
}