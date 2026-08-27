using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AlipiriAR.Utilities
{
    /// <summary>Cross-platform StreamingAssets text loader. File.ReadAllText fails on Android,
    /// where the folder lives inside the compressed APK — UnityWebRequest is the only path
    /// that works uniformly across Editor, standalone and device. Shared by LocalizationService,
    /// JsonDatabase and anything else that reads from StreamingAssets.
    ///
    /// URLs are built with plain string concatenation, never Path.Combine — on Android,
    /// Application.streamingAssetsPath is already a URI ("jar:file:///.../base.apk!/assets"),
    /// and Path.Combine's separator normalization mangles the "file:///" triple-slash and the
    /// "!" jar-entry separator, silently producing a URL UnityWebRequest can't resolve. This
    /// was found on-device (a real phone), not in the Editor, where the plain-filesystem
    /// fast path below always short-circuits before the URL is ever built.</summary>
    public static class StreamingAssetsLoader
    {
        private static string BuildUrl(string relativePath)
        {
            string basePath = Application.streamingAssetsPath;
            string separator = basePath.EndsWith("/") ? string.Empty : "/";
            string url = basePath + separator + relativePath;
#if !UNITY_ANDROID || UNITY_EDITOR
            if (!url.Contains("://")) url = "file://" + url;
#endif
            return url;
        }

        public static IEnumerator LoadText(string relativePath, Action<string> onLoaded)
        {
#if UNITY_EDITOR
            string localPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (File.Exists(localPath))
            {
                onLoaded(File.ReadAllText(localPath));
                yield break;
            }
#endif
            using var request = UnityWebRequest.Get(BuildUrl(relativePath));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[StreamingAssetsLoader] Failed to load \"{relativePath}\": {request.error}");
                onLoaded(null);
                yield break;
            }

            onLoaded(request.downloadHandler.text);
        }

        public static IEnumerator LoadBytes(string relativePath, Action<byte[]> onLoaded)
        {
#if UNITY_EDITOR
            string localPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (File.Exists(localPath))
            {
                onLoaded(File.ReadAllBytes(localPath));
                yield break;
            }
#endif
            using var request = UnityWebRequest.Get(BuildUrl(relativePath));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[StreamingAssetsLoader] Failed to load \"{relativePath}\": {request.error}");
                onLoaded(null);
                yield break;
            }

            onLoaded(request.downloadHandler.data);
        }

        /// <summary>Decodes a StreamingAssets image (PNG/JPG) into a Texture2D. Goes through
        /// LoadBytes + Texture2D.LoadImage rather than UnityWebRequestTexture — one code path for
        /// every StreamingAssets read (Editor fast path included) instead of a second, parallel
        /// Android-vs-Editor branch just for images. onLoaded(null) on any failure — a missing or
        /// corrupt landmark portrait must never be fatal, only fall back to the procedural icon
        /// that already covers every landmark without one (PLAN.md §03).</summary>
        public static IEnumerator LoadTexture(string relativePath, Action<Texture2D> onLoaded)
        {
            byte[] bytes = null;
            yield return LoadBytes(relativePath, b => bytes = b);
            if (bytes == null) { onLoaded(null); yield break; }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                onLoaded(null);
                yield break;
            }
            onLoaded(tex);
        }
    }
}
