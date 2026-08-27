using UnityEngine;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Insets its RectTransform to Screen.safeArea so content clears notches, punch-holes and
    /// the gesture-navigation bar on every Android device. Attach to a child of the root canvas
    /// that should respect the safe area — most screens attach this once at the root; the bottom
    /// tab bar additionally anchors to the safe-area bottom specifically (see BottomNavBar).
    /// PLAN.md §07.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool _applyLeft = true;
        [SerializeField] private bool _applyRight = true;
        [SerializeField] private bool _applyTop = true;
        [SerializeField] private bool _applyBottom = true;

        private RectTransform _rect;
        private Rect _lastSafeArea;
        private ScreenOrientation _lastOrientation;

        private void Awake() => _rect = (RectTransform)transform;

        private void OnEnable() => Apply();

        private void Update()
        {
            // Screen.safeArea can change on foldables, split-screen, or orientation change —
            // cheap to compare every frame, only reflows when it actually differs.
            if (Screen.safeArea != _lastSafeArea || Screen.orientation != _lastOrientation)
                Apply();
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastOrientation = Screen.orientation;

            Rect safeArea = _lastSafeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            if (!_applyLeft) anchorMin.x = 0f;
            if (!_applyRight) anchorMax.x = 1f;
            if (!_applyBottom) anchorMin.y = 0f;
            if (!_applyTop) anchorMax.y = 1f;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        /// <summary>Bottom-only inset — used by BottomNavBar so it clears the gesture bar
        /// without also being pushed in from the sides.</summary>
        public static void ApplyBottomOnly(RectTransform rt)
        {
            var fitter = rt.gameObject.AddComponent<SafeAreaFitter>();
            fitter._applyLeft = false;
            fitter._applyRight = false;
            fitter._applyTop = false;
            fitter._applyBottom = true;
        }
    }
}
