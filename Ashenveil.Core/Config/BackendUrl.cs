using System;

namespace Ashenveil.Core.Config
{
    /// <summary>
    /// Tidies a typed backend address into the form the game stores and sends requests to:
    /// "127.0.0.1:8000" gets "http://" put in front, "http://host:8000/" loses its trailing
    /// slash, and anything that isn't an http(s) address is turned down with a reason.
    /// Shared by the settings screen, the `server` command and SettingsStore.Load.
    /// </summary>
    public static class BackendUrl
    {
        public static bool TryNormalize(string input, out string url, out string error)
        {
            url = null;
            error = null;
            string text = input?.Trim() ?? "";

            if (text.Length == 0)
            {
                error = $"Enter an address, like {GameSettings.DefaultBackendUrl}.";
                return false;
            }

            // No scheme typed: assume http, the way a browser would.
            if (!text.Contains("://")) text = "http://" + text;

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            {
                error = $"'{input.Trim()}' isn't an address. Try something like {GameSettings.DefaultBackendUrl}.";
                return false;
            }
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                error = $"The address must start with http:// or https://, not {uri.Scheme}://.";
                return false;
            }
            if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
            {
                error = "The address can't have a ? or # part.";
                return false;
            }

            // A path is kept (a backend behind a proxy at /ashenveil), minus its trailing slash,
            // so "/conversation" can simply be added on the end.
            url = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            return true;
        }
    }
}
