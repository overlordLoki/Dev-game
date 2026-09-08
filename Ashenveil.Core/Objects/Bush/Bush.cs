namespace Ashenveil.Core.Objects
{
    /// <summary>
    /// Base for the bush family. Like Tree, it now only threads its sprite through to
    /// GameObject, which owns the drawing and collision. Boxes are tuned per class in
    /// the editor (Content/bounds.json).
    /// </summary>
    public abstract class Bush : GameObject
    {
        protected Bush(int col, int row, string assetName, int rotate)
            : base(col, row, assetName, rotate) { }
    }
}
