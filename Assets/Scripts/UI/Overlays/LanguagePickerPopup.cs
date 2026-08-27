using System;
using AlipiriAR.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Settings' Language row — lists each language in its own script (PLAN.md §06),
    /// applies Loc.SetLocale immediately on tap so the whole UI retranslates, then closes.</summary>
    public class LanguagePickerPopup : MonoBehaviour
    {
        private CanvasGroup _group;
        private Action _onClosed;
        private bool _closed;

        public static LanguagePickerPopup Show(RectTransform parent, Action onClosed = null)
        {
            var go = new GameObject("LanguagePickerPopup", typeof(RectTransform));
            var popup = go.AddComponent<LanguagePickerPopup>();
            popup._onClosed = onClosed;
            popup.Build(parent);
            BackStackManager.Instance.Push(popup.HandleBack);
            return popup;
        }

        private void Build(RectTransform parent)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFill(rt);
            _group = gameObject.AddComponent<CanvasGroup>();

            var (scrim, modal) = UIFactory.Modal(rt, 700f);
            scrim.GetComponent<Button>().onClick.AddListener(Close);

            var cardImg = modal.gameObject.AddComponent<Image>();
            cardImg.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(UITheme.RadiusCard));
            cardImg.type = Image.Type.Sliced;
            cardImg.color = UITheme.SurfaceRaised;

            var vlg = modal.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(16, 16, 24, 24);
            vlg.spacing = 4f;

            var csf = modal.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleRt = UIFactory.CreateRect("Title", modal);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            LocalizedLabel.Bind(titleLabel, "settings.language");

            foreach (var locale in LocalizationService.AvailableLocales)
                BuildRow(modal, locale);

            UITween.PopIn(modal, _group);
        }

        private void BuildRow(Transform parent, LocaleInfo locale)
        {
            var rowRt = UIFactory.CreateRect($"Lang_{locale.Code}", parent);
            rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;

            bool active = locale.Code == Loc.CurrentLocale;
            var bg = rowRt.gameObject.AddComponent<Image>();
            bg.color = active ? UITheme.AccentDim : new Color(0f, 0f, 0f, 0f);

            var btn = rowRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;

            var label = UIFactory.Label(rowRt, locale.NativeName, UITheme.BodyFontSize,
                active ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
                active ? UITheme.Accent : UITheme.TextPrimary);
            label.margin = new Vector4(UITheme.SpaceM, 0f, UITheme.SpaceM, 0f);

            btn.onClick.AddListener(() =>
            {
                Loc.SetLocale(locale.Code);
                Close();
            });
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
