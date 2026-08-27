using System;
using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.Localization;
using AlipiriAR.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Screen 0 — First-launch profile onboarding screen.
    /// Redesigned with rich glassmorphism, dynamic bilingual language chips,
    /// animated step inputs, and high-contrast typography hierarchy.
    /// </summary>
    public class LoginScreen : UIScreen
    {
        public event Action OnCompleted;

        private TMP_InputField _nameField;
        private TMP_InputField _ageField;
        private TMP_Text _ageDisplayLabel;
        private TMP_Text _errorLabel;
        private RectTransform _errorBoxRt;
        private Button _continueButton;
        private string _selectedLocale = "en";

        private readonly Dictionary<string, Image> _chipBackgrounds = new();
        private readonly Dictionary<string, TMP_Text> _chipLabels = new();
        private readonly Dictionary<string, Image> _chipCheckIcons = new();

        private ExcelLoginStore _excelStore;
        private bool _submitting;
        private UserProfile? _prefill;

        public void Prefill(UserProfile profile) => _prefill = profile;

        protected override void Build(RectTransform root)
        {
            // 1. Deep Space Ground Background with subtle ambient gradient
            var bg = UIFactory.Panel(root, UITheme.FromHex("#070E18"));
            bg.gameObject.name = "LoginBackground";

            // Ambient Gold Top Glow
            var glowRt = UIFactory.CreateRect("TopGlow", bg.transform);
            glowRt.anchorMin = new Vector2(0.5f, 1f);
            glowRt.anchorMax = new Vector2(0.5f, 1f);
            glowRt.pivot = new Vector2(0.5f, 1f);
            UIFactory.SetSize(glowRt, 700f, 350f);
            var glowImg = glowRt.gameObject.AddComponent<Image>();
            glowImg.sprite = UIShapes.Circle();
            glowImg.color = new Color(0.95f, 0.75f, 0.2f, 0.08f);

            // 2. Main Center Card Window
            var scrollArea = UIFactory.CreateRect("Content", root);
            scrollArea.anchorMin = new Vector2(0.5f, 0.5f);
            scrollArea.anchorMax = new Vector2(0.5f, 0.5f);
            scrollArea.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(scrollArea, 920f, 1580f);

            // Glassmorphic Outer Border & Surface
            var cardBg = UIFactory.Panel(scrollArea, UITheme.FromHex("#0F1A28"), 36f);
            cardBg.gameObject.name = "LoginGlassCard";

            var vlg = scrollArea.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.spacing = UITheme.SpaceL;
            vlg.padding = new RectOffset(48, 48, 54, 54);
            vlg.childAlignment = TextAnchor.UpperCenter;

            // Build UI Sections
            BuildHeader(scrollArea);
            BuildNameSection(scrollArea);
            BuildAgeSection(scrollArea);
            BuildLanguageSection(scrollArea);
            BuildErrorBox(scrollArea);
            BuildContinueButton(scrollArea);

            _selectedLocale = Loc.CurrentLocale;
            if (_prefill.HasValue)
            {
                _nameField.text = _prefill.Value.Name;
                _ageField.text = _prefill.Value.Age.ToString();
                if (_ageDisplayLabel != null) _ageDisplayLabel.text = _prefill.Value.Age.ToString();
                _selectedLocale = _prefill.Value.LocaleCode;
            }
            RefreshChipVisuals();

            _excelStore = new ExcelLoginStore();
            StartCoroutine(_excelStore.LoadExistingLog());
        }

        protected override void OnShown()
        {
            base.OnShown();
            if (Root != null)
            {
                var cg = Root.GetComponent<CanvasGroup>();
                if (cg != null) UITween.PopIn(Root, cg, 0.25f);
            }
        }

        private void BuildHeader(Transform parent)
        {
            var headerGroup = UIFactory.CreateRect("HeaderGroup", parent);
            headerGroup.gameObject.AddComponent<LayoutElement>().preferredHeight = 240f;
            var hlg = headerGroup.gameObject.AddComponent<VerticalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = UITheme.SpaceS;
            hlg.childForceExpandWidth = true;

            // Glowing Emblem Container
            var iconCircle = UIFactory.CreateRect("EmblemCircle", headerGroup);
            UIFactory.SetSize(iconCircle, 110f, 110f);
            iconCircle.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;

            var outerRing = iconCircle.gameObject.AddComponent<Image>();
            outerRing.sprite = UIShapes.Circle();
            outerRing.color = UITheme.FromHex("#1B2A3D");

            var iconInner = UIFactory.CreateRect("EmblemInner", iconCircle);
            iconInner.anchorMin = iconInner.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(iconInner, 92f, 92f);
            var innerImg = iconInner.gameObject.AddComponent<Image>();
            innerImg.sprite = UIShapes.Circle();
            innerImg.color = UITheme.FromHex("#121D2B");

            UIFactory.CenteredIcon(iconInner, IconType.Gopuram, 54f, UITheme.Gold);

            // App Title
            var titleRt = UIFactory.CreateRect("Title", headerGroup);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            var titleText = UIFactory.Label(titleRt, string.Empty, 42f, FontStyles.Bold, TextAlignmentOptions.Center, UITheme.TextPrimary);
            LocalizedLabel.Bind(titleText, "login.title");

            // Subtitle
            var subtitleRt = UIFactory.CreateRect("Subtitle", headerGroup);
            subtitleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            var subtitleText = UIFactory.Label(subtitleRt, string.Empty, 26f, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            LocalizedLabel.Bind(subtitleText, "login.subtitle");
        }

        private void BuildNameSection(Transform parent)
        {
            var sectionRt = UIFactory.CreateRect("NameSection", parent);
            var vlg = sectionRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = UITheme.SpaceXS;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;

            // Section Label with Profile Icon
            var labelHeader = UIFactory.CreateRect("LabelHeader", sectionRt);
            labelHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            var hlg = labelHeader.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = UITheme.SpaceS;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;

            var iconRt = UIFactory.CreateRect("Icon", labelHeader);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 32f;
            UIFactory.CenteredIcon(iconRt, IconType.Profile, 26f, UITheme.Accent);

            var labelRt = UIFactory.CreateRect("Text", labelHeader);
            labelRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var labelText = UIFactory.Label(labelRt, string.Empty, 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
            LocalizedLabel.Bind(labelText, "login.name_label");

            // Input Field Box
            var fieldRt = UIFactory.CreateRect("NameFieldBox", sectionRt);
            fieldRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;

            _nameField = UIFactory.InputField(fieldRt, string.Empty);
            _nameField.characterLimit = 40;
            _nameField.onValueChanged.AddListener(_ => ClearError());

            // Bind placeholder label for live localized translation
            if (_nameField.placeholder is TMP_Text placeholderTmp)
            {
                LocalizedLabel.Bind(placeholderTmp, "login.name_placeholder");
            }
        }

        private void BuildAgeSection(Transform parent)
        {
            var sectionRt = UIFactory.CreateRect("AgeSection", parent);
            var vlg = sectionRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = UITheme.SpaceXS;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;

            // Section Label with Ruler Icon
            var labelHeader = UIFactory.CreateRect("LabelHeader", sectionRt);
            labelHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            var hlg = labelHeader.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = UITheme.SpaceS;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;

            var iconRt = UIFactory.CreateRect("Icon", labelHeader);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 32f;
            UIFactory.CenteredIcon(iconRt, IconType.Ruler, 26f, UITheme.Accent);

            var labelRt = UIFactory.CreateRect("Text", labelHeader);
            labelRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var labelText = UIFactory.Label(labelRt, string.Empty, 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
            LocalizedLabel.Bind(labelText, "login.age_label");

            // Age Stepper Control Row
            var rowRt = UIFactory.CreateRect("AgeRow", sectionRt);
            rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;

            var rowHlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowHlg.spacing = UITheme.SpaceM;
            rowHlg.childAlignment = TextAnchor.MiddleCenter;
            rowHlg.childForceExpandHeight = true;

            // Decrement Button (-)
            var minusRt = UIFactory.CreateRect("MinusContainer", rowRt);
            minusRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 96f;
            var minusBtn = UIFactory.CircleButton(minusRt, 90f, () => StepAge(-1), UITheme.FromHex("#162436"));
            UIFactory.CenteredIcon(minusBtn.transform, IconType.Minus, 36f, UITheme.TextPrimary);

            // Central Age Display Card
            var ageDisplayCard = UIFactory.CreateRect("AgeCard", rowRt);
            ageDisplayCard.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var cardImg = UIFactory.Panel(ageDisplayCard, UITheme.FromHex("#152233"), 20f);
            cardImg.gameObject.name = "AgeCardBg";

            var cardVlg = ageDisplayCard.gameObject.AddComponent<VerticalLayoutGroup>();
            cardVlg.childAlignment = TextAnchor.MiddleCenter;
            cardVlg.spacing = 0f;

            var numRt = UIFactory.CreateRect("NumLabel", ageDisplayCard);
            numRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            _ageDisplayLabel = UIFactory.Label(numRt, "25", 42f, FontStyles.Bold, TextAlignmentOptions.Center, UITheme.TextPrimary);

            // Hidden backing input field for form validation logic compatibility
            var hiddenFieldRt = UIFactory.CreateRect("HiddenAgeInput", sectionRt);
            hiddenFieldRt.gameObject.SetActive(false);
            _ageField = hiddenFieldRt.gameObject.AddComponent<TMP_InputField>();
            _ageField.text = "25";
            _ageField.onValueChanged.AddListener(_ => ClearError());
            _ageField.characterLimit = 3;

            // Increment Button (+)
            var plusRt = UIFactory.CreateRect("PlusContainer", rowRt);
            plusRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 96f;
            var plusBtn = UIFactory.CircleButton(plusRt, 90f, () => StepAge(1), UITheme.FromHex("#162436"));
            UIFactory.CenteredIcon(plusBtn.transform, IconType.Plus, 36f, UITheme.TextPrimary);
        }

        private void StepAge(int delta)
        {
            int.TryParse(_ageField.text, out int current);
            if (current == 0 && delta > 0) current = 24; // default start step
            current = Mathf.Clamp(current + delta, 1, 120);

            string ageStr = current.ToString();
            _ageField.text = ageStr;
            if (_ageDisplayLabel != null) _ageDisplayLabel.text = ageStr;
            ClearError();
        }

        private void BuildLanguageSection(Transform parent)
        {
            var sectionRt = UIFactory.CreateRect("LangSection", parent);
            var vlg = sectionRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = UITheme.SpaceXS;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;

            // Section Label with Globe Icon
            var labelHeader = UIFactory.CreateRect("LabelHeader", sectionRt);
            labelHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            var hlg = labelHeader.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = UITheme.SpaceS;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;

            var iconRt = UIFactory.CreateRect("Icon", labelHeader);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 32f;
            UIFactory.CenteredIcon(iconRt, IconType.Globe, 26f, UITheme.Accent);

            var labelRt = UIFactory.CreateRect("Text", labelHeader);
            labelRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var labelText = UIFactory.Label(labelRt, string.Empty, 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
            LocalizedLabel.Bind(labelText, "login.language_label");

            // Language Grid
            var gridRt = UIFactory.CreateRect("LangGrid", sectionRt);
            gridRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 220f;

            var flow = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            flow.cellSize = new Vector2(390f, 90f);
            flow.spacing = new Vector2(20f, 16f);
            flow.childAlignment = TextAnchor.MiddleCenter;

            // Display language names clearly with English + Native script pair
            var customDisplayNames = new Dictionary<string, string>
            {
                { "en", "English" },
                { "te", "Telugu • తెలుగు" },
                { "hi", "Hindi • हिन्दी" },
                { "ta", "Tamil • தமிழ்" },
                { "kn", "Kannada • ಕನ್ನಡ" }
            };

            foreach (var locale in LocalizationService.AvailableLocales)
            {
                string code = locale.Code;
                string title = customDisplayNames.TryGetValue(code, out var disp) ? disp : locale.NativeName;

                var (bg, btn, label, checkIcon) = CreateLanguageChip(gridRt, title, code == _selectedLocale);
                btn.onClick.AddListener(() => SelectLanguage(code));

                _chipBackgrounds[code] = bg;
                _chipLabels[code] = label;
                _chipCheckIcons[code] = checkIcon;
            }
        }

        private (Image bg, Button btn, TMP_Text label, Image checkIcon) CreateLanguageChip(
            Transform parent, string title, bool selected)
        {
            var pillImg = UIFactory.Pill(parent, selected ? UITheme.Accent : UITheme.FromHex("#152233"));
            pillImg.gameObject.name = $"Chip_{title}";
            var btn = pillImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = pillImg;

            // Horizontal content layout
            var contentRt = UIFactory.CreateRect("Content", pillImg.transform);
            UIFactory.StretchFill(contentRt, 12f);
            var hlg = contentRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = UITheme.SpaceS;
            hlg.childForceExpandWidth = false;

            var label = UIFactory.Label(contentRt, title, 24f, selected ? FontStyles.Bold : FontStyles.Normal,
                TextAlignmentOptions.Center, selected ? Color.white : UITheme.TextSecondary);

            var checkRt = UIFactory.CreateRect("CheckIcon", contentRt);
            checkRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 28f;
            var checkIcon = UIFactory.CenteredIcon(checkRt, IconType.Check, 24f, Color.white);
            checkIcon.gameObject.SetActive(selected);

            return (pillImg, btn, label, checkIcon);
        }

        private void SelectLanguage(string code)
        {
            _selectedLocale = code;
            Loc.SetLocale(code); // Trigger immediate retranslation of all bound labels
            RefreshChipVisuals();
        }

        private void RefreshChipVisuals()
        {
            foreach (var kv in _chipBackgrounds)
            {
                bool selected = kv.Key == _selectedLocale;
                kv.Value.color = selected ? UITheme.Accent : UITheme.FromHex("#152233");

                if (_chipLabels.TryGetValue(kv.Key, out var label))
                {
                    label.color = selected ? Color.white : UITheme.TextSecondary;
                    label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                }

                if (_chipCheckIcons.TryGetValue(kv.Key, out var checkIcon))
                {
                    checkIcon.gameObject.SetActive(selected);
                }
            }
        }

        private void BuildErrorBox(Transform parent)
        {
            _errorBoxRt = UIFactory.CreateRect("ErrorBox", parent);
            _errorBoxRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;

            var bg = UIFactory.Panel(_errorBoxRt, UITheme.FromHex("#3B1B15"), 16f);
            bg.gameObject.name = "ErrorBg";

            var hlg = _errorBoxRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = UITheme.SpaceS;
            hlg.padding = new RectOffset(20, 20, 8, 8);

            var iconRt = UIFactory.CreateRect("WarnIcon", _errorBoxRt);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 32f;
            UIFactory.CenteredIcon(iconRt, IconType.Warning, 26f, UITheme.Warning);

            var labelRt = UIFactory.CreateRect("ErrorLabel", _errorBoxRt);
            labelRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _errorLabel = UIFactory.Label(labelRt, string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.Warning);

            _errorBoxRt.gameObject.SetActive(false);
        }

        private void BuildContinueButton(Transform parent)
        {
            var btnContainer = UIFactory.CreateRect("ContinueContainer", parent);
            btnContainer.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

            _continueButton = UIFactory.PrimaryButton(btnContainer, string.Empty, Submit);

            // Add arrow icon right aligned inside button
            var arrowRt = UIFactory.CreateRect("BtnArrow", _continueButton.transform);
            arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(0.92f, 0.5f);
            UIFactory.CenteredIcon(arrowRt, IconType.North, 28f, Color.white);

            var label = _continueButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.fontSize = 28f;
                LocalizedLabel.Bind(label, "login.continue");
            }
        }

        private void ClearError()
        {
            if (_errorBoxRt != null && _errorBoxRt.gameObject.activeSelf)
            {
                _errorBoxRt.gameObject.SetActive(false);
            }
        }

        private void Submit()
        {
            if (_submitting) return;

            string name = _nameField.text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowError(Loc.T("login.error_name_required"));
                return;
            }

            if (!int.TryParse(_ageField.text, out int age) || age < 1 || age > 120)
            {
                ShowError(Loc.T("login.error_age_range"));
                return;
            }

            _submitting = true;
            _continueButton.interactable = false;

            var profile = ProfileService.Resolve().Save(name, age, _selectedLocale);

            string languageLabel = _selectedLocale;
            foreach (var locale in LocalizationService.AvailableLocales)
            {
                if (locale.Code == _selectedLocale) languageLabel = locale.NativeName;
            }

            bool exported = _excelStore.AppendAndExport(profile.Name, profile.Age, languageLabel);
            if (!exported)
            {
                Debug.LogWarning("[LoginScreen] login.xlsx export failed this run — profile still saved, continuing anyway.");
            }

            OnCompleted?.Invoke();
        }

        private void ShowError(string message)
        {
            _errorLabel.text = message;
            if (_errorBoxRt != null)
            {
                _errorBoxRt.gameObject.SetActive(true);
            }
        }
    }
}
