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
        private readonly List<(Filter filter, Image bg, TMP_Text label, LayoutElement le)> _chips = new();

        private RectTransform _listContent;
        private RectTransform _emptyRt;
        private RectTransform _searchRow;
        private TMP_InputField _searchField;
        private bool _searchVisible;
        private Filter _activeFilter = Filter.All;
        private LandmarkPopup _activePopup;

        protected override void Build(RectTransform root)
        {
            UIFactory.Panel(root, UITheme.Ground);

            var rootVlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootVlg.childForceExpandWidth = true;
            rootVlg.childForceExpandHeight = false;
            rootVlg.childControlWidth = true;
            rootVlg.childControlHeight = true;
            rootVlg.spacing = UITheme.SpaceS;
            rootVlg.padding = new RectOffset(0, 0, (int)UITheme.SpaceM, 0);

            BuildHeader(root);
            BuildSearchRow(root);

            var chipsRowRt = UIFactory.CreateRect("ChipsRow", root);
            chipsRowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
            BuildFilterChips(chipsRowRt);

            var listWrapRt = UIFactory.CreateRect("ListWrap", root);
            listWrapRt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
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

            Canvas.ForceUpdateCanvases();
            Debug.Log($"[LandmarksDiag] Root.rect={Root.rect} Root.anchoredPosition={Root.anchoredPosition} Root.anchorMin={Root.anchorMin} Root.anchorMax={Root.anchorMax}");
            for (int i = 0; i < Root.childCount; i++)
            {
                var child = (RectTransform)Root.GetChild(i);
                Debug.Log($"[LandmarksDiag] child[{i}]={child.name} rect={child.rect} anchoredPos={child.anchoredPosition} sizeDelta={child.sizeDelta} anchorMin={child.anchorMin} anchorMax={child.anchorMax}");
            }
            var parentRt = (RectTransform)Root.parent;
            Debug.Log($"[LandmarksDiag] Parent(ScreensContainer).rect={parentRt.rect} offsetMin={parentRt.offsetMin} offsetMax={parentRt.offsetMax}");
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

        private void BuildHeader(Transform parent)
        {
            var headerRt = UIFactory.CreateRect("Header", parent);
            headerRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;
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

            var searchBtnRt = UIFactory.CreateRect("SearchBtn", headerRt);
            searchBtnRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 72f;
            UIFactory.CircleShadow(searchBtnRt, 72f);
            var searchBtn = UIFactory.CircleButton(searchBtnRt, 72f, ToggleSearch, new Color(1f, 1f, 1f, 0.06f));
            UIFactory.CenteredIcon(searchBtn.transform, IconType.Search, 32f);
        }

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
            _searchRow.gameObject.SetActive(false);
        }

        private void ToggleSearch()
        {
            _searchVisible = !_searchVisible;
            _searchRow.gameObject.SetActive(_searchVisible);
            if (!_searchVisible)
            {
                _searchField.text = string.Empty;
            }
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
            thumbImg.color = Color.Lerp(LandmarkVisuals.TintFor(landmark.Type), UITheme.Ground, 0.7f);
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
