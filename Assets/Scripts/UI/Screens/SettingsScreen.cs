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
        private TMP_Text _profileSummaryLabel;
        private TMP_Text _exportStatusLabel;
        private CanvasGroup _exportStatusGroup;

        protected override void Build(RectTransform root)
        {
            UIFactory.Panel(root, UITheme.Ground);
            var content = UIFactory.VerticalScroll(root, 0f,
                new RectOffset(0, 0, (int)UITheme.SpaceM, (int)UITheme.SpaceL));

            BuildHeader(content);
            BuildProfileCard(content);
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
            RefreshProfileSummary();
        }

        /// <summary>Redesign's profile card summary chip (Docs/Images/New UI #5: "English •
        /// Metric Units") — same two values the rows below already track, just mirrored up top.</summary>
        private void RefreshProfileSummary()
        {
            if (_profileSummaryLabel == null) return;
            _profileSummaryLabel.text = CurrentLanguageName() + "  •  " + UnitsValueText();
        }

        /// <summary>Redesign's devotee identity header (Docs/Images/New UI #5) — a static
        /// placeholder avatar/role, not backed by ProfileService: the app's actual profile fields
        /// (name/age, PLAN.md §06) surface through "Edit Profile" below, same as before this
        /// redesign; this card is a visual summary, not a second source of truth for them.</summary>
        private void BuildProfileCard(Transform parent)
        {
            var wrapRt = UIFactory.CreateRect("ProfileCardWrap", parent);
            var le = wrapRt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 172f;
            var outerVlg = wrapRt.gameObject.AddComponent<VerticalLayoutGroup>();
            outerVlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, 0, (int)UITheme.SpaceM);
            outerVlg.childControlWidth = true;
            outerVlg.childControlHeight = true;
            outerVlg.childForceExpandWidth = true;
            outerVlg.childForceExpandHeight = true;

            var cardRt = UIFactory.CreateRect("ProfileCard", wrapRt);
            var card = UIFactory.Card(cardRt, UITheme.Surface);
            UIFactory.GlowBorder(card.transform, UITheme.RadiusCard);
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OpenEditProfile);

            var hlg = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(28, 28, 24, 24);
            hlg.spacing = UITheme.SpaceM;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var avatarRt = UIFactory.CreateRect("Avatar", card.transform);
            var avatarLe = avatarRt.gameObject.AddComponent<LayoutElement>();
            avatarLe.preferredWidth = 108f;
            avatarLe.preferredHeight = 108f;
            var ringImg = avatarRt.gameObject.AddComponent<Image>();
            ringImg.sprite = UIShapes.Ring(54, 4);
            ringImg.color = UITheme.Gold;
            UIFactory.CenteredIcon(avatarRt, IconType.Profile, 48f, UITheme.TextPrimary);

            var textRt = UIFactory.CreateRect("Text", card.transform);
            textRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var vlg = textRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 2f;

            var kickerRt = UIFactory.CreateRect("Kicker", textRt);
            kickerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            var kickerLabel = UIFactory.Label(kickerRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
            LocalizedLabel.Bind(kickerLabel, "settings.profile_kicker");

            var roleRt = UIFactory.CreateRect("Role", textRt);
            roleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            var roleLabel = UIFactory.Label(roleRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Bind(roleLabel, "settings.profile_role");

            var summaryRt = UIFactory.CreateRect("Summary", textRt);
            summaryRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            _profileSummaryLabel = UIFactory.Label(summaryRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
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

        /// <summary>Redesign's grouped sections (Docs/Images/New UI #5: EXPERIENCE / NAVIGATION /
        /// ACCESSIBILITY / ABOUT) — replaces the earlier single flat row list. Row wiring/callbacks
        /// are unchanged from before this pass, just regrouped under section headers + separate
        /// cards instead of one continuous list.</summary>
        private void BuildSectionCard(Transform parent)
        {
            var settings = SettingsStore.Resolve();

            var experience = BuildGroup(parent, "settings.section_experience");
            AddSwitchRow(experience, IconType.Speaker, "settings.voice_guidance",
                settings.VoiceGuidanceEnabled, v => settings.VoiceGuidanceEnabled = v);
            AddDivider(experience);
            AddSwitchRow(experience, IconType.Haptic, "settings.haptic_feedback",
                settings.HapticFeedbackEnabled, v =>
                {
                    settings.HapticFeedbackEnabled = v;
                    if (v) HapticService.Pulse();
                });
            AddDivider(experience);
            AddSwitchRow(experience, IconType.Sun, "settings.auto_brightness",
                settings.AutoBrightnessEnabled, v => settings.AutoBrightnessEnabled = v);

            // Field-test instrument (NewPlan.md §04 Phase B) — hidden from a pilgrim by default,
            // shown here for whoever is tuning HybridLocalizationEngine on a device walk.
            if (ServiceLocator.TryGet<Diagnostics.DebugOverlay>(out var debugOverlay))
            {
                AddDivider(experience);
                AddSwitchRow(experience, IconType.Gear, "settings.debug_overlay",
                    debugOverlay.IsVisible, v => debugOverlay.SetVisible(v));
            }

            var navigation = BuildGroup(parent, "settings.section_navigation");
            var (_, mapsLabel, _) = UIFactory.SettingsRow(navigation, IconType.Map, string.Empty, null, OpenOfflineMapsInfo, rowHeight: RowHeight);
            LocalizedLabel.Bind(mapsLabel, "settings.offline_maps");

            var accessibility = BuildGroup(parent, "settings.section_accessibility");
            var (_, langLabel, langValue) = UIFactory.SettingsRow(accessibility, IconType.Globe, string.Empty, CurrentLanguageName(), OpenLanguagePicker, rowHeight: RowHeight);
            LocalizedLabel.Bind(langLabel, "settings.language");
            _languageValueLabel = langValue;
            AddDivider(accessibility);
            var (_, unitsLabel, unitsValue) = UIFactory.SettingsRow(accessibility, IconType.Ruler, string.Empty, UnitsValueText(), ToggleUnits, rowHeight: RowHeight);
            LocalizedLabel.Bind(unitsLabel, "settings.units");
            _unitsValueLabel = unitsValue;

            var about = BuildGroup(parent, "settings.section_about");
            var (_, editLabel, _) = UIFactory.SettingsRow(about, IconType.Profile, string.Empty, null, OpenEditProfile, rowHeight: RowHeight);
            LocalizedLabel.Bind(editLabel, "settings.edit_profile");
            AddDivider(about);
            var (_, exportLabel, _) = UIFactory.SettingsRow(about, IconType.Export, string.Empty, null, ExportLoginSheet, rowHeight: RowHeight);
            LocalizedLabel.Bind(exportLabel, "settings.export_login_sheet");
            AddDivider(about);
            var (_, aboutLabel, _) = UIFactory.SettingsRow(about, IconType.Info, string.Empty, null, OpenAbout, rowHeight: RowHeight);
            LocalizedLabel.Bind(aboutLabel, "settings.about");

            BuildExportStatusLabel(about.parent);
        }

        /// <summary>One section header caption + its own rounded card — the rows added inside are
        /// the returned Transform's children, laid out by its VerticalLayoutGroup exactly like the
        /// old single flat list was.</summary>
        private static Transform BuildGroup(Transform parent, string headerKey)
        {
            var outerRt = UIFactory.CreateRect($"Group_{headerKey}", parent);
            var outerVlg = outerRt.gameObject.AddComponent<VerticalLayoutGroup>();
            outerVlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, 0, (int)UITheme.SpaceS);
            outerVlg.spacing = UITheme.SpaceXS;
            outerVlg.childForceExpandWidth = true;
            outerVlg.childControlWidth = true;
            outerVlg.childControlHeight = true;

            var headerRt = UIFactory.CreateRect("Header", outerRt);
            headerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var headerLabel = UIFactory.Label(headerRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
            LocalizedLabel.Bind(headerLabel, headerKey);

            // Card() is called with outerRt itself as the parent (not an extra wrapping rect) —
            // the returned Image's own GameObject gets a VerticalLayoutGroup added directly below,
            // which is what lets it report its own computed preferred height back up to outerVlg;
            // an inert rect sitting between the two breaks that chain (it has no ILayoutElement of
            // its own to report), collapsing the card to zero height.
            var card = UIFactory.Card(outerRt, UITheme.Surface);
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(0, 0, 4, 4);

            return card.transform;
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
            RefreshProfileSummary();
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
            RefreshProfileSummary();
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
