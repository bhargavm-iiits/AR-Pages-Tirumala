using UnityEngine;

namespace AlipiriAR.UI
{
    /// <summary>Base class every full screen (Login, AR Navigation, Map, Landmarks, Progress,
    /// Settings) derives from. Screens build their contents once and are shown/hidden rather
    /// than destroyed, so tab switches preserve scroll position and map camera state (PLAN.md §06).</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        private CanvasGroup _canvasGroup;
        public bool IsBuilt { get; private set; }

        public void Initialize(RectTransform parent)
        {
            Root = UIFactory.CreateRect(GetType().Name, parent);
            UIFactory.StretchFill(Root);
            _canvasGroup = Root.gameObject.AddComponent<CanvasGroup>();
        }

        public void EnsureBuilt()
        {
            if (IsBuilt) return;
            Build(Root);
            IsBuilt = true;
        }

        public void Show()
        {
            EnsureBuilt();
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            OnShown();
        }

        public void Hide()
        {
            // alpha/interactable/blocksRaycasts alone were never enough — a "hidden" screen
            // stayed fully opaque and on top of whatever showed next, and its own Update() kept
            // running underneath. Confirmed on a real device: the Map screen's entire chrome
            // (top pill, right rail, route) stayed visibly rendered over the Landmarks screen
            // after switching tabs. SetActive(false) is what actually stops both the rendering
            // and the Update() loop; OnHidden() still runs first so overlay cleanup (closing any
            // open AR cards, etc.) happens while the screen is still logically live.
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;
            OnHidden();
            gameObject.SetActive(false);
        }

        /// <summary>Called once, the first time this screen becomes visible.</summary>
        protected abstract void Build(RectTransform root);

        /// <summary>Called every time this screen becomes the active tab/overlay.</summary>
        protected virtual void OnShown() { }

        /// <summary>Called every time this screen stops being the active tab/overlay.</summary>
        protected virtual void OnHidden() { }
    }
}
