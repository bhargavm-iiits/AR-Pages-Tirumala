using System;
using AlipiriAR.UI;
using AlipiriAR.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace AlipiriAR.Map
{
    /// <summary>
    /// Pans/zooms a coordinate space, not a texture (PLAN.md §08). World-pixel origin is the
    /// route's centroid at ReferenceZoom, computed in double precision — every layer (basemap,
    /// route, POIs, user marker) converts lat/lon through WorldPosition(), so nothing can drift
    /// relative to anything else at any zoom. Reference zoom is pinned to MaxZoom so the demo
    /// basemap's baked graticule/hatch is never upscaled past its native resolution — only ever
    /// downscaled at lower zoom, which never looks bad.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        public const int ReferenceZoom = 18;
        public const float MinZoom = 14f;
        public const float MaxZoom = 18f;

        private RectTransform _viewport;
        private RectTransform _world;
        private double _originX, _originY;
        private float _zoom = 16.5f;
        private Vector2 _panWorldPx; // World-space anchoredPosition target, in ReferenceZoom pixels
        private Rect _boundsWorldPx; // Clamp range for panning, in ReferenceZoom pixels
        private bool _followMe = true;
        private bool _tilted;
        private float _headingRotationZ;

        private bool _dragging;
        private Vector2 _dragLastScreenPos;
        private float _lastPinchDistance = -1f;
        private float _lastTapTime = -10f;
        private Vector2 _lastTapPos;

        public event Action OnUserPanned;
        public event Action OnZoomChanged;
        public event Action OnFollowMeChanged;

        public RectTransform WorldRoot => _world;
        public RectTransform Viewport => _viewport;
        public float Zoom => _zoom;
        public float ZoomScale => Mathf.Pow(2f, _zoom - ReferenceZoom);
        public bool FollowMe => _followMe;
        public bool Tilted => _tilted;

        public static MapView Create(RectTransform parent, double originLon, double originLat, Rect boundsLatLonMargin)
        {
            var go = new GameObject("MapView", typeof(RectTransform));
            var view = go.AddComponent<MapView>();
            view.Build(parent, originLon, originLat, boundsLatLonMargin);
            return view;
        }

        private void Build(RectTransform parent, double originLon, double originLat, Rect boundsLatLon)
        {
            _viewport = (RectTransform)transform;
            _viewport.SetParent(parent, false);
            UIFactory.StretchFill(_viewport);
            gameObject.AddComponent<RectMask2D>();
            var raycastBg = gameObject.AddComponent<Image>();
            raycastBg.color = new Color(0f, 0f, 0f, 0f);

            var (ox, oy) = GeoMath.LonLatToWorldPixelD(originLon, originLat, ReferenceZoom);
            _originX = ox;
            _originY = oy;

            var worldGo = new GameObject("World", typeof(RectTransform));
            _world = (RectTransform)worldGo.transform;
            _world.SetParent(_viewport, false);
            _world.anchorMin = _world.anchorMax = new Vector2(0.5f, 0.5f);
            _world.pivot = new Vector2(0.5f, 0.5f);
            _world.sizeDelta = Vector2.zero;

            // Pan bounds: project the two extreme corners of the lat/lon box into World-space.
            Vector2 a = WorldPositionRelative(boundsLatLon.xMin, boundsLatLon.yMin);
            Vector2 b = WorldPositionRelative(boundsLatLon.xMax, boundsLatLon.yMax);
            _boundsWorldPx = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

            _panWorldPx = Vector2.zero;
            ApplyTransform();
        }

        /// <summary>Lon/lat → local position inside World, relative to the origin, in
        /// ReferenceZoom pixels (pre-zoom-scale; World's own localScale applies the zoom).</summary>
        public Vector2 WorldPositionRelative(double lon, double lat)
        {
            var (x, y) = GeoMath.LonLatToWorldPixelD(lon, lat, ReferenceZoom);
            // Mercator Y grows south; RectTransform Y grows up — flip.
            return new Vector2((float)(x - _originX), -(float)(y - _originY));
        }

        private void Update()
        {
            PollGestures();
        }

        private void PollGestures()
        {
            var touch = Touchscreen.current;
            TouchControl activeTouch0 = null, activeTouch1 = null;
            int activeTouchCount = 0;

            if (touch != null)
            {
                // touch.touches is a fixed-size slot array (usually 10 entries on every real
                // touchscreen) representing every possible contact, not the fingers currently
                // down — .Count is >= 2 at all times regardless of actual touches. Only
                // press.isPressed tells a live finger from an idle slot; without this filter
                // gestures always fell into the 2-finger branch below, on every device, even
                // untouched, making the map dead to single-finger pan/tap.
                foreach (var t in touch.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (activeTouchCount == 0) activeTouch0 = t;
                    else if (activeTouchCount == 1) activeTouch1 = t;
                    activeTouchCount++;
                }
            }

            if (activeTouchCount >= 2)
            {
                HandlePinch(activeTouch0, activeTouch1);
                return;
            }

            Vector2? pointerScreenPos = null;
            bool pointerDown = false, pointerUp = false, pointerHeld = false;

            if (activeTouchCount == 1)
            {
                pointerScreenPos = activeTouch0.position.ReadValue();
                pointerDown = activeTouch0.press.wasPressedThisFrame;
                pointerUp = activeTouch0.press.wasReleasedThisFrame;
                pointerHeld = activeTouch0.press.isPressed;
            }
            else if (Mouse.current != null)
            {
                pointerScreenPos = Mouse.current.position.ReadValue();
                pointerDown = Mouse.current.leftButton.wasPressedThisFrame;
                pointerUp = Mouse.current.leftButton.wasReleasedThisFrame;
                pointerHeld = Mouse.current.leftButton.isPressed;

                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && RectTransformUtility.RectangleContainsScreenPoint(_viewport, pointerScreenPos.Value))
                    SetZoom(_zoom + scroll * 0.001f);
            }

            if (pointerScreenPos == null) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_viewport, pointerScreenPos.Value) && !_dragging) return;

            if (pointerDown)
            {
                _dragging = true;
                _dragLastScreenPos = pointerScreenPos.Value;

                if (Time.unscaledTime - _lastTapTime < 0.3f && Vector2.Distance(pointerScreenPos.Value, _lastTapPos) < 40f)
                {
                    SetZoom(_zoom + 1f);
                    _lastTapTime = -10f;
                }
                else
                {
                    _lastTapTime = Time.unscaledTime;
                    _lastTapPos = pointerScreenPos.Value;
                }
            }
            else if (pointerHeld && _dragging)
            {
                Vector2 delta = pointerScreenPos.Value - _dragLastScreenPos;
                if (delta.sqrMagnitude > 0.01f)
                {
                    Pan(delta);
                    _dragLastScreenPos = pointerScreenPos.Value;
                }
            }
            else if (pointerUp)
            {
                _dragging = false;
            }

            _lastPinchDistance = -1f;
        }

        private void HandlePinch(TouchControl touch0, TouchControl touch1)
        {
            _dragging = false;
            Vector2 p0 = touch0.position.ReadValue();
            Vector2 p1 = touch1.position.ReadValue();
            float dist = Vector2.Distance(p0, p1);
            Vector2 mid = (p0 + p1) * 0.5f;

            if (_lastPinchDistance > 0f)
            {
                float ratio = dist / Mathf.Max(_lastPinchDistance, 1f);
                SetZoom(_zoom + Mathf.Log(ratio, 2f));
            }
            else
            {
                _dragLastScreenPos = mid;
            }

            Vector2 midDelta = mid - _dragLastScreenPos;
            if (_lastPinchDistance > 0f) Pan(midDelta);
            _dragLastScreenPos = mid;
            _lastPinchDistance = dist;
        }

        private void Pan(Vector2 screenDelta)
        {
            _followMe = false;
            OnFollowMeChanged?.Invoke();

            // Screen-space canvas is at CanvasScaler-scaled pixels; convert via the canvas' own
            // scale factor so drag distance feels 1:1 regardless of device resolution.
            var canvas = GetComponentInParent<Canvas>();
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 worldDelta = (screenDelta / Mathf.Max(canvasScale, 0.01f)) / ZoomScale;
            _panWorldPx += new Vector2(worldDelta.x, worldDelta.y);
            ClampPan();
            ApplyTransform();
            OnUserPanned?.Invoke();
        }

        private void ClampPan()
        {
            _panWorldPx.x = Mathf.Clamp(_panWorldPx.x, -_boundsWorldPx.xMax, -_boundsWorldPx.xMin);
            _panWorldPx.y = Mathf.Clamp(_panWorldPx.y, -_boundsWorldPx.yMax, -_boundsWorldPx.yMin);
        }

        public void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            ClampPan();
            ApplyTransform();
            OnZoomChanged?.Invoke();
        }

        public void CenterOn(double lon, double lat, bool resumeFollow = true)
        {
            Vector2 pos = WorldPositionRelative(lon, lat);
            _panWorldPx = -pos;
            ClampPan();
            if (resumeFollow)
            {
                _followMe = true;
                OnFollowMeChanged?.Invoke();
            }
            ApplyTransform();
        }

        public void SetTilted(bool tilted)
        {
            _tilted = tilted;
            ApplyTransform();
        }

        /// <summary>Rotates the flat map so the given screen-up heading (degrees, 0 = north)
        /// points away from the camera — the Map screen's North toggle in "rotate-with-heading"
        /// mode. Pass 0 to return to north-locked.</summary>
        public void SetHeadingRotationZ(float headingDeg)
        {
            _headingRotationZ = headingDeg;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            _world.localScale = Vector3.one * ZoomScale;
            _world.anchoredPosition = _panWorldPx * ZoomScale;

            Quaternion tilt = _tilted ? Quaternion.Euler(50f, 0f, 0f) : Quaternion.identity;
            Quaternion heading = Quaternion.Euler(0f, 0f, _headingRotationZ);
            _world.localRotation = tilt * heading;
        }

        /// <summary>Converts a lon/lat straight to a screen-space anchored position inside
        /// Viewport (for the bottom "Next" card's off-map calculations, etc.) — not needed by
        /// markers, which parent directly under WorldRoot instead.</summary>
        public Vector2 ViewportPosition(double lon, double lat)
        {
            Vector2 worldPos = WorldPositionRelative(lon, lat);
            return (worldPos + _panWorldPx) * ZoomScale;
        }

        /// <summary>Inverse of WorldPositionRelative — the lon/lat under a given ReferenceZoom
        /// world-pixel offset. Used by TileBasemap to work out which OSM tiles are actually on
        /// screen; not precision-sensitive the way marker placement is, so the float overload
        /// of GeoMath's Mercator math is fine here.</summary>
        public (double lon, double lat) LonLatAtWorldPositionRelative(Vector2 worldPxRelative)
        {
            double x = _originX + worldPxRelative.x;
            double y = _originY - worldPxRelative.y; // undo WorldPositionRelative's Mercator Y-flip
            return GeoMath.WorldPixelToLonLat(new Vector2((float)x, (float)y), ReferenceZoom);
        }

        /// <summary>The region of the map actually on screen right now, in the same
        /// ReferenceZoom world-pixel-relative-to-origin space WorldPositionRelative returns.
        /// Ignores tilt rotation — a margin in the caller covers the sliver a tilted viewport
        /// exposes at its screen edges.</summary>
        public Rect VisibleWorldPxRect()
        {
            float halfW = _viewport.rect.width * 0.5f / ZoomScale;
            float halfH = _viewport.rect.height * 0.5f / ZoomScale;
            return Rect.MinMaxRect(-halfW - _panWorldPx.x, -halfH - _panWorldPx.y, halfW - _panWorldPx.x, halfH - _panWorldPx.y);
        }
    }
}
