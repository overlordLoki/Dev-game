using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Ashenveil.Editor.Core;
using Photino.NET;

namespace Ashenveil.Editor.App
{
    /// <summary>
    /// The native shell. Its whole job is to open a window, put the UI in it, and
    /// carry messages between that UI and <see cref="EditorApi"/>. No editor logic
    /// lives here - that is all in Ashenveil.Editor.Core, which knows nothing about
    /// Photino. Replacing this file with an ASP.NET host would leave the rest of
    /// the app untouched.
    /// </summary>
    internal static class Program
    {
        [DllImport("libc", SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);

        [STAThread]
        private static void Main()
        {
            // WebKitGTK 2.42+ paints through a DMA-BUF buffer that several Wayland
            // compositors reject, killing the window the moment the page renders:
            //   Gdk-Message: Error 71 (Protocol error) dispatching to Wayland display.
            // The older renderer costs nothing at this size. Set here rather than in a
            // launch script so it applies however the editor is started, and before the
            // window exists so WebKit's child processes inherit it.
            // Environment.SetEnvironmentVariable is no good here: on Unix it only updates
            // a managed copy, which the unmanaged WebKit never reads. setenv is the real one.
            if (OperatingSystem.IsLinux())
            {
                setenv("WEBKIT_DISABLE_DMABUF_RENDERER", "1", 1);
            }

            var config = EditorConfig.Load();
            var api = new EditorApi(config);

            PhotinoWindow? window = null;

            window = new PhotinoWindow()
                .SetTitle("Ashenveil level editor")
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

                string[]? picked = window!.ShowOpenFolder("Select the Ashenveil game folder", start, false);
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
