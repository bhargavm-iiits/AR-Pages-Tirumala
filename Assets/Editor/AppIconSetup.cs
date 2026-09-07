using UnityEditor;
using UnityEngine;

namespace AlipiriAR.EditorTools
{
    /// <summary>Applies Assets/Icons/app_icon_sacred_aura.jpg as the app's icon (what shows on a
    /// user's home screen after install) automatically on Editor load — same "no manual asset
    /// wiring" pattern as TMPEssentialResourcesImporter, idempotent so it only writes
    /// PlayerSettings once. Legacy icon slot only (not per-platform Adaptive foreground/
    /// background layers, which need safe-zone-padded source art this image doesn't have) — the
    /// same square, already sitting inside its own rounded-square frame, is what every Android
    /// version below Adaptive Icons (8.0) and every non-Android build target shows directly.</summary>
    [InitializeOnLoad]
    public static class AppIconSetup
    {
        private const string IconPath = "Assets/Icons/app_icon_sacred_aura.jpg";

        static AppIconSetup()
        {
            EditorApplication.delayCall += ApplyIfNeeded;
        }

        private static void ApplyIfNeeded()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) return;

            var currentAndroid = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Android);
            var currentDefault = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
            bool androidSet = currentAndroid != null && currentAndroid.Length > 0 && currentAndroid[0] == icon;
            bool defaultSet = currentDefault != null && currentDefault.Length > 0 && currentDefault[0] == icon;
            if (androidSet && defaultSet) return;

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon });
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            Debug.Log("[AppIconSetup] Applied app_icon_sacred_aura.jpg as the Android/default app icon.");
        }
    }
}
