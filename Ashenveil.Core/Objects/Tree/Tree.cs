namespace Ashenveil.Core.Objects
{
    /// <summary>
    /// Base for the tree family. Everything shared - position, texture, drawing and
    /// collision - now lives in GameObject; a tree subclass only names its sprite.
    /// Collision boxes are tuned per class in the editor (Content/bounds.json).
    /// </summary>
    public abstract class Tree : GameObject
    {
        protected Tree(int col, int row, string assetName, int rotate)
            : base(col, row, assetName, rotate) { }
    }
}
