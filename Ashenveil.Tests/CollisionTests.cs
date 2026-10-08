using Ashenveil.Core;
using Ashenveil.Core.Collision;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Every entity here is a 100x100 square with its top-left at Position (see TestEntity),
/// so the numbers in each test can be checked on paper.
/// </summary>
public class CollisionTests
{
    public CollisionTests() => Layout.Update(500);   // 500 / 5 visible rows = 100px cells

    // ------------------------------------------------------------------ the fixture

    [Fact]
    public void TestEntity_body_is_a_cell_sized_square_at_its_position()
    {
        var e = new TestEntity(30, 40);

        Assert.Equal(new Rectangle(30, 40, 100, 100), e.Bounds);
    }

    // --------------------------------------------------------------- Entity.Overlap

    [Theory]
    // other is...        ox   oy   pushX pushY
    [InlineData(/*right*/  90,  20,  -10,  -80)]
    [InlineData(/*left */ -90,  20,   10,  -80)]
    [InlineData(/*below*/  20,  90,  -80,  -10)]
    [InlineData(/*above*/  20, -90,  -80,   10)]
    public void Overlap_pushes_away_from_the_nearer_edge_on_each_axis(int ox, int oy, float pushX, float pushY)
    {
        var me    = new Rectangle(0, 0, 100, 100);
        var other = new Rectangle(ox, oy, 100, 100);

        Assert.Equal((pushX, pushY), TestEntity.OverlapOf(me, other));
    }

    [Fact]
    public void Overlap_when_dead_centre_pushes_right_and_down()
    {
        // Both sides are equally close, so the "<" comparisons fall through to the
        // positive direction. Pinned so the tie-break can't change by accident.
        var box = new Rectangle(0, 0, 100, 100);

        Assert.Equal((100f, 100f), TestEntity.OverlapOf(box, box));
    }

    // ------------------------------------------------ ResolveCollision(Rectangle)

    [Theory]
    // start         obstacle        end up
    [InlineData(  0,   0,  90,  20,  -10,    0)]   // clipped its left edge   -> pushed left
    [InlineData( 90,   0,   0,  20,  100,    0)]   // clipped its right edge  -> pushed right
    [InlineData(  0,   0,  20,  90,    0,  -10)]   // landed on its top edge  -> pushed up
    [InlineData( 20,  90,   0,   0,   20,  100)]   // hit it from underneath  -> pushed down
    public void ResolveCollision_pushes_out_along_the_shallower_axis(
        float startX, float startY, int obstacleX, int obstacleY, float endX, float endY)
    {
        var e = new TestEntity(startX, startY);
        var obstacle = new Rectangle(obstacleX, obstacleY, 100, 100);

        e.ResolveCollision(obstacle);

        Assert.Equal(new Vector2(endX, endY), e.Position);
        Assert.False(e.Bounds.Intersects(obstacle));
    }

    [Fact]
    public void ResolveCollision_on_an_exact_corner_resolves_vertically()
    {
        // 10px deep on both axes: a tie goes to Y. This is the "sticks on corners"
        // case - if it ever changes to X, walking along a wall will feel different.
        var e = new TestEntity(0, 0);

        e.ResolveCollision(new Rectangle(90, 90, 100, 100));

        Assert.Equal(new Vector2(0, -10), e.Position);
    }

    [Fact]
    public void ResolveCollision_only_ever_moves_one_axis()
    {
        var e = new TestEntity(0, 0);

        e.ResolveCollision(new Rectangle(70, 60, 100, 100));   // 30 deep in X, 40 in Y

        Assert.Equal(new Vector2(-30, 0), e.Position);
    }

    [Theory]
    [InlineData(100,   0)]   // edges touching exactly - not an overlap
    [InlineData(  0, 100)]
    [InlineData(300, 300)]   // nowhere near
    public void ResolveCollision_leaves_position_alone_when_not_overlapping(int ox, int oy)
    {
        var e = new TestEntity(0, 0);

        e.ResolveCollision(new Rectangle(ox, oy, 100, 100));

        Assert.Equal(Vector2.Zero, e.Position);
    }

    // ---------------------------------------------------- ResolveCollision(list)

    [Fact]
    public void ResolveCollision_list_resolves_against_the_deepest_overlap_only()
    {
        var e = new TestEntity(0, 0);
        var shallow = new Rectangle(95, 0, 50, 100);    // 5px into the right side
        var deep    = new Rectangle(0, 80, 100, 50);    // 20px into the bottom

        e.ResolveCollision(new[] { shallow, deep });

        // Pushed up out of `deep`; X untouched, so it is still 5px inside `shallow`,
        // which is left for the next frame.
        Assert.Equal(new Vector2(0, -20), e.Position);
        Assert.False(e.Bounds.Intersects(deep));
        Assert.True(e.Bounds.Intersects(shallow));
    }

    [Fact]
    public void ResolveCollision_list_gives_the_same_answer_in_either_order()
    {
        var shallow = new Rectangle(95, 0, 50, 100);
        var deep    = new Rectangle(0, 80, 100, 50);
        var a = new TestEntity(0, 0);
        var b = new TestEntity(0, 0);

        a.ResolveCollision(new[] { shallow, deep });
        b.ResolveCollision(new[] { deep, shallow });

        Assert.Equal(a.Position, b.Position);
    }

    [Fact]
    public void ResolveCollision_list_ignores_boxes_that_do_not_overlap()
    {
        var e = new TestEntity(0, 0);

        e.ResolveCollision(new[] { new Rectangle(100, 0, 50, 50), new Rectangle(400, 400, 10, 10) });
        e.ResolveCollision(Array.Empty<Rectangle>());

        Assert.Equal(Vector2.Zero, e.Position);
    }

    // ------------------------------------------------------ CheckEntityCollision

    [Fact]
    public void CheckEntityCollision_moves_the_caller_and_not_the_other_entity()
    {
        var mover = new TestEntity(0, 0);
        var other = new TestEntity(90, 20);

        mover.CheckEntityCollision(other);

        Assert.Equal(new Vector2(-10, 0), mover.Position);
        Assert.Equal(new Vector2(90, 20), other.Position);
    }

    [Fact]
    public void Npc_blocked_sideways_is_pushed_out_and_turns_around_horizontally()
    {
        var npc = new TestNpc(0, 0) { Direction = new Vector2(1, 1) };

        npc.CheckEntityCollision(new TestEntity(90, 20));   // shallow in X

        Assert.Equal(new Vector2(-10, 0), npc.Position);
        Assert.Equal(new Vector2(-1, 1), npc.Direction);
    }

    [Fact]
    public void Npc_blocked_from_below_is_pushed_out_and_turns_around_vertically()
    {
        var npc = new TestNpc(0, 0) { Direction = new Vector2(1, 1) };

        npc.CheckEntityCollision(new TestEntity(20, 90));   // shallow in Y

        Assert.Equal(new Vector2(0, -10), npc.Position);
        Assert.Equal(new Vector2(1, -1), npc.Direction);
    }

    [Fact]
    public void Npc_that_is_not_touching_anything_keeps_its_heading()
    {
        var npc = new TestNpc(0, 0) { Direction = new Vector2(1, 1) };

        npc.CheckEntityCollision(new TestEntity(100, 0));   // edges touching only

        Assert.Equal(Vector2.Zero, npc.Position);
        Assert.Equal(new Vector2(1, 1), npc.Direction);
    }

    // ------------------------------------------------------------- Box / Geometry

    [Fact]
    public void Box_ToRectangle_scales_fractions_against_the_base_and_offsets_by_origin()
    {
        var box = new Box(0.25f, 0.5f, 0.5f, 0.25f);

        Assert.Equal(new Rectangle(1025, 2100, 50, 50), box.ToRectangle(1000, 2000, 100, 200));
    }

    [Fact]
    public void Union_covers_every_box()
    {
        var boxes = new[]
        {
            new Rectangle(10, 10, 20, 20),
            new Rectangle(50, 0, 10, 10),
            new Rectangle(0, 40, 5, 60),
        };

        Assert.Equal(new Rectangle(0, 0, 60, 100), Geometry.Union(boxes));
    }

    [Fact]
    public void Union_of_one_box_is_that_box()
    {
        var only = new Rectangle(3, 4, 5, 6);

        Assert.Equal(only, Geometry.Union(new[] { only }));
    }

    [Fact]
    public void Union_of_nothing_is_empty()
    {
        Assert.Equal(Rectangle.Empty, Geometry.Union(Array.Empty<Rectangle>()));
        Assert.Equal(Rectangle.Empty, Geometry.Union(null));
    }
}
