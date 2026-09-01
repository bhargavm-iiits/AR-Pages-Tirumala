using System;
using AlipiriAR.Route;
using AlipiriAR.Utilities;

namespace AlipiriAR.Positioning
{
    /// <summary>Derives every number the four data screens show — completed/remaining distance,
    /// %, ETA — from one fused along-track scalar (PLAN.md §08 step 3/4, §09; Docs/update1.md
    /// §03 Phase 3 item 7). Nothing computes its own version of "how far along the route am I"
    /// anywhere else.
    ///
    /// Public surface is unchanged from before the Phase 3 fusion rewrite — every downstream
    /// consumer (Map/Progress/Landmarks/AR screens) still reads CumulativeDistanceMeters,
    /// Latitude/Longitude etc. exactly as before. What changed is who computes the input: Feed
    /// used to project a raw GPS fix itself; it now takes an already-fused s/σ from
    /// PositionFusionService, which folds in GPS, step counter and barometer, and derives
    /// Latitude/Longitude by walking that s back onto the polyline (PolylineUtility.
    /// PointAtDistance) instead of snapping a fix onto it.</summary>
    public class RouteProgressTracker
    {
        /// <summary>Small floor purely against floating-point/near-zero-sigma edge cases, not a
        /// real accuracy judgement the way the old GPS-accuracy floor was — the fused sigma
        /// PositionFusionService supplies already reflects genuine estimator uncertainty (built
        /// from each source's own measurement variance), so a second independent floor on top of
        /// it would just double-count caution the estimator already applied.</summary>
        private const double MinToleranceMeters = 0.1;

        private readonly RouteResult _route;
        private readonly EtaEstimator _eta = new();
        private double _highWaterMarkMeters;

        public double CumulativeDistanceMeters { get; private set; }

        /// <summary>Diagnostic only — how far the most recent raw GPS fix sat from the corridor
        /// before fusion, not derived from CumulativeDistanceMeters. Set by
        /// PositionFusionService via SetLateralDiagnostic; DebugOverlay/GpsTraceRecorder read it
        /// exactly as they did before this class's Feed signature changed.</summary>
        public double LateralDistanceMeters { get; private set; }

        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        public double TotalDistanceMeters => _route.TotalDistanceMeters;
        public double RemainingDistanceMeters => Math.Max(TotalDistanceMeters - CumulativeDistanceMeters, 0.0);
        public float FractionComplete => TotalDistanceMeters > 0 ? (float)(CumulativeDistanceMeters / TotalDistanceMeters) : 0f;

        public event Action OnUpdated;

        public RouteProgressTracker(RouteResult route)
        {
            _route = route;
        }

        /// <summary>Feeds one fused position update. tolerance is sigma itself (floored at
        /// MinToleranceMeters) rather than a fixed accuracy-scaled band — a source that has
        /// genuinely converged (e.g. a re-anchor fix, sigma ≈ 2 m) is allowed to move the
        /// high-water mark on a small delta; a source still uncertain (fresh CoarseAcquire,
        /// sigma ≈ 50 m) is not. Same symmetric-band reasoning as before applies either way: a
        /// fix within tolerance of the tracked mark, in EITHER direction, is treated as noise
        /// and absorbed rather than as the walker moving forward or back.</summary>
        public void Feed(double fusedS, double sigmaMeters, double timestamp)
        {
            double clampedS = Math.Clamp(fusedS, 0.0, TotalDistanceMeters);
            double tolerance = Math.Max(MinToleranceMeters, sigmaMeters);
            double delta = clampedS - _highWaterMarkMeters;

            if (Math.Abs(delta) > tolerance)
                _highWaterMarkMeters = clampedS;

            CumulativeDistanceMeters = _highWaterMarkMeters;

            (Latitude, Longitude) = PolylineUtility.PointAtDistance(_route.Waypoints, CumulativeDistanceMeters);

            _eta.Feed(CumulativeDistanceMeters, timestamp);
            OnUpdated?.Invoke();
        }

        /// <summary>See LateralDistanceMeters — purely a diagnostic side-channel, does not affect CumulativeDistanceMeters or fire OnUpdated.</summary>
        public void SetLateralDiagnostic(double lateralMeters) => LateralDistanceMeters = lateralMeters;

        public int EtaMinutes() => _eta.EstimateMinutes(RemainingDistanceMeters);
    }
}
