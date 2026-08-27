using System.Globalization;
using TMPro;
using UnityEngine;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Single source of truth for every colour, spacing and radius value in the app.
    /// Values are transcribed from the 11 mockup screens in Assets/Imgaes/ — see PLAN.md §05.
    /// </summary>
    public static class UITheme
    {
        // ---- Colour ----------------------------------------------------------
        public static readonly Color Ground = FromHex("#0B1520");
        public static readonly Color GroundDeep = FromHex("#111C27");
        /// <summary>Map basemap fill — a real terrain green instead of the navy graph-paper look
        /// the demo basemap used before, since the user wants the map to read as land, not a grid.</summary>
        public static readonly Color TerrainGreen = FromHex("#173A27");
        public static readonly Color Surface = FromHex("#16202B");
        public static readonly Color SurfaceRaised = FromHex("#1C2933");
        public static readonly Color Rule = FromHex("#263340");

        public static readonly Color TextPrimary = FromHex("#E7EEF2");
        public static readonly Color TextSecondary = FromHex("#93A7B2");
        public static readonly Color TextTertiary = FromHex("#5E7480");

        public static readonly Color Accent = FromHex("#2F7BFF");
        public static readonly Color AccentDim = FromHex("#1B3E73");
        public static readonly Color Success = FromHex("#3DBE6E");
        public static readonly Color SuccessDim = FromHex("#173A28");
        public static readonly Color Warning = FromHex("#F0A824");
        public static readonly Color WarningDim = FromHex("#43330E");
        public static readonly Color Critical = FromHex("#E5573F");
        public static readonly Color CriticalDim = FromHex("#3D1E17");
        public static readonly Color Gold = FromHex("#C9A227");

        /// <summary>Camera-feed glass overlay used on the AR HUD cards (top pill, bottom stat card).</summary>
        public static readonly Color Glass = new Color(Surface.r, Surface.g, Surface.b, 0.82f);

        // ---- Spacing (px @ 1080 reference width) ------------------------------
        public const float SpaceXS = 6f;
        public const float SpaceS = 12f;
        public const float SpaceM = 20f;
        public const float SpaceL = 32f;
        public const float SpaceXL = 48f;

        // ---- Radii --------------------------------------------------------
        public const float RadiusCard = 26f;
        public const float RadiusChip = 999f;   // stadium
        public const float RadiusPill = 999f;   // stadium

        // ---- Touch / type -------------------------------------------------
        public const float MinTouchTarget = 84f;   // ~48dp at the 1080 reference width
        public const float BodyFontSize = 34f;
        public const float LabelFontSize = 27f;
        public const float CaptionFontSize = 23f;
        public const float TitleFontSize = 44f;
        public const float DisplayFontSize = 60f;

        /// <summary>Every one of the app's ~35 icon placements goes through
        /// UIFactory.CenteredIcon, which multiplies its requested size by this — a single lever
        /// to make every icon in the app bigger or smaller without touching each call site's own
        /// literal size. Raised from 1.2 (Docs/UIplan.md §03 Phase 2 follow-up — icons read too
        /// small against the mockup even after the shadow/spacing pass). 1.5 still stays inside
        /// the tightest containers' ~45% clipping headroom noted when 1.2 was chosen.</summary>
        public const float IconSizeMultiplier = 1.5f;

        public static TMP_FontAsset PrimaryFont { get; private set; }

        public static void SetPrimaryFont(TMP_FontAsset font)
        {
            PrimaryFont = font;
            EnsureIndicFontSupport();
        }

        public static void EnsureIndicFontSupport()
        {
            if (PrimaryFont == null) return;
            try
            {
                string[] fontNames = { "Nirmala UI", "Gautami", "Mangal", "Latha", "Tunga", "Segoe UI", "Arial", "Noto Sans" };
                Font osFont = Font.CreateDynamicFontFromOSFont(fontNames, 32);
                if (osFont != null)
                {
                    var fallbackAsset = TMP_FontAsset.CreateFontAsset(osFont);
                    if (fallbackAsset != null)
                    {
                        fallbackAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                        PrimaryFont.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
                        if (!PrimaryFont.fallbackFontAssetTable.Contains(fallbackAsset))
                        {
                            PrimaryFont.fallbackFontAssetTable.Add(fallbackAsset);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UITheme] Indic font fallback note: {ex.Message}");
            }
        }

        public static Color FromHex(string hex)
        {
            hex = hex.TrimStart('#');
            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte a = hex.Length >= 8
                ? byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : (byte)255;
            return new Color32(r, g, b, a);
        }

        /// <summary>Returns the semantic colour pair (fill, text) for a landmark status ring / chip.</summary>
        public static (Color fill, Color dim) Semantic(SemanticState state) => state switch
        {
            SemanticState.Success => (Success, SuccessDim),
            SemanticState.Warning => (Warning, WarningDim),
            SemanticState.Critical => (Critical, CriticalDim),
            _ => (Accent, AccentDim),
        };
    }

    public enum SemanticState { Neutral, Success, Warning, Critical }
}
