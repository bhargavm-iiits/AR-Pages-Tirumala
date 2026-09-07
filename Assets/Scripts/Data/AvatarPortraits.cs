using System.Collections.Generic;

namespace AlipiriAR.Data
{
    /// <summary>StreamingAssets portrait paths for the six Dashavatara landmarks this route's
    /// data and Docs/Dashavatar's supplied images actually agree on (see PoiMarkerLayer's own
    /// doc for why only six of the ten). Shared between PoiMarkerLayer (map callout cards) and
    /// LandmarkPopup (tap-to-open detail sheet) so both show the same real portrait instead of
    /// LandmarkPopup falling back to the generic per-type icon every other landmark uses.</summary>
    public static class AvatarPortraits
    {
        public static readonly Dictionary<string, string> Paths = new()
        {
            ["Mathsyavataram"] = "Images/Dashavatar/matsya.png",
            ["Kurma Avataram"] = "Images/Dashavatar/kurma.png",
            ["Varaha Avataram"] = "Images/Dashavatar/varaha.png",
            ["Sri Narasimha Avataram"] = "Images/Dashavatar/narasimha.png",
            ["Sri Vamana Avataram"] = "Images/Dashavatar/vamana.png",
            ["Sri Krishna Avataram"] = "Images/Dashavatar/krishna.png",
        };

        public static bool TryGet(string landmarkName, out string relativePath) =>
            Paths.TryGetValue(landmarkName, out relativePath);
    }
}
