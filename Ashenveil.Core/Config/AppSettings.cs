using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Utility;

namespace Ashenveil.Core.Config
{
    /// <summary>
    /// The chosen settings and where they live: loaded first thing in AshenveilGame.Initialize,
    /// changed by the settings screen and the console's `server` command, saved by Commit.
    /// </summary>
    public class AppSettings
    {
        private readonly SettingsStore _store;

        /// <summary>The values themselves. The settings screen edits this object in place.</summary>
        public GameSettings Values { get; }

        public AppSettings(SettingsStore store)
        {
            _store = store;
            Values = store.Load();
        }

        /// <summary>
        /// Puts the settings into effect. Runs at startup and after every change, so each setting
        /// has exactly one place that makes it happen.
        /// </summary>
        public void Apply()
        {
            DialogueApi.BaseUrl = Values.backendUrl;
            Debug.DEVMODE = Values.devMode && Debug.DevModeAllowed;
        }

        /// <summary>
        /// Applies then saves the settings. Applied even if the save fails, so the change still
        /// works this session; the IOException is thrown on for the caller to report.
        /// </summary>
        public void Commit()
        {
            Apply();
            _store.Save(Values);
        }

        // The console's `server <url>`: the URL is already normalised by BackendCommands.
        public void SetBackendUrl(string url)
        {
            Values.backendUrl = url;
            Commit();
        }
    }
}
