using System;
using System.IO;
using System.Linq;
using Merder.Editor.Core;
using Photino.NET;

namespace Merder.Editor.App
{
    /// <summary>
    /// The native shell. Its whole job is to open a window, put the UI in it, and
    /// carry messages between that UI and <see cref="EditorApi"/>. No editor logic
    /// lives here - that is all in Merder.Editor.Core, which knows nothing about
    /// Photino. Replacing this file with an ASP.NET host would leave the rest of
    /// the app untouched.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var config = EditorConfig.Load();
            var api = new EditorApi(config);

            PhotinoWindow? window = null;

            window = new PhotinoWindow()
                .SetTitle("merder level editor")
                .SetUseOsDefaultSize(false)
                .SetSize(1360, 900)
                .SetUseOsDefaultLocation(false)
                .SetLeft(80).SetTop(60)
                .SetDevToolsEnabled(true)

                // Lets the UI reference game art directly as <img src="sprite:///Tiles/grass.png">.
                // Serving bytes on demand beats base64-inlining 2MB of PNGs into the page,
                // and means changed art shows up on a reload rather than a rebuild.
                .RegisterCustomSchemeHandler("sprite", (object sender, string scheme, string url, out string contentType) =>
                {
                    contentType = "image/png";
                    return OpenSprite(api.Project, url);
                })

                // The bridge: one JSON request in, one JSON response out.
                .RegisterWebMessageReceivedHandler((sender, message) =>
                {
                    var self = (PhotinoWindow)sender!;
                    self.SendWebMessage(api.HandleJson(message));
                });

            // Picking a folder needs a native dialog, which only the shell can show.
            api.FolderPicker = () =>
            {
                string start = config.IsValid
                    ? config.GameFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                string[]? picked = window!.ShowOpenFolder("Select the merder game folder", start, false);
                return picked is { Length: > 0 } ? picked[0] : null;
            };

            // Absolute, not "ui/index.html": a relative path resolves against the
            // working directory, which is whatever folder the app was launched from.
            window.Load(Path.Combine(AppContext.BaseDirectory, "ui", "index.html"));
            window.WaitForClose();
        }

        /// <summary>
        /// Serves one PNG out of the project's Sprites folder.
        ///
        /// The url arrives as "sprite:///Tiles/grass.png" (browsers vary on how many
        /// slashes survive), so the path is taken apart defensively. Anything that
        /// resolves outside the Sprites folder is refused - a web view should not be
        /// able to read arbitrary files off disk just by asking for "../../secrets".
        /// </summary>
        private static Stream OpenSprite(GameProject? project, string url)
        {
            if (project == null) return Stream.Null;

            try
            {
                string relative = url;

                int scheme = relative.IndexOf("://", StringComparison.Ordinal);
                if (scheme >= 0) relative = relative[(scheme + 3)..];
                relative = relative.TrimStart('/');

                int query = relative.IndexOfAny(new[] { '?', '#' });
                if (query >= 0) relative = relative[..query];

                relative = Uri.UnescapeDataString(relative);
                if (relative.Length == 0) return Stream.Null;

                string root = Path.GetFullPath(project.SpritesDir);
                string full = Path.GetFullPath(Path.Combine(root, relative));

                if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return Stream.Null;
                if (!File.Exists(full))
                    return Stream.Null;

                return File.OpenRead(full);
            }
            catch
            {
                // A broken image in the palette is survivable; a crashed window is not.
                return Stream.Null;
            }
        }
    }
}
