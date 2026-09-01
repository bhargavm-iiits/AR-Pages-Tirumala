using System;

namespace AlipiriAR.Positioning
{
    /// <summary>
    /// The 1-D fused estimator over route distance — Docs/update1.md §05. State is
    /// [s, k_low, k_high, b]: along-track position, two calibrated stride constants (grade &lt;
    /// 8% and grade ≥ 8% — §01's correction that a single stride constant is insufficient), and
    /// barometric bias. Everything reduces to the same 1-D Kalman gain regardless of which
    /// source produced the observation — no matrix math needed since nothing here is coupled
    /// across states except through the caller's own route-geometry lookups (PolylineUtility).
    ///
    /// Tuning constants below (miscount fraction, stride clamp, initial stride values) are §05's
    /// design targets, not measurements — §05 states this explicitly and gives the falsification
    /// plan: replay the Phase 1 GPS+barometer+step-counter trace and correct these from real
    /// data before trusting the filter in the field.
    /// </summary>
    public class AlongTrackEstimator
    {
        private const double StepCountMiscountFraction = 0.04; // §05 predict: "4% miscount on stairs"
        private const double StrideClampFraction = 0.20; // §05: "clamped to ±20% of its calibrated value"

        public double S { get; private set; }
        public double SigmaMeters { get; private set; }
        public double MetresPerStepLowGrade { get; private set; }
        public double MetresPerStepHighGrade { get; private set; }
        public double BarometricBiasMeters { get; private set; }

        private readonly double _calibratedLowGrade;
        private readonly double _calibratedHighGrade;
        private readonly double _highGradeThreshold;

        /// <param name="highGradeThreshold">Fractional grade (0.08 = 8%) at or above which the high-grade stride constant applies — §01's two-constant correction.</param>
        public AlongTrackEstimator(
            double initialS,
            double initialSigmaMeters,
            double calibratedLowGradeMetresPerStep = 0.35,
            double calibratedHighGradeMetresPerStep = 0.22,
            double highGradeThreshold = 0.08)
        {
            S = initialS;
            SigmaMeters = Math.Max(initialSigmaMeters, 0.01);
            _calibratedLowGrade = calibratedLowGradeMetresPerStep;
            _calibratedHighGrade = calibratedHighGradeMetresPerStep;
            _highGradeThreshold = highGradeThreshold;
            MetresPerStepLowGrade = calibratedLowGradeMetresPerStep;
            MetresPerStepHighGrade = calibratedHighGradeMetresPerStep;
        }

        /// <summary>§05 "predict" — one step-counter tick of deltaSteps at the given local grade. Negative deltaSteps (a walker briefly backtracking) moves s backward, same as forward.</summary>
        public void PredictSteps(int deltaSteps, double gradeAtS)
        {
            if (deltaSteps == 0) return;

            double k = !double.IsNaN(gradeAtS) && Math.Abs(gradeAtS) >= _highGradeThreshold
                ? MetresPerStepHighGrade
                : MetresPerStepLowGrade;

            double deltaMeters = deltaSteps * k;
            S += deltaMeters;

            double sigmaStep = Math.Abs(deltaMeters) * StepCountMiscountFraction;
            SigmaMeters = Math.Sqrt(SigmaMeters * SigmaMeters + sigmaStep * sigmaStep);
        }

        /// <summary>§05's GPS/anchor/Geospatial update — every absolute fix "s is here" reduces
        /// to this one call regardless of source (§03's "one interface"). Corridor rejection
        /// ("off-corridor, not a real fix") is each source's own responsibility before it ever
        /// emits a PositionMeasurement — see GpsPositionSource, which is the one source that
        /// actually needs it; an anchor/image-target fix is definitionally on-corridor.</summary>
        public void UpdateAbsolute(double z, double sigmaZ)
        {
            ApplyKalmanUpdate(z, sigmaZ);
        }

        /// <summary>§05 "update — barometer". sigmaAltMeters / grade is what makes this
        /// self-weighting — a steep flight contributes a tight measurement and dominates, a flat
        /// approach contributes a loose one and is correctly ignored, with no mode logic. Caller
        /// must gate on grade being known and above the minimum threshold before calling this at
        /// all (PositionFusionService does), since a NaN or near-zero grade would produce a
        /// meaningless or infinite sigmaZ.</summary>
        public void UpdateBarometric(double zFromElevationInverse, double sigmaAltMeters, double gradeAtS)
        {
            double sigmaZ = sigmaAltMeters / Math.Max(Math.Abs(gradeAtS), 1e-3);
            ApplyKalmanUpdate(zFromElevationInverse, sigmaZ);
        }

        /// <summary>Re-estimates barometric bias from a known elevation at the current s — §05's "re-solve b from checkpoint.ele".</summary>
        public void ReconcileBarometricBias(double observedElevationMeters, double modelledElevationMeters)
        {
            BarometricBiasMeters = observedElevationMeters - modelledElevationMeters;
        }

        /// <summary>Resets the step-counter delta baseline is the caller's job (it owns the raw
        /// hardware counter); this only re-estimates the stride constant itself, clamped to ±20%
        /// of its calibrated value (§05) so a burst of miscounted steps on rough ground can't run
        /// the constant away.</summary>
        public void AdjustStride(bool highGrade, double observedMetresPerStep)
        {
            double calibrated = highGrade ? _calibratedHighGrade : _calibratedLowGrade;
            double min = calibrated * (1 - StrideClampFraction);
            double max = calibrated * (1 + StrideClampFraction);
            double clamped = Math.Clamp(observedMetresPerStep, min, max);

            if (highGrade) MetresPerStepHighGrade = clamped;
            else MetresPerStepLowGrade = clamped;
        }

        /// <summary>Hard override for exact/noise-free sources — TraceReplaySource's simulated
        /// walk, and the "trustExactly" bypass RouteProgressTracker previously implemented
        /// itself. Running an exact fix through the Kalman blend instead would turn its smooth
        /// per-fix progress into the jitter-absorption behaviour meant for noisy real GPS.</summary>
        public void SetExact(double s, double sigmaMeters)
        {
            S = s;
            SigmaMeters = Math.Max(sigmaMeters, 0.01);
        }

        private void ApplyKalmanUpdate(double z, double sigmaZ)
        {
            double varS = SigmaMeters * SigmaMeters;
            double varZ = Math.Max(sigmaZ * sigmaZ, 1e-6);
            double gain = varS / (varS + varZ);
            S += gain * (z - S);
            SigmaMeters = Math.Sqrt(Math.Max((1 - gain) * varS, 1e-6));
        }
    }
}
