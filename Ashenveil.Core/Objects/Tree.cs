using System.Dynamic;
using Ashenveil.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.Objects
{
    public class Tree : IObject
    {
        public int Col { get; }
        public int Row { get; }
        public int Variety { get; }
        public Texture2D Texture { get; private set; }
        public int Rotate { get; }
        // Depth-sort key: the trunk base = bottom edge of the tree's cell.
        public int SortY => (Row + 1) * Layout.CellHeight;
        public Rectangle Bounds {
            get
            {
                const float trunkW    = 0.40f;  // trunk width  as fraction of a cell
                const float trunkH    = 0.35f;  // trunk height as fraction of a cell
                const float footInset = 0.00f;  // lift box UP off the cell bottom
                const float xShift    = 0.00f;  // + right, - left (fraction of a cell)

                int cw = Layout.CellWidth, ch = Layout.CellHeight;
                int bw = (int)(cw * trunkW);
                int bh = (int)(ch * trunkH);
                int bx = Col * cw + (cw - bw) / 2 + (int)(cw * xShift);
                int by = Row * ch + ch - bh - (int)(ch * footInset);
                return new Rectangle(bx, by, bw, bh);
            }
        }

        public Tree(int col, int row, int variety, int rotate = 0)
        {
            Col = col;
            Row = row;
            Variety = variety;
            this.Rotate = rotate;
            string imgName = variety switch
            {
                1 => Assets.Tree_Small,
                2 => Assets.Tree_Medium,
                _ => Assets.Tree_Large,
            };
            Texture = Textures.Get(imgName);
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
            Debug.DrawRect(spriteBatch,Bounds,Color.Black);
        }
    }
}
