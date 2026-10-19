using System;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// Which backend to call and how long to wait for it.
    /// </summary>
    public sealed class BackendEndpoint
    {
        /// <summary>
        /// The team's shared API server, used when nothing else names one.
        /// </summary>
        /// <remarks>
        /// GAME_BACKEND_URL and .local/settings.json still win, so pointing an
        /// editor or a build at another backend works as before. Without this a
        /// fresh clone could not start until each developer created the private
        /// file.
        /// </remarks>
        public const string DefaultBackendUrl = "https://localhost";

        /// <summary>The deployed backend.</summary>
        public static string Deployed
        {
            get
            {
                var configured = Environment.GetEnvironmentVariable("GAME_BACKEND_URL");
                if (!string.IsNullOrWhiteSpace(configured)) return NormalizeBaseUrl(configured);
#if UNITY_EDITOR
                var local = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), ".local", "settings.json");
                if (System.IO.File.Exists(local))
                {
                    configured = JsonUtility.FromJson<DeploymentSettings>(System.IO.File.ReadAllText(local))?.backendUrl;
                    if (!string.IsNullOrWhiteSpace(configured)) return NormalizeBaseUrl(configured);
                }
#endif
                var asset = Resources.Load<TextAsset>("DeploymentSettings");
                var settings = asset == null ? null : JsonUtility.FromJson<DeploymentSettings>(asset.text);
                return NormalizeBaseUrl(string.IsNullOrWhiteSpace(settings?.backendUrl)
                    ? DefaultBackendUrl
                    : settings.backendUrl);
            }
        }

        [Serializable]
        private sealed class DeploymentSettings { public string backendUrl; }

        public static string NormalizeBaseUrl(string value)
        {
            var url = value?.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.AbsolutePath != "/")
                throw new ArgumentException("Backend URL must be an HTTP or HTTPS origin without credentials, path, query or fragment.", nameof(value));
            return url.TrimEnd('/');
        }

        /// <summary>
        /// Long enough that a slow phone network still completes, short enough
        /// that a player who tapped refresh is not left watching a spinner.
        /// </summary>
        private const int DefaultTimeoutSeconds = 10;

        public BackendEndpoint(string baseUrl = null, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            }

            var url = string.IsNullOrWhiteSpace(baseUrl) ? Deployed : baseUrl.Trim();

            // Trimmed here so that callers can write paths that start with a
            // slash without every one of them producing a double slash.
            BaseUrl = NormalizeBaseUrl(url);
            TimeoutSeconds = timeoutSeconds;
        }

        public string BaseUrl { get; }

        public int TimeoutSeconds { get; }

        public string Url(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is required.", nameof(path));
            }

            return path[0] == '/' ? BaseUrl + path : BaseUrl + "/" + path;
        }
    }
}
