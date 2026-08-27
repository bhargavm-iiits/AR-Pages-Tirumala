using System;
using AlipiriAR.Audio;
using AlipiriAR.Data;
using AlipiriAR.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Frame 6 — tap a landmark card to see this. Listen drives the real clip → TTS → caption
    /// chain (Scene 7's VoiceNavigationManager) — button state follows actual playback start/
    /// finish events rather than a blind toggle (PLAN.md §06 — "Shows playing/stop state").
    /// </summary>
    public class LandmarkPopup : MonoBehaviour
    {
        private CanvasGroup _group;
        private RectTransform _modal;
        private Action _onClosed;
        private bool _closed;

        private bool _listening;
        private Image _listenIcon;
        private TMP_Text _listenLabel;
        private VoiceNavigationManager _voice;
        private string _voiceText;

        public static LandmarkPopup Show(RectTransform parent, LandmarkData landmark, Action onClosed = null)
        {
            var go = new GameObject("LandmarkPopup", typeof(RectTransform));
            var popup = go.AddComponent<LandmarkPopup>();
            popup._onClosed = onClosed;
            popup.Build(parent, landmark);
            BackStackManager.Instance.Push(popup.HandleBack);
            return popup;
        }

        private void Build(RectTransform parent, LandmarkData landmark)
        {
            _voiceText = landmark.VoiceText;
            _voice = VoiceNavigationManager.Resolve();
            _voice.OnPlaybackStarted += OnPlaybackStarted;
            _voice.OnPlaybackFinished += OnPlaybackFinished;

            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFill(rt);
            _group = gameObject.AddComponent<CanvasGroup>();

            var (scrim, modal) = UIFactory.Modal(rt, 860f);
            _modal = modal;
            scrim.GetComponent<Button>().onClick.AddListener(Close);

            var cardImg = modal.gameObject.AddComponent<Image>();
            cardImg.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(UITheme.RadiusCard));
            cardImg.type = Image.Type.Sliced;
            cardImg.color = UITheme.SurfaceRaised;

            var vlg = modal.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var csf = modal.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHero(modal, landmark);
            BuildBody(modal, landmark);

            UITween.PopIn(modal, _group);
        }

        private void BuildHero(Transform parent, LandmarkData landmark)
        {
            var heroRt = UIFactory.CreateRect("Hero", parent);
            heroRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 420f;

            var bg = heroRt.gameObject.AddComponent<Image>();
            bg.color = Color.Lerp(LandmarkVisuals.TintFor(landmark.Type), UITheme.Ground, 0.65f);

            UIFactory.CenteredIcon(heroRt, LandmarkVisuals.IconFor(landmark.Type), 200f, new Color(1f, 1f, 1f, 0.85f));

            var closeBtn = UIFactory.CircleButton(heroRt, 72f, Close, new Color(0f, 0f, 0f, 0.45f));
            var closeRt = (RectTransform)closeBtn.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-48f, -48f);
            UIFactory.CenteredIcon(closeBtn.transform, IconType.Close, 32f, Color.white);
        }

        private void BuildBody(Transform parent, LandmarkData landmark)
        {
            var bodyRt = UIFactory.CreateRect("Body", parent);
            var bodyVlg = bodyRt.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyVlg.childForceExpandWidth = true;
            bodyVlg.childControlWidth = true;
            bodyVlg.childControlHeight = true;
            bodyVlg.spacing = UITheme.SpaceS;
            bodyVlg.padding = new RectOffset(48, 48, 32, 40);

            var nameRt = UIFactory.CreateRect("Name", bodyRt);
            nameRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            var nameLabel = UIFactory.Label(nameRt, landmark.Name, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 26f;
            nameLabel.fontSizeMax = UITheme.TitleFontSize;

            var subtitleRt = UIFactory.CreateRect("Subtitle", bodyRt);
            subtitleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            UIFactory.Label(subtitleRt, Loc.T(landmark.Type.LocKey()), UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);

            var descRt = UIFactory.CreateRect("Description", bodyRt);
            descRt.gameObject.AddComponent<LayoutElement>().minHeight = 60f;
            var descLabel = UIFactory.Label(descRt, landmark.Description, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            descLabel.enableWordWrapping = true;

            var spacerRt = UIFactory.CreateRect("Spacer", bodyRt);
            spacerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.SpaceM;

            var listenRt = UIFactory.CreateRect("Listen", bodyRt);
            listenRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;
            BuildListenButton(listenRt);
        }

        private void BuildListenButton(Transform parent)
        {
            var pillImg = UIFactory.Pill(parent, UITheme.Accent);
            pillImg.gameObject.name = "ListenButton";
            var btn = pillImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = pillImg;

            var contentRt = UIFactory.CreateRect("Content", pillImg.transform);
            // Same bug class as the rails (see ARNavigationScreen.BuildLeftRail's comment): a
            // freshly created rect defaults to a zero-size corner anchor, so without stretching
            // it to fill the pill first, the HorizontalLayoutGroup below has no real space to
            // lay the icon+label out in.
            UIFactory.StretchFill(contentRt);
            var hlg = contentRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = UITheme.SpaceS;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            // The StretchFill above only sizes the *container* — without also controlling each
            // child's width, the icon/label's own LayoutElement.preferredWidth below is never
            // actually applied to their RectTransforms (Unity leaves them at CreateRect's
            // zero-size default), so the label's inner text StretchFills into ~0 px. Confirmed
            // on-device: "Listen" wrapped into "List"/"en" because of exactly this. Every other
            // icon+label HorizontalLayoutGroup in this codebase sets both of these; this one had
            // been missed.
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var iconRt = UIFactory.CreateRect("Icon", contentRt);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 40f;
            _listenIcon = UIFactory.CenteredIcon(iconRt, IconType.Speaker, 36f, Color.white);

            var labelRt = UIFactory.CreateRect("Label", contentRt);
            labelRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;
            _listenLabel = UIFactory.Label(labelRt, Loc.T("common.listen"), UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            btn.onClick.AddListener(ToggleListen);
        }

        private void ToggleListen()
        {
            if (_listening) _voice.Stop();
            else _voice.Speak(_voiceText);
        }

        private void OnPlaybackStarted()
        {
            _listening = true;
            RefreshListenVisual();
        }

        private void OnPlaybackFinished()
        {
            _listening = false;
            RefreshListenVisual();
        }

        private void RefreshListenVisual()
        {
            _listenIcon.sprite = IconGraphic.Get(_listening ? IconType.SpeakerMuted : IconType.Speaker);
            _listenLabel.text = Loc.T(_listening ? "common.stop" : "common.listen");
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
            if (_listening) _voice.Stop();
            _voice.OnPlaybackStarted -= OnPlaybackStarted;
            _voice.OnPlaybackFinished -= OnPlaybackFinished;
            BackStackManager.Instance.Pop(HandleBack);
            UITween.FadeOut(_group, 0.15f, () =>
            {
                _onClosed?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
