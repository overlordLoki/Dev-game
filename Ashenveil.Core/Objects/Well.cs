namespace Ashenveil.Core.Objects
{
    /// <summary>
    /// A well. Its sprite is chosen by the caller rather than fixed per subclass, so it
    /// keeps its own constructor; everything else - drawing, collision - comes from
    /// GameObject. Its box is tuned under the "Well" key in Content/bounds.json.
    /// </summary>
    public class Well : GameObject
    {
        public Well(int col, int row, string assetName, int rotate = 0)
            : base(col, row, assetName, rotate) { }
    }
}
