using UnityEngine;

namespace AlipiriAR.Utilities
{
    /// <summary>Placeholder constant-pace ETA shared by Progress and Map screens until Scene 5's
    /// EtaEstimator (rolling pace, not a constant — PLAN.md §09) replaces it.</summary>
    public static class EtaEstimate
    {
        /// <summary>Conservative pace for a stone hill stairway, not flat-ground walking speed.</summary>
        public const float AssumedWalkingSpeedMps = 0.7f;

        public static int Minutes(double remainingMeters) =>
            Mathf.CeilToInt((float)(remainingMeters / AssumedWalkingSpeedMps) / 60f);
    }
}
