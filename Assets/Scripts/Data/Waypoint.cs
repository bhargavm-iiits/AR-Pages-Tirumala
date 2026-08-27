namespace AlipiriAR.Data
{
    /// <summary>One vertex of the built route polyline, in walking order from Alipiri to Tirumala.</summary>
    public readonly struct Waypoint
    {
        public readonly double Latitude;
        public readonly double Longitude;

        /// <summary>Metres above sea level, or float.NaN if no source provided altitude
        /// (true for the current GeoJSON — see PLAN.md §01/§03). ElevationProfile checks this.</summary>
        public readonly float Elevation;

        /// <summary>Metres from the route start (Alipiri), following the polyline.</summary>
        public readonly double CumulativeDistanceMeters;

        /// <summary>True if this vertex falls inside the straight-line bridge across the
        /// 1,171.7 m OSM gap (PLAN.md §01) rather than surveyed geometry. Drives the amber
        /// arrow tint and the "route approximate" notice until a KML import replaces it.</summary>
        public readonly bool IsBridged;

        public Waypoint(double latitude, double longitude, float elevation, double cumulativeDistanceMeters, bool isBridged)
        {
            Latitude = latitude;
            Longitude = longitude;
            Elevation = elevation;
            CumulativeDistanceMeters = cumulativeDistanceMeters;
            IsBridged = isBridged;
        }

        public bool HasElevation => !float.IsNaN(Elevation);
    }
}
