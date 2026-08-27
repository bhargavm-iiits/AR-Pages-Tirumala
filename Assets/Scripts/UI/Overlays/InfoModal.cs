using System;
using System.Collections.Generic;
using AlipiriAR.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Generic read-only info sheet — Settings' Offline Maps and About rows are both
    /// "a title plus a few label/value lines and a close button" (PLAN.md §06), so they share
    /// this instead of two near-identical modal implementations.</summary>
    public class InfoModal : MonoBehaviour
    {
        private CanvasGroup _group;
        private Action _onClosed;
        private bool _closed;

        public static InfoModal Show(RectTransform parent, string titleKey, IReadOnlyList<(string label, string value)> rows, Action onClosed = null)
        {
            var go = new GameObject("InfoModal", typeof(RectTransform));
            var modal = go.AddComponent<InfoModal>();
            modal._onClosed = onClosed;
            modal.Build(parent, titleKey, rows);
            BackStackManager.Instance.Push(modal.HandleBack);
            return modal;
        }

        private void Build(RectTransform parent, string titleKey, IReadOnlyList<(string label, string value)> rows)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            UIFactory.StretchFill(rt);
            _group = gameObject.AddComponent<CanvasGroup>();

            var (scrim, modalRt) = UIFactory.Modal(rt, 780f);
            scrim.GetComponent<Button>().onClick.AddListener(Close);

            var cardImg = modalRt.gameObject.AddComponent<Image>();
            cardImg.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(UITheme.RadiusCard));
            cardImg.type = Image.Type.Sliced;
            cardImg.color = UITheme.SurfaceRaised;

            var vlg = modalRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(40, 40, 32, 32);
            vlg.spacing = UITheme.SpaceS;

            var csf = modalRt.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleRt = UIFactory.CreateRect("Title", modalRt);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            LocalizedLabel.Bind(titleLabel, titleKey);

            foreach (var (label, value) in rows)
            {
                var rowRt = UIFactory.CreateRect("Row", modalRt);
                rowRt.gameObject.AddComponent<LayoutElement>().minHeight = 48f;
                var hlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.spacing = UITheme.SpaceS;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;

                var labelRt = UIFactory.CreateRect("Label", rowRt);
                labelRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                UIFactory.Label(labelRt, label, UITheme.LabelFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);

                var valueRt = UIFactory.CreateRect("Value", rowRt);
                valueRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 360f;
                var valueLabel = UIFactory.Label(valueRt, value, UITheme.LabelFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineRight, UITheme.TextPrimary);
                valueLabel.enableWordWrapping = true;
            }

            var spacerRt = UIFactory.CreateRect("Spacer", modalRt);
            spacerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.SpaceM;

            var closeRt = UIFactory.CreateRect("Close", modalRt);
            closeRt.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.MinTouchTarget;
            var closeBtn = UIFactory.PrimaryButton(closeRt, string.Empty, Close);
            var closeLabel = closeBtn.GetComponentInChildren<TMP_Text>();
            LocalizedLabel.Bind(closeLabel, "common.close");

            UITween.PopIn(modalRt, _group);
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
