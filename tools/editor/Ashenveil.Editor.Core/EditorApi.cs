using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ashenveil.Editor.Core
{
    /// <summary>A call from the UI. Deliberately HTTP-shaped.</summary>
    public class ApiRequest
    {
        /// <summary>Correlation id echoed back so the UI can match the reply.</summary>
        public string id { get; set; } = "";
        public string method { get; set; } = "GET";
        public string path { get; set; } = "";
        /// <summary>Raw JSON body, or null.</summary>
        public JsonNode? body { get; set; }
    }

    public class ApiResponse
    {
        public string id { get; set; } = "";
        public bool ok { get; set; }
        public JsonNode? body { get; set; }
        public string? error { get; set; }
    }

    /// <summary>
    /// The editor backend. Everything the UI can do goes through Handle(), and nothing
    /// in here knows about Photino — swapping the native shell for an ASP.NET server
    /// means mapping HTTP requests onto this same method and deleting nothing else.
    /// </summary>
    public class EditorApi
    {
        private readonly EditorConfig _config;

        /// <summary>
        /// Supplied by the shell, because picking a folder needs a native dialog.
        /// Returns the chosen path, or null if the user cancelled.
        /// </summary>
        public Func<string?>? FolderPicker { get; set; }

        public EditorApi(EditorConfig config) => _config = config;

        public GameProject? Project => _config.IsValid ? new GameProject(_config.GameFolder) : null;

        public string HandleJson(string requestJson)
        {
            ApiResponse response;
            string id = "";
            try
            {
                var request = JsonSerializer.Deserialize<ApiRequest>(requestJson)
                              ?? throw new InvalidOperationException("Empty request.");
                id = request.id;
                response = Handle(request);
            }
            catch (Exception ex)
            {
                response = new ApiResponse { id = id, ok = false, error = ex.Message };
            }

            return JsonSerializer.Serialize(response);
        }

        public ApiResponse Handle(ApiRequest request)
        {
            try
            {
                return new ApiResponse { id = request.id, ok = true, body = Route(request) };
            }
            catch (Exception ex)
            {
                return new ApiResponse { id = request.id, ok = false, error = ex.Message };
            }
        }

        private JsonNode? Route(ApiRequest request)
        {
            string path = request.path.Trim('/');
            string[] parts = path.Length == 0 ? Array.Empty<string>() : path.Split('/');
            string method = request.method.ToUpperInvariant();

            return (method, parts) switch
            {
                ("GET",  ["project"])            => ProjectState(),
                ("POST", ["project", "browse"])  => BrowseForProject(),
                ("POST", ["project"])            => SetProject(Str(request.body, "path")),

                // The palette, derived from the game's own source.
                ("GET",  ["catalogue"])          => Json(GameCatalogue.Build(RequireProject())),
                // Every PNG under Content/Sprites, for browsing art not yet wired up.
                ("GET",  ["sprites"])            => Json(SpriteScanner.Scan(RequireProject())),

                // Collision boxes, read from and written back to Content/bounds.json.
                ("GET", ["bounds"])              => Json(BoundsScanner.Build(RequireProject())),
                ("PUT", ["bounds", var b])       => SaveBounds(Decode(b), request.body),

                // Asset audit: what's on disk vs .mgcb vs Assets.cs, and the .mgcb auto-fix.
                ("GET",  ["assets", "audit"])    => Json(AssetAudit.Run(RequireProject())),
                ("POST", ["assets", "register"]) => RegisterAssets(request.body),

                ("GET",    ["levels"])           => Json(Levels().List()),
                ("GET",    ["levels", var g])    => JsonNode.Parse(Levels().Read(Decode(g))),
                ("PUT",    ["levels", var p])    => SaveLevel(Decode(p), request.body),
                ("DELETE", ["levels", var d])    => DeleteLevel(Decode(d)),

                _ => throw new InvalidOperationException($"Unknown route: {method} /{path}"),
            };
        }

        private JsonNode ProjectState() => new JsonObject
        {
            ["gameFolder"] = _config.GameFolder,
            ["valid"]      = _config.IsValid,
            ["auto"]       = _config.AutoDetected,
        };

        private JsonNode BrowseForProject()
        {
            string? picked = FolderPicker?.Invoke();
            if (picked == null) return ProjectState();   // cancelled — nothing changes
            return SetProject(picked);
        }

        private JsonNode SetProject(string path)
        {
            string root = EditorConfig.ResolveGameFolder(path)
                ?? throw new InvalidOperationException(
                    $"'{path}' isn't inside the game. Pick the repo root - the folder holding the .Core project.");

            _config.GameFolder = root;
            _config.AutoDetected = false;   // an explicit choice, and it gets persisted
            _config.Save();
            return ProjectState();
        }

        private JsonNode SaveLevel(string name, JsonNode? body)
        {
            if (body == null) throw new InvalidOperationException("No level data in request.");

            var opts = new JsonSerializerOptions { WriteIndented = true };
            Levels().Write(name, body.ToJsonString(opts));

            return new JsonObject { ["saved"] = name };
        }

        /// <summary>
        /// Writes a target's full box list into Content/bounds.json. The whole list is
        /// sent each save, so this covers add, remove and edit with one route. The target
        /// is re-scanned to settle its basis (cell vs sprite) rather than trusting the UI.
        /// </summary>
        private JsonNode SaveBounds(string id, JsonNode? body)
        {
            if (body == null) throw new InvalidOperationException("No boxes in request.");

            var project = RequireProject();
            var target = BoundsScanner.Build(project).Targets.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"No collision target called '{id}'.");

            var boxes = new List<BoxDto>();
            if (body["boxes"] is JsonArray arr)
                foreach (var b in arr)
                    boxes.Add(new BoxDto
                    {
                        X = (float)(b?["x"]?.GetValue<double>() ?? 0),
                        Y = (float)(b?["y"]?.GetValue<double>() ?? 0),
                        W = (float)(b?["w"]?.GetValue<double>() ?? 0),
                        H = (float)(b?["h"]?.GetValue<double>() ?? 0),
                    });

            BoundsFile.Save(project, id, target.Basis, boxes);

            return new JsonObject
            {
                ["id"]    = id,
                ["file"]  = target.File,
                ["count"] = boxes.Count,
            };
        }

        /// <summary>
        /// Appends .mgcb entries for the given content names (or, if none are given,
        /// every sprite the audit found missing from the .mgcb). Re-audits first so the
        /// decision is made against the files on disk right now, not a stale UI list.
        /// </summary>
        private JsonNode RegisterAssets(JsonNode? body)
        {
            var project = RequireProject();

            // Explicit list from the UI, or fall back to "everything currently unregistered".
            List<string> names;
            var requested = body?["paths"]?.AsArray();
            if (requested != null)
                names = requested.Select(n => n!.GetValue<string>()).ToList();
            else
                names = AssetAudit.Run(project).NotRegistered;

            var added = MgcbRegistrar.Register(project, names);

            return new JsonObject
            {
                ["added"] = Json(added),
                ["audit"] = Json(AssetAudit.Run(project)),   // fresh state after the write
            };
        }

        private JsonNode DeleteLevel(string name)
        {
            Levels().Delete(name);
            return new JsonObject { ["deleted"] = name };
        }

        private LevelStore Levels() => new LevelStore(RequireProject());

        private GameProject RequireProject() =>
            Project ?? throw new InvalidOperationException("No game folder selected yet.");

        private static string Decode(string segment) => Uri.UnescapeDataString(segment);

        private static string Str(JsonNode? body, string key) =>
            body?[key]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Missing '{key}' in request body.");

        // C# properties are PascalCase; the UI reads camelCase. Convert once, here.
        private static readonly JsonSerializerOptions CamelCase =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private static JsonNode Json<T>(T value) =>
            JsonNode.Parse(JsonSerializer.Serialize(value, CamelCase))!;
    }
}
