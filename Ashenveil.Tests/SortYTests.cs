using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Entities depth-sort by their feet (the bottom of the collision box), not the bottom of the
/// sprite - sprites have transparent padding under the feet. A 1-cell entity is a 1x1-cell
/// sprite whose top-left is Position; SortY is in cells too.
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
    }

    [Fact]
    public void Sorts_by_the_feet_not_the_bottom_of_the_sprite()
    {
        var e = new PaddedEntity(0, 2);

        // Feet = 2 + 0.75. The old rule (sprite bottom) would have said 2 + 1 = 3.
        // Why it matters: next to a tree whose base is at y = 2.9, feet at 2.75 are behind the
        // tree and drawn first; the old 3 drew this entity in front of a tree it stands behind.
        Assert.Equal(2.75f, e.SortY);
        Assert.Equal(e.Bounds.Bottom, e.SortY);
    }

    [Fact]
    public void Without_collision_boxes_it_falls_back_to_the_sprite_bottom()
    {
        var e = new BoxlessEntity(0, 2);

        Assert.Equal(3f, e.SortY);
    }

    [Fact]
    public void A_full_frame_box_gives_the_same_answer_as_before()
    {
        var e = new TestEntity(0, 2);   // its box covers the whole sprite

        Assert.Equal(3f, e.SortY);
    }
}
