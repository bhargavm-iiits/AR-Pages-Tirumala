using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AlipiriAR.EditorTools
{
    /// <summary>
    /// Android Build Script.
    /// Exposes menu items under top-level "Alipiri AR" menu and "Tools ▸ Alipiri AR".
    /// Generates standalone APK to Builds/AlipiriAR.apk and provides interactive feedback.
    /// </summary>
    public static class BuildScript
    {
        private const string OutputPath = "Builds/AlipiriAR.apk";

        [MenuItem("Alipiri AR/Build Android APK", false, 1)]
        [MenuItem("Tools/Alipiri AR/Build Android APK", false, 1)]
        public static void BuildAndroidApk() => Run(BuildOptions.None);

        [MenuItem("Alipiri AR/Build and Run on Device", false, 2)]
        [MenuItem("Tools/Alipiri AR/Build and Run on Device", false, 2)]
        public static void BuildAndRunOnDevice() => Run(BuildOptions.AutoRunPlayer | BuildOptions.Development);

        private static void Run(BuildOptions options)
        {
            var scenes = new[] { "Assets/Scenes/Pages.unity" };

            // Ensure destination directory exists
            string fullPath = Path.GetFullPath(OutputPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = options,
            };

            var report = BuildPipeline.BuildPlayer(buildOptions);
            var summary = report.summary;

            Debug.Log($"[BuildScript] Result: {summary.result}, Errors: {summary.totalErrors}, Warnings: {summary.totalWarnings}, Size: {summary.totalSize} bytes, Time: {summary.totalTime}");

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] Android APK built successfully at: {fullPath}");
                if (!Application.isBatchMode)
                {
                    float sizeMb = summary.totalSize / (1024f * 1024f);
                    EditorUtility.DisplayDialog("Build Succeeded", $"Android APK built successfully!\n\nFile: {fullPath}\nSize: {sizeMb:F1} MB", "OK");
                    EditorUtility.RevealInFinder(fullPath);
                }
            }
            else
            {
                string msg = $"Android build ended with status: {summary.result} ({summary.totalErrors} errors, {summary.totalWarnings} warnings).";
                Debug.LogError($"[BuildScript] {msg}");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                else
                {
                    EditorUtility.DisplayDialog("Build Failed or Cancelled", $"{msg}\n\nCheck the Unity Console log for details.", "OK");
                }
            }
        }
    }
}
