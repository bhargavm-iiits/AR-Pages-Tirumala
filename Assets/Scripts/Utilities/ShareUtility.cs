using System.IO;
using UnityEngine;

namespace AlipiriAR.Utilities
{
    /// <summary>Settings' "Export Login Sheet". Android hands the file to the OS share sheet via
    /// FileProvider (Assets/Plugins/Android/AlipiriFileProvider.androidlib/AndroidManifest.xml + res/xml/file_paths.xml). Editor
    /// builds have no share sheet, so they reveal the file in Explorer instead — PLAN.md §06:
    /// "Editor builds reveal the Assets/ path."</summary>
    public static class ShareUtility
    {
        private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static void ShareFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ShareUtility] Nothing to share — {path} does not exist.");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            ShareAndroid(path);
#elif UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#else
            Debug.Log($"[ShareUtility] Saved to {path}");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void ShareAndroid(string path)
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var file = new AndroidJavaObject("java.io.File", path);

            string authority = Application.identifier + ".fileprovider";
            var fileProviderClass = new AndroidJavaClass("androidx.core.content.FileProvider");
            var uri = fileProviderClass.CallStatic<AndroidJavaObject>("getUriForFile", currentActivity, authority, file);

            var intent = new AndroidJavaObject("android.content.Intent");
            intent.Call("setAction", "android.intent.action.SEND");
            intent.Call("putExtra", "android.intent.extra.STREAM", uri);
            intent.Call("setType", XlsxMimeType);
            intent.Call("addFlags", 1); // Intent.FLAG_GRANT_READ_URI_PERMISSION

            var intentClass = new AndroidJavaClass("android.content.Intent");
            var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share login sheet");
            currentActivity.Call("startActivity", chooser);
        }
#endif
    }
}
