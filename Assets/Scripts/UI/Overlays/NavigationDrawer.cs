using System;
using AlipiriAR.Localization;
using AlipiriAR.Positioning;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Hamburger side drawer (PLAN.md §06) — start/pause/resume, end navigation,
    /// language, jump to any tab. A panel sliding in from the edge rather than the tap-outside-modal
    /// pattern the rest of the app uses, since that's the standard reading of a drawer control.
    ///
    /// The Start row exists because navigation used to begin itself the moment the AR or Map tab
    /// was first opened (NavigationSession.Resolve()'s doc comment called this out directly) — on
    /// the trace-replay harness that meant the nav board visibly climbed on its own in the Editor
    /// with nobody touching anything, and on a real device it meant Start() (and the FineLocation
    /// permission prompt behind it) fired before the user had asked for navigation at all. This
    /// row is now the one and only place that ever calls NavigationSession.Start(); see
    /// Docs/Draft1.md D11 follow-up.</summary>
    public class NavigationDrawer : MonoBehaviour
    {
        private CanvasGroup _group;
        private Action _onClosed;
        private bool _closed;

        public static NavigationDrawer Show(RectTransform parent, NavigationState sessionState, Action onStart, Action onPauseResume, Action onEnd, Action<int> onJumpToTab, Action onClosed = null)
        {
            var go = new GameObject("NavigationDrawer", typeof(RectTransform));
            var drawer = go.AddComponent<NavigationDrawer>();
            drawer._onClosed = onClosed;
            drawer.Build(parent, sessionState, onStart, onPauseResume, onEnd, onJumpToTab);
            BackStackManager.Instance.Push(drawer.HandleBack);
            return drawer;
        }

        private void Build(RectTransform parent, NavigationState sessionState, Action onStart, Action onPauseResume, Action onEnd, Action<int> onJumpToTab)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFill(rt);
            _group = gameObject.AddComponent<CanvasGroup>();

            var scrimRt = UIFactory.CreateRect("Scrim", rt);
            UIFactory.StretchFill(scrimRt);
            var scrimImg = scrimRt.gameObject.AddComponent<Image>();
            scrimImg.color = new Color(0f, 0f, 0f, 0.55f);
            var scrimBtn = scrimRt.gameObject.AddComponent<Button>();
            scrimBtn.targetGraphic = scrimImg;
            scrimBtn.onClick.AddListener(Close);

            var panel = UIFactory.CreateRect("Panel", rt);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 0f);
            panel.anchoredPosition = Vector2.zero;

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = UITheme.SurfaceRaised;

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(0, 0, 90, 40);

            if (sessionState == NavigationState.Idle || sessionState == NavigationState.Ended)
                AddRow(panel, IconType.Check, "nav.drawer_start", () => { onStart?.Invoke(); Close(); });
            else
                AddRow(panel, IconType.Pause, sessionState == NavigationState.Paused ? "nav.drawer_resume" : "nav.drawer_pause", () => { onPauseResume?.Invoke(); Close(); });
            AddDivider(panel);
            AddRow(panel, IconType.Close, "overlay.end_navigation", () => { onEnd?.Invoke(); Close(); });
            AddDivider(panel);
            AddRow(panel, IconType.Globe, "settings.language", () =>
            {
                Close();
                LanguagePickerPopup.Show(parent);
            });
            AddDivider(panel);

            var headerRt = UIFactory.CreateRect("JumpHeader", panel);
            headerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            var headerLabel = UIFactory.Label(headerRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, UITheme.TextTertiary);
            headerLabel.margin = new Vector4(UITheme.SpaceM, 0f, 0f, 0f);
            LocalizedLabel.Bind(headerLabel, "nav.drawer_go_to");

            AddRow(panel, IconType.Map, "tabs.map", () => { onJumpToTab?.Invoke(1); Close(); });
            AddRow(panel, IconType.Gopuram, "tabs.landmarks", () => { onJumpToTab?.Invoke(2); Close(); });
            AddRow(panel, IconType.Check, "tabs.progress", () => { onJumpToTab?.Invoke(3); Close(); });
            AddRow(panel, IconType.Gear, "tabs.settings", () => { onJumpToTab?.Invoke(4); Close(); });

            UITween.FadeIn(_group, 0.15f);
        }

        private static void AddRow(Transform parent, IconType icon, string labelKey, Action onClick)
        {
            var (_, label, _) = UIFactory.SettingsRow(parent, icon, string.Empty, null, onClick, showChevron: false);
            LocalizedLabel.Bind(label, labelKey);
        }

        private static void AddDivider(Transform parent)
        {
            var rt = UIFactory.CreateRect("Divider", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UITheme.Rule;
        }

        private bool HandleBack()
        {
            Close();
            return true;
        }

        private void Close()
        {
            if (_closed) return;
            _closed = true;
            BackStackManager.Instance.Pop(HandleBack);
            UITween.FadeOut(_group, 0.15f, () =>
            {
                _onClosed?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
