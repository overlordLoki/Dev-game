using Ashenveil.Core;
using Ashenveil.Core.Collision;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Levels;
using Ashenveil.Core.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Ashenveil.Core.GameWorld.Locations;
using Ashenveil.Core.Graphics;

// Layout, Textures and BoundsStore are static, so two test classes running at once
// would trample each other's cell size. Run everything on one thread instead.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Ashenveil.Tests;

/// <summary>
/// A bare entity with a known body: Content/bounds.json (the tests' own copy) gives
/// "TestEntity" one box covering the whole frame, and with no animations registered
/// Width == Height == 1 cell. So the body is exactly a 1x1-cell square whose top-left
/// corner is Position - at any window size.
/// </summary>
public class TestEntity : Entity
{
    public TestEntity(float x, float y, int id = 0) : base(new Vector2(x, y), id) { }

    protected override float SizeInCells => 1f;

    // Overlap is protected; this is the door in for the tests.
    public static (float pushX, float pushY) OverlapOf(RectF me, RectF other) => Overlap(me, other);
}

/// <summary>
/// Equality give or take a rounding error. World positions are floats, and numbers like 0.9
/// or 0.1 aren't exact in binary, so 0.9 - 1 comes out as -0.099999964, not -0.1.
/// </summary>
public static class Near
{
    public const float Tolerance = 1e-4f;

    public static void Equal(Vector2 expected, Vector2 actual)
    {
        if (Vector2.Distance(expected, actual) > Tolerance)
            throw new Xunit.Sdk.XunitException($"Expected {expected} (give or take {Tolerance}), but got {actual}.");
    }

    public static void Equal(RectF expected, RectF actual)
    {
        bool close = Math.Abs(expected.X - actual.X) <= Tolerance && Math.Abs(expected.Y - actual.Y) <= Tolerance
                  && Math.Abs(expected.Width - actual.Width) <= Tolerance && Math.Abs(expected.Height - actual.Height) <= Tolerance;
        if (!close)
            throw new Xunit.Sdk.XunitException($"Expected {expected} (give or take {Tolerance}), but got {actual}.");
    }

    public static void Equal(float expected, float actual) => Assert.Equal(expected, actual, Tolerance);
}

/// <summary>Same 1x1-cell body as <see cref="TestEntity"/>, but with an NPC's brain attached.</summary>
public class TestNpc : NPC
{
    public TestNpc(float x, float y) : this(x, y, new WanderBrain()) { }

    // Inject the brain so a test can keep a reference and drive it directly
    // (NPC.Brain is protected, so this is the only way in from the test side).
    public TestNpc(float x, float y, NpcBrain brain) : base(new Vector2(x, y), 0, "Test", brain) { }

    // A specific id, for tests that look NPCs up by id.
    public TestNpc(float x, float y, int id) : base(new Vector2(x, y), id, "Test", new WanderBrain()) { }

    protected override float SizeInCells => 1f;
    protected override string BoundsKey => nameof(TestEntity);
}

/// <summary>
/// Stands in for the game's ContentManager so code that asks for a texture (TileMap,
/// trees, wells) can be constructed without a graphics device. Every load "succeeds"
/// and returns null - fine as long as nothing gets drawn.
/// </summary>
public class NullContentManager : ContentManager
{
    public NullContentManager() : base(new GameServiceContainer()) { }

    public override T Load<T>(string assetName) => default!;

    public static void Install() => Textures.Init(new NullContentManager());
}

/// <summary>
/// MapLoader only reads Content/levels/{name}.json from beside the assembly, so to
/// feed it a JSON string we drop the string there under a throwaway name, load it,
/// and delete it again.
/// </summary>
public static class TestLevels
{
    private const string Prefix = "__test_";

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Content", "levels");

    /// <summary>The names of the real levels copied in from Ashenveil.Core/Content/levels.</summary>
    public static string[] ShippedNames() =>
        Directory.GetFiles(Folder, "*.json")
                 .Select(f => Path.GetFileNameWithoutExtension(f)!)
                 .Where(n => !n.StartsWith(Prefix))
                 .ToArray();

    public static LoadedMap Load(string json) => With(json, MapLoader.Load);

    public static Location Location(string json) => With(json, name => new Location(name));

    private static T With<T>(string json, Func<string, T> use)
    {
        NullContentManager.Install();
        Directory.CreateDirectory(Folder);

        string name = Prefix + Guid.NewGuid().ToString("N");
        string path = Path.Combine(Folder, name + ".json");
        File.WriteAllText(path, json);
        try { return use(name); }
        finally { File.Delete(path); }
    }
}
