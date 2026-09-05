namespace Ashenveil.Core.Objects
{
    public class TreeLarge : Tree
    {
        protected override float TrunkW => 0.40f;
        protected override float TrunkH => 0.35f;

        public TreeLarge(int col, int row, int rotate = 0)
            : base(col, row, Assets.Tree_Large, rotate) { }
    }
}
