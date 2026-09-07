using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AlipiriAR.Core;
using AlipiriAR.Data;
using AlipiriAR.Database;
using AlipiriAR.Localization;
using AlipiriAR.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>Frame 3 — search, six filter chips, a scrollable card list keyed off the same
    /// route-resolved distances JsonDatabase computes for every screen (PLAN.md §06).</summary>
    public class LandmarksScreen : UIScreen
    {
        private enum Filter { All, Temple, Water, Statue, Steps, Other }

        private static readonly (Filter filter, string locKey)[] FilterChips =
        {
            (Filter.All, "landmarks.filter_all"),
            (Filter.Temple, "landmarks.filter_temple"),
            (Filter.Water, "landmarks.filter_water"),
            (Filter.Statue, "landmarks.filter_statue"),
            (Filter.Steps, "landmarks.filter_steps"),
            (Filter.Other, "landmarks.filter_other"),
        };

        private VisitedStore _visitedStore;
        private List<LandmarkData> _routeOrdered = new();
        private readonly Dictionary<int, GameObject> _cardObjects = new();
        private readonly Dictionary<int, (GameObject filled, GameObject ring)> _statusVisuals = new();
        private readonly Dictionary<int, TMP_Text> _distanceLabels = new();
        private readonly Dictionary<int, GameObject> _activeBorders = new();
        private readonly Dictionary<int, TMP_Text> _aheadLabels = new();
        private readonly List<(Filter filter, Image bg, TMP_Text label, LayoutElement le)> _chips = new();

        private RectTransform _listContent;
        private RectTransform _emptyRt;
        private RectTransform _searchRow;
        private TMP_InputField _searchField;
        private Filter _activeFilter = Filter.All;
        private LandmarkPopup _activePopup;

        // Two prior fixes (ignoreLayout on the background, then moving the VerticalLayoutGroup
        // onto a dedicated "Shell" child) were both verified present in the actual compiled,
        // installed build via direct IL2CPP metadata inspection — and the real-device gap
        // (Header/Search rendering ~500px down empty space) persisted regardless either time.
        // Rather than a third theory about what's misbehaving inside VerticalLayoutGroup, this
        // removes it from the equation entirely: Header/SearchRow/ChipsRow/ListWrap are now
        // explicitly anchored top-to-bottom by hand (PositionFixed below), the same fixed-anchor
        // technique every screen's floating cards already use elsewhere in this codebase — no
        // automatic layout system left in the stack for whatever this was to hide inside.
        private const float HeaderHeight = 96f;
        private const float ChipsHeight = 76f;
        private const float RowGap = UITheme.SpaceS;
        private const float TopPad = UITheme.SpaceM;

        protected override void Build(RectTransform root)
        {
            UIFactory.Panel(root, UITheme.Ground);

            var headerRt = BuildHeader(root);
            PositionFixed(headerRt, TopPad, HeaderHeight);

            BuildSearchRow(root);
            float searchTop = TopPad + HeaderHeight + RowGap;
            PositionFixed(_searchRow, searchTop, UITheme.MinTouchTarget);

            float chipsTop = searchTop + UITheme.MinTouchTarget + RowGap;
            var chipsRowRt = UIFactory.CreateRect("ChipsRow", root);
            PositionFixed(chipsRowRt, chipsTop, ChipsHeight);
            BuildFilterChips(chipsRowRt);

            float listTop = chipsTop + ChipsHeight + RowGap;
            var listWrapRt = UIFactory.CreateRect("ListWrap", root);
            listWrapRt.anchorMin = Vector2.zero;
            listWrapRt.anchorMax = Vector2.one;
            listWrapRt.offsetMin = Vector2.zero;
            listWrapRt.offsetMax = new Vector2(0f, -listTop);

            // Inter-card gap widened from SpaceM (20px) to sit closer to the mockup's rhythm
            // (Docs/UIplan.md §03 Phase 2) — cards otherwise read as touching at this list length.
            _listContent = UIFactory.VerticalScroll(listWrapRt, UITheme.SpaceM + UITheme.SpaceXS,
                new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceS, (int)UITheme.SpaceM));

            _emptyRt = UIFactory.CreateRect("Empty", listWrapRt);
            UIFactory.StretchFill(_emptyRt, UITheme.SpaceL);
            var emptyLabel = UIFactory.Label(_emptyRt, string.Empty, UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextTertiary);
            LocalizedLabel.Bind(emptyLabel, "landmarks.empty");
            _emptyRt.gameObject.SetActive(false);

            var db = ServiceLocator.Get<JsonDatabase>();
            _visitedStore = VisitedStore.Resolve();
            _routeOrdered = db.Landmarks.OrderBy(l => l.CumulativeDistanceMeters).ToList();

            for (int i = 0; i < _routeOrdered.Count; i++)
                BuildCard(_listContent, _routeOrdered[i], i + 1);

            ApplyFilter();
            RefreshActiveHighlight();
            Loc.OnLocaleChanged += OnLocaleChanged;
            SettingsStore.Resolve().OnUnitsChanged += RefreshDistances;
        }

        private void OnDestroy()
        {
            Loc.OnLocaleChanged -= OnLocaleChanged;
            SettingsStore.Resolve().OnUnitsChanged -= RefreshDistances;
        }

        protected override void OnShown()
        {
            foreach (var lm in _routeOrdered) RefreshStatusVisual(lm.Id);
            RefreshDistances();
            RefreshActiveHighlight();
        }

        /// <summary>Anchors rt to root's top edge at a fixed pixel offset/height — see this
        /// class's Build() comment for why this replaced VerticalLayoutGroup here.</summary>
        private static void PositionFixed(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(0f, height);
        }

        private void RefreshDistances()
        {
            foreach (var lm in _routeOrdered)
            {
                if (_distanceLabels.TryGetValue(lm.Id, out var label))
                    label.text = DistanceFormatter.FormatMeters(lm.CumulativeDistanceMeters);
            }
        }

        private void OnLocaleChanged()
        {
            RetranslateChips();
            if (_searchField != null)
                ((TMP_Text)_searchField.placeholder).text = Loc.T("landmarks.search_placeholder");
        }

        // ---------------------------------------------------------------
        // Header + search
        // ---------------------------------------------------------------

        private RectTransform BuildHeader(Transform parent)
        {
            var headerRt = UIFactory.CreateRect("Header", parent);
            var hlg = headerRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, 0, 0);
            hlg.spacing = UITheme.SpaceS;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var markRt = UIFactory.CreateRect("Mark", headerRt);
            markRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 56f;
            UIFactory.CenteredIcon(markRt, IconType.Gopuram, 48f, UITheme.Gold);

            var titleRt = UIFactory.CreateRect("Title", headerRt);
            titleRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.TitleFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Bind(titleLabel, "landmarks.title");

            var searchIconRt = UIFactory.CreateRect("SearchIcon", headerRt);
            searchIconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 72f;
            UIFactory.CircleShadow(searchIconRt, 72f);
            var searchIconBg = UIFactory.CircleButton(searchIconRt, 72f, FocusSearchField, new Color(1f, 1f, 1f, 0.06f));
            UIFactory.CenteredIcon(searchIconBg.transform, IconType.Search, 32f);

            return headerRt;
        }

        /// <summary>Search bar sits permanently under the header now (user request) — no more
        /// toggle button hiding it; the header's search icon just focuses the always-visible field.</summary>
        private void BuildSearchRow(Transform parent)
        {
            _searchRow = UIFactory.CreateRect("SearchRow", parent);
            var le = _searchRow.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = UITheme.MinTouchTarget;

            var paddedRt = UIFactory.CreateRect("Padded", _searchRow);
            UIFactory.StretchFill(paddedRt);
            paddedRt.offsetMin = new Vector2(UITheme.SpaceM, 0f);
            paddedRt.offsetMax = new Vector2(-UITheme.SpaceM, 0f);

            _searchField = UIFactory.InputField(paddedRt, Loc.T("landmarks.search_placeholder"));
            _searchField.onValueChanged.AddListener(_ => ApplyFilter());
        }

        private void FocusSearchField()
        {
            _searchField.Select();
            _searchField.ActivateInputField();
        }

        // ---------------------------------------------------------------
        // Filter chips
        // ---------------------------------------------------------------

        private void BuildFilterChips(Transform parent)
        {
            var content = UIFactory.HorizontalScroll(parent, 76f, UITheme.SpaceS);
            var padLeft = UIFactory.CreateRect("PadLeft", content);
            padLeft.gameObject.AddComponent<LayoutElement>().preferredWidth = UITheme.SpaceM - UITheme.SpaceS;

            foreach (var (filter, locKey) in FilterChips)
            {
                var (bg, btn, label) = UIFactory.Chip(content, Loc.T(locKey), filter == _activeFilter);
                var le = bg.gameObject.AddComponent<LayoutElement>();
                ResizeChip(label, le);
                btn.onClick.AddListener(() => SelectFilter(filter));
                _chips.Add((filter, bg, label, le));
            }
        }

        private static void ResizeChip(TMP_Text label, LayoutElement le)
        {
            Vector2 pref = label.GetPreferredValues();
            le.preferredWidth = pref.x + UITheme.SpaceL * 2f;
            le.preferredHeight = 76f;
        }

        private void RetranslateChips()
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                var (_, _, label, le) = _chips[i];
                label.text = Loc.T(FilterChips[i].locKey);
                ResizeChip(label, le);
            }
        }

        private void SelectFilter(Filter filter)
        {
            if (_activeFilter == filter) return;
            _activeFilter = filter;
            RefreshChipVisuals();
            ApplyFilter();
        }

        private void RefreshChipVisuals()
        {
            foreach (var (filter, bg, label, _) in _chips)
            {
                bool selected = filter == _activeFilter;
                Color targetBg = selected ? UITheme.Accent : UITheme.Surface;
                Color targetLabel = selected ? Color.white : UITheme.TextSecondary;
                Color fromBg = bg.color;
                Color fromLabel = label.color;
                UITween.SlideProgress(t =>
                {
                    bg.color = Color.Lerp(fromBg, targetBg, t);
                    label.color = Color.Lerp(fromLabel, targetLabel, t);
                }, 0f, 1f, 0.14f);
            }
        }

        private static bool Matches(LandmarkData lm, Filter f) => f switch
        {
            Filter.All => true,
            Filter.Temple => lm.Type == LandmarkType.Temple,
            Filter.Water => lm.Type == LandmarkType.WaterPoint,
            Filter.Statue => lm.Type == LandmarkType.Statue,
            Filter.Steps => lm.Type == LandmarkType.Steps,
            Filter.Other => lm.Type.IsOtherCategory(),
            _ => true,
        };

        // ---------------------------------------------------------------
        // List + filtering
        // ---------------------------------------------------------------

        private void ApplyFilter()
        {
            string query = _searchField != null ? NormalizeForSearch(_searchField.text) : string.Empty;
            bool any = false;

            foreach (var lm in _routeOrdered)
            {
                bool matchesFilter = Matches(lm, _activeFilter);
                bool matchesSearch = string.IsNullOrEmpty(query)
                    || NormalizeForSearch(lm.Name).Contains(query)
                    || NormalizeForSearch(lm.VoiceText).Contains(query);
                bool visible = matchesFilter && matchesSearch;

                if (_cardObjects.TryGetValue(lm.Id, out var go)) go.SetActive(visible);
                any |= visible;
            }

            _emptyRt.gameObject.SetActive(!any);
        }

        private static string NormalizeForSearch(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var formD = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var c in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        }

        // ---------------------------------------------------------------
        // Card
        // ---------------------------------------------------------------

        private void BuildCard(Transform parent, LandmarkData landmark, int sequenceNumber)
        {
            const float thumbSize = 136f;
            const float ringSize = 64f;
            const float pad = 20f;
            const float gap = 16f;

            // The scroll content's VerticalLayoutGroup treats every direct child as a list row, so
            // a shadow sibling can't sit next to the Card itself here (unlike a screen-level card,
            // where the containing rect is just a fixed-position wrapper) — it would become its
            // own empty row. Wrap instead: the wrapper is the actual list item, Shadow and Card are
            // both its children (Docs/UIplan.md §03 Phase 2 — same fix needed by the AR/Map screen
            // floating cards, which don't have this problem since they aren't inside a LayoutGroup).
            var wrapper = UIFactory.CreateRect($"CardWrap_{landmark.Id}", parent);
            wrapper.gameObject.AddComponent<LayoutElement>().minHeight = 176f;
            UIFactory.RoundedShadow(wrapper, UITheme.RadiusCard);

            var card = UIFactory.Card(wrapper, UITheme.Surface);
            card.gameObject.name = $"Card_{landmark.Id}";

            // Redesign's "active/next waypoint" treatment (Docs/Images/New UI #3) — a glowing gold
            // border shown on exactly one card at a time (the first not-yet-visited landmark),
            // toggled by RefreshActiveHighlight rather than baked in here since visiting/unvisiting
            // any landmark can shift which one that is.
            var border = UIFactory.GlowBorder(card.transform, UITheme.RadiusCard);
            border.gameObject.SetActive(false);
            _activeBorders[landmark.Id] = border.gameObject;

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OpenPopup(landmark));

            var thumbRt = UIFactory.CreateRect("Thumbnail", card.transform);
            thumbRt.anchorMin = thumbRt.anchorMax = new Vector2(0f, 0.5f);
            thumbRt.pivot = new Vector2(0f, 0.5f);
            thumbRt.anchoredPosition = new Vector2(pad, 0f);
            UIFactory.SetSize(thumbRt, thumbSize, thumbSize);
            var thumbImg = thumbRt.gameObject.AddComponent<Image>();
            thumbImg.sprite = UIShapes.RoundedRect(20);
            thumbImg.type = Image.Type.Sliced;
            // Was 0.7 toward Ground — fine when Ground was a neutral warm grey, but now that
            // Ground is forest green, diluting a gold tint 70% toward it reads as a muddy olive
            // (confirmed on-device screenshot, 2026-09-06) rather than a warm temple-gold tile.
            // Lower ratio keeps the tint itself recognizable regardless of Ground's hue.
            thumbImg.color = Color.Lerp(LandmarkVisuals.TintFor(landmark.Type), UITheme.Ground, 0.4f);
            UIFactory.CenteredIcon(thumbRt, LandmarkVisuals.IconFor(landmark.Type), 64f, LandmarkVisuals.TintFor(landmark.Type));

            var statusSlot = UIFactory.CreateRect("StatusSlot", card.transform);
            statusSlot.anchorMin = statusSlot.anchorMax = new Vector2(1f, 0.5f);
            statusSlot.pivot = new Vector2(1f, 0.5f);
            statusSlot.anchoredPosition = new Vector2(-pad, 0f);
            UIFactory.SetSize(statusSlot, ringSize, ringSize);
            BuildStatusRing(statusSlot, landmark);

            var midRt = UIFactory.CreateRect("Middle", card.transform);
            midRt.anchorMin = Vector2.zero;
            midRt.anchorMax = Vector2.one;
            midRt.offsetMin = new Vector2(pad + thumbSize + gap, 16f);
            midRt.offsetMax = new Vector2(-(pad + ringSize + gap), -16f);

            var midVlg = midRt.gameObject.AddComponent<VerticalLayoutGroup>();
            midVlg.childForceExpandWidth = true;
            midVlg.childForceExpandHeight = false;
            midVlg.childControlWidth = true;
            midVlg.childControlHeight = true;
            midVlg.spacing = 4f;
            midVlg.childAlignment = TextAnchor.MiddleLeft;

            var topRt = UIFactory.CreateRect("Top", midRt);
            topRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            var topHlg = topRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            topHlg.spacing = UITheme.SpaceS;
            topHlg.childAlignment = TextAnchor.MiddleLeft;
            topHlg.childControlWidth = true;
            topHlg.childControlHeight = true;
            topHlg.childForceExpandWidth = false;
            topHlg.childForceExpandHeight = false;

            var badgeRt = UIFactory.CreateRect("Badge", topRt);
            var badgeLe = badgeRt.gameObject.AddComponent<LayoutElement>();
            badgeLe.preferredWidth = 44f;
            badgeLe.preferredHeight = 44f;
            var badgeImg = badgeRt.gameObject.AddComponent<Image>();
            badgeImg.sprite = UIShapes.Circle();
            badgeImg.color = UITheme.Success;
            UIFactory.Label(badgeRt, sequenceNumber.ToString(CultureInfo.InvariantCulture), UITheme.CaptionFontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            var nameRt = UIFactory.CreateRect("Name", topRt);
            var nameLe = nameRt.gameObject.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1f;
            nameLe.minWidth = 160f;
            nameLe.preferredHeight = 44f;
            var nameLabel = UIFactory.Label(nameRt, landmark.Name, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            nameLabel.enableWordWrapping = false;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            var distRt = UIFactory.CreateRect("Distance", topRt);
            var distLe = distRt.gameObject.AddComponent<LayoutElement>();
            distLe.preferredWidth = 130f;
            distLe.preferredHeight = 44f;
            var distLabel = UIFactory.Label(distRt, DistanceFormatter.FormatMeters(landmark.CumulativeDistanceMeters), UITheme.LabelFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineRight, UITheme.TextSecondary);
            _distanceLabels[landmark.Id] = distLabel;

            var descRt = UIFactory.CreateRect("Description", midRt);
            descRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            var descLabel = UIFactory.Label(descRt, landmark.Description, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);
            descLabel.enableWordWrapping = false;
            descLabel.overflowMode = TextOverflowModes.Ellipsis;
            _aheadLabels[landmark.Id] = descLabel;

            _cardObjects[landmark.Id] = wrapper.gameObject;
        }

        private void BuildStatusRing(Transform slot, LandmarkData landmark)
        {
            var btn = slot.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            btn.targetGraphic = hit;

            var filledRt = UIFactory.CreateRect("Filled", slot);
            UIFactory.StretchFill(filledRt);
            var filledImg = filledRt.gameObject.AddComponent<Image>();
            filledImg.sprite = UIShapes.Circle();
            filledImg.color = UITheme.Success;
            UIFactory.CenteredIcon(filledRt, IconType.Check, 30f, Color.white);

            var ringRt = UIFactory.CreateRect("Ring", slot);
            UIFactory.StretchFill(ringRt);
            var ringImg = ringRt.gameObject.AddComponent<Image>();
            ringImg.sprite = UIShapes.Ring(32, 5);
            ringImg.color = UITheme.TextTertiary;

            btn.onClick.AddListener(() => ToggleVisited(landmark.Id));

            _statusVisuals[landmark.Id] = (filledRt.gameObject, ringRt.gameObject);
            RefreshStatusVisual(landmark.Id);
        }

        private void ToggleVisited(int landmarkId)
        {
            bool newState = !_visitedStore.IsVisited(landmarkId);
            _visitedStore.SetVisited(landmarkId, newState);
            RefreshStatusVisual(landmarkId);
            RefreshActiveHighlight();
        }

        /// <summary>Redesign's single glowing-gold "next waypoint" card (Docs/Images/New UI #3) —
        /// the first landmark in route order that isn't yet visited. Recomputed on every visited-
        /// state change since toggling any landmark can shift which one that is.</summary>
        private void RefreshActiveHighlight()
        {
            int activeId = -1;
            foreach (var lm in _routeOrdered)
            {
                if (!_visitedStore.IsVisited(lm.Id)) { activeId = lm.Id; break; }
            }

            foreach (var lm in _routeOrdered)
            {
                bool isActive = lm.Id == activeId;
                if (_activeBorders.TryGetValue(lm.Id, out var border)) border.SetActive(isActive);
                if (_aheadLabels.TryGetValue(lm.Id, out var label))
                {
                    label.color = isActive ? UITheme.Accent : UITheme.TextSecondary;
                    label.text = isActive
                        ? Loc.T("nav.ahead_format", DistanceFormatter.FormatMeters(lm.CumulativeDistanceMeters - CompletedMeters()))
                        : lm.Description;
                }
            }
        }

        private double CompletedMeters()
        {
            double completed = 0.0;
            foreach (var lm in _routeOrdered)
                if (_visitedStore.IsVisited(lm.Id) && lm.CumulativeDistanceMeters > completed)
                    completed = lm.CumulativeDistanceMeters;
            return completed;
        }

        private void RefreshStatusVisual(int landmarkId)
        {
            if (!_statusVisuals.TryGetValue(landmarkId, out var pair)) return;
            bool visited = _visitedStore.IsVisited(landmarkId);
            pair.filled.SetActive(visited);
            pair.ring.SetActive(!visited);
        }

        // ---------------------------------------------------------------
        // Popup
        // ---------------------------------------------------------------

        private void OpenPopup(LandmarkData landmark)
        {
            if (_activePopup != null) return;
            var overlayContainer = ServiceLocator.Get<UIRoot>().OverlayContainer;
            _activePopup = LandmarkPopup.Show(overlayContainer, landmark, () => _activePopup = null);
        }
    }
}
