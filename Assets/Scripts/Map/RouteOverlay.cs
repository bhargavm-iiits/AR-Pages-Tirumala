using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.Map
{
    /// <summary>Blue route polyline with vertex dots, amber-tinted where RouteBuilder had to
    /// bridge a survey gap in a straight line (PLAN.md §01/§08's "route approximate" notice).
    /// Route.Waypoints is the raw ~157-vertex chain (not Densify()'d — that's the AR trail's
    /// job in Scene 6), cheap enough to render every segment directly.
    ///
    /// Drawn as two passes — a wide, translucent white casing underneath, then the narrower
    /// colored core on top — the way every real map app renders a route. A single flat-color
    /// line with a same-width dot stamped at every one of ~165 source vertices (the previous
    /// version) reads as a dotted "connect the dots" trace rather than a road; the casing gives
    /// the core a soft light halo against the terrain, and drawing dots at both widths keeps
    /// the joints between segments smooth instead of visibly mitred.</summary>
    public class RouteOverlay : MonoBehaviour
    {
        private const float CasingWidth = 16f;
        private const float CoreWidth = 8f;
        private static readonly Color CasingColor = new(1f, 1f, 1f, 0.55f);

        public static RouteOverlay Create(MapView map, IReadOnlyList<Waypoint> waypoints)
        {
            var go = new GameObject("RouteOverlay", typeof(RectTransform));
            var overlay = go.AddComponent<RouteOverlay>();
            overlay.Build(map, waypoints);
            return overlay;
        }

        private void Build(MapView map, IReadOnlyList<Waypoint> waypoints)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(map.WorldRoot, false);

            DrawPass(rt, map, waypoints, CasingWidth, _ => CasingColor);
            DrawPass(rt, map, waypoints, CoreWidth, isBridged => isBridged ? UITheme.Warning : UITheme.Accent);
        }

        private static void DrawPass(Transform parent, MapView map, IReadOnlyList<Waypoint> waypoints, float width, System.Func<bool, Color> colorFor)
        {
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var a = waypoints[i];
                var b = waypoints[i + 1];
                Vector2 pa = map.WorldPositionRelative(a.Longitude, a.Latitude);
                Vector2 pb = map.WorldPositionRelative(b.Longitude, b.Latitude);
                BuildSegment(parent, pa, pb, colorFor(a.IsBridged || b.IsBridged), width);
            }

            foreach (var w in waypoints)
            {
                Vector2 p = map.WorldPositionRelative(w.Longitude, w.Latitude);
                BuildDot(parent, p, colorFor(w.IsBridged), width);
            }
        }

        private static void BuildSegment(Transform parent, Vector2 a, Vector2 b, Color color, float width)
        {
            var rt = UIFactory.CreateRect("Segment", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Vector2 mid = (a + b) * 0.5f;
            float length = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(length, width);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void BuildDot(Transform parent, Vector2 pos, Color color, float diameter)
        {
            var rt = UIFactory.CreateRect("Dot", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(diameter, diameter);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIShapes.Circle();
            img.color = color;
            img.raycastTarget = false;
        }
    }
}
