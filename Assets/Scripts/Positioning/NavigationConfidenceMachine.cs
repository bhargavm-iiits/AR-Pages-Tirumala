using System;

namespace AlipiriAR.Positioning
{
    /// <summary>Confidence in where the walker actually is — Docs/update1.md §04. Deliberately
    /// not named NavigationState (NavigationSession already owns that name for a different axis:
    /// Idle/Active/Paused/Ended, whether a session is running at all). This is orthogonal to
    /// that — "connectivity is an input to the estimator, not a state of the app" (§04); the only
    /// thing the app genuinely has states about is how well it knows where you are.</summary>
    public enum NavigationConfidence { Booting, AwaitingFix, Confident, Coasting, Uncertain, Lost }

    /// <summary>
    /// Drives NavigationConfidence from AlongTrackEstimator's sigma with hysteresis on every
    /// band boundary (§04's table) — so a sigma sitting right at 15 m doesn't flap Confident/
    /// Coasting on every tick. Pure logic, no Unity dependency, easy to unit-test against
    /// synthetic sigma sequences (Docs/update1.md §08 item 3).
    /// </summary>
    public class NavigationConfidenceMachine
    {
        // §04's table, both directions per band.
        private const double EnterCoastingSigma = 15.0;
        private const double LeaveCoastingSigma = 12.0;
        private const double EnterUncertainSigma = 40.0;
        private const double LeaveUncertainSigma = 32.0;
        private const double LostLateralMeters = 150.0;
        private const double LostSigmaMeters = 200.0;
        private const double LostSustainedSeconds = 60.0;

        /// <summary>§04's "one time-based edge": no measurement of any kind for this long means
        /// sigma has grown past the Coasting band by growth alone, and the app must say so.</summary>
        private const double NoMeasurementTimeoutSeconds = 120.0;

        public NavigationConfidence Confidence { get; private set; } = NavigationConfidence.Booting;

        public event Action<NavigationConfidence, NavigationConfidence> OnConfidenceChanged;

        private double _lastMeasurementTimestamp = double.NegativeInfinity;
        private double _lastAnyUpdateTimestamp = double.NegativeInfinity;
        private double _lateralOutOfBandSinceTimestamp = double.NaN;

        /// <summary>Call once, when DB load + AR state + permissions have all settled — §04's Booting → Awaiting Fix.</summary>
        public void MarkReady()
        {
            SetConfidence(NavigationConfidence.AwaitingFix);
        }

        /// <summary>Feed on every fused position update — sigmaMeters from
        /// AlongTrackEstimator.SigmaMeters, lateralMeters from RouteProgressTracker.
        /// LateralDistanceMeters (0 when the fix isn't from GPS; Lost's lateral trigger only
        /// makes sense for an actual off-corridor GPS disagreement).</summary>
        public void Feed(double sigmaMeters, double lateralMeters, double timestampSeconds)
        {
            _lastMeasurementTimestamp = timestampSeconds;
            _lastAnyUpdateTimestamp = timestampSeconds;

            if (Confidence == NavigationConfidence.Booting) return; // MarkReady hasn't fired yet

            if (Confidence == NavigationConfidence.AwaitingFix)
            {
                if (sigmaMeters < EnterCoastingSigma) SetConfidence(NavigationConfidence.Confident);
                return;
            }

            bool lateralSustained = TrackLateralOutOfBand(lateralMeters, timestampSeconds);
            if (lateralSustained || sigmaMeters > LostSigmaMeters)
            {
                SetConfidence(NavigationConfidence.Lost);
                return;
            }

            switch (Confidence)
            {
                case NavigationConfidence.Confident when sigmaMeters >= EnterCoastingSigma:
                    SetConfidence(sigmaMeters >= EnterUncertainSigma ? NavigationConfidence.Uncertain : NavigationConfidence.Coasting);
                    break;
                case NavigationConfidence.Coasting when sigmaMeters < LeaveCoastingSigma:
                    SetConfidence(NavigationConfidence.Confident);
                    break;
                case NavigationConfidence.Coasting when sigmaMeters >= EnterUncertainSigma:
                    SetConfidence(NavigationConfidence.Uncertain);
                    break;
                case NavigationConfidence.Uncertain when sigmaMeters < LeaveUncertainSigma:
                    SetConfidence(NavigationConfidence.Coasting);
                    break;
            }
        }

        /// <summary>Any re-anchor (image target, manual pick, a GPS fix good enough to re-seed) — §04: "Lost → Confident: any re-anchor."</summary>
        public void NotifyReanchored(double timestampSeconds)
        {
            _lateralOutOfBandSinceTimestamp = double.NaN;
            _lastMeasurementTimestamp = timestampSeconds;
            _lastAnyUpdateTimestamp = timestampSeconds;
            SetConfidence(NavigationConfidence.Confident);
        }

        /// <summary>Call periodically (e.g. once a second) even when no measurement has arrived —
        /// this is what actually detects §04's 120 s no-measurement timeout, since Feed alone
        /// only runs when something did arrive.</summary>
        public void Tick(double timestampSeconds)
        {
            if (Confidence == NavigationConfidence.Booting || Confidence == NavigationConfidence.AwaitingFix) return;
            if (Confidence == NavigationConfidence.Lost) return;

            if (timestampSeconds - _lastMeasurementTimestamp > NoMeasurementTimeoutSeconds)
            {
                SetConfidence(NavigationConfidence.Lost);
            }
        }

        private bool TrackLateralOutOfBand(double lateralMeters, double timestampSeconds)
        {
            if (lateralMeters <= LostLateralMeters)
            {
                _lateralOutOfBandSinceTimestamp = double.NaN;
                return false;
            }

            if (double.IsNaN(_lateralOutOfBandSinceTimestamp))
            {
                _lateralOutOfBandSinceTimestamp = timestampSeconds;
                return false;
            }

            return timestampSeconds - _lateralOutOfBandSinceTimestamp >= LostSustainedSeconds;
        }

        private void SetConfidence(NavigationConfidence next)
        {
            if (next == Confidence) return;
            var previous = Confidence;
            Confidence = next;
            OnConfidenceChanged?.Invoke(previous, next);
        }
    }
}
