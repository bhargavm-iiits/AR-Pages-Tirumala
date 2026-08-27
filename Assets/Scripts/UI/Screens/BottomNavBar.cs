using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>The 5-tab bar — Navigate / Map / Landmarks / Progress / Settings (PLAN.md §05).
    /// Parented inside ResponsiveUI's SafeAreaRoot, so anchoring to its own parent's bottom
    /// edge already clears the gesture-navigation bar with no extra safe-area code needed here.</summary>
    public class BottomNavBar : MonoBehaviour
    {
        public const float Height = 150f;

        private readonly List<(Button button, Image icon, TMP_Text label)> _tabs = new();
        private int _activeIndex = -1;

        public static BottomNavBar Create(RectTransform parent, (string labelKey, IconType icon)[] tabs, Action<int> onSelected)
        {
            var rt = UIFactory.CreateRect("BottomNavBar", parent);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, Height);
            rt.anchoredPosition = Vector2.zero;

            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = UITheme.Surface;

            var nav = rt.gameObject.AddComponent<BottomNavBar>();

            var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var (labelKey, icon) = tabs[i];
                var tabRt = UIFactory.CreateRect($"Tab_{labelKey}", rt);
                var tabBtn = tabRt.gameObject.AddComponent<Button>();
                var tabBg = tabRt.gameObject.AddComponent<Image>();
                tabBg.color = new Color(0, 0, 0, 0);
                tabBtn.targetGraphic = tabBg;

                var vlg = tabRt.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.MiddleCenter;
                // NOT childForceExpandWidth = true: verified directly against Unity's installed
                // HorizontalOrVerticalLayoutGroup source (SetChildrenAlongAxis) — on the cross
                // axis, childForceExpand forces flexible >= 1 regardless of any LayoutElement
                // preferred size, which makes the group clamp to the FULL column width instead of
                // the child's preferred size. That's what was actually squashing this icon into
                // an oval — a preferredWidth alone (still set below) can't override force-expand.
                vlg.childForceExpandWidth = false;
                vlg.spacing = 4f;

                var iconRt = UIFactory.CreateRect("Icon", tabRt);
                var iconLe = iconRt.gameObject.AddComponent<LayoutElement>();
                iconLe.preferredWidth = 44f;
                iconLe.preferredHeight = 44f;
                // Routed through CenteredIcon (a nested rect inside this layout slot, same
                // pattern every other icon placement in the app uses) rather than hand-building
                // the Image directly — that also means this icon now picks up
                // UITheme.IconSizeMultiplier like everywhere else, instead of being a one-off
                // exception stuck at a fixed 44px.
                var iconImg = UIFactory.CenteredIcon(iconRt, icon, 44f, UITheme.TextSecondary);

                var labelRt = UIFactory.CreateRect("Label", tabRt);
                labelRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
                // TextSecondary (not TextTertiary) for the inactive state — TextTertiary reads
                // as too low-contrast against Ground at nav-bar size on a real device.
                var label = AlipiriAR.Localization.LocalizedLabel.Bind(
                    UIFactory.Label(labelRt, Localization.Loc.T(labelKey), UITheme.CaptionFontSize,
                        FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary),
                    labelKey);

                tabBtn.onClick.AddListener(() => onSelected(index));
                nav._tabs.Add((tabBtn, iconImg, (TMP_Text)label.GetComponent<TextMeshProUGUI>()));
            }

            return nav;
        }

        public void SetActive(int index)
        {
            if (_activeIndex == index) return;
            _activeIndex = index;

            for (int i = 0; i < _tabs.Count; i++)
            {
                var color = i == index ? UITheme.Accent : UITheme.TextSecondary;
                _tabs[i].icon.color = color;
                _tabs[i].label.color = color;
            }
        }
    }
}
