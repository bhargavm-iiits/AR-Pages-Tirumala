using UnityEngine;

namespace AlipiriAR.Positioning
{
    /// <summary>Variance-based 1D Kalman filter applied to lat/lon, smoothing the 10–30 m jitter
    /// raw Android fixes show under tree canopy (PLAN.md §08 step 2). Not a full state-space
    /// filter with a velocity term — a variance blend is enough at walking pace and is far
    /// simpler to reason about and review than a constant-velocity model.</summary>
    public class GpsKalmanFilter
    {
        /// <summary>How fast the filter's confidence decays between fixes — larger means it
        /// trusts new measurements more readily after a gap.</summary>
        private const float ProcessNoiseMetersPerSecond = 3f;

        private double _lat, _lon;
        private float _varianceMeters;
        private double _lastFixTime;
        private bool _initialized;

        public double Latitude => _lat;
        public double Longitude => _lon;
        public bool HasFix => _initialized;

        public void Reset() => _initialized = false;

        /// <summary>Feeds one raw fix through the filter. accuracyMeters is the fix's own
        /// reported 1-sigma accuracy (Input.location.lastData.horizontalAccuracy on device, or
        /// TraceReplaySource's injected synthetic value).</summary>
        public void Update(double lat, double lon, float accuracyMeters, double timestamp)
        {
            if (!_initialized)
            {
                _lat = lat;
                _lon = lon;
                _varianceMeters = Mathf.Max(accuracyMeters, 1f);
                _lastFixTime = timestamp;
                _initialized = true;
                return;
            }

            double dt = System.Math.Max(timestamp - _lastFixTime, 0.0);
            _lastFixTime = timestamp;

            _varianceMeters += (float)dt * ProcessNoiseMetersPerSecond;

            float gain = _varianceMeters / (_varianceMeters + Mathf.Max(accuracyMeters, 1f));
            _lat += gain * (lat - _lat);
            _lon += gain * (lon - _lon);
            _varianceMeters *= (1f - gain);
        }
    }
}
