using System;
using AlipiriAR.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Frame 9 — amber warning triangle, "GPS Signal Weak" (PLAN.md §06/§10). Shown
    /// when LocationProvider.OnAccuracyLevelChanged reports Poor; ARNavigationScreen suppresses
    /// re-showing for 60 s after OK is tapped ("LocationProvider watchdog", PLAN.md §06).</summary>
    public class LowGpsWarning : MonoBehaviour
    {
        private CanvasGroup _group;
        private Action _onDismissed;
        private bool _closed;

        public static LowGpsWarning Show(RectTransform parent, Action onDismissed = null)
        {
            var go = new GameObject("LowGpsWarning", typeof(RectTransform));
            var card = go.AddComponent<LowGpsWarning>();
            card._onDismissed = onDismissed;
            card.Build(parent);
            return card;
        }

        private void Build(RectTransform parent)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFill(rt);
            _group = gameObject.AddComponent<CanvasGroup>();

            var (scrim, modal) = UIFactory.Modal(rt, 620f);
            scrim.color = new Color(0f, 0f, 0f, 0.7f);

            var bg = modal.gameObject.AddComponent<Image>();
            bg.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(UITheme.RadiusCard));
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.SurfaceRaised;

            var vlg = modal.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(40, 40, 40, 40);
            vlg.spacing = UITheme.SpaceM;

            var csf = modal.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconRt = UIFactory.CreateRect("Icon", modal);
            // 96 * UITheme.IconSizeMultiplier (1.5) = 144 — this row has to be tall enough to hold
            // that or the icon overflows into the title below it (raising the multiplier for
            // Docs/UIplan.md §03's icon-size follow-up exposed this one specifically).
            iconRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 152f;
            UIFactory.CenteredIcon(iconRt, IconType.Warning, 96f, UITheme.Warning);

            var titleRt = UIFactory.CreateRect("Title", modal);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.Center, UITheme.Warning);
            LocalizedLabel.Bind(titleLabel, "overlay.low_gps_title");

            var bodyRt = UIFactory.CreateRect("Body", modal);
            bodyRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
            var bodyLabel = UIFactory.Label(bodyRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            bodyLabel.enableWordWrapping = true;
            LocalizedLabel.Bind(bodyLabel, "overlay.low_gps_body");

            var okRt = UIFactory.CreateRect("OK", modal);
            okRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;
            var okBtn = UIFactory.PrimaryButton(okRt, string.Empty, Close);
            okBtn.GetComponent<Image>().color = UITheme.Warning;
            var okLabel = okBtn.GetComponentInChildren<TMP_Text>();
            okLabel.color = UITheme.Ground;
            LocalizedLabel.Bind(okLabel, "common.ok");

            UITween.PopIn(modal, _group);
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            UITween.FadeOut(_group, 0.2f, () =>
            {
                _onDismissed?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
