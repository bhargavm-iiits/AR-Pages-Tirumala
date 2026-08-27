using AlipiriAR.Positioning;
using UnityEngine;

namespace AlipiriAR.AR
{
    /// <summary>Fuses ARCore VIO (via the tracked AR camera pose), filtered GPS, route-snap and
    /// compass into one GeoAnchorFrame (PLAN.md §09 — "fuse four weak signals into one good
    /// one"). Route-snap already happened by the time a fix reaches here (RouteProgressTracker,
    /// Scene 5) — FeedFix takes the snapped lat/lon, not the raw fix, so the AR frame inherits
    /// the corridor-constraint accuracy win for free. Device-unverified — see
    /// ARSessionBootstrapper's class doc.</summary>
    public class HybridLocalizationEngine
    {
        /// <summary>Floor on the outlier-reject distance — below this, ordinary GPS jitter under
        /// canopy would trip false rejections even at good accuracy. Matches
        /// LocationProvider.LowAccuracyThresholdMeters, the same bar used to decide a fix is
        /// confident enough to (re-)establish the frame at all.</summary>
        private const float k_GpsOutlierRejectMeters = LocationProvider.LowAccuracyThresholdMeters;

        /// <summary>The reject threshold scales up with the fix's own reported accuracy — a
        /// marginal fix shouldn't be held to the same bar as a precise one. Effective threshold
        /// is max(k_GpsOutlierRejectMeters, k_GpsOutlierAccuracyFactor * accuracyMeters).</summary>
        private const float k_GpsOutlierAccuracyFactor = 4f;

        /// <summary>Consecutive rejected fixes before the tracked AR pose — not the GPS — is
        /// assumed to be the one that's wrong. At that point a 6%/fix nudge (GeoAnchorFrame's
        /// NudgeFactor) cannot pull the frame back from drift on its own, so re-anchor instead of
        /// leaving the walker permanently stranded off-route.</summary>
        private const int k_MaxConsecutiveGpsRejections = 6;

        private readonly GeoAnchorFrame _frame = new();
        private readonly Camera _arCamera;
        private int _consecutiveRejections;

        public GeoAnchorFrame Frame => _frame;

        /// <summary>Distance between where the last fix implied the camera should be and where it
        /// actually was. 0 until the frame is established. Surfaced for DebugOverlay.</summary>
        public float LastResidualMeters { get; private set; }

        /// <summary>How many GPS fixes in a row have been rejected as outliers. Resets to zero the
        /// moment a fix is accepted or the frame is re-seeded.</summary>
        public int ConsecutiveGpsRejections => _consecutiveRejections;

        /// <summary>Human-readable reason the frame was last (re-)established — "initial seed" or
        /// why a forced re-seed fired. Surfaced for DebugOverlay.</summary>
        public string LastReseedReason { get; private set; } = "not yet seeded";

        public HybridLocalizationEngine(Camera arCamera)
        {
            _arCamera = arCamera;
        }

        /// <summary>Feed every snapped RouteProgressTracker position here — establishes the
        /// frame on the first confident fix, nudges it on every accepted one after, and forces a
        /// re-seed once sustained disagreement shows the tracked pose has diverged rather than
        /// the GPS being merely noisy.</summary>
        public void FeedFix(double lat, double lon, float compassHeadingDeg, float accuracyMeters)
        {
            if (!_frame.IsEstablished)
            {
                if (accuracyMeters > LocationProvider.LowAccuracyThresholdMeters) return; // wait for a confident first fix
                Establish(lat, lon, compassHeadingDeg, "initial seed");
                return;
            }

            float residual = _frame.ResidualMeters(lat, lon, _arCamera.transform.position);
            LastResidualMeters = residual;

            float rejectThreshold = Mathf.Max(k_GpsOutlierRejectMeters, k_GpsOutlierAccuracyFactor * accuracyMeters);

            if (residual > rejectThreshold)
            {
                _consecutiveRejections++;
                Debug.Log($"[Localization] Rejected GPS fix {residual:F0} m from the tracked pose " +
                          $"(threshold {rejectThreshold:F0} m, {_consecutiveRejections} consecutive).");

                if (_consecutiveRejections >= k_MaxConsecutiveGpsRejections)
                {
                    Establish(lat, lon, compassHeadingDeg, "GPS repeatedly disagreed with the tracked pose");
                }
                return;
            }

            _consecutiveRejections = 0;
            _frame.Nudge(lat, lon, _arCamera.transform.position);
            _frame.NudgeHeading(compassHeadingDeg, _arCamera.transform.eulerAngles.y);
        }

        private void Establish(double lat, double lon, float compassHeadingDeg, string reason)
        {
            _frame.Establish(lat, lon, compassHeadingDeg, _arCamera.transform.position, _arCamera.transform.eulerAngles.y);
            _consecutiveRejections = 0;
            LastResidualMeters = 0f;
            LastReseedReason = reason;
            Debug.Log($"[Localization] Frame (re-)seeded: {reason}.");
        }

        public Vector3 GeoToWorld(double lat, double lon) => _frame.GeoToWorld(lat, lon);
    }
}
