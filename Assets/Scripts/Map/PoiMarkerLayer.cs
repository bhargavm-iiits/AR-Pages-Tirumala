using System;
using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.Map
{
    /// <summary>41 landmark pins (PLAN.md §02/§08). All 41 GameObjects always exist — cheap at
    /// this count — but below DeclutterZoom they collapse to small dots rather than full tap
    /// targets, since every landmark in this dataset shares the same priority (§04: no field
    /// distinguishes "major" from "minor"), so a real cluster/priority system has nothing to key
    /// off yet. Tapping a full pin opens the same LandmarkPopup the Landmarks tab uses.</summary>
    public class PoiMarkerLayer : MonoBehaviour
    {
        private const float DeclutterZoom = 15.5f;

        private readonly List<(LandmarkData landmark, RectTransform pinRt, RectTransform dotRt)> _markers = new();
        private MapView _map;
        private Func<RectTransform> _overlayContainerProvider;

        public static PoiMarkerLayer Create(MapView map, IReadOnlyList<LandmarkData> landmarks, Func<RectTransform> overlayContainerProvider)
        {
            var go = new GameObject("PoiMarkerLayer", typeof(RectTransform));
            var layer = go.AddComponent<PoiMarkerLayer>();
            layer.Build(map, landmarks, overlayContainerProvider);
            return layer;
        }

        private void Build(MapView map, IReadOnlyList<LandmarkData> landmarks, Func<RectTransform> overlayContainerProvider)
        {
            _map = map;
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

            var dotRt = UIFactory.CreateRect($"Dot_{landmark.Id}", parent);
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = pos;
            UIFactory.SetSize(dotRt, 14f, 14f);
            var dotImg = dotRt.gameObject.AddComponent<Image>();
            dotImg.sprite = UIShapes.Circle();
            dotImg.color = LandmarkVisuals.TintFor(landmark.Type);

            // Thin white ring so the collapsed low-zoom dot still pops against green terrain or
            // real map tiles, matching the pin's own ring below.
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
            UIFactory.SetSize(pinRt, 56f, 56f);

            // Soft shadow drawn first (beneath everything) — the bit of depth is what keeps a
            // flat colored circle from reading as a sticker glued flat onto the map.
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
            UIFactory.SetSize(fillRt, 56f, 56f);
            var bg = fillRt.gameObject.AddComponent<Image>();
            bg.sprite = UIShapes.Circle();
            bg.color = Color.Lerp(LandmarkVisuals.TintFor(landmark.Type), UITheme.Ground, 0.5f);

            var ringRt = UIFactory.CreateRect("Ring", pinRt);
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(ringRt, 56f, 56f);
            var ringImg = ringRt.gameObject.AddComponent<Image>();
            ringImg.sprite = UIShapes.Ring(28, 3);
            ringImg.color = new Color(1f, 1f, 1f, 0.9f);
            ringImg.raycastTarget = false;

            var btn = pinRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OpenPopup(landmark));

            UIFactory.CenteredIcon(pinRt, LandmarkVisuals.IconFor(landmark.Type), 32f, Color.white);

            _markers.Add((landmark, pinRt, dotRt));
        }

        private void RefreshDeclutter()
        {
            bool showPins = _map.Zoom >= DeclutterZoom;
            float inverse = 1f / Mathf.Max(_map.ZoomScale, 0.0001f);

            foreach (var (_, pinRt, dotRt) in _markers)
            {
                pinRt.gameObject.SetActive(showPins);
                dotRt.gameObject.SetActive(!showPins);
                pinRt.localScale = Vector3.one * inverse;
                dotRt.localScale = Vector3.one * inverse;
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
