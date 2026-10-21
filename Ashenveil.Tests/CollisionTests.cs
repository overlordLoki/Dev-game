using Ashenveil.Core.Collision;
using Microsoft.Xna.Framework;

namespace Ashenveil.Tests;

/// <summary>
/// Every entity here is a 1x1-cell square with its top-left at Position (see TestEntity),
/// so the numbers in each test can be checked on paper. Collision is in world units
/// (cells), so no Layout.Update is needed - the window size doesn't come into it.
/// Floats like 0.9 aren't exact in binary, so positions are compared with Near.
/// </summary>
public class CollisionTests
{
    // ------------------------------------------------------------------ the fixture

    [Fact]
    public void TestEntity_body_is_a_one_cell_square_at_its_position()
    {
        var e = new TestEntity(0.3f, 0.4f);

        Near.Equal(new RectF(0.3f, 0.4f, 1, 1), e.Bounds);
    }

    // --------------------------------------------------------------- Entity.Overlap

    [Theory]
    // other is...         ox     oy    pushX  pushY
    [InlineData(/*right*/  0.9f,  0.2f, -0.1f, -0.8f)]
    [InlineData(/*left */ -0.9f,  0.2f,  0.1f, -0.8f)]
    [InlineData(/*below*/  0.2f,  0.9f, -0.8f, -0.1f)]
    [InlineData(/*above*/  0.2f, -0.9f, -0.8f,  0.1f)]
    public void Overlap_pushes_away_from_the_nearer_edge_on_each_axis(float ox, float oy, float pushX, float pushY)
    {
        var me    = new RectF(0, 0, 1, 1);
        var other = new RectF(ox, oy, 1, 1);

        var (x, y) = TestEntity.OverlapOf(me, other);

        Near.Equal(new Vector2(pushX, pushY), new Vector2(x, y));
    }

    [Fact]
    public void Overlap_when_dead_centre_pushes_right_and_down()
    {
        // Both sides are equally close, so the "<" comparisons fall through to the
        // positive direction. Pinned so the tie-break can't change by accident.
        var box = new RectF(0, 0, 1, 1);

        Assert.Equal((1f, 1f), TestEntity.OverlapOf(box, box));
    }

    // ------------------------------------------------ ResolveCollision(RectF)

    [Theory]
    // start          obstacle       end up
    [InlineData(0,    0,    0.9f, 0.2f, -0.1f, 0)]      // clipped its left edge   -> pushed left
    [InlineData(0.9f, 0,    0,    0.2f,  1,    0)]      // clipped its right edge  -> pushed right
    [InlineData(0,    0,    0.2f, 0.9f,  0,   -0.1f)]   // landed on its top edge  -> pushed up
    [InlineData(0.2f, 0.9f, 0,    0,     0.2f, 1)]      // hit it from underneath  -> pushed down
    public void ResolveCollision_pushes_out_along_the_shallower_axis(
        float startX, float startY, float obstacleX, float obstacleY, float endX, float endY)
    {
        var e = new TestEntity(startX, startY);
        var obstacle = new RectF(obstacleX, obstacleY, 1, 1);

        e.ResolveCollision(obstacle);

        Near.Equal(new Vector2(endX, endY), e.Position);
        Assert.False(e.Bounds.Intersects(obstacle));
    }

    [Fact]
    public void ResolveCollision_on_an_exact_corner_resolves_vertically()
    {
        // A quarter cell deep on both axes: a tie goes to Y. This is the "sticks on corners"
        // case - if it ever changes to X, walking along a wall will feel different.
        // (Quarters are exact in binary, so the tie really is a tie.)
        var e = new TestEntity(0, 0);

        e.ResolveCollision(new RectF(0.75f, 0.75f, 1, 1));

        Assert.Equal(new Vector2(0, -0.25f), e.Position);
    }

    [Fact]
    public void ResolveCollision_only_ever_moves_one_axis()
    {
        var e = new TestEntity(0, 0);

        e.ResolveCollision(new RectF(0.7f, 0.6f, 1, 1));   // 0.3 deep in X, 0.4 in Y

        Near.Equal(new Vector2(-0.3f, 0), e.Position);
    }

    [Theory]
    [InlineData(1, 0)]   // edges touching exactly - not an overlap
    [InlineData(0, 1)]
    [InlineData(3, 3)]   // nowhere near
    public void ResolveCollision_leaves_position_alone_when_not_overlapping(float ox, float oy)
    {
        var e = new TestEntity(0, 0);

        e.ResolveCollision(new RectF(ox, oy, 1, 1));

        Assert.Equal(Vector2.Zero, e.Position);
    }

    [Fact]
    public void ResolveCollision_handles_overlaps_smaller_than_a_pixel()
    {
        // In cells, a hundredth of a cell is ~1.4px at 720p. Pixel-rectangle collision
        // rounded this away; float rectangles see it and push it out.
        var e = new TestEntity(0, 0);

        e.ResolveCollision(new RectF(0.99f, 0, 1, 1));

        Near.Equal(new Vector2(-0.01f, 0), e.Position);
    }

    // ---------------------------------------------------- ResolveCollision(list)

    [Fact]
    public void ResolveCollision_list_resolves_against_the_deepest_overlap_only()
    {
        var e = new TestEntity(0, 0);
        var shallow = new RectF(0.95f, 0, 0.5f, 1);    // 0.05 into the right side
        var deep    = new RectF(0, 0.8f, 1, 0.5f);     // 0.2 into the bottom

        e.ResolveCollision(new[] { shallow, deep });

        // Pushed up out of `deep`; X untouched, so it is still 0.05 inside `shallow`,
        // which is left for the next frame.
        Near.Equal(new Vector2(0, -0.2f), e.Position);
        Assert.False(e.Bounds.Intersects(deep));
        Assert.True(e.Bounds.Intersects(shallow));
    }

    [Fact]
    public void ResolveCollision_list_gives_the_same_answer_in_either_order()
    {
        var shallow = new RectF(0.95f, 0, 0.5f, 1);
        var deep    = new RectF(0, 0.8f, 1, 0.5f);
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

        e.ResolveCollision(new[] { new RectF(1, 0, 0.5f, 0.5f), new RectF(4, 4, 0.1f, 0.1f) });
        e.ResolveCollision(Array.Empty<RectF>());

        Assert.Equal(Vector2.Zero, e.Position);
    }

    // ------------------------------------------------------ CheckEntityCollision

    [Fact]
    public void CheckEntityCollision_moves_the_caller_and_not_the_other_entity()
    {
        var mover = new TestEntity(0, 0);
        var other = new TestEntity(0.9f, 0.2f);

        mover.CheckEntityCollision(other);

        Near.Equal(new Vector2(-0.1f, 0), mover.Position);
        Assert.Equal(new Vector2(0.9f, 0.2f), other.Position);
    }

    [Fact]
    public void Npc_blocked_sideways_is_pushed_out_and_turns_around_horizontally()
    {
        var npc = new TestNpc(0, 0) { Direction = new Vector2(1, 1) };

        npc.CheckEntityCollision(new TestEntity(0.9f, 0.2f));   // shallow in X

        Near.Equal(new Vector2(-0.1f, 0), npc.Position);
        Assert.Equal(new Vector2(-1, 1), npc.Direction);
    }

    [Fact]
    public void Npc_blocked_from_below_is_pushed_out_and_turns_around_vertically()
    {
        var npc = new TestNpc(0, 0) { Direction = new Vector2(1, 1) };

        npc.CheckEntityCollision(new TestEntity(0.2f, 0.9f));   // shallow in Y

        Near.Equal(new Vector2(0, -0.1f), npc.Position);
        Assert.Equal(new Vector2(1, -1), npc.Direction);
    }

    [Fact]
    public void Npc_that_is_not_touching_anything_keeps_its_heading()
    {
        var npc = new TestNpc(0, 0) { Direction = new Vector2(1, 1) };

        npc.CheckEntityCollision(new TestEntity(1, 0));   // edges touching only

        Assert.Equal(Vector2.Zero, npc.Position);
        Assert.Equal(new Vector2(1, 1), npc.Direction);
    }

    // ------------------------------------------------------------- Box / Geometry / RectF

    [Fact]
    public void Box_ToRect_scales_fractions_against_the_base_and_offsets_by_origin()
    {
        var box = new Box(0.25f, 0.5f, 0.5f, 0.25f);

        Assert.Equal(new RectF(10.25f, 21f, 0.5f, 0.5f), box.ToRect(10, 20, 1, 2));
    }

    [Fact]
    public void Union_covers_every_box()
    {
        var boxes = new[]
        {
            new RectF(0.5f, 0.5f, 1, 1),
            new RectF(2.5f, 0, 0.5f, 0.5f),
            new RectF(0, 2, 0.25f, 3),
        };

        Assert.Equal(new RectF(0, 0, 3, 5), Geometry.Union(boxes));
    }

    [Fact]
    public void Union_of_one_box_is_that_box()
    {
        var only = new RectF(3, 4, 5, 6);

        Assert.Equal(only, Geometry.Union(new[] { only }));
    }

    [Fact]
    public void Union_of_nothing_is_empty()
    {
        Assert.Equal(RectF.Empty, Geometry.Union(Array.Empty<RectF>()));
        Assert.Equal(RectF.Empty, Geometry.Union(null!));
        Assert.True(RectF.Empty.IsEmpty);
    }

    [Fact]
    public void RectF_edges_and_centre()
    {
        var r = new RectF(1, 2, 3, 4);

        Assert.Equal((1f, 4f, 2f, 6f), (r.Left, r.Right, r.Top, r.Bottom));
        Assert.Equal(new Vector2(2.5f, 4), r.Center);
    }

    [Fact]
    public void RectF_Intersect_is_the_overlap_or_empty()
    {
        var a = new RectF(0, 0, 2, 2);

        Assert.Equal(new RectF(1, 1, 1, 1), RectF.Intersect(a, new RectF(1, 1, 2, 2)));
        Assert.True(RectF.Intersect(a, new RectF(2, 0, 1, 1)).IsEmpty);   // touching only
    }

    [Fact]
    public void RectF_Inflate_grows_every_side()
    {
        Assert.Equal(new RectF(0.5f, 1.75f, 3, 2.5f), new RectF(1, 2, 2, 2).Inflate(0.5f, 0.25f));
    }
}
