using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Every screen is built by calling these functions — no prefabs, no scene wiring.
    /// A widget here is a function call with UITheme tokens, so a spacing/colour change
    /// propagates everywhere at once. See PLAN.md §07 "Why procedural C# UI, not prefabs".
    /// </summary>
    public static class UIFactory
    {
        // ---------------------------------------------------------------
        // Layout primitives
        // ---------------------------------------------------------------

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Stretches a RectTransform to fill its parent with the given inset.</summary>
        public static RectTransform StretchFill(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform SetSize(RectTransform rt, float width, float height)
        {
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        // ---------------------------------------------------------------
        // Text
        // ---------------------------------------------------------------

        public static TMP_Text Label(
            Transform parent, string text, float size = UITheme.BodyFontSize,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft,
            Color? color = null)
        {
            var rt = CreateRect("Label", parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = color ?? UITheme.TextPrimary;
            tmp.raycastTarget = false;
            if (UITheme.PrimaryFont != null) tmp.font = UITheme.PrimaryFont;
            StretchFill(rt);
            return tmp;
        }

        /// <summary>Label that auto-shrinks so a longer translated string never clips (§07).</summary>
        public static TMP_Text AutoSizeLabel(
            Transform parent, string text, float maxSize = UITheme.BodyFontSize, float minSize = 18f,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, Color? color = null)
        {
            var tmp = Label(parent, text, maxSize, FontStyles.Normal, align, color);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            return tmp;
        }

        // ---------------------------------------------------------------
        // Surfaces
        // ---------------------------------------------------------------

        public static Image Panel(Transform parent, Color color, float radius = 0f)
        {
            var rt = CreateRect("Panel", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (radius > 0f)
            {
                img.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(radius));
                img.type = Image.Type.Sliced;
            }
            StretchFill(rt);
            return img;
        }

        public static Image Card(Transform parent, Color? color = null)
        {
            var img = Panel(parent, color ?? UITheme.Surface, UITheme.RadiusCard);
            img.gameObject.name = "Card";
            return img;
        }

        /// <summary>Translucent glass card used over the live camera feed (AR HUD chrome).</summary>
        public static Image GlassCard(Transform parent)
        {
            var img = Card(parent, UITheme.Glass);
            img.gameObject.name = "GlassCard";
            return img;
        }

        public static Image Pill(Transform parent, Color? color = null)
        {
            var rt = CreateRect("Pill", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color ?? UITheme.Surface;
            img.sprite = UIShapes.RoundedRect(64);
            img.type = Image.Type.Sliced;
            StretchFill(rt);
            return img;
        }

        // ---------------------------------------------------------------
        // Soft shadows — the depth pass (mockup comparison, Docs/UIplan.md §03 Phase 2)
        // ---------------------------------------------------------------

        /// <summary>Same idiom PoiMarkerLayer already uses for map pins: a low-alpha circle
        /// nudged down a few px behind the real graphic. Procedural sprites have hard edges (no
        /// blur available — see UIShapes), so the softness comes entirely from low alpha + a small
        /// offset, not a gaussian falloff. Call this BEFORE CircleButton/CenteredIcon on the same
        /// parent — every UIFactory surface helper (CircleButton, Card, Panel, Pill) creates its
        /// own child rect rather than drawing on the passed-in transform, so an earlier sibling
        /// here reliably ends up underneath it. Skip this call entirely if the parent slot already
        /// has its own Image component (e.g. ARNavigationScreen's StepBadge) — uGUI always draws a
        /// GameObject's own graphic beneath its children regardless of sibling order, so a shadow
        /// added as that slot's child would render on top of the slot's own fill instead of behind it.</summary>
        public static Image CircleShadow(Transform parent, float diameter, float dropY = -6f, float alpha = 0.4f)
        {
            var rt = CreateRect("Shadow", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            SetSize(rt, diameter, diameter);
            rt.anchoredPosition = new Vector2(0f, dropY);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIShapes.Circle();
            img.color = new Color(0f, 0f, 0f, alpha);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Rounded-rect counterpart of <see cref="CircleShadow"/> — for Card/Panel-shaped
        /// surfaces (list rows, stat cards, floating pills). Same rule: call it on the wrapping
        /// rect BEFORE Card()/Panel()/Pill(), never on a rect that already carries its own Image.</summary>
        public static Image RoundedShadow(Transform parent, float radius, float dropY = -9f, float alpha = 0.38f)
        {
            var rt = CreateRect("Shadow", parent);
            StretchFill(rt);
            rt.offsetMin += new Vector2(0f, dropY);
            rt.offsetMax += new Vector2(0f, dropY);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(radius));
            img.type = Image.Type.Sliced;
            img.color = new Color(0f, 0f, 0f, alpha);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A soft colored halo behind an accent-tinted control — the flat shadow above
        /// reads as "raised", this reads as "lit" (Docs/UIplan.md §03 Phase 2 follow-up). No blur
        /// available (see UIShapes), so it's a low-alpha circle noticeably larger than the control
        /// and NOT offset — an ambient glow, unlike CircleShadow's directional drop shadow. Same
        /// draw-order rule applies: call it on the wrapping rect before the actual control.
        /// Returns the Image so callers with a toggleable active state (map recenter/north-lock)
        /// can flip its GameObject on/off alongside the control's own colour.</summary>
        public static Image CircleGlow(Transform parent, float diameter, Color color, float spread = 20f, float alpha = 0.4f)
        {
            var rt = CreateRect("Glow", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float size = diameter + spread * 2f;
            SetSize(rt, size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIShapes.Circle();
            var c = color;
            c.a = alpha;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        // ---------------------------------------------------------------
        // Buttons
        // ---------------------------------------------------------------

        public static Button PrimaryButton(Transform parent, string text, System.Action onClick)
        {
            var pillImg = Pill(parent, UITheme.Accent);
            pillImg.gameObject.name = "PrimaryButton";
            var btn = pillImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = pillImg;
            SetButtonColors(btn, UITheme.Accent);
            AddPressFeedback(pillImg.gameObject);
            Label(pillImg.transform, text, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static Button GhostButton(Transform parent, string text, System.Action onClick)
        {
            var pillImg = Pill(parent, new Color(1, 1, 1, 0.06f));
            pillImg.gameObject.name = "GhostButton";
            var btn = pillImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = pillImg;
            SetButtonColors(btn, new Color(1, 1, 1, 0.06f));
            AddPressFeedback(pillImg.gameObject);
            Label(pillImg.transform, text, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextSecondary);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Circular icon button — used on the AR HUD left rail, map right rail, and the
        /// age stepper. Always centres itself within its parent regardless of the parent's own
        /// size, so it drops cleanly into a LayoutGroup cell without extra positioning code.</summary>
        public static Button CircleButton(Transform parent, float diameter, System.Action onClick, Color? bg = null)
        {
            var rt = CreateRect("CircleButton", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            SetSize(rt, diameter, diameter);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIShapes.Circle();
            img.color = bg ?? new Color(0.06f, 0.09f, 0.12f, 0.72f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            SetButtonColors(btn, img.color);
            AddPressFeedback(rt.gameObject);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Scale-down-then-up on every button press (0.94 -> 1.0, ~90ms), pairing the
        /// haptic pulse HapticService already fires on interaction with a visual one — before this,
        /// the haptic had no matching on-screen confirmation. Pure pointer-driven, no dependency
        /// on Selectable's own Transition system (kept at None everywhere via SetButtonColors), so
        /// it works identically whether the button also has a sprite-swap or color transition.</summary>
        private static void AddPressFeedback(GameObject go) => go.AddComponent<PressScaleFeedback>();

        private class PressScaleFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            private RectTransform _rt;
            private Coroutine _running;

            private void Awake() => _rt = (RectTransform)transform;

            public void OnPointerDown(PointerEventData eventData) => AnimateTo(0.94f, 0.05f);
            public void OnPointerUp(PointerEventData eventData) => AnimateTo(1f, 0.09f);
            public void OnPointerExit(PointerEventData eventData) => AnimateTo(1f, 0.09f);

            private void AnimateTo(float target, float duration)
            {
                if (_rt == null || UITween.ReducedMotion) { if (_rt != null) _rt.localScale = Vector3.one * (UITween.ReducedMotion ? 1f : target); return; }
                if (_running != null) StopCoroutine(_running);
                _running = StartCoroutine(ScaleRoutine(target, duration));
            }

            private System.Collections.IEnumerator ScaleRoutine(float target, float duration)
            {
                float from = _rt.localScale.x;
                float t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float v = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
                    _rt.localScale = Vector3.one * v;
                    yield return null;
                }
                _rt.localScale = Vector3.one * target;
            }
        }

        /// <summary>A centred icon sized to fit inside any non-LayoutGroup parent (a circular
        /// button, a card corner) — plain CreateRect defaults to a corner anchor, which is wrong
        /// for this very common case, so every icon placement doesn't have to remember the fix.</summary>
        public static Image CenteredIcon(Transform parent, IconType icon, float size, Color? color = null)
        {
            var rt = CreateRect($"Icon_{icon}", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            // Every one of this app's ~35 icon placements passes its size through here, so
            // UITheme.IconSizeMultiplier is the one place to make every icon in the app larger
            // or smaller at once, rather than hand-editing every call site's literal pixel size.
            float scaled = size * UITheme.IconSizeMultiplier;
            SetSize(rt, scaled, scaled);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = IconGraphic.Get(icon);
            img.color = color ?? UITheme.TextPrimary;
            return img;
        }

        private static void SetButtonColors(Button btn, Color baseColor)
        {
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
        }

        // ---------------------------------------------------------------
        // Chips / toggles / switches
        // ---------------------------------------------------------------

        /// <summary>A single-select filter chip (Landmarks screen) or language chip (Login screen).</summary>
        public static (Image background, Button button, TMP_Text label) Chip(
            Transform parent, string text, bool selected)
        {
            var img = Pill(parent, selected ? UITheme.Accent : UITheme.Surface);
            img.gameObject.name = $"Chip_{text}";
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            SetButtonColors(btn, img.color);
            var label = Label(img.transform, text, UITheme.LabelFontSize, FontStyles.Normal,
                TextAlignmentOptions.Center, selected ? Color.white : UITheme.TextSecondary);
            return (img, btn, label);
        }

        /// <summary>An iOS-style switch. Returns the Toggle so callers can subscribe to
        /// onValueChanged. Sized a step above a bare 48dp touch target (Docs/UIplan.md §03 Phase 2
        /// — the mockup's switches read larger than the original 92×52) while keeping the same
        /// knob-inset proportions.</summary>
        public static Toggle Switch(Transform parent, bool initial, System.Action<bool> onChanged)
        {
            const float trackWidth = 108f;
            const float trackHeight = 60f;
            const float knobDiameter = 48f;
            const float inset = (trackHeight - knobDiameter) / 2f;
            const float knobOnX = trackWidth - knobDiameter - inset;

            var rt = CreateRect("Switch", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            SetSize(rt, trackWidth, trackHeight);

            var trackImg = rt.gameObject.AddComponent<Image>();
            trackImg.sprite = UIShapes.RoundedRect(Mathf.RoundToInt(trackHeight / 2f));
            trackImg.type = Image.Type.Sliced;
            trackImg.color = initial ? UITheme.Accent : UITheme.Rule;

            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = trackImg;
            toggle.isOn = initial;
            toggle.transition = Selectable.Transition.None;

            var knobRt = CreateRect("Knob", rt);
            SetSize(knobRt, knobDiameter, knobDiameter);
            knobRt.anchorMin = knobRt.anchorMax = new Vector2(0f, 0.5f);
            knobRt.anchoredPosition = new Vector2(initial ? knobOnX : inset, 0f);
            var knobImg = knobRt.gameObject.AddComponent<Image>();
            knobImg.sprite = UIShapes.Circle();
            knobImg.color = Color.white;

            toggle.onValueChanged.AddListener(isOn =>
            {
                float fromX = knobRt.anchoredPosition.x;
                float toX = isOn ? knobOnX : inset;
                Color fromColor = trackImg.color;
                Color toColor = isOn ? UITheme.Accent : UITheme.Rule;
                UITween.SlideProgress(blend =>
                {
                    knobRt.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, blend), 0f);
                    trackImg.color = Color.Lerp(fromColor, toColor, blend);
                }, 0f, 1f, 0.16f);
                onChanged?.Invoke(isOn);
            });

            return toggle;
        }

        // ---------------------------------------------------------------
        // Rows (Settings / Landmarks list)
        // ---------------------------------------------------------------

        /// <summary>A Settings-style row: optional leading icon, label, trailing value + chevron.
        /// Takes plain resolved strings (not loc keys) like every other UIFactory widget — the
        /// caller binds LocalizedLabel on the returned Text components if live retranslation is
        /// needed. Returns the row plus its label/value Text so the caller can update either.</summary>
        public static (RectTransform row, TMP_Text label, TMP_Text value) SettingsRow(
            Transform parent, IconType? icon, string label, string value, System.Action onClick, bool showChevron = true, float rowHeight = UITheme.MinTouchTarget)
        {
            var row = CreateRect($"Row_{label}", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = rowHeight;
            le.preferredHeight = rowHeight;

            if (onClick != null)
            {
                var btn = row.gameObject.AddComponent<Button>();
                var bg = row.gameObject.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0);
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onClick());
            }

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.spacing = UITheme.SpaceM;

            if (icon.HasValue)
            {
                var iconRt = CreateRect("Icon", row);
                iconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 48f;
                CenteredIcon(iconRt, icon.Value, 40f, UITheme.TextSecondary);
            }

            var labelRt = CreateRect("Label", row);
            labelRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var labelText = Label(labelRt, label, UITheme.BodyFontSize);

            TMP_Text valueText = null;
            if (!string.IsNullOrEmpty(value))
            {
                var valueRt = CreateRect("Value", row);
                valueRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 260f;
                valueText = Label(valueRt, value, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineRight, UITheme.TextSecondary);
            }

            if (showChevron)
            {
                var chevRt = CreateRect("Chevron", row);
                chevRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 28f;
                Label(chevRt, ">", UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextTertiary);
            }

            return (row, labelText, valueText);
        }

        // ---------------------------------------------------------------
        // AR HUD stat column (Distance / Next Landmark / ETA)
        // ---------------------------------------------------------------

        public static (TMP_Text valueLabel, TMP_Text captionLabel) StatColumn(
            Transform parent, string caption, string value)
        {
            var col = CreateRect($"Stat_{caption}", parent);
            var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 2f;

            var valueRt = CreateRect("Value", col);
            valueRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var valueLabel = Label(valueRt, value, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

            var captionRt = CreateRect("Caption", col);
            captionRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
            var captionLabel = Label(captionRt, caption, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextTertiary);

            return (valueLabel, captionLabel);
        }

        // ---------------------------------------------------------------
        // Progress
        // ---------------------------------------------------------------

        public static Image ProgressBar(Transform parent, float fillAmount)
        {
            var track = Pill(parent, UITheme.Rule);
            track.gameObject.name = "ProgressTrack";

            var fillRt = CreateRect("ProgressFill", track.transform);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(Mathf.Clamp01(fillAmount), 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = UIShapes.RoundedRect(64);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = UITheme.Success;
            return fillImg;
        }

        /// <summary>Circular completion ring for the Progress screen. Returns the fill Image —
        /// set fillImage.fillAmount in [0,1] to animate.</summary>
        public static Image ProgressRing(Transform parent, float diameter, float thickness, float fillAmount, Color? fillColor = null)
        {
            return ProgressRing(parent, diameter, thickness, fillAmount, out _, fillColor);
        }

        /// <summary>Overload that also hands back the ring's own centred container, so callers
        /// can drop a percentage label (or anything else) in the middle of the ring.</summary>
        public static Image ProgressRing(Transform parent, float diameter, float thickness, float fillAmount, out RectTransform container, Color? fillColor = null)
        {
            container = CreateRect("ProgressRing", parent);
            container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.5f);
            SetSize(container, diameter, diameter);
            int radius = Mathf.RoundToInt(diameter / 2f);
            int thick = Mathf.RoundToInt(thickness);

            var bgRt = CreateRect("RingBg", container);
            StretchFill(bgRt);
            var bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.sprite = UIShapes.Ring(radius, thick);
            bgImg.color = UITheme.Rule;

            var fillRt = CreateRect("RingFill", container);
            StretchFill(fillRt);
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = UIShapes.Ring(radius, thick);
            fillImg.color = fillColor ?? UITheme.Success;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillAmount = Mathf.Clamp01(fillAmount);

            return fillImg;
        }

        // ---------------------------------------------------------------
        // Input fields
        // ---------------------------------------------------------------

        public static TMP_InputField InputField(Transform parent, string placeholder, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard)
        {
            var pillImg = Pill(parent, UITheme.Surface);
            pillImg.gameObject.name = $"Input_{placeholder}";

            var textAreaRt = CreateRect("TextArea", pillImg.transform);
            StretchFill(textAreaRt, UITheme.SpaceM);

            var placeholderLabel = Label(textAreaRt, placeholder, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextTertiary);
            var textLabel = Label(textAreaRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextPrimary);

            var input = pillImg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRt;
            input.textComponent = (TextMeshProUGUI)textLabel;
            input.placeholder = (TextMeshProUGUI)placeholderLabel;
            input.contentType = contentType;
            input.targetGraphic = pillImg;
            return input;
        }

        // ---------------------------------------------------------------
        // Scroll containers
        // ---------------------------------------------------------------

        /// <summary>A vertically scrolling list. Returns the Content rect — add children to it
        /// (they'll stack top-down); ContentSizeFitter grows it to fit, ScrollRect clips + scrolls.</summary>
        public static RectTransform VerticalScroll(Transform parent, float spacing = UITheme.SpaceM, RectOffset padding = null)
        {
            var viewport = CreateRect("ScrollView", parent);
            StretchFill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = padding ?? new RectOffset(0, 0, 0, 0);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 28f;
            scrollRect.viewport = viewport;

            return content;
        }

        /// <summary>A horizontally scrolling strip (filter chips). Returns the Content rect —
        /// give each child an explicit LayoutElement.preferredWidth (e.g. from
        /// TMP_Text.GetPreferredValues()) since childControlWidth is off here.</summary>
        public static RectTransform HorizontalScroll(Transform parent, float height, float spacing = UITheme.SpaceS)
        {
            var viewport = CreateRect("HScrollView", parent);
            viewport.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;

            var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = content;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.viewport = viewport;

            return content;
        }

        // ---------------------------------------------------------------
        // Overlay scrim + modal card (used by every overlay: popup, turn card, arrival, etc.)
        // ---------------------------------------------------------------

        public static (Image scrim, RectTransform modal) Modal(Transform parent, float modalWidth = 700f)
        {
            var scrimRt = CreateRect("Scrim", parent);
            StretchFill(scrimRt);
            var scrim = scrimRt.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            scrim.gameObject.AddComponent<Button>().targetGraphic = scrim; // tap-outside dismiss target

            var modal = CreateRect("Modal", scrimRt);
            modal.anchorMin = modal.anchorMax = new Vector2(0.5f, 0.5f);
            modal.sizeDelta = new Vector2(modalWidth, 0f); // height driven by ContentSizeFitter in caller

            return (scrim, modal);
        }
    }
}
