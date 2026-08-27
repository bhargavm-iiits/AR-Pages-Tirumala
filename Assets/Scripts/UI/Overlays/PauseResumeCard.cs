using System;
using AlipiriAR.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Frame 10 — "Navigation Paused", pause glyph in a ring, Resume / End Navigation
    /// (PLAN.md §06/§10). Tapping End swaps this card's own buttons for a confirm/cancel pair
    /// rather than stacking a second modal on top.</summary>
    public class PauseResumeCard : MonoBehaviour
    {
        private CanvasGroup _group;
        private Action _onResume;
        private Action _onEndConfirmed;
        private bool _closed;
        private RectTransform _bodyRt;

        public static PauseResumeCard Show(RectTransform parent, Action onResume, Action onEndConfirmed)
        {
            var go = new GameObject("PauseResumeCard", typeof(RectTransform));
            var card = go.AddComponent<PauseResumeCard>();
            card._onResume = onResume;
            card._onEndConfirmed = onEndConfirmed;
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
            scrim.color = new Color(0f, 0f, 0f, 0.75f);

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

            var titleRt = UIFactory.CreateRect("Title", modal);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            LocalizedLabel.Bind(titleLabel, "overlay.paused_title");

            var ringRt = UIFactory.CreateRect("Ring", modal);
            ringRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 160f;
            var ringSlot = UIFactory.CreateRect("Slot", ringRt);
            ringSlot.anchorMin = ringSlot.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(ringSlot, 140f, 140f);
            var ringImg = ringSlot.gameObject.AddComponent<Image>();
            ringImg.sprite = UIShapes.Ring(70, 5);
            ringImg.color = UITheme.Accent;
            UIFactory.CenteredIcon(ringSlot, IconType.Pause, 56f, UITheme.Accent);

            _bodyRt = UIFactory.CreateRect("Body", modal);
            var bodyVlg = _bodyRt.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyVlg.childForceExpandWidth = true;
            bodyVlg.childControlWidth = true;
            bodyVlg.childControlHeight = true;
            bodyVlg.spacing = UITheme.SpaceS;

            BuildDefaultButtons();

            UITween.PopIn(modal, _group);
        }

        private void BuildDefaultButtons()
        {
            ClearBody();

            var resumeRt = UIFactory.CreateRect("Resume", _bodyRt);
            resumeRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;
            var resumeBtn = UIFactory.PrimaryButton(resumeRt, string.Empty, () =>
            {
                _onResume?.Invoke();
                Close();
            });
            LocalizedLabel.Bind(resumeBtn.GetComponentInChildren<TMP_Text>(), "overlay.resume");

            var endRt = UIFactory.CreateRect("End", _bodyRt);
            endRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;
            var endBtn = UIFactory.GhostButton(endRt, string.Empty, BuildConfirmButtons);
            LocalizedLabel.Bind(endBtn.GetComponentInChildren<TMP_Text>(), "overlay.end_navigation");
        }

        private void BuildConfirmButtons()
        {
            ClearBody();

            var titleRt = UIFactory.CreateRect("ConfirmTitle", _bodyRt);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            LocalizedLabel.Bind(titleLabel, "overlay.end_navigation_confirm_title");

            var bodyRt = UIFactory.CreateRect("ConfirmBody", _bodyRt);
            bodyRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            var bodyLabel = UIFactory.Label(bodyRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            bodyLabel.enableWordWrapping = true;
            LocalizedLabel.Bind(bodyLabel, "overlay.end_navigation_confirm_body");

            var confirmRt = UIFactory.CreateRect("Confirm", _bodyRt);
            confirmRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;
            var confirmBtn = UIFactory.PrimaryButton(confirmRt, string.Empty, () =>
            {
                _onEndConfirmed?.Invoke();
                Close();
            });
            confirmBtn.GetComponent<Image>().color = UITheme.Critical;
            LocalizedLabel.Bind(confirmBtn.GetComponentInChildren<TMP_Text>(), "overlay.end_navigation");

            var cancelRt = UIFactory.CreateRect("Cancel", _bodyRt);
            cancelRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;
            var cancelBtn = UIFactory.GhostButton(cancelRt, string.Empty, BuildDefaultButtons);
            LocalizedLabel.Bind(cancelBtn.GetComponentInChildren<TMP_Text>(), "common.cancel");
        }

        private void ClearBody()
        {
            foreach (Transform child in _bodyRt) Destroy(child.gameObject);
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            UITween.FadeOut(_group, 0.2f, () => Destroy(gameObject));
        }
    }
}
