using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Full-screen launch video shown while AppBootstrap loads localisation/database in the
    /// background — sits on its own top-most Canvas so it can be torn down independently of
    /// UIRoot, which isn't built yet while this plays.
    /// </summary>
    public static class SplashVideoScreen
    {
        private const string VideoRelativePath = "Videos/welcome_to_tirumala.mp4";

        public static void Play(Action onComplete)
        {
            var go = new GameObject("~SplashVideo", typeof(RectTransform), typeof(Canvas));
            UnityEngine.Object.DontDestroyOnLoad(go);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            go.AddComponent<CanvasScaler>();

            var bg = UIFactory.CreateRect("Background", go.transform);
            UIFactory.StretchFill(bg);
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.color = Color.black;
            bgImage.raycastTarget = false;

            var videoRect = UIFactory.CreateRect("VideoImage", go.transform);
            UIFactory.StretchFill(videoRect);
            var rawImage = videoRect.gameObject.AddComponent<RawImage>();
            rawImage.raycastTarget = false;

            var audioSource = go.AddComponent<AudioSource>();
            var player = go.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.SetTargetAudioSource(0, audioSource);
            player.source = VideoSource.Url;
            player.url = BuildVideoUrl();

            player.errorReceived += (vp, message) =>
            {
                Debug.LogError($"SplashVideoScreen: playback failed for '{player.url}' — {message}");
                UnityEngine.Object.Destroy(go);
                onComplete?.Invoke();
            };

            player.prepareCompleted += vp =>
            {
                var rt = new RenderTexture((int)vp.width, (int)vp.height, 0);
                vp.targetTexture = rt;
                rawImage.texture = rt;
                vp.Play();
            };

            player.loopPointReached += vp =>
            {
                UnityEngine.Object.Destroy(go);
                onComplete?.Invoke();
            };

            player.Prepare();
        }

        /// <summary>
        /// VideoPlayer.url needs a "file://" scheme for local Standalone/Editor paths — a plain
        /// OS path (esp. one containing spaces, like this repo's "Unity Projects" folder) fails
        /// to resolve and silently fires errorReceived instead of playing. Android's StreamingAssets
        /// lives inside the APK/AAB and is read through UnityWebRequest internally, so it takes the
        /// bare path with no scheme prefix.
        /// </summary>
        private static string BuildVideoUrl()
        {
            string path = Path.Combine(Application.streamingAssetsPath, VideoRelativePath).Replace("\\", "/");
#if UNITY_ANDROID && !UNITY_EDITOR
            return path;
#else
            return "file://" + path;
#endif
        }
    }
}
