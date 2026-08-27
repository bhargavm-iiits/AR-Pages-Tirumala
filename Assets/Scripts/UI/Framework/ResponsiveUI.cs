using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Configures the root Canvas so the layout is identical in proportion on every Android
    /// aspect ratio from 16:9 to 21:9, then exposes a safe-area-inset child ("SafeAreaRoot")
    /// that every screen parents into. See PLAN.md §07 — this is the whole "fit every phone" spec.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ResponsiveUI : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        public RectTransform SafeAreaRoot { get; private set; }

        /// <summary>Full-bleed panel behind SafeAreaRoot in this same Overlay canvas — exposed so
        /// ARNavigationScreen can make it transparent too. Found on a real device: making only
        /// ARNavigationScreen's own _backgroundImg transparent left the AR camera fully hidden,
        /// because this panel is an earlier Canvas sibling covering the ENTIRE screen (not just the
        /// notch strip it was meant for), sitting behind SafeAreaRoot but still fully opaque and
        /// still part of the same Screen Space Overlay canvas the AR camera renders underneath —
        /// so it blocked the passthrough on every frame regardless of AR state.</summary>
        public Image EdgeToEdgeBackground { get; private set; }

        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // match width — see PLAN.md §07

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Full-bleed background BEHIND the safe-area content, covering the notch/status-bar
            // strip too. SafeAreaFitter insets SafeAreaRoot itself, so every screen's own
            // background panel (parented under it) stops at the safe-area edge — nothing this
            // app draws ever reaches the notch region. Without this, that strip shows whatever
            // Unity's empty scene renders behind the Canvas (no camera exists there outside AR),
            // which is a jarring, unstyled gray-blue bar — found on a real device, not visible
            // in any Editor check since there's no notch to test against there.
            var edgeToEdgeBg = new GameObject("EdgeToEdgeBackground", typeof(RectTransform));
            var edgeToEdgeRt = (RectTransform)edgeToEdgeBg.transform;
            edgeToEdgeRt.SetParent(transform, false);
            UIFactory.StretchFill(edgeToEdgeRt);
            var edgeToEdgeImg = edgeToEdgeBg.AddComponent<Image>();
            edgeToEdgeImg.color = UITheme.Ground;
            edgeToEdgeImg.raycastTarget = false;
            EdgeToEdgeBackground = edgeToEdgeImg;

            var safeAreaGo = new GameObject("SafeAreaRoot", typeof(RectTransform));
            SafeAreaRoot = (RectTransform)safeAreaGo.transform;
            SafeAreaRoot.SetParent(transform, false);
            UIFactory.StretchFill(SafeAreaRoot);
            safeAreaGo.AddComponent<SafeAreaFitter>();
        }
    }
}
