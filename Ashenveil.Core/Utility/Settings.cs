namespace Ashenveil.Core
{
    public static class Settings
    {
        private const int ReferenceHeight = 720;
        public static float Scale { get; private set; } = 1f;

        public static void UpdateScale(int screenHeight)
        {
            Scale = (float)screenHeight / ReferenceHeight;
        }
    }
}
