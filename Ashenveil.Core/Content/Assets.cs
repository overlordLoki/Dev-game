//path to imgs to be loaded
namespace Ashenveil.Core
{
    // Describes one sprite sheet: where it is, how many frames, and each frame's size.
    public record SpriteSheetInfo(string Path, int FrameCount, int FrameSize);
    public static class Assets
    {
        // Idle: 4 frames, 32x32 each
        public static readonly SpriteSheetInfo PLAYERIDLE = new("Sprites/Player/D_Idle", 4, 32);
        // Walk: 6, 32x32 each
        public static readonly SpriteSheetInfo PlayerWalk = new("Sprites/Player/D_Walk", 6, 32);

        //Tiles
        public const string Grass           = "Sprites/Tiles/grass";
        public const string Dirt            = "Sprites/Tiles/dirt";
        public const string DirtRoad        = "Sprites/Tiles/straight_dirt_road";
        public const string DirtRoadCross   = "Sprites/Tiles/cross_dirt_road";
        public const string TurnDirtRoad    = "Sprites/Tiles/turn_dirt_road";
        public const string BendDirtRoad    = "Sprites/Tiles/bend_dirt_road";
        public const string DirtRoadEnd     = "Sprites/Tiles/end_dirt_road";
        public const string DirtRoadTee     = "Sprites/Tiles/tee_dirt_road";
        public const string SingleDirtRoad   = "Sprites/Tiles/single_dirt_road";

        //Objects
        public const string Tree_Small      = "Sprites/Objects/Tree_Small";
        public const string Tree_Medium     = "Sprites/Objects/Tree_Medium";
        public const string Tree_Large      = "Sprites/Objects/Tree_Large";
        public const string Well             = "Sprites/Objects/Well";
        public const string Bush_Small      = "Sprites/Objects/Bushes_Small";
        public const string Bush_Medium     = "Sprites/Objects/Bushes_Medium";
        public const string Bush_Large      = "Sprites/Objects/Bushes_Large";
        public const string Castle_Square   = "Sprites/Objects/Castle_Square";
  
    }
}
