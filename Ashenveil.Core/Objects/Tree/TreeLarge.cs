namespace Ashenveil.Core.Objects
{
    public class TreeLarge : Tree
    {
        protected override float TrunkW => 0.29f;
        protected override float TrunkH => 0.12f;
        protected override float FootInset => 0.24f;
        protected override float XShift => 0.07f;

        public TreeLarge(int col, int row, int rotate = 0)
            : base(col, row, Assets.Tree_Large, rotate) { }
    }
}
