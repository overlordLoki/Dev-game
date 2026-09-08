using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Objects
{
    public class Well : IObject
    {
        public int Col { get; }

        public int Row { get; }

        public Texture2D Texture { get; }

        public int Rotate { get; }

        // The collision box, expressed as fractions of a single cell so it stays
        // correct at any zoom level. Tune these four numbers and nothing else.
        protected float TrunkW    => 0.97f;  // box width  as fraction of a cell
        protected float TrunkH    => 1.03f;  // box height as fraction of a cell
        protected float FootInset => 0.17f;  // lift box UP off the cell bottom
        protected float XShift    => 0.02f;  // + right, - left (fraction of a cell)

        public Rectangle Bounds {
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

        public int SortY => (Row + 1) * Layout.CellHeight;

        public Well(int col, int row, string assetName, int rotate = 0)
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