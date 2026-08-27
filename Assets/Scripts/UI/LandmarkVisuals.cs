using AlipiriAR.Data;
using UnityEngine;

namespace AlipiriAR.UI
{
    /// <summary>Type→icon/colour mapping shared by LandmarksScreen and LandmarkPopup. Lives in
    /// the UI layer (not on LandmarkType itself) so Data stays free of UI dependencies.</summary>
    public static class LandmarkVisuals
    {
        public static IconType IconFor(LandmarkType type) => type switch
        {
            LandmarkType.Temple => IconType.Gopuram,
            LandmarkType.WaterPoint => IconType.Droplet,
            LandmarkType.Statue => IconType.Statue,
            LandmarkType.Steps => IconType.Steps,
            LandmarkType.Medical => IconType.Medical,
            LandmarkType.Shops => IconType.Shop,
            _ => IconType.Gopuram,
        };

        public static Color TintFor(LandmarkType type) => type switch
        {
            LandmarkType.Temple => UITheme.Gold,
            LandmarkType.WaterPoint => UITheme.Accent,
            LandmarkType.Statue => UITheme.Warning,
            LandmarkType.Steps => UITheme.TextSecondary,
            LandmarkType.Medical => UITheme.Critical,
            LandmarkType.Shops => UITheme.Success,
            _ => UITheme.Gold,
        };
    }
}
