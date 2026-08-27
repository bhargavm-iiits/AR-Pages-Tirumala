using System.Collections.Generic;
using AlipiriAR.Utilities;
using UnityEngine;

namespace AlipiriAR.Positioning
{
    /// <summary>Rolling pace, not a constant (PLAN.md §09) — averages recent actual progress
    /// once a live session has enough history. Falls back to EtaEstimate's fixed conservative
    /// assumption (the same one Progress/Map show before any session exists) when there isn't
    /// enough history yet, so the number never looks unstable right after a session starts.</summary>
    public class EtaEstimator
    {
        private const int WindowSize = 6;
        private const double MinWindowSeconds = 20.0;

        private readonly Queue<(double distance, double time)> _samples = new();

        public void Feed(double cumulativeDistanceMeters, double timestamp)
        {
            _samples.Enqueue((cumulativeDistanceMeters, timestamp));
            while (_samples.Count > WindowSize) _samples.Dequeue();
        }

        public int EstimateMinutes(double remainingMeters)
        {
            float paceMetersPerSecond = EtaEstimate.AssumedWalkingSpeedMps;

            if (_samples.Count >= 2)
            {
                var oldest = _samples.Peek();
                (double distance, double time) newest = default;
                foreach (var s in _samples) newest = s;

                double dt = newest.time - oldest.time;
                double dd = newest.distance - oldest.distance;
                if (dt >= MinWindowSeconds && dd > 0.5)
                    paceMetersPerSecond = (float)(dd / dt);
            }

            paceMetersPerSecond = Mathf.Max(paceMetersPerSecond, 0.1f);
            return Mathf.CeilToInt((float)(remainingMeters / paceMetersPerSecond) / 60f);
        }
    }
}
