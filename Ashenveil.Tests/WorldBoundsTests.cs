using Ashenveil.Core.Entities;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.Graphics;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// WorldBounds keeps every entity's body inside the map and tells it which way it was
/// stopped - it doesn't decide how anything reacts. A 5x5 map at Layout.Update(500) is
/// 500x500px; a 1-cell test entity is a 100x100 sprite whose top-left is Position.
/// </summary>
public class WorldBoundsTests
{
    private const string Level = """
        { "cols": 5, "rows": 5,
          "tiles": [["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"],
                    ["grass","grass","grass","grass","grass"]] }
        """;

    // Its collision box covers y 25%..75% of the sprite: the bottom quarter is transparent padding.
    private class PaddedEntity : Entity
    {
        public PaddedEntity(float x, float y) : base(new Vector2(x, y), 0) { }
        protected override float SizeInCells => 1f;
        protected override string BoundsKey => "TestPaddedEntity";
    }

    // Has no collision boxes at all.
    private class BoxlessEntity : Entity
    {
        public BoxlessEntity(float x, float y) : base(new Vector2(x, y), 0) { }
        protected override float SizeInCells => 1f;
        protected override string BoundsKey => "NoSuchEntry";
    }

    // A brain that only records what it was told, so a test can see what reached it.
    private class RecordingBrain : NpcBrain
    {
        public List<bool> Blocked { get; } = new();
        public override void OnBlocked(bool horizontal) => Blocked.Add(horizontal);
    }

    private readonly WorldBounds _bounds;

    public WorldBoundsTests()
    {
        NullContentManager.Install();
        Layout.Update(500);   // 100px cells
        _bounds = new WorldBounds(TestLevels.Location(Level).tileMap);
    }

    [Fact]
    public void The_area_is_the_whole_tile_grid()
    {
        Assert.Equal(new Rectangle(0, 0, 500, 500), _bounds.Area);
    }

    // ------------------------------------------------------------------ keeping things in

    [Theory]
    [InlineData(-30, 200,   0, 200)]   // past the left edge
    [InlineData(450, 200, 400, 200)]   // past the right edge
    [InlineData(200, -30, 200,   0)]   // past the top
    [InlineData(200, 450, 200, 400)]   // past the bottom
    [InlineData(-30, -30,   0,   0)]   // past a corner: both axes
    public void An_entity_past_an_edge_is_pushed_back_inside(float x, float y, float expectedX, float expectedY)
    {
        var e = new TestEntity(x, y);   // box covers the whole sprite

        _bounds.Clamp(e);

        Assert.Equal(new Vector2(expectedX, expectedY), e.Position);
    }

    [Fact]
    public void An_entity_inside_the_map_is_left_alone()
    {
        var e = new TestEntity(200, 200);

        _bounds.Clamp(e);

        Assert.Equal(new Vector2(200, 200), e.Position);
    }

    [Fact]
    public void The_feet_reach_the_edge_not_the_padding()
    {
        // Feet (box bottom) at 430 + 75 = 505: 5px over. Only the feet are kept in, so the
        // transparent padding below them may hang past the edge - the old sprite-based rule
        // stopped the feet 25px short.
        var e = new PaddedEntity(200, 430);

        _bounds.Clamp(e);

        Assert.Equal(500, e.Bounds.Bottom);
    }

    [Fact]
    public void Without_collision_boxes_the_sprite_is_kept_in()
    {
        var e = new BoxlessEntity(450, 200);

        _bounds.Clamp(e);

        Assert.Equal(400, e.Position.X);
    }

    // ------------------------------------------------------------------ who decides the reaction

    [Fact]
    public void Hitting_a_side_tells_the_NPCs_brain_it_was_blocked_horizontally()
    {
        var brain = new RecordingBrain();
        var npc = new TestNpc(450, 200, brain);

        _bounds.Clamp(npc);

        Assert.Equal(new[] { true }, brain.Blocked);
    }

    [Fact]
    public void Hitting_the_top_or_bottom_tells_the_brain_it_was_blocked_vertically()
    {
        var brain = new RecordingBrain();
        var npc = new TestNpc(200, 450, brain);

        _bounds.Clamp(npc);

        Assert.Equal(new[] { false }, brain.Blocked);
    }

    [Fact]
    public void An_NPC_inside_the_map_is_not_told_anything()
    {
        var brain = new RecordingBrain();

        _bounds.Clamp(new TestNpc(200, 200, brain));

        Assert.Empty(brain.Blocked);
    }

    [Fact]
    public void A_wandering_NPC_turns_back_from_the_edge()
    {
        // The reaction lives in WanderBrain now, not in WorldBounds - same result as before.
        var npc = new TestNpc(450, 200, new WanderBrain()) { Direction = new Vector2(1, 0) };

        _bounds.Clamp(npc);

        Assert.Equal(new Vector2(-1, 0), npc.Direction);
    }

    [Fact]
    public void An_NPC_whose_brain_ignores_walls_keeps_its_heading()
    {
        // Before, WorldBounds flipped every NPC's heading. Now a brain that doesn't react
        // (like the knight's) isn't turned around - e.g. mid-GoToTask.
        var npc = new TestNpc(450, 200, new RecordingBrain()) { Direction = new Vector2(1, 0) };

        _bounds.Clamp(npc);

        Assert.Equal(new Vector2(1, 0), npc.Direction);
    }
}
