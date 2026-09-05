namespace Ashenveil.Core.Objects
{
    public class TreeMedium : Tree
    {
        protected override float TrunkW => 0.40f;
        protected override float TrunkH => 0.35f;

        public TreeMedium(int col, int row, int rotate = 0)
            : base(col, row, Assets.Tree_Medium, rotate) { }
    }
}
