using System.Collections;
using System.Text;
using AlipiriAR.Core;
using AlipiriAR.Utilities;
using UnityEngine;
using UnityEngine.Networking;

namespace AlipiriAR.Map
{
    /// <summary>
    /// Client for Google's Map Tiles API (tile.googleapis.com) — the online counterpart to
    /// TileBasemap's offline-baked StreamingAssets tiles. Added so the Map tab shows real map
    /// imagery immediately wherever the device has a network connection, without waiting on an
    /// offline bake for that area. TileBasemap tries the baked file first and only falls back
    /// here on a miss, so the app's offline-first behaviour is unchanged when there's no
    /// network — this is purely additive.
    ///
    /// Uses the same x/y/z slippy-map addressing GeoMath already documents ("OSM/Google XYZ"),
    /// so tile placement math is identical for both sources.
    ///
    /// Session state is cached in static fields so every MapScreen open after the first reuses
    /// the same session token (Google sessions last ~2 days — see CreateSessionResponse.expiry)
    /// instead of re-creating one on every screen open.
    ///
    /// The API key ships inside the app on purpose — this is a client SDK key, not a server
    /// secret, the same category as any Google Maps Android key. It MUST be restricted in
    /// Google Cloud Console: Application restriction → Android apps → package
    /// "com.alipiriar.navigation" + this build's signing SHA-1, and API restriction → only
    /// "Map Tiles API". An unrestricted key here is a real liability; a restricted one is not.
    /// </summary>
    public static class GoogleTileSession
    {
        private const string ApiKeyStreamingAssetsPath = "map_api_key.txt";
        private const string CreateSessionUrl = "https://tile.googleapis.com/v1/createSession?key=";
        private const string TileUrlFormat = "https://tile.googleapis.com/v1/2dtiles/{0}/{1}/{2}?session={3}&key={4}";

        private const float InitialBackoffSeconds = 30f;
        private const float MaxBackoffSeconds = 300f;

        private static string _apiKey;
        private static string _sessionToken;
        private static bool _available;
        private static bool _keyLoaded;

        // Retry state — see EnsureSession's class doc note. A once-per-process latch previously
        // sat here instead: one failure (e.g. one dead zone on the walk) disabled online tiles
        // for the rest of the run with no way to recover even once the network came back
        // (Docs/update1.md §02 F-06 / Phase 0 item 5).
        private static float _nextAttemptAllowedAt;
        private static float _currentBackoffSeconds = InitialBackoffSeconds;
        private static NetworkReachability _lastReachability = NetworkReachability.NotReachable;

        /// <summary>True once a session token has been obtained and online tile fetches can be attempted.</summary>
        public static bool IsAvailable => _available && !string.IsNullOrEmpty(_sessionToken);

        [System.Serializable]
        private class CreateSessionResponse
        {
            public string session;
            public string expiry;
            public int tileWidth;
            public int tileHeight;
            public string imageFormat;
        }

        /// <summary>
        /// Safe to call from every TileBasemap load attempt. Cheap when already available (one
        /// float comparison) or still inside a backoff window (same). On failure, schedules a
        /// retry with exponential backoff (30 s → 5 min) instead of the previous once-per-process
        /// latch that disabled online tiles for the rest of the run after a single failure — one
        /// dead zone for 30 seconds during the walk no longer costs the rest of the climb
        /// (Docs/update1.md §02 F-06 / Phase 0 item 5). A change in
        /// Application.internetReachability (e.g. airplane mode toggled off) resets the backoff
        /// immediately, so recovery doesn't wait out whatever window was already in progress.
        /// Never throws: any failure just leaves <see cref="IsAvailable"/> false and callers fall
        /// back to the demo basemap exactly as if this class didn't exist.
        /// </summary>
        public static IEnumerator EnsureSession()
        {
            if (IsAvailable) yield break;

            var reachability = Application.internetReachability;
            if (reachability != _lastReachability)
            {
                _lastReachability = reachability;
                _nextAttemptAllowedAt = 0f; // reachability changed — don't make it wait out a stale backoff
            }

            if (Time.unscaledTime < _nextAttemptAllowedAt) yield break;

            // Gate through ConnectivityService when it's registered (Phase 2 item 5) — an
            // unregistered service (very early cold start) is treated as "attempt it" rather
            // than blocking, since that's strictly no worse than this class's pre-Phase-2
            // behaviour and never blocks the very first tile.
            if (ServiceLocator.TryGet<ConnectivityService>(out var connectivity) && !connectivity.ShouldAttemptNetwork())
            {
                ScheduleRetry();
                yield break;
            }

            if (!_keyLoaded)
            {
                _keyLoaded = true;
                yield return StreamingAssetsLoader.LoadText(ApiKeyStreamingAssetsPath, key => _apiKey = key?.Trim());
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning($"[GoogleTileSession] No StreamingAssets/{ApiKeyStreamingAssetsPath} found — online map tiles disabled, offline/demo basemap only.");
                yield break; // a missing key file isn't a transient failure — no point retrying until the app is rebuilt
            }

            string body = "{\"mapType\":\"roadmap\",\"language\":\"en-US\",\"region\":\"IN\",\"scale\":\"scaleFactor1x\"}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            using var request = new UnityWebRequest(CreateSessionUrl + _apiKey, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[GoogleTileSession] Session creation failed, retrying in {_currentBackoffSeconds:F0}s: " +
                                  $"{request.error} ({request.downloadHandler?.text})");
                ScheduleRetry();
                yield break;
            }

            CreateSessionResponse response = null;
            try
            {
                response = JsonUtility.FromJson<CreateSessionResponse>(request.downloadHandler.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[GoogleTileSession] Could not parse session response: " + ex.Message);
            }

            if (response == null || string.IsNullOrEmpty(response.session))
            {
                Debug.LogWarning("[GoogleTileSession] Session response had no token.");
                ScheduleRetry();
                yield break;
            }

            _sessionToken = response.session;
            _available = true;
            _currentBackoffSeconds = InitialBackoffSeconds; // reset so a future failure, after a long healthy run, starts short again
            Debug.Log("[GoogleTileSession] Online map tiles session established.");
        }

        private static void ScheduleRetry()
        {
            _nextAttemptAllowedAt = Time.unscaledTime + _currentBackoffSeconds;
            _currentBackoffSeconds = Mathf.Min(_currentBackoffSeconds * 2f, MaxBackoffSeconds);
        }

        /// <summary>Only valid once <see cref="IsAvailable"/> is true.</summary>
        public static string TileUrl(int z, int x, int y) =>
            string.Format(TileUrlFormat, z, x, y, _sessionToken, _apiKey);
    }
}
