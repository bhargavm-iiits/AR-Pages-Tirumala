namespace AlipiriAR.Data
{
    /// <summary>How a geospatial-enabled landmark's AR content anchors to the world
    /// (Docs/GeospatialPlan.md §04/§06/§07). Terrain anchors resolve a height relative to ground —
    /// what a statue or persistent AR object standing on a slope needs; Wgs84 anchors fix an exact
    /// altitude, useful only where that altitude is actually known (e.g. from a KML with elevation).
    /// Inert until Rev 4 Phase J's ARAnchorService exists to read it. Defaults to Terrain, matching
    /// GeospatialPlan.md §07's schema default.</summary>
    public enum GeospatialAnchorType
    {
        Terrain,
        Wgs84,
    }

    public static class GeospatialAnchorTypeExtensions
    {
        public static GeospatialAnchorType Parse(string raw)
        {
            return raw?.Trim().ToLowerInvariant() switch
            {
                "wgs84" => GeospatialAnchorType.Wgs84,
                _ => GeospatialAnchorType.Terrain,
            };
        }
    }
}
