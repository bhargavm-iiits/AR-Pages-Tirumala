using System;
using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.Utilities;
using UnityEngine;

namespace AlipiriAR.Positioning
{
    /// <summary>
    /// Wraps the existing LocationProvider (which already owns GpsKalmanFilter and the
    /// trace-replay/device-GPS split) as an IPositionSource — Docs/update1.md §03/Phase 3 item
    /// 2. Projects every filtered fix onto the route via PolylineUtility.ClosestPoint, sets
    /// Sigma from the fix's own reported accuracy, and rejects a fix whose lateral corridor
    /// offset exceeds 3σ ("off-corridor, not a real fix" — §05) by simply not emitting it, rather
    /// than letting a bad projection corrupt the fused estimate.
    ///
    /// TraceReplaySource fixes are exact/noise-free by construction (same property
    /// RouteProgressTracker's old trustExactly parameter existed for) — tagged
    /// SourceKind.TraceReplay instead of SourceKind.Gps so PositionFusionService can route them
    /// to AlongTrackEstimator.SetExact instead of the Kalman blend.
    /// </summary>
    public class GpsPositionSource : IPositionSource
    {
        private readonly LocationProvider _location;
        private readonly IReadOnlyList<Waypoint> _waypoints;

        public SourceKind Kind => Provenance;

        private SourceKind Provenance =>
            _location.Mode == LocationSourceMode.TraceReplay ? SourceKind.TraceReplay : SourceKind.Gps;

        /// <summary>Always true — GPS/trace-replay is this app's baseline source and always
        /// registers; genuine unavailability (permission denied, services disabled) surfaces
        /// through LocationProvider.Status instead of this flag, matching how the rest of the
        /// app already treats that distinction.</summary>
        public bool IsAvailable => true;

        /// <summary>How far the most recent raw fix sat from the corridor, set on every fix
        /// (accepted or rejected) — diagnostic only. PositionFusionService forwards this into
        /// RouteProgressTracker.SetLateralDiagnostic so DebugOverlay/GpsTraceRecorder keep
        /// reading it exactly as before the fusion rewrite.</summary>
        public float LastLateralDistanceMeters { get; private set; }

        public event Action<PositionMeasurement> OnMeasurement;

        public GpsPositionSource(LocationProvider location, IReadOnlyList<Waypoint> waypoints)
        {
            _location = location ?? throw new ArgumentNullException(nameof(location));
            _waypoints = waypoints ?? throw new ArgumentNullException(nameof(waypoints));
        }

        public void Start() => _location.OnFixFiltered += HandleFix;

        public void Stop() => _location.OnFixFiltered -= HandleFix;

        private void HandleFix(double lat, double lon, float headingDeg, float accuracyMeters)
        {
            if (_waypoints.Count == 0) return;

            var projection = PolylineUtility.ClosestPoint(_waypoints, lat, lon);
            float sigma = Mathf.Max(accuracyMeters, 1f);
            LastLateralDistanceMeters = (float)projection.LateralDistanceMeters;

            bool isTraceReplay = _location.Mode == LocationSourceMode.TraceReplay;
            if (!isTraceReplay && projection.LateralDistanceMeters > 3.0 * sigma)
            {
                Debug.Log($"[GpsPositionSource] Rejected fix {projection.LateralDistanceMeters:F0} m off-corridor (3σ = {3.0 * sigma:F0} m).");
                return;
            }

            OnMeasurement?.Invoke(new PositionMeasurement(
                projection.CumulativeDistanceMeters, sigma, headingDeg, null,
                Time.unscaledTimeAsDouble, Provenance));
        }
    }
}
