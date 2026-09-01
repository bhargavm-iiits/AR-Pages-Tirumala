using AlipiriAR.Data;
using AlipiriAR.Route;
using AlipiriAR.Utilities;
using UnityEngine;

namespace AlipiriAR.Positioning
{
    /// <summary>
    /// Owns every IPositionSource and the AlongTrackEstimator, and drives
    /// RouteProgressTracker.Feed from the fused result — Docs/update1.md §03 item 6, Phase 3
    /// item 6. This is what "one always-on offline estimator" (§00) actually means in code: GPS,
    /// step counter and barometer are all folded in here as opportunistic corrections with a
    /// stated uncertainty, and nothing about being online or offline is a branch anywhere in this
    /// class — a source simply does or doesn't have a measurement to contribute right now.
    /// </summary>
    public class PositionFusionService
    {
        /// <summary>§05: "gate: skip when grade &lt; 0.05" — below this the barometer's
        /// along-track uncertainty exceeds ~40 m (§01) and contributes nothing worth folding in.</summary>
        private const double BarometerGradeGateThreshold = 0.05;

        /// <summary>Smartphone barometer sensor noise — roughly 0.1 hPa resolution, ~1 m of
        /// altitude, with margin for the sensor's own short-term jitter. Weather drift is not
        /// part of this term; that's what BarometricBiasMeters and re-anchor recalibration are for.</summary>
        private const double BarometerAltitudeSigmaMeters = 2.0;

        /// <summary>How far from the current estimate to search when inverting an altitude back
        /// to a chainage — bounds the search to "near where we already think we are" per
        /// PolylineUtility.InvertElevationNear's own doc, not a global search of the whole route.</summary>
        private const double BarometerSearchWindowMeters = 250.0;

        /// <summary>Exact/noise-free sources (trace replay) get a near-zero sigma instead of
        /// whatever synthetic accuracy value rode along on the shared LocationProvider event
        /// pipeline — see AlongTrackEstimator.SetExact's doc for why passing that value through
        /// unchanged would reintroduce the old trustExactly bug (chunky multi-metre jumps).</summary>
        private const double ExactSourceSigmaMeters = 0.001;

        public AlongTrackEstimator Estimator { get; }

        private readonly RouteProgressTracker _progress;
        private readonly System.Collections.Generic.IReadOnlyList<Waypoint> _waypoints;
        private readonly GpsPositionSource _gps;
        private readonly StepCounterSource _steps;
        private readonly BarometerSource _barometer;
        private readonly bool _routeHasElevation;

        public PositionFusionService(RouteResult route, RouteProgressTracker progress, LocationProvider location, double initialSMeters = 0.0, double initialSigmaMeters = 50.0)
        {
            _waypoints = route.Waypoints;
            _progress = progress;
            _routeHasElevation = route.Waypoints.Exists(w => w.HasElevation);

            Estimator = new AlongTrackEstimator(initialSMeters, initialSigmaMeters);

            _gps = new GpsPositionSource(location, _waypoints);
            _gps.OnMeasurement += HandleGps;

            _steps = new StepCounterSource();
            _steps.OnMeasurement += HandleSteps;

            _barometer = new BarometerSource();
            _barometer.OnMeasurement += HandleBarometer;
        }

        public void Start()
        {
            _gps.Start();
            _steps.Start(); // registers conditionally on hardware/permission internally

            if (_routeHasElevation)
            {
                _barometer.Start();
            }
            else
            {
                Debug.Log("[PositionFusionService] Route has no elevation data yet (§01 — Phase 1 survey blocker). Barometer source not started.");
            }
        }

        public void Stop()
        {
            _gps.Stop();
            _steps.Stop();
            _barometer.Stop();
        }

        /// <summary>Call on every confirmed re-anchor (Phase 5 image/manual targets, or a
        /// Geospatial fix once Phase 6 ships) — §05's "one hit corrects all three drift
        /// channels": resets chainage, and re-solves barometric bias from the checkpoint's known
        /// elevation when the route has one.</summary>
        public void ApplyAnchor(double checkpointS, double sigmaMeters, double? knownElevationMeters = null)
        {
            Estimator.UpdateAbsolute(checkpointS, sigmaMeters);

            if (knownElevationMeters.HasValue && _routeHasElevation)
            {
                double modelled = PolylineUtility.ElevationAtDistance(_waypoints, checkpointS);
                if (!double.IsNaN(modelled))
                {
                    Estimator.ReconcileBarometricBias(knownElevationMeters.Value, modelled);
                }
            }

            PushToProgress(Time.unscaledTimeAsDouble);
        }

        private void HandleGps(PositionMeasurement m)
        {
            if (m.Provenance == SourceKind.TraceReplay)
            {
                Estimator.SetExact(m.S, ExactSourceSigmaMeters);
            }
            else
            {
                Estimator.UpdateAbsolute(m.S, m.Sigma);
            }

            _progress.SetLateralDiagnostic(_gps.LastLateralDistanceMeters);
            PushToProgress(m.Timestamp);
        }

        private void HandleSteps(PositionMeasurement m)
        {
            double grade = PolylineUtility.GradeAtDistance(_waypoints, Estimator.S);
            Estimator.PredictSteps((int)m.S, grade);
            PushToProgress(m.Timestamp);
        }

        private void HandleBarometer(PositionMeasurement m)
        {
            if (!_routeHasElevation) return;

            double grade = PolylineUtility.GradeAtDistance(_waypoints, Estimator.S);
            if (double.IsNaN(grade) || System.Math.Abs(grade) < BarometerGradeGateThreshold) return;

            double biasCorrectedAltitude = m.S - Estimator.BarometricBiasMeters;
            double invertedS = PolylineUtility.InvertElevationNear(_waypoints, biasCorrectedAltitude, Estimator.S, BarometerSearchWindowMeters);
            if (double.IsNaN(invertedS)) return;

            Estimator.UpdateBarometric(invertedS, BarometerAltitudeSigmaMeters, grade);
            PushToProgress(m.Timestamp);
        }

        private void PushToProgress(double timestamp)
        {
            _progress.Feed(Estimator.S, Estimator.SigmaMeters, timestamp);
        }
    }
}
