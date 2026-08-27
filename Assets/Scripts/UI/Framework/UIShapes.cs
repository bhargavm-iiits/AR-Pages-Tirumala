using System.Collections.Generic;
using UnityEngine;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Generates every rounded-rect, pill, ring and chevron sprite the app needs at runtime,
    /// so no authored art asset is required for basic chrome. Sprites are cached by shape key
    /// and reused across every screen — see PLAN.md §03 (procedural icon set) and §07.
    /// </summary>
    public static class UIShapes
    {
        private const int Padding = 4; // antialiasing border baked into every generated texture
        private static readonly Dictionary<int, Sprite> RoundedRectCache = new();
        private static readonly Dictionary<int, Sprite> RingCache = new();
        private static Sprite _circleSprite;
        private static Sprite _chevronSprite;

        /// <summary>A 9-sliced rounded rectangle. Stretches cleanly to any card/button size.</summary>
        public static Sprite RoundedRect(int radius)
        {
            if (RoundedRectCache.TryGetValue(radius, out var cached)) return cached;

            int half = radius + Padding;
            int size = half * 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var center = new Vector2(size / 2f, size / 2f);
            var halfBox = new Vector2(size / 2f - Padding, size / 2f - Padding);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float alpha = RoundedBoxAlpha(p, halfBox, radius);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            float border = radius + Padding;
            var sprite = Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = $"RoundedRect_{radius}";
            RoundedRectCache[radius] = sprite;
            return sprite;
        }

        /// <summary>Full-stadium pill — a rounded rect whose radius equals half its own size.</summary>
        public static Sprite Pill() => RoundedRect(64);

        /// <summary>A stroked ring/annulus for progress indicators (used with Image.Type.Filled,
        /// FillMethod.Radial360 to draw the Progress screen's completion ring).</summary>
        public static Sprite Ring(int outerRadius, int thickness)
        {
            int key = outerRadius * 10000 + thickness;
            if (RingCache.TryGetValue(key, out var cached)) return cached;

            int half = outerRadius + Padding;
            int size = half * 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var center = new Vector2(size / 2f, size / 2f);
            float inner = outerRadius - thickness;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float outerA = Mathf.Clamp01(outerRadius - dist);
                    float innerA = Mathf.Clamp01(dist - inner);
                    float alpha = Mathf.Min(outerA, innerA);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"Ring_{outerRadius}_{thickness}";
            RingCache[key] = sprite;
            return sprite;
        }

        /// <summary>Plain filled circle — used for status dots, avatar frames, small icon backgrounds.</summary>
        public static Sprite Circle()
        {
            if (_circleSprite != null) return _circleSprite;
            _circleSprite = Ring(128, 128); // thickness == radius means fully filled
            return _circleSprite;
        }

        /// <summary>The chevron used for the AR ground arrows and the map turn-instruction glyph.</summary>
        public static Sprite Chevron()
        {
            if (_chevronSprite != null) return _chevronSprite;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            float thickness = size * 0.22f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - new Vector2(size / 2f, size * 0.28f);
                    // Two diagonal strokes forming a "^" — signed distance to each line segment.
                    float dLeft = DistToSegment(p, new Vector2(-size * 0.32f, size * 0.62f), Vector2.zero);
                    float dRight = DistToSegment(p, Vector2.zero, new Vector2(size * 0.32f, size * 0.62f));
                    float d = Mathf.Min(dLeft, dRight);
                    float alpha = Mathf.Clamp01(thickness * 0.5f - d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _chevronSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _chevronSprite.name = "Chevron";
            return _chevronSprite;
        }

        private static float RoundedBoxAlpha(Vector2 p, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + Vector2.one * radius;
            float dist = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
            return Mathf.Clamp01(0.5f - dist);
        }

        /// <summary>Distance from p to the segment a-b, in the same pixel space as the caller.
        /// Shared with IconGraphic's procedural icon rasteriser.</summary>
        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            Vector2 closest = a + t * ab;
            return Vector2.Distance(p, closest);
        }

        /// <summary>Alpha coverage for a stroked line segment of the given thickness, antialiased.</summary>
        public static float StrokeAlpha(Vector2 p, Vector2 a, Vector2 b, float thickness)
            => Mathf.Clamp01(thickness * 0.5f - DistToSegment(p, a, b));

        /// <summary>Alpha coverage for a filled circle centred at c, antialiased.</summary>
        public static float CircleAlpha(Vector2 p, Vector2 c, float radius)
            => Mathf.Clamp01(radius - Vector2.Distance(p, c));

        /// <summary>Alpha coverage for a ring (stroked circle) centred at c.</summary>
        public static float CircleStrokeAlpha(Vector2 p, Vector2 c, float radius, float thickness)
        {
            float d = Mathf.Abs(Vector2.Distance(p, c) - radius);
            return Mathf.Clamp01(thickness * 0.5f - d);
        }
    }
}
