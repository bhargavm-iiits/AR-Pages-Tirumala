namespace AlipiriAR.Data
{
    /// <summary>One landmark, as loaded from StreamingAssets/Database/landmarks.json.
    /// Description is display copy, distinct from VoiceText (NewPlan.md §03 D6) — source data
    /// has no "description" field yet, so JsonDatabase falls it back to VoiceText on parse,
    /// keeping every existing entry working without a content pass.</summary>
    public class LandmarkData
    {
        public int Id;
        public string Name;
        public LandmarkType Type;
        public double Latitude;
        public double Longitude;
        public float TriggerRadiusMeters;
        public string VoiceText;
        public string Description;
        public int Priority;

        /// <summary>Metres from the route start, resolved once against the built route by
        /// JsonDatabase — used for the Landmarks list's distance column and trigger ordering.</summary>
        public double CumulativeDistanceMeters;

        /// <summary>Landmark's position projected onto the route polyline, resolved once
        /// alongside CumulativeDistanceMeters — Latitude/Longitude above stay the real surveyed
        /// position (what AR placement and geofence triggering need), but a landmark set back
        /// even a few metres from the drawn line reads as visibly "off the path" on the Map
        /// screen's schematic view, so PoiMarkerLayer pins to this snapped pair instead.</summary>
        public double SnappedLatitude;
        public double SnappedLongitude;
    }
}
