namespace AlipiriAR.Data
{
    /// <summary>Which portion of the route a landmark sits in (Docs/GeospatialPlan.md §07) —
    /// drives the START_GEO / OFFLINE_NAV / END_GEO transitions a future RouteModeManager owns
    /// (Rev 4 Phase L). Inert metadata until that class exists. Defaults to Route: no landmark in
    /// the source data has a "zone" field yet, so every one of the 40 entries keeps behaving
    /// exactly as it does today.</summary>
    public enum RouteZone
    {
        Route,
        Start,
        End,
    }

    public static class RouteZoneExtensions
    {
        public static RouteZone Parse(string raw)
        {
            return raw?.Trim().ToLowerInvariant() switch
            {
                "start" => RouteZone.Start,
                "end" => RouteZone.End,
                _ => RouteZone.Route,
            };
        }
    }
}
