using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Microsoft.Xna.Framework;
using Ashenveil.Core.Graphics;

namespace Ashenveil.Tests;

/// <summary>
/// Entities depth-sort by their feet (the bottom of the collision box), not the bottom of the
/// sprite - sprites have transparent padding under the feet. After Layout.Update(500) a
/// 1-cell entity is a 100x100 sprite whose top-left is Position.
/// </summary>
public class SortYTests
{
    // Its box (Content/bounds.json) spans y 25%..75% of the sprite: the bottom quarter is padding.
    private class PaddedEntity : Entity
    {
        public PaddedEntity(float x, float y) : base(new Vector2(x, y), 0) { }
        protected override float SizeInCells => 1f;
        protected override string BoundsKey => "TestPaddedEntity";
    }

    // No entry in bounds.json at all.
    private class BoxlessEntity : Entity
    {
        public BoxlessEntity(float x, float y) : base(new Vector2(x, y), 0) { }
        protected override float SizeInCells => 1f;
        protected override string BoundsKey => "NoSuchEntry";
    }

    public SortYTests()
    {
        NullContentManager.Install();
        Layout.Update(500);   // 100px cells
    }

    [Fact]
    public void Sorts_by_the_feet_not_the_bottom_of_the_sprite()
    {
        var e = new PaddedEntity(0, 200);

        // Feet = 200 + 75. The old rule (sprite bottom) would have said 200 + 100 = 300.
        // Why it matters: next to a tree whose base is at y = 290, feet at 275 are behind the
        // tree and drawn first; the old 300 drew this entity in front of a tree it stands behind.
        Assert.Equal(275, e.SortY);
        Assert.Equal(e.Bounds.Bottom, e.SortY);
    }

    [Fact]
    public void Without_collision_boxes_it_falls_back_to_the_sprite_bottom()
    {
        var e = new BoxlessEntity(0, 200);

        Assert.Equal(300, e.SortY);
    }

    [Fact]
    public void A_full_frame_box_gives_the_same_answer_as_before()
    {
        var e = new TestEntity(0, 200);   // its box covers the whole sprite

        Assert.Equal(300, e.SortY);
    }
}
