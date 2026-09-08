namespace Ashenveil.Core.Objects
{
    public class BushLarge : Bush
    {
        protected override float TrunkW => 0.29f;
        protected override float TrunkH => 0.12f;
        protected override float FootInset => 0.24f;
        protected override float XShift => 0.07f;

        public BushLarge(int col, int row, int rotate = 0)
            : base(col, row, Assets.Bush_Large, rotate) { }
    }
}