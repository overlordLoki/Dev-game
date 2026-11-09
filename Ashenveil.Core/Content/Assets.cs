//path to imgs to be loaded
namespace Ashenveil.Core
{
    // Describes one sprite sheet: where it is, how many frames, and each frame's size.
    // Frames sit side by side in one row and don't have to be square.
    public record SpriteSheetInfo(string Path, int FrameCount, int FrameWidth, int FrameHeight);
    public static class Assets
    {
        //Player. 32x32 frames, one sheet per action and facing: D_ down, U_ up, S_ side
        // (drawn facing left, flipped for right). Idle down goes first, for the editor's preview.
        public static readonly SpriteSheetInfo PlayerIdleDown   = new("Sprites/Player/D_Idle", 4, 32, 32);
        public static readonly SpriteSheetInfo PlayerIdleUp     = new("Sprites/Player/U_Idle", 4, 32, 32);
        public static readonly SpriteSheetInfo PlayerIdleSide   = new("Sprites/Player/S_Idle", 4, 32, 32);
        public static readonly SpriteSheetInfo PlayerWalkDown   = new("Sprites/Player/D_Walk", 6, 32, 32);
        public static readonly SpriteSheetInfo PlayerWalkUp     = new("Sprites/Player/U_Walk", 6, 32, 32);
        public static readonly SpriteSheetInfo PlayerWalkSide   = new("Sprites/Player/S_Walk", 6, 32, 32);
        public static readonly SpriteSheetInfo PlayerAttackDown = new("Sprites/Player/D_Attack", 4, 32, 32);
        public static readonly SpriteSheetInfo PlayerAttackUp   = new("Sprites/Player/U_Attack", 4, 32, 32);
        public static readonly SpriteSheetInfo PlayerAttackSide = new("Sprites/Player/S_Attack", 4, 32, 32);
        public static readonly SpriteSheetInfo PlayerHurtDown   = new("Sprites/Player/D_Hurt", 2, 32, 32);
        public static readonly SpriteSheetInfo PlayerHurtUp     = new("Sprites/Player/U_Hurt", 2, 32, 32);
        public static readonly SpriteSheetInfo PlayerHurtSide   = new("Sprites/Player/S_Hurt", 2, 32, 32);
        public static readonly SpriteSheetInfo PlayerDeathDown  = new("Sprites/Player/D_Death", 8, 32, 32);
        public static readonly SpriteSheetInfo PlayerDeathUp    = new("Sprites/Player/U_Death", 8, 32, 32);
        public static readonly SpriteSheetInfo PlayerDeathSide  = new("Sprites/Player/S_Death", 8, 32, 32);

        //Knight. Every sheet is 96x84 frames, facing right.
        // Idle goes first: the editor previews an entity with the first sheet whose name
        // starts with the class name (Knight -> KnightIdle).
        public static readonly SpriteSheetInfo KnightIdle    = new("Sprites/NPC/Knight/IDLE", 7, 96, 84);
        public static readonly SpriteSheetInfo KnightWalk    = new("Sprites/NPC/Knight/WALK", 8, 96, 84);
        public static readonly SpriteSheetInfo KnightRun     = new("Sprites/NPC/Knight/RUN", 8, 96, 84);
        public static readonly SpriteSheetInfo KnightJump    = new("Sprites/NPC/Knight/JUMP", 5, 96, 84);
        public static readonly SpriteSheetInfo KnightAttack1 = new("Sprites/NPC/Knight/ATTACK1", 6, 96, 84);
        public static readonly SpriteSheetInfo KnightAttack2 = new("Sprites/NPC/Knight/ATTACK2", 5, 96, 84);
        public static readonly SpriteSheetInfo KnightAttack3 = new("Sprites/NPC/Knight/ATTACK3", 6, 96, 84);
        public static readonly SpriteSheetInfo KnightDefend  = new("Sprites/NPC/Knight/DEFEND", 6, 96, 84);
        public static readonly SpriteSheetInfo KnightHurt    = new("Sprites/NPC/Knight/HURT", 4, 96, 84);
        public static readonly SpriteSheetInfo KnightDeath   = new("Sprites/NPC/Knight/DEATH", 12, 96, 84);

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

        //Portraits
        public const string KnightPortrait = "Sprites/Portrait/soldier";
        public const string BanditPortrait = "Sprites/Portrait/bandit";
        //UI
        public const string UiAtlas = "Sprites/UI/spritesheet-default.xml";

    }
}
