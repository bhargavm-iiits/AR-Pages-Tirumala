using AlipiriAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.Map
{
    /// <summary>
    /// Deep navy ground + a graticule that densifies with zoom (PLAN.md §08) — drawn directly in
    /// World-space through MapView's own projection (thin Image lines, not a raster), so it can
    /// never drift from the route or markers at any zoom, and never needs the megapixel-sized
    /// single texture a literal per-bbox bake would require at high zoom (running the numbers:
    /// the ~1.2 km × 7.3 km route bbox at z18 is ~2,000 × 12,500 px — ~100 MB as one RGBA32
    /// texture). A real IBasemapSource tile abstraction — z/x/y-addressed, LRU-cached — is what
    /// PLAN.md originally sketched for this, and is the right shape once real tile imagery
    /// exists; building it now, with only a procedural source and no second implementation to
    /// justify the seam, would be speculative.
    /// </summary>
    public class DemoBasemap : MonoBehaviour
    {
        private static readonly float[] ZoomBandThresholds = { 14f, 15f, 16f, 17f, 18f };
        private static readonly double[] GraticuleSpacingDeg = { 0.01, 0.005, 0.002, 0.001, 0.0005 };

        private MapView _map;
        private Rect _boundsLatLon; // x/xMin/xMax = longitude, y/yMin/yMax = latitude
        private RectTransform _graticuleRoot;
        private int _lastBand = -1;
        private static Sprite _hatchSpriteCache;

        public static DemoBasemap Create(MapView map, Rect boundsLatLon)
        {
            var go = new GameObject("DemoBasemap", typeof(RectTransform));
            var basemap = go.AddComponent<DemoBasemap>();
            basemap.Build(map, boundsLatLon);
            return basemap;
        }

        private void Build(MapView map, Rect boundsLatLon)
        {
            _map = map;
            _boundsLatLon = boundsLatLon;

            var rt = (RectTransform)transform;
            rt.SetParent(map.WorldRoot, false);
            rt.SetAsFirstSibling();

            BuildGroundHatch(rt);

            _graticuleRoot = UIFactory.CreateRect("Graticule", rt);
            map.OnZoomChanged += RefreshGraticuleIfBandChanged;
            RefreshGraticuleIfBandChanged();
        }

        private void BuildGroundHatch(Transform parent)
        {
            Vector2 a = _map.WorldPositionRelative(_boundsLatLon.xMin, _boundsLatLon.yMin);
            Vector2 b = _map.WorldPositionRelative(_boundsLatLon.xMax, _boundsLatLon.yMax);

            var rt = UIFactory.CreateRect("Hatch", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            rt.SetAsFirstSibling();

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = HatchSprite();
            img.type = Image.Type.Tiled;
            img.color = UITheme.TerrainGreen;
        }

        private static Sprite HatchSprite()
        {
            if (_hatchSpriteCache != null) return _hatchSpriteCache;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int diag = (x + y) % 16;
                    float a = diag < 2 ? 0.10f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            _hatchSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return _hatchSpriteCache;
        }

        private void RefreshGraticuleIfBandChanged()
        {
            int band = 0;
            for (int i = ZoomBandThresholds.Length - 1; i >= 0; i--)
            {
                if (_map.Zoom >= ZoomBandThresholds[i]) { band = i; break; }
            }
            if (band == _lastBand) return;
            _lastBand = band;
            BuildGraticule(GraticuleSpacingDeg[band]);
        }

        private void BuildGraticule(double spacingDeg)
        {
            foreach (Transform child in _graticuleRoot) Destroy(child.gameObject);

            double startLat = System.Math.Floor(_boundsLatLon.yMin / spacingDeg) * spacingDeg;
            for (double lat = startLat; lat <= _boundsLatLon.yMax; lat += spacingDeg)
            {
                if (lat < _boundsLatLon.yMin) continue;
                Vector2 p0 = _map.WorldPositionRelative(_boundsLatLon.xMin, lat);
                Vector2 p1 = _map.WorldPositionRelative(_boundsLatLon.xMax, lat);
                BuildLine(p0, p1);
            }

            double startLon = System.Math.Floor(_boundsLatLon.xMin / spacingDeg) * spacingDeg;
            for (double lon = startLon; lon <= _boundsLatLon.xMax; lon += spacingDeg)
            {
                if (lon < _boundsLatLon.xMin) continue;
                Vector2 p0 = _map.WorldPositionRelative(lon, _boundsLatLon.yMin);
                Vector2 p1 = _map.WorldPositionRelative(lon, _boundsLatLon.yMax);
                BuildLine(p0, p1);
            }
        }

        private void BuildLine(Vector2 a, Vector2 b)
        {
            var rt = UIFactory.CreateRect("Grid", _graticuleRoot);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Vector2 mid = (a + b) * 0.5f;
            float length = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(length, 2f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.035f);
        }
    }
}
