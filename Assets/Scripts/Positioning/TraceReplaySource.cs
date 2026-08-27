using System;
using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.Utilities;
using UnityEngine;

namespace AlipiriAR.Positioning
{
    /// <summary>Walks a synthetic fix along the route polyline at ~1.2 m/s (PLAN.md §08) — the
    /// "highest-leverage test harness in the project," exercising the map's user marker without
    /// a device or GPS. Scene 5's real LocationProvider becomes the map's position source later;
    /// nothing downstream needs to know which one is driving it.</summary>
    public class TraceReplaySource : MonoBehaviour
    {
        private const float SpeedMetersPerSecond = 1.2f;

        private IReadOnlyList<Waypoint> _waypoints;
        private double _cumulativeDistance;
        private bool _running;

        public event Action<double, double, float, double> OnPositionChanged; // lat, lon, headingDeg, cumulativeDistanceMeters

        public static TraceReplaySource Create(IReadOnlyList<Waypoint> waypoints)
        {
            var go = new GameObject("~TraceReplaySource");
            DontDestroyOnLoad(go);
            var source = go.AddComponent<TraceReplaySource>();
            source._waypoints = waypoints;
            return source;
        }

        public void Play() => _running = true;
        public void Pause() => _running = false;
        public bool IsPlaying => _running;

        private void Update()
        {
            if (!_running || _waypoints == null || _waypoints.Count < 2) return;

            _cumulativeDistance += SpeedMetersPerSecond * Time.deltaTime;
            double total = _waypoints[^1].CumulativeDistanceMeters;
            if (_cumulativeDistance >= total)
            {
                _cumulativeDistance = total;
                _running = false;
            }

            var (lat, lon, heading) = SampleAt(_cumulativeDistance);
            OnPositionChanged?.Invoke(lat, lon, heading, _cumulativeDistance);
        }

        private (double lat, double lon, float headingDeg) SampleAt(double distance)
        {
            int i = 0;
            while (i < _waypoints.Count - 2 && _waypoints[i + 1].CumulativeDistanceMeters < distance) i++;

            var a = _waypoints[i];
            var b = _waypoints[Math.Min(i + 1, _waypoints.Count - 1)];
            double segLen = b.CumulativeDistanceMeters - a.CumulativeDistanceMeters;
            double t = segLen > 0.0001 ? Clamp01((distance - a.CumulativeDistanceMeters) / segLen) : 0.0;

            double lat = a.Latitude + (b.Latitude - a.Latitude) * t;
            double lon = a.Longitude + (b.Longitude - a.Longitude) * t;
            float heading = (float)GeoMath.BearingDegrees(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

            return (lat, lon, heading);
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
