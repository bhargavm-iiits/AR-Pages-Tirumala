using System;
using AlipiriAR.Route;
using AlipiriAR.Utilities;

namespace AlipiriAR.Positioning
{
    /// <summary>Snaps LocationProvider's filtered fix onto the route polyline and derives every
    /// number the four data screens show — completed/remaining distance, %, ETA — from that one
    /// scalar (PLAN.md §08 step 3/4, §09). Nothing computes its own version of "how far along
    /// the route am I" anywhere else. Snapping is safe here specifically because the stairway is
    /// a single corridor with no alternative path (PLAN.md §08 step 3).</summary>
    public class RouteProgressTracker
    {
        /// <summary>Floor on the jitter band applied symmetrically in both directions (see Feed)
        /// — below this, a fix that snaps behind OR ahead of the tracked high-water mark is
        /// ordinary GPS noise and gets absorbed, not treated as the walker moving. Originally
        /// backward-only: found on a real device that closest-point snapping with no floor made
        /// CumulativeDistanceMeters visibly dip backward frame to frame, and since
        /// DynamicArrowManager anchors the whole chevron trail to this value, that dip showed up
        /// as the arrows themselves jumping backward. That fix left the forward side completely
        /// unguarded, though: a Math.Max ratchets the high-water mark up on ANY forward-snapping
        /// fix, no matter how small, so ordinary noise while standing still — a fix snapping 1-3m
        /// further along the corridor purely by chance — got locked in permanently, one small
        /// step at a time, forever. That is what "the navigation updates on its own, before
        /// anyone's moved" actually was: not simulated movement, a one-directional ratchet with
        /// no forward floor. See JitterToleranceMeters below for the real (accuracy-scaled) value
        /// used at runtime — this constant is only its minimum.</summary>
        private const double MinJitterToleranceMeters = 5.0;

        /// <summary>Reported GPS accuracy IS the fix's own noise estimate — scaling the jitter
        /// band by it (floor MinJitterToleranceMeters, since a suspiciously confident fix
        /// shouldn't get a zero tolerance either) means a degraded fix indoors, which might report
        /// 20-30m accuracy, doesn't masquerade as three or four "steps" of real progress the
        /// moment GPS reacquires — the corridor-snapped position only advances once a fix is
        /// further from the tracked point than its own uncertainty already explains.</summary>
        private static double JitterToleranceMeters(float accuracyMeters) => Math.Max(MinJitterToleranceMeters, accuracyMeters);

        private readonly RouteResult _route;
        private readonly EtaEstimator _eta = new();
        private double _highWaterMarkMeters;

        public double CumulativeDistanceMeters { get; private set; }
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

        /// <summary>trustExactly bypasses the jitter band entirely — set for TraceReplaySource's
        /// fixes (NavigationSession.HandleFix), which are exact/noise-free by construction, so
        /// there is no jitter to guard against and a 5m+ floor would only make the desk-testable
        /// walker (PLAN.md §08) advance in chunky multi-metre jumps instead of the smooth
        /// per-fix progress it's meant to simulate. Real device GPS always goes through the full
        /// accuracy-scaled tolerance below.</summary>
        public void Feed(double rawLat, double rawLon, double timestamp, float accuracyMeters, bool trustExactly = false)
        {
            var projection = PolylineUtility.ClosestPoint(_route.Waypoints, rawLat, rawLon);
            double tolerance = trustExactly ? 0.0 : JitterToleranceMeters(accuracyMeters);
            double delta = projection.CumulativeDistanceMeters - _highWaterMarkMeters;

            // Symmetric band: a fix within `tolerance` of the tracked mark, in EITHER direction,
            // is ordinary noise and changes nothing. Only a delta that exceeds the fix's own
            // reported uncertainty counts as the walker actually having moved — forward or back.
            if (Math.Abs(delta) > tolerance)
                _highWaterMarkMeters = projection.CumulativeDistanceMeters;

            CumulativeDistanceMeters = _highWaterMarkMeters;

            LateralDistanceMeters = projection.LateralDistanceMeters;
            Latitude = projection.Latitude;
            Longitude = projection.Longitude;

            _eta.Feed(CumulativeDistanceMeters, timestamp);
            OnUpdated?.Invoke();
        }

        public int EtaMinutes() => _eta.EstimateMinutes(RemainingDistanceMeters);
    }
}
