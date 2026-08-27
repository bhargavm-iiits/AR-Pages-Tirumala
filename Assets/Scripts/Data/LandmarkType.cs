namespace AlipiriAR.Data
{
    /// <summary>Normalised landmark category. The source data mixes casing ("Statue"/"statue",
    /// "Steps"/"steps") — JsonDatabase folds all of that into this enum on load (PLAN.md §04).
    /// Medical and Shops have no dedicated mockup filter chip, so LandmarksScreen buckets them
    /// under Other.</summary>
    public enum LandmarkType
    {
        Temple,
        WaterPoint,
        Statue,
        Steps,
        Medical,
        Shops,
    }

    public static class LandmarkTypeExtensions
    {
        public static LandmarkType Parse(string raw)
        {
            return raw?.Trim().ToLowerInvariant() switch
            {
                "temple" => LandmarkType.Temple,
                "water point" => LandmarkType.WaterPoint,
                "statue" => LandmarkType.Statue,
                "steps" => LandmarkType.Steps,
                "medical" => LandmarkType.Medical,
                "shops" => LandmarkType.Shops,
                _ => LandmarkType.Temple,
            };
        }

        /// <summary>True for the two types that share the "Other" filter chip (PLAN.md §04/§05).</summary>
        public static bool IsOtherCategory(this LandmarkType type) =>
            type == LandmarkType.Medical || type == LandmarkType.Shops;

        /// <summary>Localisation key for this type's display name (landmark card thumbnail
        /// caption, popup subtitle).</summary>
        public static string LocKey(this LandmarkType type) => type switch
        {
            LandmarkType.Temple => "landmark.temple",
            LandmarkType.WaterPoint => "landmark.water_point",
            LandmarkType.Statue => "landmark.statue",
            LandmarkType.Steps => "landmark.steps_marker",
            LandmarkType.Medical => "landmark.medical",
            LandmarkType.Shops => "landmark.shops",
            _ => "landmark.temple",
        };
    }
}
