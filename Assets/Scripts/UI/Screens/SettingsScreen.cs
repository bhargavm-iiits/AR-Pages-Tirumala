using System.Collections;
using System.Collections.Generic;
using AlipiriAR.Core;
using AlipiriAR.Data;
using AlipiriAR.Database;
using AlipiriAR.Localization;
using AlipiriAR.Profile;
using AlipiriAR.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Frame 5 — every row from PLAN.md §06, plus Edit Profile / Export Login Sheet
    /// which the mockup crop doesn't show but the spec requires given the login+Excel feature.</summary>
    public class SettingsScreen : UIScreen
    {
        // Docs/UIplan.md §03 Phase 2 — rows, the back button and the switches all read a step
        // larger in the mockup than UITheme.MinTouchTarget's bare 48dp minimum; scoped to this
        // screen only rather than raising the shared constant, which other screens size against.
        private const float RowHeight = 104f;
        private const float BackButtonSize = 88f;

        private TMP_Text _languageValueLabel;
        private TMP_Text _unitsValueLabel;
        private TMP_Text _exportStatusLabel;
        private CanvasGroup _exportStatusGroup;

        protected override void Build(RectTransform root)
        {
            UIFactory.Panel(root, UITheme.Ground);
            var content = UIFactory.VerticalScroll(root, 0f,
                new RectOffset(0, 0, (int)UITheme.SpaceM, (int)UITheme.SpaceL));

            BuildHeader(content);
            BuildSectionCard(content);

            SettingsStore.Resolve().OnUnitsChanged += RefreshUnitsValueLabel;
            Loc.OnLocaleChanged += RefreshLanguageValueLabel;
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<SettingsStore>(out var settings))
                settings.OnUnitsChanged -= RefreshUnitsValueLabel;
            Loc.OnLocaleChanged -= RefreshLanguageValueLabel;
        }

        protected override void OnShown()
        {
            RefreshLanguageValueLabel();
            RefreshUnitsValueLabel();
        }

        private void BuildHeader(Transform parent)
        {
            var headerRt = UIFactory.CreateRect("Header", parent);
            headerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 108f;
            var hlg = headerRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset((int)UITheme.SpaceS, (int)UITheme.SpaceM, 0, 0);
            hlg.spacing = UITheme.SpaceS;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            var backRt = UIFactory.CreateRect("Back", headerRt);
            backRt.gameObject.AddComponent<LayoutElement>().preferredWidth = BackButtonSize;
            UIFactory.CircleShadow(backRt, BackButtonSize);
            var backBtn = UIFactory.CircleButton(backRt, BackButtonSize, () => ServiceLocator.Get<UIRoot>().SelectDefaultTab(), new Color(1f, 1f, 1f, 0.06f));
            UIFactory.CenteredIcon(backBtn.transform, IconType.Back, 36f);

            var titleRt = UIFactory.CreateRect("Title", headerRt);
            titleRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Bind(titleLabel, "settings.title");
        }

        private void BuildSectionCard(Transform parent)
        {
            var wrapRt = UIFactory.CreateRect("Wrap", parent);
            var vlg = wrapRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, 0, 0);

            var settings = SettingsStore.Resolve();

            AddSwitchRow(wrapRt, IconType.Speaker, "settings.voice_guidance",
                settings.VoiceGuidanceEnabled, v => settings.VoiceGuidanceEnabled = v);
            AddDivider(wrapRt);

            var (_, langLabel, langValue) = UIFactory.SettingsRow(wrapRt, IconType.Globe, string.Empty, CurrentLanguageName(), OpenLanguagePicker, rowHeight: RowHeight);
            LocalizedLabel.Bind(langLabel, "settings.language");
            _languageValueLabel = langValue;
            AddDivider(wrapRt);

            var (_, unitsLabel, unitsValue) = UIFactory.SettingsRow(wrapRt, IconType.Ruler, string.Empty, UnitsValueText(), ToggleUnits, rowHeight: RowHeight);
            LocalizedLabel.Bind(unitsLabel, "settings.units");
            _unitsValueLabel = unitsValue;
            AddDivider(wrapRt);

            AddSwitchRow(wrapRt, IconType.Sun, "settings.auto_brightness",
                settings.AutoBrightnessEnabled, v => settings.AutoBrightnessEnabled = v);
            AddDivider(wrapRt);

            AddSwitchRow(wrapRt, IconType.Haptic, "settings.haptic_feedback",
                settings.HapticFeedbackEnabled, v =>
                {
                    settings.HapticFeedbackEnabled = v;
                    if (v) HapticService.Pulse();
                });
            AddDivider(wrapRt);

            // Field-test instrument (NewPlan.md §04 Phase B) — hidden from a pilgrim by default,
            // shown here for whoever is tuning HybridLocalizationEngine on a device walk.
            if (ServiceLocator.TryGet<Diagnostics.DebugOverlay>(out var debugOverlay))
            {
                AddSwitchRow(wrapRt, IconType.Gear, "settings.debug_overlay",
                    debugOverlay.IsVisible, v => debugOverlay.SetVisible(v));
                AddDivider(wrapRt);
            }


            var (_, mapsLabel, _) = UIFactory.SettingsRow(wrapRt, IconType.Map, string.Empty, null, OpenOfflineMapsInfo, rowHeight: RowHeight);
            LocalizedLabel.Bind(mapsLabel, "settings.offline_maps");
            AddDivider(wrapRt);

            var (_, editLabel, _) = UIFactory.SettingsRow(wrapRt, IconType.Profile, string.Empty, null, OpenEditProfile, rowHeight: RowHeight);
            LocalizedLabel.Bind(editLabel, "settings.edit_profile");
            AddDivider(wrapRt);

            var (_, exportLabel, _) = UIFactory.SettingsRow(wrapRt, IconType.Export, string.Empty, null, ExportLoginSheet, rowHeight: RowHeight);
            LocalizedLabel.Bind(exportLabel, "settings.export_login_sheet");
            AddDivider(wrapRt);

            var (_, aboutLabel, _) = UIFactory.SettingsRow(wrapRt, IconType.Info, string.Empty, null, OpenAbout, rowHeight: RowHeight);
            LocalizedLabel.Bind(aboutLabel, "settings.about");

            BuildExportStatusLabel(wrapRt);
        }

        private void AddSwitchRow(Transform parent, IconType icon, string labelKey, bool initial, System.Action<bool> onChanged)
        {
            var (row, label, _) = UIFactory.SettingsRow(parent, icon, string.Empty, null, null, showChevron: false, rowHeight: RowHeight);
            LocalizedLabel.Bind(label, labelKey);

            var switchSlotRt = UIFactory.CreateRect("SwitchSlot", row);
            var le = switchSlotRt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 108f;
            le.preferredHeight = 60f;
            UIFactory.Switch(switchSlotRt, initial, onChanged);
        }

        private static void AddDivider(Transform parent)
        {
            var rt = UIFactory.CreateRect("Divider", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UITheme.Rule;
        }

        // ---------------------------------------------------------------
        // Language
        // ---------------------------------------------------------------

        private static string CurrentLanguageName()
        {
            foreach (var locale in LocalizationService.AvailableLocales)
                if (locale.Code == Loc.CurrentLocale) return locale.NativeName;
            return Loc.CurrentLocale;
        }

        private void RefreshLanguageValueLabel()
        {
            if (_languageValueLabel != null) _languageValueLabel.text = CurrentLanguageName();
        }

        private void OpenLanguagePicker()
        {
            LanguagePickerPopup.Show(ServiceLocator.Get<UIRoot>().OverlayContainer);
        }

        // ---------------------------------------------------------------
        // Units
        // ---------------------------------------------------------------

        private static string UnitsValueText() =>
            Loc.T(SettingsStore.Resolve().UnitsMetric ? "settings.units_metric" : "settings.units_imperial");

        private void RefreshUnitsValueLabel()
        {
            if (_unitsValueLabel != null) _unitsValueLabel.text = UnitsValueText();
        }

        private void ToggleUnits()
        {
            var settings = SettingsStore.Resolve();
            settings.UnitsMetric = !settings.UnitsMetric;
            RefreshUnitsValueLabel();
        }

        // ---------------------------------------------------------------
        // Offline maps / About
        // ---------------------------------------------------------------

        private void OpenOfflineMapsInfo()
        {
            var db = ServiceLocator.Get<JsonDatabase>();
            double bridged = db.Route.BridgedDistanceMeters;
            string bridgeText = bridged > 0.5
                ? Loc.T("settings.offline_maps_gap_bridged_format", DistanceFormatter.FormatMeters(bridged))
                : Loc.T("settings.offline_maps_gap_surveyed");

            var rows = new List<(string, string)>
            {
                (Loc.T("settings.offline_maps_basemap_label"), Loc.T("settings.offline_maps_demo")),
                (Loc.T("settings.offline_maps_route_source_label"), Loc.T("settings.offline_maps_route_source_value")),
                (Loc.T("settings.offline_maps_gap_label"), bridgeText),
            };
            InfoModal.Show(ServiceLocator.Get<UIRoot>().OverlayContainer, "settings.offline_maps", rows);
        }

        private void OpenAbout()
        {
            var rows = new List<(string, string)>
            {
                (Loc.T("settings.about_version_label"), Application.version),
                (Loc.T("settings.about_route_data_label"), Loc.T("settings.about_route_data_value")),
                (Loc.T("settings.about_map_data_label"), Loc.T("map.attribution")),
            };
            InfoModal.Show(ServiceLocator.Get<UIRoot>().OverlayContainer, "settings.about", rows);
        }

        // ---------------------------------------------------------------
        // Edit profile / export
        // ---------------------------------------------------------------

        private void OpenEditProfile() => ServiceLocator.Get<UIRoot>().ShowEditProfileOverlay();

        private void BuildExportStatusLabel(Transform parent)
        {
            var rt = UIFactory.CreateRect("ExportStatus", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            _exportStatusLabel = UIFactory.Label(rt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextTertiary);
            _exportStatusGroup = rt.gameObject.AddComponent<CanvasGroup>();
            _exportStatusGroup.alpha = 0f;
        }

        private void ExportLoginSheet() => StartCoroutine(ExportLoginSheetRoutine());

        private IEnumerator ExportLoginSheetRoutine()
        {
            var store = new ExcelLoginStore();
            yield return store.LoadExistingLog();
            bool ok = store.RegenerateExport();

            _exportStatusLabel.text = ok
                ? Loc.T("settings.export_saved_format", store.XlsxPath)
                : Loc.T("settings.export_failed");
            UITween.FadeIn(_exportStatusGroup, 0.15f, () =>
                StartCoroutine(HideStatusAfterDelay()));

            if (ok) ShareUtility.ShareFile(store.XlsxPath);
        }

        private IEnumerator HideStatusAfterDelay()
        {
            yield return new WaitForSeconds(4f);
            UITween.FadeOut(_exportStatusGroup, 0.3f);
        }
    }
}
