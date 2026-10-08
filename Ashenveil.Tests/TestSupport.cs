using Ashenveil.Core;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Levels;
using Ashenveil.Core.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

// Layout, Textures and BoundsStore are static, so two test classes running at once
// would trample each other's cell size. Run everything on one thread instead.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Ashenveil.Tests;

/// <summary>
/// A bare entity with a known body: Content/bounds.json (the tests' own copy) gives
/// "TestEntity" one box covering the whole frame, and with no animations registered
/// Width == Height == CellSize. So after Layout.Update(500) the body is exactly a
/// 100x100 square whose top-left corner is Position.
/// </summary>
public class TestEntity : Entity
{
    public TestEntity(float x, float y, int id = 0) : base(new Vector2(x, y), id) { }

    protected override float SizeInCells => 1f;

    // Overlap is protected; this is the door in for the tests.
    public static (float pushX, float pushY) OverlapOf(Rectangle me, Rectangle other) => Overlap(me, other);
}

/// <summary>Same 100x100 body as <see cref="TestEntity"/>, but with an NPC's brain attached.</summary>
public class TestNpc : NPC
{
    public TestNpc(float x, float y) : base(new Vector2(x, y), 0, "Test") { }

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
