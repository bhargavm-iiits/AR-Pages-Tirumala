using System.Globalization;
using TMPro;
using UnityEngine;

namespace AlipiriAR.UI
{
    /// <summary>
    /// Single source of truth for every colour, spacing and radius value in the app.
    ///
    /// Palette is "Stone &amp; Copper" — granite ground, copper accent in place of the original
    /// tech-blue, warm ivory text instead of cool blue-white. Chosen over three other devotional
    /// directions (Temple Gold, Dawn Saffron, Serene Lotus) explicitly for this app's outdoor
    /// constraints: a dark ground stays legible against sunlight glare and reads consistently
    /// behind the live AR camera passthrough regardless of what's behind it (sky, stone, crowd),
    /// where a light/glass palette would wash out; dark UI is also the cheaper draw on OLED across
    /// the 2-4 hour walk this app is used for (see the battery discussion in Docs/update1.md §10).
    /// Every value below was previously transcribed from the 11 mockup screens in Assets/Imgaes/
    /// (PLAN.md §05) with a tech-blue accent; only the hue story changed here, not the structure —
    /// every screen keeps reading off these same names.
    /// </summary>
    public static class UITheme
    {
        // ---- Colour ----------------------------------------------------------
        public static readonly Color Ground = FromHex("#211E1B");
        public static readonly Color GroundDeep = FromHex("#17140F");
        /// <summary>Map basemap fill — a real terrain green instead of the navy graph-paper look
        /// the demo basemap used before, since the user wants the map to read as land, not a grid.
        /// Left as true terrain green rather than warmed toward the rest of this palette — it
        /// represents real vegetation on the map, not app chrome.</summary>
        public static readonly Color TerrainGreen = FromHex("#173A27");
        public static readonly Color Surface = FromHex("#2A2723");
        public static readonly Color SurfaceRaised = FromHex("#3A3631");
        public static readonly Color Rule = FromHex("#4A4038");

        public static readonly Color TextPrimary = FromHex("#EDE6DA");
        public static readonly Color TextSecondary = FromHex("#9A9284");
        public static readonly Color TextTertiary = FromHex("#6B655C");

        /// <summary>Copper — was tech-blue (#2F7BFF). The one hue swap that reads everywhere:
        /// every "AR" pill, active tab, progress fill and primary button inherits it from here.</summary>
        public static readonly Color Accent = FromHex("#B87333");
        public static readonly Color AccentDim = FromHex("#3D2E1E");
        public static readonly Color Success = FromHex("#7A9B68");
        public static readonly Color SuccessDim = FromHex("#26301F");
        public static readonly Color Warning = FromHex("#D4915A");
        public static readonly Color WarningDim = FromHex("#3D2D1C");
        public static readonly Color Critical = FromHex("#C1502E");
        public static readonly Color CriticalDim = FromHex("#3A1F16");
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

        /// <summary>Real Noto Sans {Telugu,Devanagari,Tamil,Kannada} TTFs, once bundled, go in
        /// Assets/Resources/Fonts/Noto/ under these exact names (Rev 3/4 D8: no files exist yet —
        /// drop them in and this resolves them automatically, no code change, no re-wiring). Loaded via
        /// Resources rather than referenced as a serialized field so this class stays static and
        /// callable from AppBootstrap before any MonoBehaviour exists to hold the reference.</summary>
        private static readonly string[] IndicFontResourcePaths =
        {
            "Fonts/Noto/NotoSansTelugu-Regular",
            "Fonts/Noto/NotoSansDevanagari-Regular",
            "Fonts/Noto/NotoSansTamil-Regular",
            "Fonts/Noto/NotoSansKannada-Regular",
        };

        public static void EnsureIndicFontSupport()
        {
            if (PrimaryFont == null) return;

            bool anyBundled = false;
            foreach (string resourcePath in IndicFontResourcePaths)
            {
                var bundledFont = Resources.Load<Font>(resourcePath);
                if (bundledFont == null) continue;
                anyBundled = true;
                AddFallback(TMP_FontAsset.CreateFontAsset(bundledFont));
            }

            if (anyBundled) return;

            // No bundled Noto TTFs yet (D8). Font.CreateDynamicFontFromOSFont resolves fonts
            // INSTALLED ON THE RUNNING OS — "Nirmala UI"/"Gautami"/"Mangal"/"Latha"/"Tunga" are
            // Windows-shipped families with no Android equivalent, so this call was previously
            // attempted on every platform and silently produced nothing on device (caught by the
            // try/catch below, logged as a generic "note" indistinguishable from success). Restricted
            // to the Editor now — the one place those names can resolve, useful for previewing
            // Indic text before the real TTFs exist — and replaced with a specific, actionable
            // warning on Android/device builds, so a translated string rendering as tofu boxes has
            // an obvious cause in the log instead of a silent no-op.
#if UNITY_EDITOR
            try
            {
                string[] editorPreviewFontNames = { "Nirmala UI", "Gautami", "Mangal", "Latha", "Tunga", "Segoe UI", "Arial", "Noto Sans" };
                Font osFont = Font.CreateDynamicFontFromOSFont(editorPreviewFontNames, 32);
                if (osFont != null) AddFallback(TMP_FontAsset.CreateFontAsset(osFont));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UITheme] Editor-only Indic font preview unavailable: {ex.Message}");
            }
#else
            Debug.LogWarning("[UITheme] No Noto Sans Indic fonts bundled — Telugu/Devanagari/Tamil/" +
                              "Kannada text will render as tofu boxes on this device. Drop " +
                              "NotoSansTelugu-Regular.ttf etc. into Assets/Resources/Fonts/Noto/ " +
                              "(Rev 3/4 D8) to fix with no further code change.");
#endif
        }

        private static void AddFallback(TMP_FontAsset fallbackAsset)
        {
            if (fallbackAsset == null) return;
            fallbackAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            PrimaryFont.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (!PrimaryFont.fallbackFontAssetTable.Contains(fallbackAsset))
                PrimaryFont.fallbackFontAssetTable.Add(fallbackAsset);
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
