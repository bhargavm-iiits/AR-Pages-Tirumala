using System.Linq;
using UnityEditor;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

namespace AlipiriAR.EditorTools
{
    /// <summary>
    /// Fixes a project configuration gap found on a real device (OnePlus 11R 5G, ARCore-certified
    /// hardware, Google Play Services for AR installed) where AR still reported
    /// ArAvailabilityState.Unsupported regardless of the phone. Root cause, confirmed by
    /// inspecting the actual settings asset: ProjectSettings/EditorBuildSettings.asset correctly
    /// points XR Plug-in Management at Assets/XR/XRGeneralSettingsPerBuildTarget.asset, but that
    /// asset's Keys/Values were both empty — no XRGeneralSettings, and therefore no loader, was
    /// ever assigned for ANY build target, including Android. ARCoreLoader.asset existed on disk
    /// but was never wired in as the active loader, so ARSession.CheckAvailability() had no XR
    /// subsystem to query and correctly (from its own perspective) reported Unsupported — this
    /// was never a hardware or app-code problem.
    ///
    /// [InitializeOnLoad] self-heals this on every domain reload (idempotent — does nothing once
    /// Android has a loader), and the menu item lets it be run immediately without waiting for a
    /// reload to happen on its own.
    /// </summary>
    [InitializeOnLoad]
    public static class XRSetup
    {
        private const string ArCoreLoaderTypeName = "UnityEngine.XR.ARCore.ARCoreLoader";

        private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";

        static XRSetup()
        {
            EditorApplication.delayCall += EnsureAndroidArCoreLoaderAssigned;
            EditorApplication.delayCall += EnsureAndroidGraphicsApiCompatibleWithArCore;
            EditorApplication.delayCall += EnsureArCoreRequirementIsOptional;
            EditorApplication.delayCall += EnsureArBackgroundRendererFeature;
        }

        /// <summary>The actual reason no camera passthrough ever appeared, confirmed by tracing the
        /// asset chain a real device build uses: ProjectSettings/QualitySettings.asset maps Android's
        /// default quality level to "Mobile", whose customRenderPipeline is Mobile_RPAsset, whose
        /// renderer data is Assets/Settings/Mobile_Renderer.asset — and that renderer's
        /// m_RendererFeatures list was completely empty. Under URP, ARCameraManager/ARCameraBackground
        /// (built correctly in ARSessionBootstrapper.BuildHierarchy) have nowhere to inject the
        /// camera-feed blit without an ARBackgroundRendererFeature registered on the active renderer —
        /// so the AR session tracks fine underneath (planes, poses) while the screen just shows the AR
        /// camera's plain clear color, indistinguishable from "no camera access at all". This adds the
        /// feature using the same public API (ScriptableRendererData.rendererFeatures / SetDirty) the
        /// URP inspector's own "Add Renderer Feature" button calls.</summary>
        [MenuItem("Tools/Alipiri AR/Fix AR Camera Background (add renderer feature)", false, 6)]
        public static void EnsureArBackgroundRendererFeature()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(MobileRendererPath);
            if (rendererData == null)
            {
                Debug.LogWarning($"[XRSetup] {MobileRendererPath} not found — nothing to fix.");
                return;
            }

            if (rendererData.rendererFeatures.Any(f => f is ARBackgroundRendererFeature))
            {
                Debug.Log("[XRSetup] AR Background Renderer Feature already present on Mobile_Renderer — nothing to do.");
                return;
            }

            var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = "AR Background Renderer Feature";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(rendererData), ImportAssetOptions.ForceUpdate);
            Debug.Log("[XRSetup] Added AR Background Renderer Feature to Mobile_Renderer — the camera " +
                      "passthrough should now actually draw during AR Navigation. Rebuild the APK.");
        }

        /// <summary>AR now confirmed working on a real device (OnePlus 11R 5G), but the app must
        /// also install and run on phones that AREN'T ARCore-certified, falling back gracefully —
        /// ARNavigationScreen already has that fallback UI built (OnArStateChanged handles
        /// Unsupported/Failed/NeedsInstall with its own panel). What was actually undermining that:
        /// Assets/XR/Settings/ARCoreSettings.asset has m_Requirement left at its package default,
        /// Required (0), not Optional (1). At build time ARCoreManifest.OnPostGenerateGradleAndroidProject
        /// (installed ARCore XR Plugin source, ARCoreBuildProcessor.cs:356-364) reads this setting and
        /// REWRITES the hand-authored AndroidManifest.xml — turns "com.google.ar.core" meta-data from
        /// "optional" to "required" and flips "android.hardware.camera.ar" uses-feature from
        /// required="false" to required="true" — regardless of what's hand-written in
        /// Assets/Plugins/Android/AndroidManifest.xml. That silently made every build declare AR as
        /// hardware-mandatory, which is exactly what blocks install/listing on non-ARCore devices.
        /// Flipping this to Optional here makes the shipped manifest match the app's actual runtime
        /// behavior (CheckAvailability + graceful fallback), so the same APK installs everywhere and
        /// only uses AR where the device actually supports it.</summary>
        [MenuItem("Tools/Alipiri AR/Fix AR Requirement (Required -> Optional)", false, 5)]
        public static void EnsureArCoreRequirementIsOptional()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var settings = ARCoreSettings.GetOrCreateSettings();
            if (settings.requirement == ARCoreSettings.Requirement.Optional)
            {
                Debug.Log("[XRSetup] ARCore requirement already Optional — nothing to do.");
                return;
            }

            settings.requirement = ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[XRSetup] ARCore requirement set to Optional — the app will now install on " +
                      "any Android device (minSdk 26) and fall back to the existing non-AR UI on " +
                      "devices without ARCore support, instead of being hardware-gated to " +
                      "ARCore-certified phones only. Rebuild the APK for this to take effect.");
        }

        /// <summary>ARCore's own build preprocessor (ARCorePreprocessBuild.EnsureMinSdkVersion)
        /// rejects any build where Vulkan is a listed Android graphics API and AndroidMinSdkVersion
        /// is below 29 — confirmed via a real failed build against this project's minSdk 26.
        /// Bumping to API 29 would drop Android 8–9 devices, which cuts against this app's own
        /// offline/wide-compatibility goal for pilgrims on older or budget phones, so this drops
        /// Vulkan instead and keeps OpenGLES3 as the sole Android graphics API — ARCore's
        /// requirement is specifically about the Vulkan backend, not ARCore itself.</summary>
        [MenuItem("Tools/Alipiri AR/Fix AR Graphics API (drop Vulkan)", false, 4)]
        public static void EnsureAndroidGraphicsApiCompatibleWithArCore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var current = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            bool hasVulkan = System.Array.IndexOf(current, GraphicsDeviceType.Vulkan) >= 0;
            if (!hasVulkan)
            {
                Debug.Log("[XRSetup] Android graphics APIs already Vulkan-free — nothing to do.");
                return;
            }

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            Debug.Log("[XRSetup] Dropped Vulkan from Android graphics APIs (OpenGLES3 only) so ARCore's " +
                      "minSdk-29-for-Vulkan requirement no longer applies, keeping minSdk 26. Rebuild the APK.");
        }

        [MenuItem("Tools/Alipiri AR/Fix AR Loader Assignment", false, 3)]
        public static void EnsureAndroidArCoreLoaderAssigned()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var settingsAsset = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            if (settingsAsset == null)
            {
                Debug.LogWarning("[XRSetup] XRGeneralSettingsPerBuildTarget.asset not found — nothing to fix.");
                return;
            }

            if (!settingsAsset.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                settingsAsset.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);

            if (!settingsAsset.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                settingsAsset.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            var manager = settingsAsset.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (manager == null)
            {
                Debug.LogError("[XRSetup] Could not create XRManagerSettings for Android.");
                return;
            }

            if (XRPackageMetadataStore.IsLoaderAssigned(ArCoreLoaderTypeName, BuildTargetGroup.Android))
            {
                Debug.Log("[XRSetup] ARCoreLoader already assigned for Android — nothing to do.");
                return;
            }

            bool assigned = XRPackageMetadataStore.AssignLoader(manager, ArCoreLoaderTypeName, BuildTargetGroup.Android);
            if (assigned)
            {
                EditorUtility.SetDirty(settingsAsset);
                AssetDatabase.SaveAssets();
                Debug.Log("[XRSetup] ARCoreLoader assigned for Android — AR should now actually initialize on ARCore-certified devices. Rebuild the APK for this to take effect.");
            }
            else
            {
                Debug.LogError("[XRSetup] Failed to assign ARCoreLoader for Android.");
            }
        }
    }
}
