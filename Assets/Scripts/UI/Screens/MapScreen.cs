using System.Collections.Generic;
using AlipiriAR.Core;
using AlipiriAR.Data;
using AlipiriAR.Database;
using AlipiriAR.Localization;
using AlipiriAR.Map;
using AlipiriAR.Positioning;
using AlipiriAR.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Frame 2 — demo basemap, route, POIs, user marker, gestures, top pill, right rail, bottom
    /// "Next" card (PLAN.md §06). Position comes from the shared NavigationSession (Scene 5) —
    /// trace-replay in v1 — so Map, Progress and Landmarks all read the same scalar. Opening
    /// this tab starts the shared session the first time (PLAN.md §08 frames the trace as the
    /// project's highest-leverage test harness); the AR screen (Scene 6) will add real
    /// Pause/Resume/End controls that act on the same session.
    /// </summary>
    public class MapScreen : UIScreen
    {
        private MapView _mapView;
        private UserMarker _userMarker;
        private NavigationSession _session;
        private JsonDatabase _db;
        private List<LandmarkData> _landmarksByDistance;

        private TMP_Text _summaryLabel;
        private TMP_Text _nextNameLabel;
        private TMP_Text _nextDistanceLabel;
        private Button _recenterBtn;
        private GameObject _recenterGlow;
        private TMP_Text _tiltLabel;
        private Image _northBg;
        private GameObject _northGlow;
        private bool _northLocked = true;

        private double _lastLat, _lastLon, _lastHeading;

        protected override void Build(RectTransform root)
        {
            // DemoBasemap's own hatch image is almost fully transparent (only faint diagonal
            // lines carry alpha, per its sprite generation) — it was never the solid color the
            // user actually sees as "the map"; THIS screen-level panel, sitting behind it, is.
            // Recoloring DemoBasemap alone left the visible map exactly as navy as before.
            UIFactory.Panel(root, UITheme.TerrainGreen);

            _db = ServiceLocator.Get<JsonDatabase>();
            var waypoints = _db.Route.Waypoints;
            if (waypoints.Count == 0)
            {
                UIFactory.Label(root, Loc.T("map.route_unavailable"), UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center);
                return;
            }

            _landmarksByDistance = new List<LandmarkData>(_db.Landmarks);
            _landmarksByDistance.Sort((a, b) => a.CumulativeDistanceMeters.CompareTo(b.CumulativeDistanceMeters));

            var bounds = ComputeBounds(waypoints);
            double centerLon = (bounds.xMin + bounds.xMax) * 0.5;
            double centerLat = (bounds.yMin + bounds.yMax) * 0.5;

            _mapView = MapView.Create(root, centerLon, centerLat, bounds);
            DemoBasemap.Create(_mapView, bounds);
            TileBasemap.Create(_mapView);
            RouteOverlay.Create(_mapView, waypoints);
            PoiMarkerLayer.Create(_mapView, _db.Landmarks, _db.Route.TotalDistanceMeters, () => ServiceLocator.Get<UIRoot>().OverlayContainer);
            _userMarker = UserMarker.Create(_mapView);
            _userMarker.SetAccuracyMeters(8f);

            _mapView.SetZoom(16f);
            _mapView.CenterOn(waypoints[0].Longitude, waypoints[0].Latitude);

            BuildTopPill(root);
            BuildRightRail(root);
            BuildBottomCard(root);
            BuildAttribution(root);

            _mapView.OnFollowMeChanged += RefreshRecenterVisual;
            RefreshRecenterVisual();

            _lastLat = waypoints[0].Latitude;
            _lastLon = waypoints[0].Longitude;

            _session = NavigationSession.Resolve();
            _session.Location.OnFixFiltered += OnLocationFix;

            RefreshSummary(_session.Progress.RemainingDistanceMeters);
            RefreshNextCard(_session.Progress.CumulativeDistanceMeters);
            _userMarker.SetPosition(_lastLon, _lastLat);
        }

        protected override void OnShown()
        {
            // Used to also call _session.Start() when Idle — opening the Map tab began walking
            // (the simulated trace and, in a device build, real GPS) with no user action, which is
            // half of the auto-advance bug in Docs/Draft1.md D11 follow-up. Starting now happens
            // only from the AR screen's NavigationDrawer; resuming a session that was already
            // active before a Pause is not a fresh start, so that half stays automatic here.
            if (_session.State == NavigationState.Paused) _session.Resume();
        }

        private static Rect ComputeBounds(IReadOnlyList<Waypoint> waypoints)
        {
            double minLon = double.MaxValue, maxLon = double.MinValue;
            double minLat = double.MaxValue, maxLat = double.MinValue;
            foreach (var w in waypoints)
            {
                if (w.Longitude < minLon) minLon = w.Longitude;
                if (w.Longitude > maxLon) maxLon = w.Longitude;
                if (w.Latitude < minLat) minLat = w.Latitude;
                if (w.Latitude > maxLat) maxLat = w.Latitude;
            }

            double lonMargin = (maxLon - minLon) * 0.15 + 0.002;
            double latMargin = (maxLat - minLat) * 0.15 + 0.002;
            return Rect.MinMaxRect(
                (float)(minLon - lonMargin), (float)(minLat - latMargin),
                (float)(maxLon + lonMargin), (float)(maxLat + latMargin));
        }

        // ---------------------------------------------------------------
        // Top pill
        // ---------------------------------------------------------------

        private void BuildTopPill(Transform parent)
        {
            var pillRt = UIFactory.CreateRect("TopPill", parent);
            pillRt.anchorMin = new Vector2(0f, 1f);
            pillRt.anchorMax = new Vector2(1f, 1f);
            pillRt.pivot = new Vector2(0.5f, 1f);
            pillRt.anchoredPosition = new Vector2(0f, -UITheme.SpaceM);
            pillRt.sizeDelta = new Vector2(-UITheme.SpaceM * 2f, 140f);

            UIFactory.RoundedShadow(pillRt, UITheme.RadiusCard);
            var bg = UIFactory.Card(pillRt, UITheme.Glass);
            var hlg = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceS, (int)UITheme.SpaceS, (int)UITheme.SpaceS);
            hlg.spacing = UITheme.SpaceS;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            var textRt = UIFactory.CreateRect("Text", bg.transform);
            textRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var vlg = textRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 2f;

            var titleRt = UIFactory.CreateRect("Title", textRt);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            var titleLabel = UIFactory.Label(titleRt, string.Empty, UITheme.LabelFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Bind(titleLabel, "map.title");

            var summaryRt = UIFactory.CreateRect("Summary", textRt);
            summaryRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            _summaryLabel = UIFactory.Label(summaryRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);

            var gearRt = UIFactory.CreateRect("Gear", bg.transform);
            gearRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
            var gearBtn = UIFactory.CircleButton(gearRt, 64f, () => ServiceLocator.Get<UIRoot>().SelectSettingsTab(), new Color(1f, 1f, 1f, 0.08f));
            UIFactory.CenteredIcon(gearBtn.transform, IconType.Gear, 30f);
        }

        private void RefreshSummary(double remainingMeters)
        {
            string dist = DistanceFormatter.FormatMeters(remainingMeters);
            int etaMinutes = EtaEstimate.Minutes(remainingMeters);
            _summaryLabel.text = Loc.T("map.summary_format", dist, Loc.T("progress.eta_minutes_format", etaMinutes));
        }

        // ---------------------------------------------------------------
        // Right rail
        // ---------------------------------------------------------------

        private void BuildRightRail(Transform parent)
        {
            var railRt = UIFactory.CreateRect("RightRail", parent);
            railRt.anchorMin = railRt.anchorMax = new Vector2(1f, 0.5f);
            railRt.pivot = new Vector2(1f, 0.5f);
            railRt.anchoredPosition = new Vector2(-UITheme.SpaceM, 40f);
            // A point-anchored rect with no explicit size defaults to 0x0 — see
            // ARNavigationScreen's left rail for the full explanation (same bug, found on the
            // same real device pass).
            UIFactory.SetSize(railRt, 84f, 3f * 84f + 2f * UITheme.SpaceS);

            var vlg = railRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = UITheme.SpaceS;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;

            var recenterSlot = UIFactory.CreateRect("RecenterSlot", railRt);
            var recenterLe = recenterSlot.gameObject.AddComponent<LayoutElement>();
            recenterLe.preferredWidth = 84f;
            recenterLe.preferredHeight = 84f;
            _recenterGlow = UIFactory.CircleGlow(recenterSlot, 84f, UITheme.Accent).gameObject;
            UIFactory.CircleShadow(recenterSlot, 84f);
            _recenterBtn = UIFactory.CircleButton(recenterSlot, 84f, OnRecenterTapped, UITheme.Surface);
            UIFactory.CenteredIcon(_recenterBtn.transform, IconType.Recenter, 36f);

            var tiltSlot = UIFactory.CreateRect("TiltSlot", railRt);
            var tiltLe = tiltSlot.gameObject.AddComponent<LayoutElement>();
            tiltLe.preferredWidth = 84f;
            tiltLe.preferredHeight = 84f;
            UIFactory.CircleShadow(tiltSlot, 84f);
            var tiltBtn = UIFactory.CircleButton(tiltSlot, 84f, OnTiltTapped, UITheme.Surface);
            _tiltLabel = UIFactory.Label(tiltBtn.transform, string.Empty, UITheme.CaptionFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            LocalizedLabel.Bind(_tiltLabel, "map.mode_2d");

            var northSlot = UIFactory.CreateRect("NorthSlot", railRt);
            var northLe = northSlot.gameObject.AddComponent<LayoutElement>();
            northLe.preferredWidth = 84f;
            northLe.preferredHeight = 84f;
            _northGlow = UIFactory.CircleGlow(northSlot, 84f, UITheme.Accent).gameObject;
            UIFactory.CircleShadow(northSlot, 84f);
            var northBtn = UIFactory.CircleButton(northSlot, 84f, OnNorthTapped, UITheme.Accent);
            _northBg = northBtn.GetComponent<Image>();
            UIFactory.CenteredIcon(northBtn.transform, IconType.North, 36f);
        }

        private void RefreshRecenterVisual()
        {
            var img = _recenterBtn.GetComponent<Image>();
            img.color = _mapView.FollowMe ? UITheme.Accent : UITheme.Surface;
            _recenterGlow.SetActive(_mapView.FollowMe);
        }

        private void OnRecenterTapped() => _mapView.CenterOn(_lastLon, _lastLat);

        private void OnTiltTapped()
        {
            bool tilted = !_mapView.Tilted;
            _mapView.SetTilted(tilted);
            _tiltLabel.text = string.Empty;
            LocalizedLabel.Bind(_tiltLabel, tilted ? "map.mode_3d" : "map.mode_2d");
        }

        private void OnNorthTapped()
        {
            _northLocked = !_northLocked;
            _northBg.color = _northLocked ? UITheme.Accent : UITheme.Surface;
            _northGlow.SetActive(_northLocked);
            ApplyHeadingMode();
        }

        private void ApplyHeadingMode()
        {
            // North-locked: map stays fixed, the chevron alone shows heading. Unlocked: the
            // whole WorldRoot rotates so the direction of travel points up, and the chevron
            // (a WorldRoot descendant) inherits that same rotation — zeroing its own local
            // contribution here is what keeps it from double-rotating, not a coincidence. The
            // sign on -heading is the one piece of this screen not visually verified against a
            // real device/Editor session (no way to render UI in this batch-mode setup) — if the
            // map ever spins opposite to actual travel direction in "rotate with heading" mode,
            // flip this sign; every other layer (route, POIs, marker) stays internally consistent
            // either way since they all inherit the same WorldRoot rotation.
            _mapView.SetHeadingRotationZ(_northLocked ? 0f : -(float)_lastHeading);
            _userMarker.SetHeadingDegrees(_northLocked ? (float)_lastHeading : 0f);
        }

        // ---------------------------------------------------------------
        // Bottom "Next" card
        // ---------------------------------------------------------------

        private void BuildBottomCard(Transform parent)
        {
            var cardRt = UIFactory.CreateRect("NextCard", parent);
            cardRt.anchorMin = new Vector2(0f, 0f);
            cardRt.anchorMax = new Vector2(1f, 0f);
            cardRt.pivot = new Vector2(0.5f, 0f);
            cardRt.anchoredPosition = new Vector2(0f, UITheme.SpaceM);
            cardRt.sizeDelta = new Vector2(-UITheme.SpaceM * 2f, 150f);

            UIFactory.RoundedShadow(cardRt, UITheme.RadiusCard);
            var bg = UIFactory.Card(cardRt, UITheme.Glass);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(OpenNextLandmarkPopup);

            var hlg = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset((int)UITheme.SpaceM, (int)UITheme.SpaceM, (int)UITheme.SpaceS, (int)UITheme.SpaceS);
            hlg.spacing = UITheme.SpaceM;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            var iconRt = UIFactory.CreateRect("Icon", bg.transform);
            iconRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 72f;
            UIFactory.CenteredIcon(iconRt, IconType.Gopuram, 48f, UITheme.Gold);

            var textRt = UIFactory.CreateRect("Text", bg.transform);
            textRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var vlg = textRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 2f;

            var nameRt = UIFactory.CreateRect("Name", textRt);
            nameRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            _nextNameLabel = UIFactory.Label(nameRt, string.Empty, UITheme.BodyFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            // nameRt's height is fixed at one line (44px) — a long landmark name ("Next:
            // Venkateshwara Swamy Padalu Mandapam") wraps by TMP's default and spills past that
            // fixed box into the distance label directly below it, confirmed on-device. Ellipsis
            // truncation instead, matching LandmarksScreen's own list-row description labels.
            _nextNameLabel.enableWordWrapping = false;
            _nextNameLabel.overflowMode = TextOverflowModes.Ellipsis;

            var distRt = UIFactory.CreateRect("Distance", textRt);
            distRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            _nextDistanceLabel = UIFactory.Label(distRt, string.Empty, UITheme.CaptionFontSize, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, UITheme.TextSecondary);

            var chevRt = UIFactory.CreateRect("Chevron", bg.transform);
            chevRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 32f;
            UIFactory.Label(chevRt, ">", UITheme.BodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center, UITheme.TextTertiary);
        }

        private LandmarkData _nextLandmark;

        private void RefreshNextCard(double cumulativeDistance)
        {
            _nextLandmark = null;
            foreach (var lm in _landmarksByDistance)
            {
                if (lm.CumulativeDistanceMeters > cumulativeDistance + 0.5)
                {
                    _nextLandmark = lm;
                    break;
                }
            }

            if (_nextLandmark == null)
            {
                _nextNameLabel.text = string.Empty;
                _nextDistanceLabel.text = string.Empty;
                return;
            }

            _nextNameLabel.text = Loc.T("map.next_format", _nextLandmark.Name);
            double ahead = _nextLandmark.CumulativeDistanceMeters - cumulativeDistance;
            _nextDistanceLabel.text = Loc.T("map.ahead_format", DistanceFormatter.FormatMeters(ahead));
        }

        private void OpenNextLandmarkPopup()
        {
            if (_nextLandmark == null) return;
            LandmarkPopup.Show(ServiceLocator.Get<UIRoot>().OverlayContainer, _nextLandmark);
        }

        // ---------------------------------------------------------------
        // Attribution (licence obligation — PLAN.md §08, ODbL)
        // ---------------------------------------------------------------

        private void BuildAttribution(Transform parent)
        {
            var rt = UIFactory.CreateRect("Attribution", parent);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 158f);
            rt.sizeDelta = new Vector2(-UITheme.SpaceM * 2f, 28f);

            var label = UIFactory.Label(rt, string.Empty, UITheme.CaptionFontSize * 0.75f, FontStyles.Normal, TextAlignmentOptions.MidlineRight, UITheme.TextTertiary);
            LocalizedLabel.Bind(label, "map.attribution");
        }

        // ---------------------------------------------------------------
        // Live position (NavigationSession — Scene 5)
        // ---------------------------------------------------------------

        /// <summary>Marker uses the route-snapped position (Progress.Latitude/Longitude), not
        /// the raw filtered fix — the corridor has no alternative path, so snapping keeps the
        /// marker glued to the path instead of drifting sideways with GPS noise (PLAN.md §08
        /// step 3). Heading stays unsnapped since direction doesn't need it. NavigationSession
        /// feeds RouteProgressTracker synchronously inside HandleFix before this fires (it
        /// subscribed to Location.OnFixFiltered first, in its own constructor), so Progress's
        /// properties are already current by the time this handler reads them.</summary>
        private void OnLocationFix(double rawLat, double rawLon, float headingDeg, float accuracyMeters)
        {
            double lat = _session.Progress.Latitude;
            double lon = _session.Progress.Longitude;
            _lastLat = lat;
            _lastLon = lon;
            _lastHeading = headingDeg;

            _userMarker.SetPosition(lon, lat);
            _userMarker.SetAccuracyMeters(accuracyMeters);
            ApplyHeadingMode();

            if (_mapView.FollowMe) _mapView.CenterOn(lon, lat);

            RefreshSummary(_session.Progress.RemainingDistanceMeters);
            RefreshNextCard(_session.Progress.CumulativeDistanceMeters);
        }
    }
}
