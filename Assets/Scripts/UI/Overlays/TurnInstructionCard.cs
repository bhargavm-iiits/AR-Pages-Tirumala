using System;
using AlipiriAR.Localization;
using AlipiriAR.Positioning;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Frame 7 — "Turn Left / in 50 m", arrow glyph, "towards X", 5-dot pagination
    /// (PLAN.md §06/§10). Auto-dismisses once the maneuver is passed; ManeuverDetector supplies
    /// fresh Maneuvers as the walk continues, so ARNavigationScreen just calls Show() again with
    /// the next one rather than this card tracking route state itself.</summary>
    public class TurnInstructionCard : MonoBehaviour
    {
        private CanvasGroup _group;
        private RectTransform _modal;
        private TMP_Text _titleLabel;
        private TMP_Text _distanceLabel;
        private TMP_Text _towardsLabel;
        private Image _arrowIcon;
        private Action _onClosed;
        private bool _closed;

        public static TurnInstructionCard Show(RectTransform parent, Maneuver maneuver, Action onClosed = null)
        {
            var go = new GameObject("TurnInstructionCard", typeof(RectTransform));
            var card = go.AddComponent<TurnInstructionCard>();
            card._onClosed = onClosed;
            card.Build(parent);
            card.Refresh(maneuver);
            return card;
        }

        private void Build(RectTransform parent)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFill(rt);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false; // informational — never blocks the AR view underneath

            _modal = UIFactory.CreateRect("Card", rt);
            _modal.anchorMin = new Vector2(0.5f, 1f);
            _modal.anchorMax = new Vector2(0.5f, 1f);
            _modal.pivot = new Vector2(0.5f, 1f);
            _modal.anchoredPosition = new Vector2(0f, -180f);
            _modal.sizeDelta = new Vector2(620f, 420f);

            var bg = _modal.gameObject.AddComponent<Image>();
            bg.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(UITheme.RadiusCard));
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.SurfaceRaised;

            var vlg = _modal.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(28, 28, 24, 24);
            vlg.spacing = UITheme.SpaceXS;

            var titleRt = UIFactory.CreateRect("Title", _modal);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            _titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

            var distRt = UIFactory.CreateRect("Distance", _modal);
            distRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            _distanceLabel = UIFactory.Label(distRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);

            var arrowRt = UIFactory.CreateRect("Arrow", _modal);
            arrowRt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            _arrowIcon = UIFactory.CenteredIcon(arrowRt, IconType.Back, 120f, UITheme.Accent);

            var towardsRt = UIFactory.CreateRect("Towards", _modal);
            towardsRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
            _towardsLabel = UIFactory.Label(towardsRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            _towardsLabel.enableWordWrapping = true;

            var dotsRt = UIFactory.CreateRect("Dots", _modal);
            dotsRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            var dotsHlg = dotsRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotsHlg.childAlignment = TextAnchor.MiddleCenter;
            dotsHlg.spacing = UITheme.SpaceXS;
            for (int i = 0; i < 5; i++)
            {
                var dotRt = UIFactory.CreateRect("Dot", dotsRt);
                dotRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 14f;
                UIFactory.SetSize(dotRt, 14f, 14f);
                var dotImg = dotRt.gameObject.AddComponent<Image>();
                dotImg.sprite = UIShapes.Circle();
                dotImg.color = i == 0 ? UITheme.Accent : UITheme.Rule;
            }

            UITween.PopIn(_modal, _group);
        }

        public void Refresh(Maneuver maneuver)
        {
            string titleKey = maneuver.Type switch
            {
                ManeuverType.TurnLeft => "overlay.turn_left",
                ManeuverType.TurnRight => "overlay.turn_right",
                _ => "overlay.continue_straight",
            };
            _titleLabel.text = Loc.T(titleKey);
            _distanceLabel.text = Loc.T("overlay.in_distance_format", Utilities.DistanceFormatter.FormatMeters(maneuver.DistanceMeters));
            _towardsLabel.text = string.IsNullOrEmpty(maneuver.TowardsName) ? string.Empty : Loc.T("overlay.towards_format", maneuver.TowardsName);

            _arrowIcon.transform.localRotation = maneuver.Type switch
            {
                ManeuverType.TurnLeft => Quaternion.identity,
                ManeuverType.TurnRight => Quaternion.Euler(0f, 0f, 180f),
                _ => Quaternion.Euler(0f, 0f, 90f),
            };
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            UITween.FadeOut(_group, 0.2f, () =>
            {
                _onClosed?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
