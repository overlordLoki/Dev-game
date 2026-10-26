namespace Ashenveil.Core.Config
{
    /// <summary>
    /// How big the UI is drawn, worked out from the window height (720 px = 1). Computed, not
    /// chosen, so it isn't part of <see cref="GameSettings"/> and is never saved.
    /// </summary>
    public static class UiScale
    {
        private const int ReferenceHeight = 720;
        public static float Value { get; private set; } = 1f;

        public static void Update(int screenHeight)
        {
            Value = (float)screenHeight / ReferenceHeight;
        }
    }
}
