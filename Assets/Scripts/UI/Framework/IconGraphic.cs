using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlipiriAR.UI
{
    /// <summary>The ~20 glyphs the mockups use. Everything here is rasterised procedurally
    /// (PLAN.md §03) — the only asset that still wants authoring by hand is the gold gopuram
    /// brand mark, which uses a simplified silhouette here as a placeholder.</summary>
    public enum IconType
    {
        Hamburger, Back, Close, Search, Speaker, SpeakerMuted,
        Compass, Sun, Pause, Check, Warning, Plus, Minus,
        Recenter, North, Gopuram, Droplet, Statue, Steps, Medical, Shop, Info,
        Globe, Ruler, Haptic, Map, Profile, Export, Gear
    }

    /// <summary>Attach to any Image to render one of the procedural icons. Colour comes from
    /// the Image's own tint, matching how UIShapes sprites are used everywhere else.</summary>
    [RequireComponent(typeof(Image))]
    public class IconGraphic : MonoBehaviour
    {
        [SerializeField] private IconType _icon;
        private Image _image;

        public IconType Icon
        {
            get => _icon;
            set { _icon = value; Refresh(); }
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
            Refresh();
        }

        private void Refresh()
        {
            if (_image == null) _image = GetComponent<Image>();
            _image.sprite = Get(_icon);
        }

        private static readonly Dictionary<IconType, Sprite> Cache = new();
        private const int Size = 96;

        public static Sprite Get(IconType type)
        {
            if (Cache.TryGetValue(type, out var cached)) return cached;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[Size * Size];
            var c = new Vector2(Size / 2f, Size / 2f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Every shape below was authored assuming screen-space Y (negative offset =
                    // up, e.g. Check's stroke to "c + (0.26, -0.18)" is meant to be the raised
                    // tip of a tick mark). But Texture2D.SetPixels32 fills bottom-to-top — row
                    // index 0 is the BOTTOM texel — so without this flip, a small y here (near
                    // Size - y) is the negative-offset math, which SetPixels32 places at the
                    // bottom instead of the top: every asymmetric icon rendered upside down.
                    // Confirmed on-device: the Landmarks "visited" checkmark badge rendered as
                    // an inverted caret instead of a ✓. Flipping the sampled Y here (not the
                    // write order) keeps every shape's math exactly as written and just corrects
                    // which physical row it lands on.
                    var p = new Vector2(x + 0.5f, Size - (y + 0.5f));
                    float a = Rasterize(type, p, c);
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"Icon_{type}";
            Cache[type] = sprite;
            return sprite;
        }

        private static float Rasterize(IconType type, Vector2 p, Vector2 c)
        {
            const float t = 6f; // stroke thickness
            float r = Size * 0.32f;

            switch (type)
            {
                case IconType.Hamburger:
                {
                    float w = Size * 0.56f;
                    float a1 = UIShapes.StrokeAlpha(p, c + new Vector2(-w / 2, -r * 0.7f), c + new Vector2(w / 2, -r * 0.7f), t);
                    float a2 = UIShapes.StrokeAlpha(p, c + new Vector2(-w / 2, 0), c + new Vector2(w / 2, 0), t);
                    float a3 = UIShapes.StrokeAlpha(p, c + new Vector2(-w / 2, r * 0.7f), c + new Vector2(w / 2, r * 0.7f), t);
                    return Mathf.Max(a1, a2, a3);
                }
                case IconType.Back:
                {
                    float s = Size * 0.22f;
                    return Mathf.Max(
                        UIShapes.StrokeAlpha(p, c + new Vector2(s, -s), c + new Vector2(-s, 0), t),
                        UIShapes.StrokeAlpha(p, c + new Vector2(-s, 0), c + new Vector2(s, s), t));
                }
                case IconType.Close:
                {
                    float s = Size * 0.22f;
                    return Mathf.Max(
                        UIShapes.StrokeAlpha(p, c + new Vector2(-s, -s), c + new Vector2(s, s), t),
                        UIShapes.StrokeAlpha(p, c + new Vector2(-s, s), c + new Vector2(s, -s), t));
                }
                case IconType.Search:
                {
                    var lensCenter = c + new Vector2(-Size * 0.06f, -Size * 0.06f);
                    float ring = UIShapes.CircleStrokeAlpha(p, lensCenter, Size * 0.18f, t);
                    float handle = UIShapes.StrokeAlpha(p, lensCenter + new Vector2(Size * 0.14f, Size * 0.14f), lensCenter + new Vector2(Size * 0.30f, Size * 0.30f), t);
                    return Mathf.Max(ring, handle);
                }
                case IconType.Speaker:
                case IconType.SpeakerMuted:
                {
                    // Body: small square + triangle bell
                    float body = UIShapes.StrokeAlpha(p, c + new Vector2(-Size * 0.22f, -Size * 0.1f), c + new Vector2(-Size * 0.08f, -Size * 0.1f), t);
                    body = Mathf.Max(body, UIShapes.StrokeAlpha(p, c + new Vector2(-Size * 0.22f, Size * 0.1f), c + new Vector2(-Size * 0.08f, Size * 0.1f), t));
                    body = Mathf.Max(body, UIShapes.StrokeAlpha(p, c + new Vector2(-Size * 0.22f, -Size * 0.1f), c + new Vector2(-Size * 0.22f, Size * 0.1f), t));
                    body = Mathf.Max(body, UIShapes.StrokeAlpha(p, c + new Vector2(-Size * 0.08f, -Size * 0.1f), c + new Vector2(Size * 0.06f, -Size * 0.24f), t));
                    body = Mathf.Max(body, UIShapes.StrokeAlpha(p, c + new Vector2(-Size * 0.08f, Size * 0.1f), c + new Vector2(Size * 0.06f, Size * 0.24f), t));
                    body = Mathf.Max(body, UIShapes.StrokeAlpha(p, c + new Vector2(Size * 0.06f, -Size * 0.24f), c + new Vector2(Size * 0.06f, Size * 0.24f), t));
                    if (type == IconType.SpeakerMuted)
                        body = Mathf.Max(body, UIShapes.StrokeAlpha(p, c + new Vector2(Size * 0.14f, -Size * 0.18f), c + new Vector2(Size * 0.32f, Size * 0.18f), t));
                    else
                        body = Mathf.Max(body, UIShapes.CircleStrokeAlpha(p, c, Size * 0.30f, t) * (p.x > c.x + Size * 0.08f ? 1f : 0f));
                    return body;
                }
                case IconType.Compass:
                {
                    float ring = UIShapes.CircleStrokeAlpha(p, c, r, t * 0.8f);
                    float needleN = UIShapes.StrokeAlpha(p, c, c + new Vector2(0, -r * 0.75f), t);
                    float needleS = UIShapes.StrokeAlpha(p, c, c + new Vector2(0, r * 0.5f), t * 0.7f);
                    return Mathf.Max(ring, needleN, needleS);
                }
                case IconType.North:
                {
                    float stem = UIShapes.StrokeAlpha(p, c + new Vector2(0, r), c + new Vector2(0, -r), t);
                    float wingL = UIShapes.StrokeAlpha(p, c + new Vector2(0, -r), c + new Vector2(-r * 0.5f, -r * 0.3f), t);
                    float wingR = UIShapes.StrokeAlpha(p, c + new Vector2(0, -r), c + new Vector2(r * 0.5f, -r * 0.3f), t);
                    return Mathf.Max(stem, Mathf.Max(wingL, wingR));
                }
                case IconType.Sun:
                {
                    float core = UIShapes.CircleAlpha(p, c, Size * 0.16f);
                    float rays = 0f;
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI / 4f;
                        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        rays = Mathf.Max(rays, UIShapes.StrokeAlpha(p, c + dir * Size * 0.24f, c + dir * Size * 0.36f, t * 0.7f));
                    }
                    return Mathf.Max(core, rays);
                }
                case IconType.Pause:
                {
                    float bar1 = UIShapes.StrokeAlpha(p, c + new Vector2(-Size * 0.12f, -r * 0.8f), c + new Vector2(-Size * 0.12f, r * 0.8f), t * 1.4f);
                    float bar2 = UIShapes.StrokeAlpha(p, c + new Vector2(Size * 0.12f, -r * 0.8f), c + new Vector2(Size * 0.12f, r * 0.8f), t * 1.4f);
                    return Mathf.Max(bar1, bar2);
                }
                case IconType.Check:
                {
                    var a1 = c + new Vector2(-Size * 0.22f, 0f);
                    var a2 = c + new Vector2(-Size * 0.04f, Size * 0.18f);
                    var a3 = c + new Vector2(Size * 0.26f, -Size * 0.18f);
                    return Mathf.Max(UIShapes.StrokeAlpha(p, a1, a2, t * 1.2f), UIShapes.StrokeAlpha(p, a2, a3, t * 1.2f));
                }
                case IconType.Warning:
                {
                    var top = c + new Vector2(0, -r);
                    var bl = c + new Vector2(-r * 0.9f, r * 0.7f);
                    var br = c + new Vector2(r * 0.9f, r * 0.7f);
                    float outline = Mathf.Max(
                        UIShapes.StrokeAlpha(p, top, bl, t),
                        UIShapes.StrokeAlpha(p, bl, br, t),
                        UIShapes.StrokeAlpha(p, br, top, t));
                    float dot = UIShapes.CircleAlpha(p, c + new Vector2(0, Size * 0.18f), t * 0.55f);
                    float stem = UIShapes.StrokeAlpha(p, c + new Vector2(0, -Size * 0.14f), c + new Vector2(0, Size * 0.02f), t * 0.8f);
                    return Mathf.Max(outline, dot, stem);
                }
                case IconType.Plus:
                {
                    float s = Size * 0.24f;
                    return Mathf.Max(
                        UIShapes.StrokeAlpha(p, c + new Vector2(-s, 0), c + new Vector2(s, 0), t),
                        UIShapes.StrokeAlpha(p, c + new Vector2(0, -s), c + new Vector2(0, s), t));
                }
                case IconType.Minus:
                {
                    float s = Size * 0.24f;
                    return UIShapes.StrokeAlpha(p, c + new Vector2(-s, 0), c + new Vector2(s, 0), t);
                }
                case IconType.Recenter:
                {
                    float ring = UIShapes.CircleStrokeAlpha(p, c, Size * 0.14f, t * 0.8f);
                    float n = UIShapes.StrokeAlpha(p, c + new Vector2(0, -r), c + new Vector2(0, -Size * 0.2f), t * 0.7f);
                    float s = UIShapes.StrokeAlpha(p, c + new Vector2(0, r), c + new Vector2(0, Size * 0.2f), t * 0.7f);
                    float e = UIShapes.StrokeAlpha(p, c + new Vector2(r, 0), c + new Vector2(Size * 0.2f, 0), t * 0.7f);
                    float w = UIShapes.StrokeAlpha(p, c + new Vector2(-r, 0), c + new Vector2(-Size * 0.2f, 0), t * 0.7f);
                    return Mathf.Max(ring, n, s, e, w);
                }
                case IconType.Droplet:
                {
                    float body = UIShapes.CircleAlpha(p, c + new Vector2(0, Size * 0.08f), Size * 0.2f);
                    float tip = UIShapes.StrokeAlpha(p, c + new Vector2(0, -Size * 0.26f), c + new Vector2(0, Size * 0.02f), Size * 0.24f);
                    return Mathf.Max(body, tip * (p.y < c.y + Size * 0.05f ? 1f : 0f));
                }
                case IconType.Statue:
                {
                    float head = UIShapes.CircleAlpha(p, c + new Vector2(0, -Size * 0.2f), Size * 0.1f);
                    float body = Mathf.Max(
                        UIShapes.StrokeAlpha(p, c + new Vector2(0, -Size * 0.1f), c + new Vector2(0, Size * 0.18f), t * 1.3f),
                        UIShapes.StrokeAlpha(p, c + new Vector2(-Size * 0.18f, Size * 0.22f), c + new Vector2(Size * 0.18f, Size * 0.22f), t));
                    return Mathf.Max(head, body);
                }
                case IconType.Steps:
                {
                    float a = 0f;
                    for (int i = 0; i < 3; i++)
                    {
                        float y = r * 0.5f - i * (r * 0.5f);
                        float xEnd = -r + i * (r * 0.7f);
                        a = Mathf.Max(a, UIShapes.StrokeAlpha(p, c + new Vector2(-r, y), c + new Vector2(xEnd, y), t * 0.8f));
                        a = Mathf.Max(a, UIShapes.StrokeAlpha(p, c + new Vector2(xEnd, y), c + new Vector2(xEnd, y - r * 0.5f), t * 0.8f));
                    }
                    return a;
                }
                case IconType.Medical:
                    goto case IconType.Plus;
                case IconType.Shop:
                {
                    float bagW = Size * 0.36f, bagH = Size * 0.28f;
                    var top = c + new Vector2(-bagW / 2, -Size * 0.06f);
                    float outline = Mathf.Max(
                        UIShapes.StrokeAlpha(p, top, top + new Vector2(bagW, 0), t * 0.7f),
                        UIShapes.StrokeAlpha(p, top, top + new Vector2(0, bagH), t * 0.7f),
                        UIShapes.StrokeAlpha(p, top + new Vector2(bagW, 0), top + new Vector2(bagW, bagH), t * 0.7f),
                        UIShapes.StrokeAlpha(p, top + new Vector2(0, bagH), top + new Vector2(bagW, bagH), t * 0.7f));
                    float handle = UIShapes.CircleStrokeAlpha(p, c + new Vector2(0, -Size * 0.14f), Size * 0.1f, t * 0.6f) * (p.y < c.y - Size * 0.04f ? 1f : 0f);
                    return Mathf.Max(outline, handle);
                }
                case IconType.Info:
                {
                    float ring = UIShapes.CircleStrokeAlpha(p, c, r, t * 0.7f);
                    float dot = UIShapes.CircleAlpha(p, c + new Vector2(0, -Size * 0.16f), t * 0.5f);
                    float stem = UIShapes.StrokeAlpha(p, c + new Vector2(0, -Size * 0.02f), c + new Vector2(0, Size * 0.18f), t * 0.7f);
                    return Mathf.Max(ring, dot, stem);
                }
                case IconType.Globe:
                {
                    float ring = UIShapes.CircleStrokeAlpha(p, c, r, t * 0.8f);
                    float eq = UIShapes.StrokeAlpha(p, c + new Vector2(-r, 0), c + new Vector2(r, 0), t * 0.6f);
                    float mer = UIShapes.StrokeAlpha(p, c + new Vector2(0, -r), c + new Vector2(0, r), t * 0.6f);
                    return Mathf.Max(ring, Mathf.Max(eq, mer));
                }
                case IconType.Ruler:
                {
                    float bar = UIShapes.StrokeAlpha(p, c + new Vector2(-r, r * 0.4f), c + new Vector2(r, -r * 0.4f), t);
                    float tick1 = UIShapes.StrokeAlpha(p, c + new Vector2(-r * 0.4f, r * 0.1f), c + new Vector2(-r * 0.2f, -r * 0.15f), t * 0.7f);
                    float tick2 = UIShapes.StrokeAlpha(p, c + new Vector2(r * 0.05f, -r * 0.05f), c + new Vector2(r * 0.3f, -r * 0.3f), t * 0.7f);
                    return Mathf.Max(bar, Mathf.Max(tick1, tick2));
                }
                case IconType.Haptic:
                {
                    float ring1 = UIShapes.CircleStrokeAlpha(p, c, Size * 0.12f, t * 0.7f);
                    float ring2 = UIShapes.CircleStrokeAlpha(p, c, Size * 0.24f, t * 0.6f);
                    float ring3 = UIShapes.CircleStrokeAlpha(p, c, Size * 0.34f, t * 0.5f);
                    return Mathf.Max(ring1, Mathf.Max(ring2, ring3));
                }
                case IconType.Map:
                {
                    var tl = c + new Vector2(-r, -r * 0.6f);
                    var tr = c + new Vector2(r, -r * 0.6f);
                    var bl = c + new Vector2(-r, r * 0.6f);
                    var br = c + new Vector2(r, r * 0.6f);
                    float outline = Mathf.Max(
                        UIShapes.StrokeAlpha(p, tl, tr, t * 0.7f),
                        Mathf.Max(UIShapes.StrokeAlpha(p, tr, br, t * 0.7f),
                        Mathf.Max(UIShapes.StrokeAlpha(p, br, bl, t * 0.7f),
                                  UIShapes.StrokeAlpha(p, bl, tl, t * 0.7f))));
                    float fold1 = UIShapes.StrokeAlpha(p, c + new Vector2(-r * 0.34f, -r * 0.6f), c + new Vector2(-r * 0.34f, r * 0.6f), t * 0.4f);
                    float fold2 = UIShapes.StrokeAlpha(p, c + new Vector2(r * 0.34f, -r * 0.6f), c + new Vector2(r * 0.34f, r * 0.6f), t * 0.4f);
                    return Mathf.Max(outline, Mathf.Max(fold1, fold2));
                }
                case IconType.Profile:
                {
                    float head = UIShapes.CircleAlpha(p, c + new Vector2(0, -Size * 0.16f), Size * 0.14f);
                    float shoulders = UIShapes.CircleStrokeAlpha(p, c + new Vector2(0, Size * 0.36f), Size * 0.28f, t) * (p.y < c.y + Size * 0.22f ? 1f : 0f);
                    return Mathf.Max(head, shoulders);
                }
                case IconType.Export:
                {
                    float stem = UIShapes.StrokeAlpha(p, c + new Vector2(0, Size * 0.12f), c + new Vector2(0, -Size * 0.24f), t);
                    float headL = UIShapes.StrokeAlpha(p, c + new Vector2(0, -Size * 0.24f), c + new Vector2(-Size * 0.14f, -Size * 0.08f), t);
                    float headR = UIShapes.StrokeAlpha(p, c + new Vector2(0, -Size * 0.24f), c + new Vector2(Size * 0.14f, -Size * 0.08f), t);
                    float tray = UIShapes.StrokeAlpha(p, c + new Vector2(-Size * 0.22f, Size * 0.22f), c + new Vector2(Size * 0.22f, Size * 0.22f), t * 0.8f);
                    return Mathf.Max(Mathf.Max(stem, headL), Mathf.Max(headR, tray));
                }
                case IconType.Gear:
                {
                    float ring = UIShapes.CircleStrokeAlpha(p, c, Size * 0.18f, t * 0.8f);
                    float hub = UIShapes.CircleAlpha(p, c, Size * 0.06f);
                    float teeth = 0f;
                    for (int i = 0; i < 6; i++)
                    {
                        float ang = i * Mathf.PI / 3f;
                        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        teeth = Mathf.Max(teeth, UIShapes.StrokeAlpha(p, c + dir * Size * 0.24f, c + dir * Size * 0.34f, t * 0.9f));
                    }
                    return Mathf.Max(ring, Mathf.Max(hub, teeth));
                }
                case IconType.Gopuram:
                {
                    // Simplified stepped-pyramid silhouette — the placeholder brand mark
                    // until an authored asset replaces it (PLAN.md §03).
                    float a = 0f;
                    for (int tier = 0; tier < 4; tier++)
                    {
                        float ty = r * 0.75f - tier * (r * 0.5f);
                        float half = (Size * 0.4f) * (1f - tier * 0.2f);
                        a = Mathf.Max(a, UIShapes.StrokeAlpha(p, c + new Vector2(-half, ty), c + new Vector2(half, ty), t * 0.7f));
                        a = Mathf.Max(a, UIShapes.StrokeAlpha(p, c + new Vector2(-half, ty), c + new Vector2(-half * 0.8f, ty - r * 0.5f), t * 0.6f));
                        a = Mathf.Max(a, UIShapes.StrokeAlpha(p, c + new Vector2(half, ty), c + new Vector2(half * 0.8f, ty - r * 0.5f), t * 0.6f));
                    }
                    return a;
                }
                default:
                    return 0f;
            }
        }
    }
}
