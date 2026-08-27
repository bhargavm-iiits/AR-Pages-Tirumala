using System.Collections;
using AlipiriAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.Map
{
    /// <summary>Blinking chevron + accuracy halo (PLAN.md §02/§08). The halo lives directly in
    /// World-space so its radius scales with real ground metres as you zoom — an honest read on
    /// GPS accuracy "rather than lying about precision". The chevron puck counter-scales against
    /// MapView's zoom so it stays a constant, readable size on screen, like every real map app's
    /// location dot. TraceReplaySource (Scene 4) and the real LocationProvider (Scene 5) both
    /// drive this through the same three setters.</summary>
    public class UserMarker : MonoBehaviour
    {
        private RectTransform _rt;
        private RectTransform _puckRt;
        private RectTransform _chevronRt;
        private RectTransform _haloRt;
        private MapView _map;

        public static UserMarker Create(MapView map)
        {
            var go = new GameObject("UserMarker", typeof(RectTransform));
            var marker = go.AddComponent<UserMarker>();
            marker.Build(map);
            return marker;
        }

        private void Build(MapView map)
        {
            _map = map;
            _rt = (RectTransform)transform;
            _rt.SetParent(map.WorldRoot, false);
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.SetAsLastSibling();

            _haloRt = UIFactory.CreateRect("Halo", _rt);
            _haloRt.anchorMin = _haloRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(_haloRt, 60f, 60f);
            var haloImg = _haloRt.gameObject.AddComponent<Image>();
            haloImg.sprite = UIShapes.Circle();
            haloImg.color = new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.18f);

            _puckRt = UIFactory.CreateRect("Puck", _rt);
            _puckRt.anchorMin = _puckRt.anchorMax = new Vector2(0.5f, 0.5f);

            var ringRt = UIFactory.CreateRect("Ring", _puckRt);
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(ringRt, 56f, 56f);
            var ringImg = ringRt.gameObject.AddComponent<Image>();
            ringImg.sprite = UIShapes.Circle();
            ringImg.color = Color.white;

            var coreRt = UIFactory.CreateRect("Core", _puckRt);
            coreRt.anchorMin = coreRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(coreRt, 48f, 48f);
            var coreImg = coreRt.gameObject.AddComponent<Image>();
            coreImg.sprite = UIShapes.Circle();
            coreImg.color = UITheme.Accent;

            _chevronRt = UIFactory.CreateRect("Chevron", coreRt);
            _chevronRt.anchorMin = _chevronRt.anchorMax = new Vector2(0.5f, 0.5f);
            UIFactory.SetSize(_chevronRt, 30f, 30f);
            var chevronImg = _chevronRt.gameObject.AddComponent<Image>();
            chevronImg.sprite = IconGraphic.Get(IconType.North);
            chevronImg.color = Color.white;

            map.OnZoomChanged += RefreshPuckScale;
            RefreshPuckScale();

            StartCoroutine(BlinkRoutine(haloImg));
        }

        private void RefreshPuckScale()
        {
            float inverse = 1f / Mathf.Max(_map.ZoomScale, 0.0001f);
            _puckRt.localScale = Vector3.one * inverse;
        }

        public void SetPosition(double lon, double lat)
        {
            _rt.anchoredPosition = _map.WorldPositionRelative(lon, lat);
        }

        public void SetHeadingDegrees(float headingDeg)
        {
            _chevronRt.localRotation = Quaternion.Euler(0f, 0f, -headingDeg);
        }

        /// <summary>Halo radius in real ground metres — grows honestly with reported GPS
        /// accuracy, at MapView.ReferenceZoom's fixed ground resolution for the route's latitude.</summary>
        public void SetAccuracyMeters(float accuracyMeters)
        {
            const float metersPerWorldPxAtRefZoom = 156543.03392f * 0.972f / 262144f; // cos(~13.66°N), 2^18
            float haloDiameterPx = (accuracyMeters / metersPerWorldPxAtRefZoom) * 2f;
            UIFactory.SetSize(_haloRt, Mathf.Max(haloDiameterPx, 60f), Mathf.Max(haloDiameterPx, 60f));
        }

        private static IEnumerator BlinkRoutine(Image halo)
        {
            var wait = new WaitForSecondsRealtime(0.02f);
            while (true)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += 0.02f / 1.6f;
                    var c = halo.color;
                    c.a = Mathf.Lerp(0.10f, 0.28f, (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f);
                    halo.color = c;
                    yield return wait;
                }
            }
        }
    }
}
