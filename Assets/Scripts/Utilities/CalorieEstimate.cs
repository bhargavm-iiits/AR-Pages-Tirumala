using UnityEngine;

namespace AlipiriAR.Utilities
{
    /// <summary>Placeholder distance-only calorie estimate for the Progress screen's redesigned
    /// metrics grid (Docs/Images/New UI #4) — no elevation/weight data exists yet to do better
    /// (ProgressScreen's elevation chart has the same "not available" honesty gap, see its own
    /// comment), so this uses a flat per-kilometre rate for uphill stair-walking rather than
    /// pretending to a precision the app can't back up.</summary>
    public static class CalorieEstimate
    {
        /// <summary>kcal per km — a rough mid-range figure for uphill walking/stair-climbing,
        /// deliberately higher than flat-ground (~50 kcal/km) since this route is a stone hill
        /// stairway (see EtaEstimate's own assumed pace comment).</summary>
        public const float KcalPerKm = 65f;

        public static int Kcal(double distanceMeters) =>
            Mathf.RoundToInt((float)(distanceMeters / 1000.0) * KcalPerKm);
    }
}
