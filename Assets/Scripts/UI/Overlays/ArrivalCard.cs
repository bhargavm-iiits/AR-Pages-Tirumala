using System;
using System.Collections;
using AlipiriAR.Data;
using AlipiriAR.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Frame 8 — confetti burst, green check disc, "You have reached X", "Next
    /// Landmark" button (PLAN.md §06/§10). Fires from LandmarkTriggerService.OnArrived (Scene 5),
    /// which has already marked the landmark visited by the time this shows — the button here
    /// just dismisses and lets DynamicArrowManager re-aim at whatever's next.</summary>
    public class ArrivalCard : MonoBehaviour
    {
        private CanvasGroup _group;
        private Action _onNext;
        private bool _closed;

        public static ArrivalCard Show(RectTransform parent, LandmarkData landmark, Action onNext = null)
        {
            var go = new GameObject("ArrivalCard", typeof(RectTransform));
            var card = go.AddComponent<ArrivalCard>();
            card._onNext = onNext;
            card.Build(parent, landmark);
            return card;
        }

        private void Build(RectTransform parent, LandmarkData landmark)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFill(rt);
            _group = gameObject.AddComponent<CanvasGroup>();

            var (scrim, modal) = UIFactory.Modal(rt, 640f);
            scrim.color = new Color(0f, 0f, 0f, 0.7f);

            var bg = modal.gameObject.AddComponent<Image>();
            bg.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(UITheme.RadiusCard));
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.SurfaceRaised;

            var vlg = modal.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(40, 40, 48, 40);
            vlg.spacing = UITheme.SpaceM;

            var csf = modal.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var checkRt = UIFactory.CreateRect("Check", modal);
            checkRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 140f;

            // Ripple ring — added before the disc so it sits behind it in sibling order, and
            // expands/fades outward from underneath. A radial echo reads as more ceremonial than
            // a flat pop for a landmark arrival on a pilgrimage route; the confetti below still
            // does the celebratory work.
            var rippleRt = UIFactory.CreateRect("Ripple", checkRt);
            rippleRt.anchorMin = rippleRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(rippleRt, 140f, 140f);
            var rippleImg = rippleRt.gameObject.AddComponent<Image>();
            rippleImg.sprite = UIShapes.Ring(70, 6);
            rippleImg.color = UITheme.Success;

            var checkBgRt = UIFactory.CreateRect("Disc", checkRt);
            checkBgRt.anchorMin = checkBgRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(checkBgRt, 140f, 140f);
            var checkBg = checkBgRt.gameObject.AddComponent<Image>();
            checkBg.sprite = UIShapes.Circle();
            checkBg.color = UITheme.Success;
            UIFactory.CenteredIcon(checkBgRt, IconType.Check, 64f, Color.white);

            var titleRt = UIFactory.CreateRect("Title", modal);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            LocalizedLabel.Bind(titleLabel, "overlay.arrived_title");

            var nameRt = UIFactory.CreateRect("Name", modal);
            nameRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            UIFactory.Label(nameRt, landmark.Name, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

            var spacerRt = UIFactory.CreateRect("Spacer", modal);
            spacerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.SpaceS;

            var nextRt = UIFactory.CreateRect("Next", modal);
            nextRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;
            var nextBtn = UIFactory.PrimaryButton(nextRt, string.Empty, Close);
            var nextLabel = nextBtn.GetComponentInChildren<TMP_Text>();
            LocalizedLabel.Bind(nextLabel, "overlay.next_landmark_button");

            ConfettiBurst.Play(rt);
            UITween.PopIn(modal, _group);
            StartCoroutine(RippleRoutine(rippleRt, rippleImg));
        }

        private static IEnumerator RippleRoutine(RectTransform rt, Image img)
        {
            const float duration = 0.6f;
            if (UITween.ReducedMotion)
            {
                var c0 = img.color;
                c0.a = 0f;
                img.color = c0;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                if (rt == null || img == null) yield break;
                t += Time.unscaledDeltaTime;
                float e = Mathf.Clamp01(t / duration);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 2.1f, e);
                var c = img.color;
                c.a = Mathf.Lerp(0.55f, 0f, e);
                img.color = c;
                yield return null;
            }
        }

        /// <summary>Public so both the "Next Landmark" button and ARNavigationScreen's
        /// Auto-advance timer can dismiss the card.</summary>
        public void Close()
        {
            if (_closed) return;
            _closed = true;
            UITween.FadeOut(_group, 0.2f, () =>
            {
                _onNext?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
