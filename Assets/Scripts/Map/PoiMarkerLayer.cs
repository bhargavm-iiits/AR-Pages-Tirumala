using System;
using System.Collections;
using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.UI;
using AlipiriAR.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.Map
{
    /// <summary>40 landmark pins (PLAN.md §02/§08). All 40 GameObjects always exist — cheap at
    /// this count — but below DeclutterZoom they collapse to small dots rather than full tap
    /// targets, since every landmark in this dataset shares the same priority (§04: no field
    /// distinguishes "major" from "minor"), so a real cluster/priority system has nothing to key
    /// off yet. Tapping a full pin opens the same LandmarkPopup the Landmarks tab uses.
    ///
    /// An earlier revision gave every one of the 40 a persistent name label above its pin — with
    /// landmarks routinely 20-300m apart and several dense clusters near both route ends, that
    /// produced exactly the "huge overlapping" it was reported as. Persistent labels are now
    /// reserved for the six landmarks in AvatarPortraits below, spaced 220m-1.1km apart along the
    /// route (nowhere near dense enough to collide at any zoom level pins are visible at) —
    /// matching the reference mockup's own choice to caption only the Dashavatara waypoints, not
    /// all 41. Every other landmark keeps its pin/dot and tap-to-open-popup, nothing else.
    ///
    /// Highlight treatment (Docs/Dashavatar/*.png, supplied 2026-08-27), rev. 2: a small point
    /// sits exactly on the route (centre-pivoted, unlike an ordinary pin's bottom-pivoted "tip" —
    /// see AvatarPoint's own doc for why that distinction matters and was actually a real bug in
    /// rev. 1), connected by a leader line to a rounded-rectangle callout card carrying the
    /// portrait on top and the avatar's name below it. Six of Docs/Dashavatar's eight supplied
    /// portraits have a matching landmark in this route's data — Matsya, Kurma, Varaha, Narasimha,
    /// Vamana, Krishna. Parashurama and Rama don't (this route has Balarama and Kalki in those
    /// sequence positions instead), so those two images are copied into StreamingAssets alongside
    /// the rest but left unwired; see AvatarPortraits' own doc.</summary>
    public class PoiMarkerLayer : MonoBehaviour
    {
        private const float DeclutterZoom = 15.5f;
        private const float NormalPinSize = 56f;

        /// <summary>The visible gold dot marking an avatar's exact position on the path — sized
        /// like the ordinary landmarks' own low-zoom dot (14px) family, just a little larger and
        /// gold, since Docs/Dashavatar's reference asked for this to read as "marking the steps,"
        /// i.e. matching the small on-path markers already there, not another big pin.</summary>
        private const float AvatarPointVisualSize = 20f;

        /// <summary>The tappable area centred on the same point — PLAN.md §07's 48dp minimum
        /// touch target, kept separate from AvatarPointVisualSize so the visual mark can stay
        /// small and precise while the tap target stays comfortable.</summary>
        private const float AvatarPointTapSize = 48f;

        private const float LeaderLineLength = 42f;
        private const float LeaderLineWidth = 3f;
        private const float CalloutWidth = 176f;
        private const float CalloutImageSize = 96f;

        /// <summary>Total steps the whole route is estimated at — set from RouteResult.
        /// TotalStepsEstimate at construction (Docs/update1.md §02 F-05: this used to be its own
        /// independently-hardcoded 3550 literal, duplicated across this file, ProgressScreen and
        /// ARNavigationScreen, with no shared source of truth).</summary>
        private int _totalStepsEstimate;

        /// <summary>Standard short epithet for each of the ten avatars of Vishnu — common
        /// knowledge across Vaishnava tradition, keyed by this dataset's exact landmark names
        /// (Assets/StreamingAssets/Database/landmarks.json). Not present here: Parashurama and
        /// Rama, whose statues this dataset doesn't carry (Balarama's stands in their sequence
        /// position along this particular stairway) — nothing to caption that doesn't exist.</summary>
        private static readonly Dictionary<string, string> AvatarSubtitles = new()
        {
            ["Mathsyavataram"] = "The Fish",
            ["Kurma Avataram"] = "The Tortoise",
            ["Varaha Avataram"] = "The Boar",
            ["Sri Narasimha Avataram"] = "The Man-Lion",
            ["Sri Vamana Avataram"] = "The Dwarf",
            ["Balarama Avataram"] = "The Mighty",
            ["Sri Krishna Avataram"] = "The Divine Statesman",
            ["Sri Kalki Avataram"] = "The Future Avatar",
        };

        /// <summary>StreamingAssets path for the six avatars this route's landmarks and
        /// Docs/Dashavatar's supplied images actually agree on. Deliberately a SUBSET of
        /// AvatarSubtitles above — Balarama and Kalki have no portrait, since no matching image
        /// exists, and (unlike AvatarSubtitles, display-only) this dict also decides which
        /// landmarks get the point+callout treatment at all. Nothing here fabricates a portrait
        /// for a landmark that doesn't have real image content behind it.</summary>
        private static readonly Dictionary<string, string> AvatarPortraits = new()
        {
            ["Mathsyavataram"] = "Images/Dashavatar/matsya.png",
            ["Kurma Avataram"] = "Images/Dashavatar/kurma.png",
            ["Varaha Avataram"] = "Images/Dashavatar/varaha.png",
            ["Sri Narasimha Avataram"] = "Images/Dashavatar/narasimha.png",
            ["Sri Vamana Avataram"] = "Images/Dashavatar/vamana.png",
            ["Sri Krishna Avataram"] = "Images/Dashavatar/krishna.png",
        };

        private readonly List<(RectTransform pinRt, RectTransform dotRt)> _plainMarkers = new();
        private readonly List<(Vector2 pos, RectTransform pointRt, RectTransform leaderRt, RectTransform calloutRt, float sideSign)> _highlightMarkers = new();
        private MapView _map;
        private Func<RectTransform> _overlayContainerProvider;
        private double _totalRouteDistanceMeters;
        private int _highlightCount;

        public static PoiMarkerLayer Create(MapView map, IReadOnlyList<LandmarkData> landmarks, double totalRouteDistanceMeters, int totalStepsEstimate, Func<RectTransform> overlayContainerProvider)
        {
            var go = new GameObject("PoiMarkerLayer", typeof(RectTransform));
            var layer = go.AddComponent<PoiMarkerLayer>();
            layer.Build(map, landmarks, totalRouteDistanceMeters, totalStepsEstimate, overlayContainerProvider);
            return layer;
        }

        private void Build(MapView map, IReadOnlyList<LandmarkData> landmarks, double totalRouteDistanceMeters, int totalStepsEstimate, Func<RectTransform> overlayContainerProvider)
        {
            _totalStepsEstimate = totalStepsEstimate;
            _map = map;
            _totalRouteDistanceMeters = totalRouteDistanceMeters;
            _overlayContainerProvider = overlayContainerProvider;

            var rt = (RectTransform)transform;
            rt.SetParent(map.WorldRoot, false);

            foreach (var landmark in landmarks)
                BuildMarker(rt, landmark);

            map.OnZoomChanged += RefreshDeclutter;
            RefreshDeclutter();
        }

        private void BuildMarker(Transform parent, LandmarkData landmark)
        {
            // The route-snapped position, not the raw surveyed one — a landmark set back even a
            // few metres from the drawn line reads as visibly "off the path" on this schematic
            // map, even though that offset is real and correctly drives AR placement/geofencing
            // elsewhere (LandmarkData.SnappedLatitude/Longitude, resolved once in JsonDatabase).
            Vector2 pos = _map.WorldPositionRelative(landmark.SnappedLongitude, landmark.SnappedLatitude);
            bool highlighted = AvatarPortraits.TryGetValue(landmark.Name, out string portraitPath);

            if (highlighted)
            {
                var pointRt = BuildAvatarPoint(parent, landmark, pos);

                // Alternates right/left in route order (Matsya first = right, Kurma = left, ...)
                // so six callouts along the route don't all stack in the same direction and
                // collide with each other or the point.
                float sideSign = _highlightCount % 2 == 0 ? 1f : -1f;
                _highlightCount++;

                var (leaderRt, calloutRt) = BuildLeaderAndCallout(parent, landmark, portraitPath, sideSign);
                _highlightMarkers.Add((pos, pointRt, leaderRt, calloutRt, sideSign));
                return;
            }

            var dotRt = UIFactory.CreateRect($"Dot_{landmark.Id}", parent);
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = pos;
            UIFactory.SetSize(dotRt, 14f, 14f);
            var dotImg = dotRt.gameObject.AddComponent<Image>();
            dotImg.sprite = UIShapes.Circle();
            dotImg.color = LandmarkVisuals.TintFor(landmark.Type);

            var dotRingRt = UIFactory.CreateRect("Ring", dotRt);
            dotRingRt.anchorMin = dotRingRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(dotRingRt, 14f, 14f);
            var dotRingImg = dotRingRt.gameObject.AddComponent<Image>();
            dotRingImg.sprite = UIShapes.Ring(7, 2);
            dotRingImg.color = new Color(1f, 1f, 1f, 0.9f);
            dotRingImg.raycastTarget = false;

            var pinRt = UIFactory.CreateRect($"Pin_{landmark.Id}", parent);
            pinRt.anchorMin = pinRt.anchorMax = new Vector2(0.5f, 1f);
            pinRt.pivot = new Vector2(0.5f, 0f);
            pinRt.anchoredPosition = pos;
            UIFactory.SetSize(pinRt, NormalPinSize, NormalPinSize);

            var shadowRt = UIFactory.CreateRect("Shadow", pinRt);
            shadowRt.anchorMin = shadowRt.anchorMax = new Vector2(0.5f, 0f);
            shadowRt.pivot = new Vector2(0.5f, 0.5f);
            shadowRt.anchoredPosition = new Vector2(0f, -1f);
            UIFactory.SetSize(shadowRt, 32f, 12f);
            var shadowImg = shadowRt.gameObject.AddComponent<Image>();
            shadowImg.sprite = UIShapes.Circle();
            shadowImg.color = new Color(0f, 0f, 0f, 0.28f);
            shadowImg.raycastTarget = false;

            // Fill is a plain child (not pinRt's own Image) so it draws after — i.e. on top of —
            // the shadow above; pinRt's own graphic would otherwise always draw beneath its
            // children regardless of add order, putting the shadow over the fill instead of under it.
            var fillRt = UIFactory.CreateRect("Fill", pinRt);
            fillRt.anchorMin = fillRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(fillRt, NormalPinSize, NormalPinSize);
            var bg = fillRt.gameObject.AddComponent<Image>();
            bg.sprite = UIShapes.Circle();
            bg.color = Color.Lerp(LandmarkVisuals.TintFor(landmark.Type), UITheme.Ground, 0.5f);

            var ringRt = UIFactory.CreateRect("Ring", pinRt);
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(ringRt, NormalPinSize, NormalPinSize);
            var ringImg = ringRt.gameObject.AddComponent<Image>();
            ringImg.sprite = UIShapes.Ring(28, 3);
            ringImg.color = new Color(1f, 1f, 1f, 0.9f);
            ringImg.raycastTarget = false;

            var btn = pinRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OpenPopup(landmark));

            UIFactory.CenteredIcon(pinRt, LandmarkVisuals.IconFor(landmark.Type), 32f, Color.white);

            _plainMarkers.Add((pinRt, dotRt));
        }

        /// <summary>The exact on-path marker for a highlighted landmark — centre-pivoted (anchor
        /// AND pivot both (0.5,0.5)) so `pos` (the route-snapped position) is the dot's true
        /// centre. Rev. 1 used an ordinary pin's bottom-pivot convention (anchor (0.5,1), pivot
        /// (0.5,0)) for this, which is correct for a pin shape with a pointed tip touching down —
        /// but this is a plain circle with no tip, so bottom-pivoting it put the circle's centre a
        /// full radius ABOVE the real path position, which is exactly the "not marked exactly on
        /// the path" symptom reported. A comfortably larger invisible Button sits behind the
        /// visible dot for a real tap target without inflating the dot itself.</summary>
        private RectTransform BuildAvatarPoint(Transform parent, LandmarkData landmark, Vector2 pos)
        {
            var tapRt = UIFactory.CreateRect($"Point_{landmark.Id}", parent);
            tapRt.anchorMin = tapRt.anchorMax = new Vector2(0.5f, 0.5f);
            tapRt.anchoredPosition = pos;
            UIFactory.SetSize(tapRt, AvatarPointTapSize, AvatarPointTapSize);
            var tapImg = tapRt.gameObject.AddComponent<Image>();
            tapImg.color = Color.clear; // invisible — exists only to give the Button a real hit area
            var btn = tapRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = tapImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OpenPopup(landmark));

            var dotRt = UIFactory.CreateRect("Dot", tapRt);
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(dotRt, AvatarPointVisualSize, AvatarPointVisualSize);
            var dotImg = dotRt.gameObject.AddComponent<Image>();
            dotImg.sprite = UIShapes.Circle();
            dotImg.color = UITheme.Gold;
            dotImg.raycastTarget = false;

            var ringRt = UIFactory.CreateRect("Ring", tapRt);
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(ringRt, AvatarPointVisualSize, AvatarPointVisualSize);
            var ringImg = ringRt.gameObject.AddComponent<Image>();
            ringImg.sprite = UIShapes.Ring(10, 2);
            ringImg.color = new Color(1f, 1f, 1f, 0.9f);
            ringImg.raycastTarget = false;

            return tapRt;
        }

        /// <summary>Point — leader line — callout card. sideSign is +1 (card runs right of the
        /// point) or -1 (runs left) — alternated per marker by the caller so six cards along the
        /// route don't collide. The card carries the portrait on top and the avatar's name below
        /// it, per the reference image, with an estimated step number as a small third line —
        /// ProgressScreen's own "~1,250 steps" figure is derived the same way (PLAN.md §03 — no
        /// per-step survey data exists, so this is always a labelled estimate). Pivots point back
        /// toward the on-path marker (0 for right side, 1 for left) so line and card both grow
        /// AWAY from the point as built, not back over it. Both RectTransforms are built at (0,0)
        /// here deliberately — RefreshDeclutter positions them every zoom change, the same reason
        /// every other marker piece in this file does.</summary>
        private (RectTransform leaderRt, RectTransform calloutRt) BuildLeaderAndCallout(Transform parent, LandmarkData landmark, string portraitPath, float sideSign)
        {
            bool isRight = sideSign > 0f;

            var leaderRt = UIFactory.CreateRect($"Leader_{landmark.Id}", parent);
            leaderRt.anchorMin = leaderRt.anchorMax = new Vector2(0.5f, 0.5f);
            leaderRt.pivot = isRight ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            UIFactory.SetSize(leaderRt, LeaderLineLength, LeaderLineWidth);
            var leaderImg = leaderRt.gameObject.AddComponent<Image>();
            leaderImg.color = UITheme.Gold;
            leaderImg.raycastTarget = false;

            var calloutRt = UIFactory.CreateRect($"Callout_{landmark.Id}", parent);
            calloutRt.anchorMin = calloutRt.anchorMax = new Vector2(0.5f, 0.5f);
            calloutRt.pivot = isRight ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            UIFactory.SetSize(calloutRt, CalloutWidth, 0f); // height driven by ContentSizeFitter below

            // Curved rectangle, not a pill — the box shape asked for.
            var bg = UIFactory.Panel(calloutRt, UITheme.Glass, 14f);
            bg.gameObject.name = "CalloutBg";

            var vlg = calloutRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 4f;
            var csf = calloutRt.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Image sits on top, per the reference — a rounded-square mask with the procedural
            // type icon as an immediate placeholder, swapped for the real portrait once (if) it
            // loads (LoadPortraitInto below). A missing/corrupt file just leaves the icon in
            // place — never fatal, matching PLAN.md §03's whole approach to optional content.
            var imageSlotRt = UIFactory.CreateRect("ImageSlot", calloutRt);
            // Both preferredWidth AND preferredHeight — the callout's VerticalLayoutGroup below
            // has childForceExpandWidth true, which stretches any child lacking an explicit
            // preferredWidth to the callout's full inner width instead of leaving it square (the
            // same layout gotcha ARNavigationScreen's own left-rail icons hit and documented).
            var imageSlotLe = imageSlotRt.gameObject.AddComponent<LayoutElement>();
            imageSlotLe.preferredWidth = CalloutImageSize;
            imageSlotLe.preferredHeight = CalloutImageSize;
            UIFactory.SetSize(imageSlotRt, CalloutImageSize, CalloutImageSize);
            var imageBg = imageSlotRt.gameObject.AddComponent<Image>();
            imageBg.sprite = UIShapes.RoundedRect(10);
            imageBg.type = Image.Type.Sliced;
            imageBg.color = Color.Lerp(LandmarkVisuals.TintFor(landmark.Type), UITheme.Ground, 0.5f);
            UIFactory.CenteredIcon(imageSlotRt, LandmarkVisuals.IconFor(landmark.Type), 40f, Color.white);
            StartCoroutine(LoadPortraitInto(imageSlotRt, portraitPath));

            bool hasSubtitle = AvatarSubtitles.TryGetValue(landmark.Name, out string subtitle);
            string nameText = hasSubtitle ? $"{landmark.Name.ToUpperInvariant()}\n({subtitle})" : landmark.Name.ToUpperInvariant();

            var nameRt = UIFactory.CreateRect("Name", calloutRt);
            var nameLabel = UIFactory.Label(nameRt, nameText, UITheme.CaptionFontSize,
                FontStyles.Bold, TextAlignmentOptions.Center, UITheme.TextPrimary);
            nameLabel.enableWordWrapping = true;
            nameRt.gameObject.AddComponent<LayoutElement>().preferredHeight = hasSubtitle ? 56f : 30f;

            int stepNumber = _totalRouteDistanceMeters > 0
                ? Mathf.RoundToInt(_totalStepsEstimate * (float)(landmark.CumulativeDistanceMeters / _totalRouteDistanceMeters))
                : 0;

            var stepRt = UIFactory.CreateRect("Step", calloutRt);
            stepRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            // "~" prefix matches ProgressScreen's own step figure — never presented as measured,
            // since no per-step survey data exists behind this route (PLAN.md §03).
            UIFactory.Label(stepRt, $"~Step {stepNumber:N0}", UITheme.CaptionFontSize * 0.8f,
                FontStyles.Normal, TextAlignmentOptions.Center, UITheme.Gold);

            var calloutBtn = calloutRt.gameObject.AddComponent<Button>();
            calloutBtn.targetGraphic = bg;
            calloutBtn.transition = Selectable.Transition.None;
            calloutBtn.onClick.AddListener(() => OpenPopup(landmark));

            return (leaderRt, calloutRt);
        }

        /// <summary>Crops the loaded portrait into the image slot's rounded-square via a Mask,
        /// rather than dropping the (already square-cropped, per the processing that produced
        /// these files) texture in as a plain Image — a Mask is what actually clips it to the
        /// slot's rounded corners; a plain Image would show square corners poking past them.
        /// Renders as a sibling added after the fallback icon built in BuildLeaderAndCallout, so
        /// it draws on top and fully covers that icon once (if) this succeeds.</summary>
        private IEnumerator LoadPortraitInto(RectTransform imageSlotRt, string relativePath)
        {
            Texture2D tex = null;
            yield return StreamingAssetsLoader.LoadTexture(relativePath, t => tex = t);
            if (tex == null || imageSlotRt == null) yield break;

            var maskRt = UIFactory.CreateRect("PhotoMask", imageSlotRt);
            UIFactory.StretchFill(maskRt);
            var maskImg = maskRt.gameObject.AddComponent<Image>();
            maskImg.sprite = UIShapes.RoundedRect(10);
            maskImg.type = Image.Type.Sliced;
            var mask = maskRt.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var photoRt = UIFactory.CreateRect("Photo", maskRt);
            UIFactory.StretchFill(photoRt);
            var photoImg = photoRt.gameObject.AddComponent<Image>();
            photoImg.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            photoImg.raycastTarget = false;
        }

        private void RefreshDeclutter()
        {
            bool showPins = _map.Zoom >= DeclutterZoom;
            float zoomScale = Mathf.Max(_map.ZoomScale, 0.0001f);
            float inverse = 1f / zoomScale;

            foreach (var (pinRt, dotRt) in _plainMarkers)
            {
                pinRt.gameObject.SetActive(showPins);
                dotRt.gameObject.SetActive(!showPins);
                pinRt.localScale = Vector3.one * inverse;
                dotRt.localScale = Vector3.one * inverse;
            }

            foreach (var (pos, pointRt, leaderRt, calloutRt, sideSign) in _highlightMarkers)
            {
                // The on-path point stays visible even below DeclutterZoom (six points, 220m+
                // apart, were never the clutter problem) — only the leader+callout, which needs
                // real screen room to read, waits for the same threshold as ordinary pins.
                pointRt.gameObject.SetActive(true);
                pointRt.localScale = Vector3.one * inverse;
                pointRt.anchoredPosition = pos;

                leaderRt.gameObject.SetActive(showPins);
                calloutRt.gameObject.SetActive(showPins);
                leaderRt.localScale = Vector3.one * inverse;
                calloutRt.localScale = Vector3.one * inverse;

                // Same zoom-compensation reasoning as every other piece here: anchoredPosition is
                // the landmark's real map position and correctly scales with zoom on its own; the
                // leader line's length and the small clearance off the point are pure screen-space
                // constants with no geographic meaning, so each has to be pre-divided by
                // zoomScale here, every zoom change, for the ancestor's scale-up to cancel out
                // into a constant on-screen length instead of one that grows with zoom. sideSign
                // flips the sign of every x offset so the whole line+card unit mirrors onto the
                // other side; both stay vertically centred on the point itself (y offset 0).
                float pointEdge = AvatarPointVisualSize * 0.5f * inverse;
                float leaderLength = LeaderLineLength * inverse;

                leaderRt.anchoredPosition = pos + new Vector2(sideSign * pointEdge, 0f);
                calloutRt.anchoredPosition = pos + new Vector2(sideSign * (pointEdge + leaderLength), 0f);
            }
        }

        private void OpenPopup(LandmarkData landmark)
        {
            var container = _overlayContainerProvider?.Invoke();
            if (container == null) return;
            LandmarkPopup.Show(container, landmark);
        }
    }
}
