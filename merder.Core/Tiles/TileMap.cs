using System.Collections.Generic;
using merder.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace merder.Core.Tiles
{
    public class TileMap
    {
        // Which content asset each tile type uses. The one place tile art is mapped.
        private static readonly Dictionary<TileType, string> _assetNames = new()
        {
            [TileType.Grass]         = Assets.Grass,
            [TileType.Dirt]          = Assets.Dirt,
            [TileType.DirtRoad]      = Assets.DirtRoad,
            [TileType.DirtRoadCross] = Assets.DirtRoadCross,
        };

        private TileType[,] _grid;
        private int[,] _rotations;              // degrees (0/90/180/270) per cell
        private Dictionary<TileType, Texture2D> _textures;

        public int Rows => _grid.GetLength(0);
        public int Cols => _grid.GetLength(1);

        public TileMap(TileType[,] grid, int[,] rotations)
        {
            _grid = grid;
            _rotations = rotations;

            // Build the texture lookup ourselves via the shared Textures accessor.
            // ContentManager caches, so loading every tile type here is cheap.
            _textures = new Dictionary<TileType, Texture2D>();
            foreach (var pair in _assetNames)
                _textures[pair.Key] = Textures.Get(pair.Value);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Cell size comes from Layout (the single source of truth), which is
            // updated on load/resize from the viewport + this grid's dimensions.
            int cellW = Layout.CellWidth;
            int cellH = Layout.CellHeight;

            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    var tex = _textures[_grid[y, x]];
                    int rot = _rotations[y, x];
                    float radians = MathHelper.ToRadians(rot);

                    // --- Rotated draw, the part worth learning ---
                    // A sprite rotates around its ORIGIN, and the origin is measured
                    // in the SOURCE texture's pixels. We want to spin around the
                    // sprite's centre, so origin = half the texture's size.
                    var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

                    // Cells are non-square (stretch-to-fill), so a 90°/270° rotation
                    // swaps the sprite's footprint and would leave gaps. Pre-swap the
                    // dest width/height for those so it fills the cell after rotating.
                    int dw = cellW, dh = cellH;
                    if (rot == 90 || rot == 270) { dw = cellH; dh = cellW; }

                    // With this overload the destination rectangle's POSITION is
                    // where the origin lands — not the top-left corner. So we point
                    // it at the cell's CENTRE.
                    var dest = new Rectangle(
                        x * cellW + cellW / 2,
                        y * cellH + cellH / 2,
                        dw, dh);

                    spriteBatch.Draw(tex, dest, null, Color.White, radians, origin,
                                     SpriteEffects.None, 0f);
                }
        }
    }
    
}
