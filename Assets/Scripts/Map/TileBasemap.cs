using System.Collections;
using System.Collections.Generic;
using AlipiriAR.UI;
using AlipiriAR.Utilities;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AlipiriAR.Map
{
    /// <summary>
    /// Real map raster tiles, drawn as a sibling directly above DemoBasemap, which stays in
    /// place underneath as the fallback ground. Two sources, tried in order per tile:
    /// (1) offline tiles baked once into StreamingAssets/Tiles/{z}/{x}/{y}.png for the Alipiri
    /// Mettu → Tirumala corridor (ODbL — attribution is MapScreen's existing "map.attribution"
    /// label, unchanged by this); (2) GoogleTileSession's live Map Tiles API fetch, for
    /// wherever the offline bake doesn't cover or hasn't been done yet. Only if both miss —
    /// no network and outside the baked AOI — does the green DemoBasemap terrain show through.
    ///
    /// Each tile is positioned by projecting its own NW/SE corner lon/lat through
    /// MapView.WorldPositionRelative — the same zoom-18 canonical pixel space every other layer
    /// (route, POIs, user marker) already uses — so a tile's footprint is correct regardless of
    /// which integer OSM zoom it was baked at; MapView's uniform WorldRoot scale does the rest.
    /// Only tiles intersecting the current viewport (+ margin) are ever instantiated — the full
    /// AOI is ~1,360 tiles across z14–18, far more than should ever be live as UI Images at once.
    /// </summary>
    public class TileBasemap : MonoBehaviour
    {
        private const int MinBakedZoom = 14;
        private const int MaxBakedZoom = 18;
        private const float ViewportMarginFactor = 0.6f; // load a bit past the visible edge
        private const int MaxCachedTextures = 220;

        private MapView _map;
        private RectTransform _root;
        private readonly Dictionary<(int z, int x, int y), RawImage> _liveTiles = new();
        private readonly Dictionary<(int z, int x, int y), Texture2D> _textureCache = new();
        private readonly Queue<(int z, int x, int y)> _textureCacheOrder = new();
        private readonly HashSet<(int z, int x, int y)> _inFlight = new();

        public static TileBasemap Create(MapView map)
        {
            var go = new GameObject("TileBasemap", typeof(RectTransform));
            var tb = go.AddComponent<TileBasemap>();
            tb.Build(map);
            return tb;
        }

        private void Build(MapView map)
        {
            _map = map;
            _root = (RectTransform)transform;
            _root.SetParent(map.WorldRoot, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = Vector2.zero;
            _root.anchoredPosition = Vector2.zero;

            map.OnZoomChanged += RefreshTiles;
            map.OnUserPanned += RefreshTiles;
            RefreshTiles();
        }

        private void OnDestroy()
        {
            if (_map != null)
            {
                _map.OnZoomChanged -= RefreshTiles;
                _map.OnUserPanned -= RefreshTiles;
            }
        }

        private int PickZoom() => Mathf.Clamp(Mathf.RoundToInt(_map.Zoom), MinBakedZoom, MaxBakedZoom);

        private void RefreshTiles()
        {
            int z = PickZoom();
            Rect visible = _map.VisibleWorldPxRect();
            Vector2 paddedSize = visible.size * (1f + ViewportMarginFactor);
            Rect padded = new Rect(visible.center - paddedSize * 0.5f, paddedSize);

            var (lonA, latA) = _map.LonLatAtWorldPositionRelative(new Vector2(padded.xMin, padded.yMin));
            var (lonB, latB) = _map.LonLatAtWorldPositionRelative(new Vector2(padded.xMax, padded.yMax));

            var (tx0, ty0) = GeoMath.LonLatToTile(System.Math.Min(lonA, lonB), System.Math.Max(latA, latB), z);
            var (tx1, ty1) = GeoMath.LonLatToTile(System.Math.Max(lonA, lonB), System.Math.Min(latA, latB), z);

            int xlo = Mathf.Min(tx0, tx1), xhi = Mathf.Max(tx0, tx1);
            int ylo = Mathf.Min(ty0, ty1), yhi = Mathf.Max(ty0, ty1);

            var wanted = new HashSet<(int, int, int)>();
            for (int x = xlo; x <= xhi; x++)
                for (int y = ylo; y <= yhi; y++)
                    wanted.Add((z, x, y));

            var toRemove = new List<(int, int, int)>();
            foreach (var key in _liveTiles.Keys)
                if (!wanted.Contains(key)) toRemove.Add(key);
            foreach (var key in toRemove)
            {
                Destroy(_liveTiles[key].gameObject);
                _liveTiles.Remove(key);
            }

            foreach (var key in wanted)
            {
                if (_liveTiles.ContainsKey(key) || _inFlight.Contains(key)) continue;
                if (_textureCache.TryGetValue(key, out var cachedTex))
                    PlaceTile(key, cachedTex);
                else
                    StartCoroutine(LoadTile(key));
            }
        }

        private IEnumerator LoadTile((int z, int x, int y) key)
        {
            _inFlight.Add(key);
            byte[] bytes = null;
            yield return StreamingAssetsLoader.LoadBytes($"Tiles/{key.z}/{key.x}/{key.y}.png", b => bytes = b);

            // Missing baked tile — outside the baked AOI, or one of the rare bake failures.
            // Fall back to a live fetch from Google's Map Tiles API (GoogleTileSession) before
            // giving up; if that's unavailable too (no key, no network, key not authorized),
            // DemoBasemap still shows through underneath exactly as before this fallback existed.
            if (bytes == null || bytes.Length == 0)
            {
                yield return GoogleTileSession.EnsureSession();
                if (this == null) yield break; // screen closed while the session request was in flight
                if (GoogleTileSession.IsAvailable)
                {
                    yield return FetchOnlineTile(key, b => bytes = b);
                }
            }

            _inFlight.Remove(key);

            if (bytes == null || bytes.Length == 0) yield break;
            if (this == null) yield break; // screen closed while the request was in flight

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            if (!ImageConversion.LoadImage(tex, bytes))
            {
                Destroy(tex);
                yield break;
            }

            CacheTexture(key, tex);
            // The zoom band this was requested for may no longer be current by the time a slow
            // load resolves (a fast pinch-zoom) — placing it anyway is still geometrically
            // correct, since its footprint comes from its own lon/lat corners, not from "being
            // the current band". RefreshTiles will drop it on the next pan/zoom if it's stale.
            PlaceTile(key, tex);
        }

        /// <summary>Raw-bytes fetch, same as StreamingAssetsLoader.LoadBytes returns for the
        /// offline path — LoadTile decodes both through the one ImageConversion.LoadImage call
        /// above, rather than branching the texture-creation code path by source.</summary>
        private static IEnumerator FetchOnlineTile((int z, int x, int y) key, System.Action<byte[]> onLoaded)
        {
            using var request = UnityWebRequest.Get(GoogleTileSession.TileUrl(key.z, key.x, key.y));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onLoaded(null);
                yield break;
            }

            onLoaded(request.downloadHandler.data);
        }

        private void CacheTexture((int z, int x, int y) key, Texture2D tex)
        {
            _textureCache[key] = tex;
            _textureCacheOrder.Enqueue(key);
            while (_textureCacheOrder.Count > MaxCachedTextures)
            {
                var oldest = _textureCacheOrder.Dequeue();
                if (oldest.Equals(key)) continue; // keep what we just added
                if (_liveTiles.ContainsKey(oldest)) continue; // still on screen — don't evict
                if (_textureCache.TryGetValue(oldest, out var oldTex))
                {
                    _textureCache.Remove(oldest);
                    Destroy(oldTex);
                }
            }
        }

        private void PlaceTile((int z, int x, int y) key, Texture2D tex)
        {
            if (_liveTiles.ContainsKey(key)) return;

            var (lonNW, latNW) = GeoMath.TileNorthWest(key.x, key.y, key.z);
            var (lonSE, latSE) = GeoMath.TileNorthWest(key.x + 1, key.y + 1, key.z);

            Vector2 a = _map.WorldPositionRelative(lonNW, latNW);
            Vector2 b = _map.WorldPositionRelative(lonSE, latSE);

            var rt = UIFactory.CreateRect($"Tile {key.z}_{key.x}_{key.y}", _root);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));

            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            raw.color = new Color(1f, 1f, 1f, 0f);
            StartCoroutine(FadeInTile(raw));

            _liveTiles[key] = raw;
        }

        private static IEnumerator FadeInTile(RawImage raw)
        {
            const float duration = 0.18f;
            if (UITween.ReducedMotion)
            {
                raw.color = Color.white;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                if (raw == null) yield break;
                t += Time.unscaledDeltaTime;
                var c = raw.color;
                c.a = Mathf.Clamp01(t / duration);
                raw.color = c;
                yield return null;
            }
            if (raw != null) raw.color = Color.white;
        }
    }
}
